/**
 * DouyinAPI - 抖音开放平台接口封装
 *
 * 功能:
 * 1. access_token 获取 & 自动刷新（有效期2小时）
 * 2. 直播数据任务管理（start/stop）
 * 3. 推送数据解析 & 签名验证
 *
 * 抖音数据推送流程:
 * 1. 主播挂载小玩法 → 抖音服务器 POST 到我们的回调接口
 * 2. 消息类型: live_comment(评论), live_gift(礼物), live_like(点赞), live_fansclub(粉丝团)
 * 3. 每种消息类型需要单独调用 task/start 启动
 * 4. QPS限制: 默认100
 * 5. 推送超时: 评论/点赞2秒, 礼物3秒
 * 6. 连续10次失败触发熔断
 *
 * 抖音推送body格式（官方文档）:
 * {
 *   roomid: "直播间ID",
 *   msg_type: "live_comment" | "live_gift" | "live_like" | "live_fansclub",
 *   payload: "[{...}, {...}]"   // JSON字符串，需要JSON.parse反序列化
 * }
 *
 * 签名验证算法（官方文档）:
 * 1. 从Header取: x-nonce-str, x-timestamp, x-roomid, x-msg-type
 * 2. 按key字典序排序拼接: key1=value1&key2=value2&...
 * 3. 追加body原始字符串 + pushSecret（推送密钥，优先）或 appSecret
 * 4. MD5 → Base64
 */

const https = require('https');
const crypto = require('crypto');

// 抖音小玩法专用API地址
const DOUYIN_TOKEN_URL = 'https://minigame.zijieapi.com/mgplatform/api/apps/v2/token';
// 直播数据任务管理
const DOUYIN_WEBCAST_BASE = 'https://webcast.bytedance.com';

class DouyinAPI {
  /**
   * @param {object} options
   * @param {string} options.appId - 抖音小玩法appId
   * @param {string} options.appSecret - 抖音小玩法appSecret
   * @param {Function} options.onComment - 评论回调 (roomId, secOpenId, nickname, avatarUrl, content)
   * @param {Function} options.onGift - 礼物回调 (roomId, secOpenId, nickname, avatarUrl, secGiftId, giftNum, giftValue)
   * @param {Function} options.onLike - 点赞回调 (roomId, secOpenId, nickname, avatarUrl, likeNum)
   */
  constructor(options = {}) {
    this.appId = options.appId || process.env.DOUYIN_APP_ID || '';
    this.appSecret = options.appSecret || process.env.DOUYIN_APP_SECRET || '';
    this.pushSecret = options.pushSecret || process.env.DOUYIN_PUSH_SECRET || '';
    this.onComment = options.onComment || (() => {});
    this.onGift = options.onGift || (() => {});
    this.onLike = options.onLike || (() => {});

    // access_token 缓存
    this._accessToken = null;
    this._tokenExpireAt = 0;
    this._refreshTimer = null;

    // 活跃任务: roomId → { msgTypes: [...], startedAt, lastRenewAt }
    this.activeTasks = new Map();

    // 推送任务自动续约定时器（每30分钟续约一次，防止任务过期）
    this._taskRenewTimer = setInterval(() => this._renewAllTasks(), 30 * 60 * 1000);

    // msg_id去重: 防止抖音重复推送同一条消息
    this._seenMsgIds = new Map(); // msg_id -> timestamp
    this._dedupeCleanupTimer = setInterval(() => this._cleanupSeenMsgIds(), 60000); // 每分钟清理

    // 统计
    this.stats = {
      totalComments: 0,
      totalGifts: 0,
      totalLikes: 0,
      duplicatesSkipped: 0,
      pushesReceived: 0,
      errors: 0
    };

    if (this.appId && this.appSecret) {
      console.log(`[DouyinAPI] Initialized with appId: ${this.appId.substring(0, 8)}...`);
    } else {
      console.log('[DouyinAPI] No credentials configured - Douyin integration disabled');
    }
  }

