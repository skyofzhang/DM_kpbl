/**
 * 卡皮巴拉对决 - Node.js 服务端
 *
 * 架构: 多房间 + 抖音数据推送
 *
 * 职责:
 * 1. 管理多个游戏房间（每个主播直播间=一个Room）
 * 2. 接收抖音直播弹幕/礼物/点赞事件推送（HTTP POST）
 * 3. WebSocket同步到Unity客户端（按roomId路由）
 *
 * 房间生命周期:
 *   客户端连接(join_room) → 创建/加入房间
 *   客户端断开 → 暂停房间（保留状态）
 *   客户端重连 → 恢复房间
 *   30分钟无连接 → 销毁房间
 *
 * 抖音数据流:
 *   主播直播间 → 抖音服务器 → POST /api/douyin/push → RoomManager路由 → Room处理
 */

const WebSocket = require('ws');
const express = require('express');
require('dotenv').config();

const RoomManager = require('./RoomManager');
const DouyinAPI = require('./DouyinAPI');
const { getAllGifts } = require('./GiftConfig');

const http = require('http');
const config = require('../config/default.json');
const PORT = process.env.PORT || config.server.wsPort; // 统一使用wsPort(8081)

// ==================== Express HTTP ====================
const app = express();

// 抖音推送签名验证需要原始body字符串，所以用verify回调保存
app.use(express.json({
  limit: '1mb',
  verify: (req, res, buf) => {
    // 保存原始body用于签名验证
    req.rawBody = buf.toString('utf8');
  }
}));

const server = http.createServer(app);

// ==================== WebSocket ====================
const wss = new WebSocket.Server({ server });

// ==================== 核心模块 ====================
const roomManager = new RoomManager(config.game);

// 抖音API（评论/礼物/点赞回调路由到RoomManager）
const douyinAPI = new DouyinAPI({
  appId: process.env.DOUYIN_APP_ID,
  appSecret: process.env.DOUYIN_APP_SECRET,
  onComment: (roomId, secOpenId, nickname, avatarUrl, content) => {
    roomManager.routeDouyinComment(roomId, secOpenId, nickname, avatarUrl, content);
  },
  onGift: (roomId, secOpenId, nickname, avatarUrl, secGiftId, giftNum, giftValue) => {
    roomManager.routeDouyinGift(roomId, secOpenId, nickname, avatarUrl, secGiftId, giftNum, giftValue);
  },
  onLike: (roomId, secOpenId, nickname, avatarUrl, likeNum) => {
    roomManager.routeDouyinLike(roomId, secOpenId, nickname, avatarUrl, likeNum);
  }
});

// ==================== HTTP 路由 ====================

// 健康检查
app.get('/health', (req, res) => {
  res.json({
    status: 'ok',
    timestamp: Date.now(),
    rooms: roomManager.getStats()
  });
});

// 全局统计
app.get('/stats', (req, res) => {
  res.json({
    rooms: roomManager.getStats(),
    douyin: douyinAPI.getStats()
  });
});

// 礼物配置
app.get('/gifts', (req, res) => {
  res.json(getAllGifts());
});

// 指定房间状态
app.get('/room/:roomId', (req, res) => {
  const room = roomManager.getRoom(req.params.roomId);
  if (!room) {
    return res.status(404).json({ error: 'Room not found' });
  }
  res.json(room.getInfo());
});

// 指定房间排行榜
app.get('/room/:roomId/rankings', (req, res) => {
  const room = roomManager.getRoom(req.params.roomId);
  if (!room) {
    return res.status(404).json({ error: 'Room not found' });
  }
  res.json(room.playerManager.getAllRankings());
});

// 指定房间历史排行
app.get('/room/:roomId/rankings/history', (req, res) => {
  const room = roomManager.getRoom(req.params.roomId);
  if (!room) {
    return res.status(404).json({ error: 'Room not found' });
  }
  const period = req.query.period || 'weekly';
  res.json(room.playerManager.getHistoryRankings(period));
});

// ==================== 抖音数据推送回调 ====================

/**
 * 抖音推送数据接收端点
 * POST /api/douyin/push
 *
 * 抖音服务器推送格式:
 * {
 *   roomid: "直播间ID",
 *   msg_type: "live_comment" | "live_gift" | "live_like" | "live_fansclub",
 *   payload: "[{...}, {...}]"   // JSON字符串，需要JSON.parse反序列化
 * }
 *
 * Headers (用于签名验证):
 *   x-nonce-str: 随机字符串
 *   x-timestamp: 时间戳
 *   x-roomid: 直播间ID
 *   x-msg-type: 消息类型
 *   x-signature: 签名值
 */