  // ==================== access_token 管理 ====================

  /**
   * 获取access_token（带缓存，过期自动刷新）
   */
  async getAccessToken() {
    if (this._accessToken && Date.now() < this._tokenExpireAt) {
      return this._accessToken;
    }

    return this._refreshAccessToken();
  }

  /**
   * 刷新access_token
   * 小玩法专用接口: POST https://minigame.zijieapi.com/mgplatform/api/apps/v2/token
   * 参数: appid + secret (不是 client_key/client_secret)
   * 响应: { err_no: 0, err_tips: "success", data: { access_token, expires_in: 7200 } }
   */
  async _refreshAccessToken() {
    if (!this.appId || !this.appSecret) {
      throw new Error('Douyin appId/appSecret not configured');
    }

    try {
      const data = JSON.stringify({
        appid: this.appId,
        secret: this.appSecret,
        grant_type: 'client_credential'
      });

      const result = await this._httpRequest('POST', DOUYIN_TOKEN_URL, data);

      if (result.err_no === 0 && result.data && result.data.access_token) {
        this._accessToken = result.data.access_token;
        // 提前5分钟刷新（有效期通常7200秒=2小时）
        const expiresIn = (result.data.expires_in || 7200) - 300;
        this._tokenExpireAt = Date.now() + expiresIn * 1000;

        console.log(`[DouyinAPI] Access token refreshed, expires in ${expiresIn}s`);

        // 设置自动刷新
        if (this._refreshTimer) clearTimeout(this._refreshTimer);
        this._refreshTimer = setTimeout(() => {
          this._refreshAccessToken().catch(e => {
            console.error(`[DouyinAPI] Auto-refresh failed: ${e.message}`);
          });
        }, expiresIn * 1000);

        return this._accessToken;
      }

      throw new Error(`Token response error: err_no=${result.err_no}, err_tips=${result.err_tips}`);
    } catch (e) {
      console.error(`[DouyinAPI] Token refresh failed: ${e.message}`);
      throw e;
    }
  }

  // ==================== 直播间信息 ====================

  /**
   * 通过直播伴侣传入的token获取直播间信息
   * POST https://webcast.bytedance.com/api/webcastmate/info
   * Header: X-Token: {token}
   * Body: { token: "..." }
   *
   * 响应格式:
   * {
   *   data: {
   *     info: {
   *       room_id: 7214015683695250235,
   *       anchor_open_id: "...",
   *       avatar_url: "...",
   *       nick_name: "..."
   *     }
   *   }
   * }
   *
   * token有效期30分钟，限流10次/秒/appId
   *
   * @param {string} token - 直播伴侣启动exe时传入的token
   * @returns {Promise<{roomId: string, anchorOpenId: string, nickName: string, avatarUrl: string}>}
   */
  async getRoomInfo(token) {
    if (!token) throw new Error('Token is required');

    console.log(`[DouyinAPI] GetRoomInfo: token=${token.substring(0, 20)}... (len=${token.length})`);

    // 先获取我们自己的access_token（appId/appSecret换取的）
    const accessToken = await this.getAccessToken();
    console.log(`[DouyinAPI] Using access_token: ${accessToken.substring(0, 20)}...`);

    const data = JSON.stringify({ token });

    // X-Token header放的是access_token，body里的token是直播伴侣传入的token
    // returnRaw=true 以保留原始JSON字符串，用于提取大整数room_id（避免JS精度丢失）
    const { parsed: result, raw: rawResponse } = await this._httpRequest(
      'POST',
      `${DOUYIN_WEBCAST_BASE}/api/webcastmate/info`,
      data,
      { 'X-Token': accessToken },
      true  // returnRaw
    );

    console.log(`[DouyinAPI] GetRoomInfo response: ${rawResponse.substring(0, 300)}`);

    if (result.data && result.data.info) {
      const info = result.data.info;

      // 从原始JSON字符串中用正则提取room_id（避免JS Number精度丢失）
      // room_id 可能超过 Number.MAX_SAFE_INTEGER (9007199254740991)
      const roomIdMatch = rawResponse.match(/"room_id"\s*:\s*(\d+)/);
      const roomId = roomIdMatch ? roomIdMatch[1] : String(info.room_id);

      console.log(`[DouyinAPI] GetRoomInfo success: room_id=${roomId} (raw_match=${!!roomIdMatch}), anchor=${info.nick_name}`);
      return {
        roomId,
        anchorOpenId: info.anchor_open_id || '',
        nickName: info.nick_name || '',
        avatarUrl: info.avatar_url || ''
      };
    }

    throw new Error(`GetRoomInfo failed: ${JSON.stringify(result)}`);
  }

  /**
   * 完整的初始化流程：token → getRoomInfo → startTasks
   * Unity客户端启动时调用一次即可
   *
   * @param {string} token - 直播伴侣传入的token
   * @returns {Promise<{roomId: string, anchorOpenId: string, nickName: string, startedTypes: string[]}>}
   */
  async initFromToken(token) {
    // 1. 用token获取直播间信息
    const roomInfo = await this.getRoomInfo(token);

    // 2. 启动推送任务（评论+礼物+点赞），失败时会自动后台重试
    const startedTypes = await this.startTasks(roomInfo.roomId);

    const taskInfo = this.activeTasks.get(roomInfo.roomId);
    const isRetrying = taskInfo && taskInfo.retrying;

    console.log(`[DouyinAPI] Init complete: room=${roomInfo.roomId}, immediate_tasks=${startedTypes.join(',') || 'none'}, retrying=${isRetrying}`);

    return {
      roomId: roomInfo.roomId,
      anchorOpenId: roomInfo.anchorOpenId,
      nickName: roomInfo.nickName,
      startedTypes,
      retrying: isRetrying
    };
  }

  // ==================== 任务管理 ====================

  /**
   * 为直播间启动数据推送任务（带自动重试）
   * @param {string} roomId - 抖音直播间ID
   * @param {string[]} msgTypes - 要订阅的消息类型
   * @param {object} options - 选项
   * @param {number} options.maxRetries - 最大重试次数（默认5）
   * @param {number} options.retryDelay - 重试间隔毫秒（默认3000）
   */
  async startTasks(roomId, msgTypes = ['live_comment', 'live_gift', 'live_like'], options = {}) {
    const maxRetries = options.maxRetries || 5;
    const retryDelay = options.retryDelay || 3000;
    const startedTypes = [];
    const failedTypes = [];

    for (const msgType of msgTypes) {
      try {
        const token = await this.getAccessToken();
        const result = await this._startTask(token, roomId, msgType);

        if (result.err_no === 0) {
          console.log(`[DouyinAPI] ✅ Task started: ${msgType} for room ${roomId} (task_id: ${result.data?.task_id})`);
          startedTypes.push(msgType);
        } else {
          console.warn(`[DouyinAPI] ❌ Task start rejected: ${msgType} for room ${roomId} (err_no: ${result.err_no}, msg: ${result.err_msg})`);
          failedTypes.push({ msgType, err_no: result.err_no, err_msg: result.err_msg });
        }
      } catch (e) {
        console.error(`[DouyinAPI] Task start error (${msgType}, room ${roomId}): ${e.message}`);
        failedTypes.push({ msgType, err_no: -1, err_msg: e.message });
      }
    }

    // 如果有失败的任务且包含5003019错误，启动后台重试
    const retryableTypes = failedTypes
      .filter(f => f.err_no === 1 || f.err_no === -1)
      .map(f => f.msgType);

    if (retryableTypes.length > 0 && maxRetries > 0) {
      console.log(`[DouyinAPI] 🔄 Will retry ${retryableTypes.length} failed tasks in ${retryDelay / 1000}s (max ${maxRetries} retries)...`);
      this._retryTasksInBackground(roomId, retryableTypes, maxRetries, retryDelay);
    }

    this.activeTasks.set(roomId, { msgTypes: [...startedTypes], startedAt: Date.now(), retrying: retryableTypes.length > 0 });
    return startedTypes;
  }