app.post('/api/douyin/push', (req, res) => {
  /**
   * 履约ACK策略: 先返回HTTP 200，再异步处理数据
   * - 抖音要求2秒内返回2XX（礼物3秒），否则计为推送失败
   * - 连续10次失败触发熔断，平台直接丢弃数据
   * - 因此签名验证失败也返回200（只记日志），避免误触熔断
   */
  const msgType = req.headers['x-msg-type'] || req.body?.msg_type || 'unknown';
  const roomId = req.headers['x-roomid'] || req.body?.roomid || 'unknown';

  // 签名验证 — 失败时仍返回200避免熔断，只记警告日志
  const signature = req.headers['x-signature'];
  if (signature && !douyinAPI.verifySignature(req.headers, req.rawBody || '')) {
    console.warn(`[KPBL] ⚠️ Push signature FAILED: msg_type=${msgType}, roomid=${roomId}`);
    res.json({ err_no: 0, err_msg: 'ok' });
    return;
  }

  // 立即返回200（ACK确认），后续异步处理数据
  res.json({ err_no: 0, err_msg: 'ok' });

  // 异步处理推送数据（不阻塞HTTP响应）
  try {
    console.log(`[KPBL] 📨 Push: msg_type=${msgType}, roomid=${roomId}`);

    let pushBody = req.body;
    if (!pushBody.msg_type && req.headers['x-msg-type']) {
      pushBody = {
        msg_type: req.headers['x-msg-type'],
        roomid: req.headers['x-roomid'],
        payload: req.rawBody || JSON.stringify(req.body)
      };
    }

    douyinAPI.handlePushData(pushBody);
  } catch (e) {
    console.error(`[KPBL] Push processing error: ${e.message}`);
  }
});

/**
 * 抖音初始化端点（Unity客户端启动时调用）
 * POST /api/douyin/init  { token: "直播伴侣传入的token" }
 *
 * 流程:
 * 1. 用token调用 GetRoomInfo 获取直播间ID
 * 2. 自动启动推送任务（评论+礼物+点赞）
 * 3. 返回roomId给客户端，客户端用此roomId加入WebSocket房间
 *
 * 响应: { success, roomId, anchorName, startedTypes }
 */
app.post('/api/douyin/init', async (req, res) => {
  try {
    const { token } = req.body;
    if (!token) return res.status(400).json({ error: 'token required' });

    console.log(`[KPBL] Douyin init request: token=${token.substring(0, 20)}... (len=${token.length})`);

    const result = await douyinAPI.initFromToken(token);
    res.json({
      success: true,
      roomId: result.roomId,
      anchorName: result.nickName,
      startedTypes: result.startedTypes,
      retrying: result.retrying || false
    });
  } catch (e) {
    console.error(`[KPBL] Douyin init failed: ${e.message}`);
    res.status(500).json({ error: e.message });
  }
});

/**
 * 抖音任务管理端点（手动启动/停止数据推送任务）
 * POST /api/douyin/task/start  { roomId, msgTypes?: [...] }
 * POST /api/douyin/task/stop   { roomId }
 */
app.post('/api/douyin/task/start', async (req, res) => {
  try {
    const { roomId, msgTypes, maxRetries, retryDelay } = req.body;
    if (!roomId) return res.status(400).json({ error: 'roomId required' });
    const result = await douyinAPI.startTasks(roomId, msgTypes, { maxRetries, retryDelay });
    const taskInfo = douyinAPI.activeTasks.get(roomId);
    res.json({
      success: true,
      startedTypes: result,
      retrying: taskInfo?.retrying || false
    });
  } catch (e) {
    res.status(500).json({ error: e.message });
  }
});

// 查看活跃推送任务状态
app.get('/api/douyin/tasks', (req, res) => {
  const tasks = {};
  for (const [roomId, info] of douyinAPI.activeTasks.entries()) {
    tasks[roomId] = {
      msgTypes: info.msgTypes,
      startedAt: new Date(info.startedAt).toISOString(),
      retrying: info.retrying || false,
      durationSec: Math.round((Date.now() - info.startedAt) / 1000)
    };
  }
  res.json({ activeTasks: tasks, douyinStats: douyinAPI.getStats() });
});