  /**
   * 后台重试启动失败的推送任务
   * 典型场景: 小玩法还未完成挂载时task/start返回5003019
   */
  async _retryTasksInBackground(roomId, msgTypes, maxRetries, retryDelay) {
    let remaining = [...msgTypes];

    for (let attempt = 1; attempt <= maxRetries && remaining.length > 0; attempt++) {
      await new Promise(r => setTimeout(r, retryDelay));

      console.log(`[DouyinAPI] 🔄 Retry attempt ${attempt}/${maxRetries} for room ${roomId}: ${remaining.join(', ')}`);

      const stillFailed = [];
      for (const msgType of remaining) {
        try {
          const token = await this.getAccessToken();
          const result = await this._startTask(token, roomId, msgType);

          if (result.err_no === 0) {
            console.log(`[DouyinAPI] ✅ Retry success: ${msgType} for room ${roomId} (attempt ${attempt})`);
            // 更新activeTasks
            const taskInfo = this.activeTasks.get(roomId);
            if (taskInfo && !taskInfo.msgTypes.includes(msgType)) {
              taskInfo.msgTypes.push(msgType);
            }
          } else {
            console.warn(`[DouyinAPI] ❌ Retry failed: ${msgType} (err_no: ${result.err_no})`);
            stillFailed.push(msgType);
          }
        } catch (e) {
          console.warn(`[DouyinAPI] ❌ Retry error: ${msgType} (${e.message})`);
          stillFailed.push(msgType);
        }
      }

      remaining = stillFailed;

      if (remaining.length === 0) {
        console.log(`[DouyinAPI] ✅ All tasks started successfully for room ${roomId}`);
        const taskInfo = this.activeTasks.get(roomId);
        if (taskInfo) taskInfo.retrying = false;
        return;
      }
    }

    if (remaining.length > 0) {
      console.error(`[DouyinAPI] ⚠️ Tasks still failed after ${maxRetries} retries for room ${roomId}: ${remaining.join(', ')}`);
      console.error('[DouyinAPI] 排查: 1. 检查直播间是否在播 2. 检查小玩法是否已挂载 3. 检查能力是否已开通');
      const taskInfo = this.activeTasks.get(roomId);
      if (taskInfo) taskInfo.retrying = false;
    }
  }

  /**
   * 定期续约所有活跃房间的推送任务
   * 抖音推送任务有时效性，需要定期重新start保持活跃
   * 如果task已存在且活跃，start会返回成功（幂等操作）
   */
  async _renewAllTasks() {
    if (this.activeTasks.size === 0) return;

    console.log(`[DouyinAPI] 🔄 Renewing push tasks for ${this.activeTasks.size} rooms...`);

    for (const [roomId, taskInfo] of this.activeTasks.entries()) {
      if (taskInfo.retrying) continue; // 正在重试中的跳过

      const msgTypes = ['live_comment', 'live_gift', 'live_like'];
      let renewed = 0;
      let failed = 0;

      for (const msgType of msgTypes) {
        try {
          const token = await this.getAccessToken();
          const result = await this._startTask(token, roomId, msgType);

          if (result.err_no === 0) {
            renewed++;
          } else {
            failed++;
            console.warn(`[DouyinAPI] ⚠️ Renew failed: ${msgType} for room ${roomId} (err_no: ${result.err_no}, msg: ${result.err_msg})`);
          }
        } catch (e) {
          failed++;
          console.warn(`[DouyinAPI] ⚠️ Renew error: ${msgType} for room ${roomId}: ${e.message}`);
        }
      }

      taskInfo.lastRenewAt = Date.now();
      taskInfo.msgTypes = msgTypes.slice(0, renewed); // 更新实际成功的类型

      if (failed > 0) {
        console.warn(`[DouyinAPI] ⚠️ Room ${roomId} renew: ${renewed} ok, ${failed} failed`);
      } else {
        console.log(`[DouyinAPI] ✅ Room ${roomId} tasks renewed (${renewed} types)`);
      }
    }
  }

  /**
   * 停止直播间的所有数据推送任务
   */
  async stopTasks(roomId) {
    const taskInfo = this.activeTasks.get(roomId);
    if (!taskInfo) return;

    const token = await this.getAccessToken();

    for (const msgType of taskInfo.msgTypes) {
      try {
        await this._stopTask(token, roomId, msgType);
        console.log(`[DouyinAPI] Task stopped: ${msgType} for room ${roomId}`);
      } catch (e) {
        console.error(`[DouyinAPI] Stop task failed (${msgType}, room ${roomId}): ${e.message}`);
      }
    }

    this.activeTasks.delete(roomId);
  }

  /**
   * 启动单个任务
   * POST https://webcast.bytedance.com/api/live_data/task/start
   * Header: access-token: {token}
   * Body: { roomid, appid, msg_type }
   */
  async _startTask(token, roomId, msgType) {
    const data = JSON.stringify({
      roomid: roomId,
      appid: this.appId,
      msg_type: msgType
    });

    return this._httpRequest('POST', `${DOUYIN_WEBCAST_BASE}/api/live_data/task/start`, data, {
      'access-token': token
    });
  }

  /**
   * 停止单个任务
   * POST https://webcast.bytedance.com/api/live_data/task/stop
   */
  async _stopTask(token, roomId, msgType) {
    const data = JSON.stringify({
      roomid: roomId,
      appid: this.appId,
      msg_type: msgType
    });

    return this._httpRequest('POST', `${DOUYIN_WEBCAST_BASE}/api/live_data/task/stop`, data, {
      'access-token': token
    });
  }

  // ==================== 推送数据处理 ====================

  /**
   * 处理抖音推送的HTTP POST回调数据
   * 统一入口，根据msg_type分发到不同处理器
   *
   * 抖音推送body格式:
   * {
   *   roomid: "直播间ID",
   *   msg_type: "live_comment" | "live_gift" | "live_like" | "live_fansclub",
   *   payload: "[{...}, {...}]"   // 注意: payload是JSON字符串，需要JSON.parse
   * }
   *
   * @param {object} body - HTTP POST body (express已解析为对象)
   * @returns {object} - 响应数据（返回给抖音）
   */
  handlePushData(body) {
    try {
      this.stats.pushesReceived++;
      const { msg_type, payload, roomid } = body;

      if (!msg_type || !payload || !roomid) {
        console.warn(`[DouyinAPI] Invalid push data: missing msg_type/payload/roomid. Got: msg_type=${msg_type}, roomid=${roomid}, payload=${!!payload}, full_body=${JSON.stringify(body).substring(0, 300)}`);
        return { err_no: 0, err_msg: 'ok' };
      }

      // payload 是JSON字符串，需要反序列化
      let items;
      try {
        items = typeof payload === 'string' ? JSON.parse(payload) : payload;
      } catch (e) {
        console.warn(`[DouyinAPI] Payload parse error: ${e.message}`);
        return { err_no: 0, err_msg: 'ok' };
      }

      if (!Array.isArray(items)) {
        console.warn('[DouyinAPI] Parsed payload is not array');
        return { err_no: 0, err_msg: 'ok' };
      }

      for (const item of items) {
        this._processItem(msg_type, roomid, item);
      }

      return { err_no: 0, err_msg: 'ok' };
    } catch (e) {
      this.stats.errors++;
      console.error(`[DouyinAPI] Push data error: ${e.message}`);
      return { err_no: 0, err_msg: 'ok' }; // 始终返回成功，避免触发熔断
    }
  }