app.post('/api/douyin/task/stop', async (req, res) => {
  try {
    const { roomId } = req.body;
    if (!roomId) return res.status(400).json({ error: 'roomId required' });
    await douyinAPI.stopTasks(roomId);
    res.json({ success: true });
  } catch (e) {
    res.status(500).json({ error: e.message });
  }
});

// ==================== 向后兼容: 旧版HTTP路由 ====================
// 这些路由操作 "default" 房间，确保旧客户端不报错

app.get('/state', (req, res) => {
  const room = roomManager.getOrCreateRoom('default');
  res.json({
    ...room.gameEngine.getState(),
    playerCount: room.playerManager.getTotalCount(),
    leftCount: room.playerManager.getCampCount('left'),
    rightCount: room.playerManager.getCampCount('right'),
    simulatorEnabled: room.simulator.isEnabled()
  });
});

app.get('/rankings', (req, res) => {
  const room = roomManager.getOrCreateRoom('default');
  res.json(room.playerManager.getAllRankings());
});

app.get('/rankings/history', (req, res) => {
  const room = roomManager.getOrCreateRoom('default');
  const period = req.query.period || 'weekly';
  res.json(room.playerManager.getHistoryRankings(period));
});

// ==================== WebSocket 连接处理 ====================

wss.on('connection', (ws, req) => {
  // 从URL参数或首条消息获取roomId
  // 支持: ws://host:port?roomId=xxx 或连接后发送 join_room 消息
  const urlParams = new URL(req.url, `http://${req.headers.host}`).searchParams;
  const roomIdFromUrl = urlParams.get('roomId');

  if (roomIdFromUrl) {
    // URL中指定了roomId，立即加入房间
    const room = roomManager.handleClientConnect(ws, roomIdFromUrl);
    console.log(`[KPBL] Client connected to room: ${roomIdFromUrl}`);
  } else {
    // 等待 join_room 消息
    ws._pendingJoin = true;
    console.log('[KPBL] Client connected (waiting for join_room)');
  }

  ws.on('message', (message) => {
    try {
      const msg = JSON.parse(message.toString());

      // 处理加入房间（首次连接时）
      if (msg.type === 'join_room') {
        const roomId = (msg.data && msg.data.roomId) || 'default';
        if (ws._pendingJoin || !ws._roomId) {
          ws._pendingJoin = false;
          const room = roomManager.handleClientConnect(ws, roomId);
          console.log(`[KPBL] Client joined room: ${roomId}`);

          // 如果游戏引擎是暂停的running状态，恢复tick
          if (room.gameEngine.state === 'running' && !room.gameEngine.timer) {
            room.gameEngine.resume();
          }
        }
        return;
      }

      // 向后兼容: 如果客户端没有发join_room，自动加入default房间
      if (ws._pendingJoin || !ws._roomId) {
        ws._pendingJoin = false;
        const room = roomManager.handleClientConnect(ws, 'default');
        console.log('[KPBL] Client auto-joined default room');

        if (room.gameEngine.state === 'running' && !room.gameEngine.timer) {
          room.gameEngine.resume();
        }
      }

      // 路由消息到对应房间
      roomManager.routeMessage(ws, msg);

    } catch (e) {
      console.error('[KPBL] Message parse error:', e.message);
    }
  });

  ws.on('close', () => {
    const roomId = ws._roomId || 'unknown';
    roomManager.handleClientDisconnect(ws);
    console.log(`[KPBL] Client disconnected from room: ${roomId}`);
  });

  ws.on('error', (err) => {
    console.error('[KPBL] WebSocket error:', err.message);
  });
});

// ==================== 优雅关闭 ====================

function gracefulShutdown() {
  console.log('[KPBL] Shutting down...');
  douyinAPI.shutdown();
  roomManager.shutdown();
  wss.close();
  server.close(() => {
    console.log('[KPBL] Server closed');
    process.exit(0);
  });
  // 强制退出保底（5秒）
  setTimeout(() => process.exit(0), 5000);
}

process.on('SIGTERM', gracefulShutdown);
process.on('SIGINT', gracefulShutdown);

// ==================== 启动服务 ====================
server.listen(PORT, () => {
  console.log(`[KPBL] HTTP + WebSocket server on port ${PORT}`);
  console.log('[KPBL] Multi-room architecture enabled');
  console.log(`[KPBL] Douyin integration: ${douyinAPI.appId ? 'ENABLED' : 'DISABLED (no credentials)'}`);
  console.log('[KPBL] Waiting for clients...');
});