  /**
   * 处理单条推送消息
   */
  _processItem(msgType, roomId, item) {
    // msg_id去重: 跳过已处理的消息
    if (item.msg_id && this._seenMsgIds.has(item.msg_id)) {
      this.stats.duplicatesSkipped++;
      return;
    }
    if (item.msg_id) {
      this._seenMsgIds.set(item.msg_id, Date.now());
    }

    switch (msgType) {
      case 'live_comment': {
        // payload item: { msg_id, sec_openid, content, avatar_url, nickname, timestamp }
        this.stats.totalComments++;
        this.onComment(
          roomId,
          item.sec_openid,
          item.nickname || '匿名用户',
          item.avatar_url || '',
          item.content || ''
        );
        break;
      }

      case 'live_gift': {
        // payload item: { msg_id, sec_openid, sec_gift_id, gift_num, gift_value,
        //                  avatar_url, nickname, timestamp, test, audience_sec_open_id }
        // test=true 表示测试/自查工具数据，需过滤
        if (item.test === true) {
          console.log(`[DouyinAPI] Filtered test gift from ${item.nickname}`);
          return;
        }
        this.stats.totalGifts++;
        this.onGift(
          roomId,
          item.sec_openid,
          item.nickname || '匿名用户',
          item.avatar_url || '',
          item.sec_gift_id || '',
          item.gift_num || 1,
          item.gift_value || 0  // 单位: 分（礼物总价值）
        );
        break;
      }

      case 'live_like': {
        // payload item: { msg_id, sec_openid, like_num, avatar_url, nickname, timestamp }
        // like_num 是上游2秒合并一次的聚合数据
        this.stats.totalLikes++;
        this.onLike(
          roomId,
          item.sec_openid,
          item.nickname || '匿名用户',
          item.avatar_url || '',
          item.like_num || 1
        );
        break;
      }

      case 'live_fansclub': {
        // payload item: { msg_id, sec_openid, avatar_url, nickname, timestamp,
        //                  fansclub_reason_type(1=升级,2=加团), fansclub_level }
        // 暂不处理
        break;
      }

      default:
        console.log(`[DouyinAPI] Unknown msg_type: ${msgType}`);
    }
  }

  // ==================== 签名验证 ====================

  /**
   * 验证抖音推送的签名
   *
   * 官方算法:
   * 1. 从Header取: x-nonce-str, x-timestamp, x-roomid, x-msg-type（排除 x-signature 和 content-type）
   * 2. 按key字典序排序，拼接为 key1=value1&key2=value2&... 格式
   * 3. 直接追加body原始字符串 + appSecret（无连接符）
   * 4. 计算 MD5（16 bytes）→ Base64 编码
   *
   * Header名称:
   *   x-nonce-str, x-timestamp, x-roomid, x-msg-type, x-signature
   *
   * @param {object} headers - HTTP请求headers (express req.headers，全小写key)
   * @param {string} rawBody - 原始body字符串（未经JSON.parse的原始请求体）
   * @returns {boolean} 签名是否有效
   */
  verifySignature(headers, rawBody) {
    // 签名密钥优先级: pushSecret > appSecret
    const secret = this.pushSecret || this.appSecret;
    if (!secret) return true; // 未配置任何密钥则跳过验证

    const signature = headers['x-signature'];
    if (!signature) return true; // 没有签名header则跳过

    // 收集需要参与签名的header（排除 x-signature 和 content-type）
    const signHeaders = {};
    const signHeaderKeys = ['x-nonce-str', 'x-timestamp', 'x-roomid', 'x-msg-type'];
    for (const key of signHeaderKeys) {
      if (headers[key]) {
        signHeaders[key] = headers[key];
      }
    }

    // 按key字典序排序
    const sortedKeys = Object.keys(signHeaders).sort();

    // 拼接为 key1=value1&key2=value2&... 格式
    const headerStr = sortedKeys.map(k => `${k}=${signHeaders[k]}`).join('&');

    // 追加body原始字符串 + 密钥（pushSecret优先）
    const signStr = headerStr + rawBody + secret;

    // MD5 → Base64
    const expected = crypto.createHash('md5').update(signStr).digest('base64');

    if (expected !== signature) {
      // 如果pushSecret验签失败，再尝试appSecret（兼容两种密钥）
      if (this.pushSecret && this.appSecret && this.pushSecret !== this.appSecret) {
        const signStr2 = headerStr + rawBody + this.appSecret;
        const expected2 = crypto.createHash('md5').update(signStr2).digest('base64');
        if (expected2 === signature) return true;
      }
      console.warn('[DouyinAPI] Signature mismatch!', {
        received: signature,
        expected: expected,
        headers: Object.fromEntries(signHeaderKeys.map(k => [k, headers[k]]))
      });
      return false;
    }

    return true;
  }

  // ==================== HTTP请求工具 ====================

  /**
   * 发送HTTPS请求
   */
  _httpRequest(method, url, body, extraHeaders = {}, returnRaw = false) {
    return new Promise((resolve, reject) => {
      const urlObj = new URL(url);

      const options = {
        hostname: urlObj.hostname,
        port: urlObj.port || 443,
        path: urlObj.pathname + urlObj.search,
        method,
        headers: {
          'Content-Type': 'application/json',
          ...extraHeaders
        }
      };

      if (body) {
        options.headers['Content-Length'] = Buffer.byteLength(body);
      }

      const req = https.request(options, (res) => {
        let data = '';
        res.on('data', chunk => { data += chunk; });
        res.on('end', () => {
          try {
            const parsed = JSON.parse(data);
            resolve(returnRaw ? { parsed, raw: data } : parsed);
          } catch (e) {
            reject(new Error(`Invalid JSON response: ${data.substring(0, 200)}`));
          }
        });
      });

      req.on('error', (e) => reject(e));
      req.setTimeout(10000, () => {
        req.destroy(new Error('Request timeout'));
      });

      if (body) req.write(body);
      req.end();
    });
  }

  // ==================== 统计 & 清理 ====================

  /**
   * 获取统计信息
   */
  getStats() {
    return {
      ...this.stats,
      activeTaskRooms: this.activeTasks.size,
      hasCredentials: !!(this.appId && this.appSecret),
      tokenValid: !!(this._accessToken && Date.now() < this._tokenExpireAt)
    };
  }

  /**
   * 清理过期的msg_id（保留最近5分钟内的）
   */
  _cleanupSeenMsgIds() {
    const expireTime = Date.now() - 5 * 60 * 1000;
    let cleaned = 0;
    for (const [msgId, ts] of this._seenMsgIds.entries()) {
      if (ts < expireTime) {
        this._seenMsgIds.delete(msgId);
        cleaned++;
      }
    }
    if (cleaned > 0) {
      console.log(`[DouyinAPI] Cleaned ${cleaned} expired msg_ids (remaining: ${this._seenMsgIds.size})`);
    }
  }

  /**
   * 关闭（服务器退出时调用）
   */
  shutdown() {
    if (this._refreshTimer) {
      clearTimeout(this._refreshTimer);
      this._refreshTimer = null;
    }
    if (this._taskRenewTimer) {
      clearInterval(this._taskRenewTimer);
      this._taskRenewTimer = null;
    }
    if (this._dedupeCleanupTimer) {
      clearInterval(this._dedupeCleanupTimer);
      this._dedupeCleanupTimer = null;
    }
    this._seenMsgIds.clear();
    console.log('[DouyinAPI] Shutdown');
  }
}

module.exports = DouyinAPI;
