# 卡皮巴拉对决 — 抖音小玩法接入指南

> **最后更新**: 2026-02-13 ✅ 端到端验证通过
> **用途**: 后续新游戏接入抖音时的完整参考手册。含完整踩坑记录和排查自检清单，目标是一遍成功。

---

## 零、抖音开发必读文档索引

> **目标**: AI和开发者不必每次全网搜索，以下是抖音小玩法开发的核心文档链接。只放开发必须的，无关文档不放。

### 基础URL
`https://developer.open-douyin.com/docs/resource/zh-CN/interaction/develop/`

### 必读文档（按开发流程排序）

| 开发阶段 | 文档名称 | URL路径 | 关键内容 |
|---------|---------|--------|---------|
| 1. 了解架构 | SDK概览 | `.../unity-sdk/overview` | 三种接入架构选择 |
| 2. 环境搭建 | Unity SDK接入 | `.../unity-sdk/unity-sdk-access` | BGDT安装、环境要求 |
| 3. API参考 | API概览 | `.../live-unity-sdk-support/api-overview` | 全部API一览 |
| 4. 核心: 数据推送 | 指令直推能力 | `.../live-unity-sdk-support/direct-push-ability` | StartPushTask/OnMessage |
| 5. 核心: 直播间信息 | 直播间数据 | `.../live-unity-sdk-support/live-room-data` | GetRoomInfo |
| 6. 必接: ACK上报 | 互动数据履约上报 | `.../live-unity-sdk-support/ack-ability` | ReportAck（即将强制） |
| 7. 进阶: 礼物效果 | 礼物进阶互动 | `.../live-unity-sdk-support/gift-interaction-upgrade` | Round API |
| 8. 问题排查 | 常见问题 | `.../unity-sdk/faq` | 常见坑和解答 |

### 服务端相关（非SDK方式需要）

| 文档 | 说明 |
|------|------|
| 推送回调签名验证 | 签名算法(MD5→Base64)、Header字段、错误码 |
| task/start 推送任务 | 启动/停止数据推送任务 |
| access_token 接口 | 小玩法专用token获取(minigame.zijieapi.com) |

> **提示**: AI需要查找抖音开发文档时，优先在上述URL路径中搜索，不需要全网搜索。

---

## 一、整体数据流

```
观众操作（评论/送礼/点赞）
    ↓
抖音服务器（聚合后 HTTP POST 推送）
    ↓
我们的回调接口 POST /api/douyin/push（签名验证 → 解析）
    ↓
DouyinAPI._processItem() 按 msg_type 分发
    ↓
RoomManager 路由到对应 Room（roomId = 抖音直播间ID）
    ↓
Room 处理业务（加入阵营 / 礼物推力 / 点赞推力）
    ↓
GameEngine 每200ms tick 更新橘子位置
    ↓
WebSocket 广播 → Unity 客户端渲染
```

---

## 二、抖音开放平台配置

### 2.1 申请凭据

1. 登录 [抖音开放平台](https://open.douyin.com/)
2. 创建 **小玩法** 类型应用
3. 获取 `appId` 和 `appSecret`
4. 配置推送回调地址: `https://你的域名/api/douyin/push`（**必须HTTPS**）
5. 开通 **能力1（评论弹幕）** 和 **能力2（礼物+点赞）**
6. 配置 **推送密钥(PUSH_SECRET)** — 用于签名验证
7. 创建专属礼物（如需要）— 创建后会生成 `sec_gift_id`

### 2.2 服务器凭据配置

**前置: 服务器环境准备**

| 项目 | 说明 |
|------|------|
| 云服务商 | 腾讯云CVM（或任何有公网IP的Linux服务器） |
| 推荐配置 | 2核4G以上，带宽5Mbps+ |
| 操作系统 | Ubuntu 20.04+ / CentOS 7+ |
| 必装软件 | Node.js 18+, PM2, Nginx, certbot |
| 域名 | 必须有域名（抖音推送回调要求HTTPS） |

**域名配置步骤**:
1. 购买域名（如 jiuqian2025.com）
2. 在DNS服务商（如腾讯云DNSPod）添加A记录: `子域名.域名 → 服务器IP`
3. 等待DNS生效（通常几分钟到几小时）
4. 验证: `ping 子域名.域名` 能解析到正确IP

**SSL证书** → 详见 [第十三章: Nginx反向代理配置](#十三nginx反向代理配置httpswss)

在服务器上创建 `.env` 文件（或设置环境变量）：

```bash
# /opt/dm_kpbl/.env
DOUYIN_APP_ID=你的appId
DOUYIN_APP_SECRET=你的appSecret
DOUYIN_PUSH_SECRET=你的推送密钥    # ⚠️ 必须配置！用于签名验证，优先于appSecret
```

> **⚠️ 三个密钥的区别**:
> - `APP_SECRET`: 用于获取 access_token（client_credential 换 token）
> - `PUSH_SECRET`: 用于推送回调签名验证（优先使用，如缺失则降级用 APP_SECRET）
> - 代码中签名验证有双密钥降级: pushSecret 验签失败 → 再尝试 appSecret

代码加载优先级（`DouyinAPI.js` 构造函数）：
```
options.appId > process.env.DOUYIN_APP_ID > ''（空=禁用抖音集成）
```

### 2.3 Token API

| 项目 | 值 |
|------|-----|
| **接口地址** | `https://minigame.zijieapi.com/mgplatform/api/apps/v2/token` |
| **方法** | POST |
| **参数** | `{ appid, secret, grant_type: "client_credential" }` |
| **响应** | `{ err_no: 0, data: { access_token, expires_in: 7200 } }` |
| **有效期** | 7200秒（2小时），代码提前5分钟自动刷新 |
| **缓存** | 内存缓存 `_accessToken`，过期自动刷新，有定时器 |

> **注意**: 小玩法用的是 `appid + secret`，不是普通抖音应用的 `client_key + client_secret`！
> Token 请求域名是 `minigame.zijieapi.com`，不是 `open.douyin.com`！

---

## 三、Token启动流程（核心链路） — 2026-02-13 实测验证

### 3.0 完整启动时序

```
主播开播 → 直播伴侣检测到小玩法已挂载
    ↓
直播伴侣启动exe: game.exe -token=xxxxx
    ↓
Unity客户端解析命令行参数: Environment.GetCommandLineArgs()
    ↓
POST https://域名/api/douyin/init {token}
    ↓
服务器用 appId+appSecret 换取 access_token (minigame.zijieapi.com)
    ↓
服务器用 access_token + 直播伴侣token 调用 GetRoomInfo
  POST https://webcast.bytedance.com/api/webcastmate/info
  Header: X-Token = access_token
  Body: {token: 直播伴侣token}
    ↓
获取 roomId（⚠️ 必须从原始JSON用正则提取，防JS大整数精度丢失！）
    ↓
服务器调用 task/start 启动3种推送任务（comment/gift/like）
  失败的任务自动后台重试（5次/3秒间隔）
    ↓
返回 {success, roomId, anchorName, startedTypes} 给Unity客户端
    ↓
Unity客户端用roomId连接WebSocket: wss://域名?roomId=xxx
    ↓
抖音服务器开始推送数据 → POST /api/douyin/push → 游戏内生效
```

### 3.0.1 GetRoomInfo API

| 项目 | 值 |
|------|-----|
| **接口地址** | `POST https://webcast.bytedance.com/api/webcastmate/info` |
| **Header** | `X-Token: {access_token}`（我们自己换取的token，不是直播伴侣token） |
| **Body** | `{ token: "直播伴侣传入的token" }` |
| **响应** | `{ data: { info: { room_id, nick_name, avatar_url, anchor_open_id } } }` |
| **token有效期** | 30分钟，限流10次/秒/appId |

> **⚠️ 致命坑: room_id 大整数精度丢失**
> room_id 如 `7606023010126089006` 超过 JS 的 `Number.MAX_SAFE_INTEGER` (2^53 = 9007199254740991)。
> `JSON.parse` 会截断为 `7606023010126089000`！
> **解决**: `_httpRequest` 加 `returnRaw` 参数，从原始JSON字符串用正则提取:
> `rawResponse.match(/"room_id"\s*:\s*(\d+)/)`

### 3.0.2 无Token降级（开发测试用）

Unity客户端没有token时，自动加入 `default` 房间。服务器 `_getTargetRooms()` 会将抖音推送数据同时转发到对应roomId房间 + default房间（如果有在线客户端）。

### 3.0.3 task/start 自动重试机制

`startTasks()` 遇到 err_no 非0时（典型: 5003019 小玩法未完成挂载），启动 `_retryTasksInBackground()`:
- 最大重试5次，间隔3秒
- `activeTasks` Map 中有 `retrying` 状态标记
- 所有重试完成或成功后更新状态

---

## 四、数据推送任务管理

### 3.1 启动推送任务

主播开播后，需要为直播间启动数据推送任务：

| 项目 | 值 |
|------|-----|
| **接口地址** | `https://webcast.bytedance.com/api/live_data/task/start` |
| **方法** | POST |
| **Header** | `access-token: {token}` |
| **Body** | `{ roomid, appid, msg_type }` |

每种消息类型需要**单独调用一次** start：
- `live_comment` — 评论弹幕
- `live_gift` — 礼物
- `live_like` — 点赞
- `live_fansclub` — 粉丝团（当前未使用）

### 3.2 停止推送任务

| 项目 | 值 |
|------|-----|
| **接口地址** | `https://webcast.bytedance.com/api/live_data/task/stop` |
| **方法** | POST |
| **Header** | `access-token: {token}` |
| **Body** | `{ roomid, appid, msg_type }` |

### 3.3 我们的管理接口

也可以通过我们自己的 HTTP 接口管理任务：

```bash
# 启动（默认订阅评论+礼物+点赞）
POST http://212.64.26.65:8081/api/douyin/task/start
{ "roomId": "直播间ID", "msgTypes": ["live_comment", "live_gift", "live_like"] }

# 停止
POST http://212.64.26.65:8081/api/douyin/task/stop
{ "roomId": "直播间ID" }
```

### 3.4 性能限制（来自抖音）

| 限制项 | 值 |
|--------|-----|
| QPS限制 | 默认 100 |
| 推送超时 | 评论/点赞 2秒，礼物 3秒 |
| 熔断触发 | 连续 10 次失败 |
| 点赞聚合 | 上游每 2 秒合并一次 |

---

## 四、推送回调接口（核心）

### 4.1 接口定义

| 项目 | 值 |
|------|-----|
| **Endpoint** | `POST /api/douyin/push` |
| **文件** | `Server/src/index.js` 第 140-153 行 |

### 4.2 Request Headers

```
x-nonce-str:  随机字符串
x-timestamp:  时间戳
x-roomid:     直播间ID
x-msg-type:   live_comment | live_gift | live_like | live_fansclub
x-signature:  签名值
Content-Type: application/json
```

### 4.3 Request Body 格式

```json
{
  "roomid": "直播间ID",
  "msg_type": "live_comment",
  "payload": "[{\"sec_openid\":\"xxx\",\"nickname\":\"用户A\",\"content\":\"1\"}]"
}
```

> **⚠️ 2026-02-13 实测发现重大差异**: 以上是文档描述的格式。
> **实际推送时**, msg_type 和 roomid 在 HTTP Header 中 (x-msg-type, x-roomid)，body 是纯 payload 数组！
> 必须做 header-based routing fallback:
> ```javascript
> if (!pushBody.msg_type && req.headers['x-msg-type']) {
>   pushBody = {
>     msg_type: req.headers['x-msg-type'],
>     roomid: req.headers['x-roomid'],
>     payload: req.rawBody || JSON.stringify(req.body)
>   };
> }
> ```
> `payload` 是 JSON 字符串时需要 `JSON.parse(payload)` 反序列化。

### 4.4 签名验证算法

```
1. 从 Header 取 4 个字段: x-nonce-str, x-timestamp, x-roomid, x-msg-type
   （排除 x-signature 和 content-type）
2. 按 key 字典序排序
3. 拼接为: key1=value1&key2=value2&key3=value3&key4=value4
4. 直接追加 body 原始字符串 + appSecret（无连接符！）
   → headerStr + rawBody + appSecret
5. 计算 MD5 → Base64 编码
6. 比较结果与 x-signature
```

> **关键**: 是 `MD5 → Base64`，不是 `SHA256 → hex`！之前调研时搞错过。

**代码实现**（`DouyinAPI.js`）：
```javascript
verifySignature(headers, rawBody) {
  const signHeaders = {};
  ['x-nonce-str', 'x-timestamp', 'x-roomid', 'x-msg-type'].forEach(key => {
    if (headers[key]) signHeaders[key] = headers[key];
  });
  const sortedKeys = Object.keys(signHeaders).sort();
  const headerStr = sortedKeys.map(k => `${k}=${signHeaders[k]}`).join('&');
  const signStr = headerStr + rawBody + this.appSecret;
  const expected = crypto.createHash('md5').update(signStr).digest('base64');
  return expected === signature;
}
```

**rawBody 捕获**（`index.js`）：
```javascript
app.use(express.json({
  verify: (req, res, buf) => {
    req.rawBody = buf.toString('utf8');  // 保存原始body用于签名验证
  }
}));
```

### 4.5 响应格式

**必须始终返回成功**，否则会触发熔断：
```json
{ "err_no": 0, "err_msg": "ok" }
```

---

## 五、四种消息类型详解

### 5.1 评论 (live_comment)

**payload item 字段**：
```json
{
  "msg_id": "消息ID",
  "sec_openid": "用户唯一标识",
  "nickname": "用户昵称",
  "avatar_url": "头像URL",
  "content": "评论内容",
  "timestamp": 1675000000
}
```

> **[项目示范]** 以下处理逻辑特定于"卡皮巴拉对决"。新游戏的评论关键词、阵营分配和推力值需根据玩法重新设计。

**我们的处理逻辑**（`Room.handleDouyinComment`）：
1. 游戏不在 running 状态 → 忽略
2. 解析 `content` 中的关键词：
   - `1` / `111` / `左` / `香橙` → 加入左方(香橙温泉)
   - `2` / `222` / `右` / `柚子` → 加入右方(柚子温泉)
3. 加入成功 → 给该阵营 +10 基础推力（永久）
4. 广播 `player_joined` + `ranking_update`
5. **推力升级**: 已加入阵营的玩家发 `666` 或 `6` → 给己方阵营 +3 临时推力（持续5秒）
6. 广播 `force_boost` 消息

### 5.2 礼物 (live_gift)

**payload item 字段**：
```json
{
  "msg_id": "消息ID",
  "sec_openid": "用户唯一标识",
  "nickname": "用户昵称",
  "avatar_url": "头像URL",
  "sec_gift_id": "抖音原生礼物ID",
  "gift_num": 1,
  "gift_value": 5200,
  "timestamp": 1675000000,
  "test": false
}
```

> **gift_value 单位是「分」（1分 = 0.01元 = 0.1抖币）**
> 例: 52元礼物 → gift_value = 5200
>
> **抖币与人民币换算**:
> - 1抖币 = 10分 = 0.1元人民币
> - gift_value ÷ 单价(分) = 礼物数量
>
> **6种专属礼物价值对照**:
>
> | 礼物 | 抖币价格 | 对应人民币 | gift_value(分) | 游戏推力 |
> |------|---------|-----------|---------------|---------|
> | 仙女棒 | 1抖币 | 0.1元 | 10 | +10 |
> | 能力药丸 | 10抖币 | 1元 | 100 | +343 |
> | 甜甜圈 | 52抖币 | 5.2元 | 520 | +808 |
> | 能量电池 | 99抖币 | 9.9元 | 990 | +1415 |
> | 爱的爆炸 | 199抖币 | 19.9元 | 1990 | +2679 |
> | 神秘空投 | 520抖币 | 52元 | 5200 | +6988 |
>
> **计算示例**: 用户送10个能力药丸 → gift_value = 100×10 = 1000分，gift_num = 10，推力 = 343×10 = 3430

> **[项目示范]** 以下处理逻辑和推力值特定于"卡皮巴拉对决"。新游戏的礼物效果和推力值需根据玩法重新设计。

**我们的处理逻辑**（`Room.handleDouyinGift`）：
1. 游戏不在 running 状态 → 忽略
2. `test === true` → 过滤（抖音自查工具的测试数据）
3. 玩家未加入阵营 → 随机分配一个，广播 `player_joined`
4. 按 `sec_gift_id` 精确映射到我们的游戏礼物 ID（**不是 gift_value！**）
5. 调用 `processGift()` → 累加推力 + 广播 `gift_received`

> **⚠️ 2026-02-13 重要更正**: 之前用 gift_value（价值/分）判断礼物种类是**错误的**！
> 抖音会在2秒内折叠同类型礼物推送，导致 gift_value = 单价×数量，同一种礼物的 gift_value 可能有无数种值。
> **必须用 sec_gift_id（加密礼物ID）精确匹配。**

> **[项目示范]** 以下sec_gift_id映射表仅适用于"卡皮巴拉对决"。每个游戏的sec_gift_id不同，新游戏需要重新获取（见下方"获取步骤"）。

**礼物 sec_gift_id 映射表**（`Room._mapDouyinGift`）：

```javascript
// 每个游戏的 sec_gift_id 不同！新游戏需要重新获取
const GIFT_ID_MAP = {
  'n1/Dg1905sj1FyoBlQBvmbaDZFBNaKuKZH6zxHkv8Lg5x2cRfrKUTb8gzMs=': 'fairy_wand',       // 仙女棒(1抖)  推力10
  '28rYzVFNyXEXFC8HI+f/WG+I7a6lfl3OyZZjUS+CVuwCgYZrPrUdytGHu0c=': 'ability_pill',    // 能力药丸(10抖)推力343
  'PJ0FFeaDzXUreuUBZH6Hs+b56Jh0tQjrq0bIrrlZmv13GSAL9Q1hf59fjGk=': 'donut',           // 甜甜圈(52抖) 推力808
  'IkkadLfz7O/a5UR45p/OOCCG6ewAWVbsuzR/Z+v1v76CBU+mTG/wPjqdpfg=': 'battery',         // 能量电池(99抖)推力1415
  'gx7pmjQfhBaDOG2XkWI2peZ66YFWkCWRjZXpTqb23O/epru+sxWyTV/3Ufs=': 'love_blast',      // 爱的爆炸(199抖)推力2679
  'pGLo7HKNk1i4djkicmJXf6iWEyd+pfPBjbsHmd3WcX0Ierm2UdnRR7UINvI=': 'mystery_drop',    // 神秘空投(520抖)推力6988
};
```

**新游戏获取 sec_gift_id（完整流程）**:

**前置条件**: 已在抖音开放平台创建专属礼物 + 服务端已部署能接收推送

1. 在 `_mapDouyinGift()` 中保留 `console.warn` 日志（默认已有）
2. 确保服务器正在运行: `pm2 status`
3. 开播 → 打开第二个终端监听日志:
   ```bash
   ssh root@212.64.26.65 "pm2 logs dm-kpbl-server | grep 'Unknown sec_gift_id'"
   ```
4. 依次赠送每种专属礼物各**一个**
5. 日志中会出现: `Unknown sec_gift_id: abc123def456...`
6. 将每个ID复制到 GIFT_ID_MAP，对应正确的礼物名
7. 重新部署并重启

**关于 sec_gift_id**:
- 每个游戏(appId)的 sec_gift_id 不同，是加密后的ID
- 同一种礼物对于不同appId，sec_gift_id 不同
- 原生礼物（非专属）也有 sec_gift_id，但通常不需要专门处理
- 不要用 gift_value 判断礼物种类（抖音会折叠同类礼物导致值变化）

### 5.3 点赞 (live_like)

**payload item 字段**：
```json
{
  "msg_id": "消息ID",
  "sec_openid": "用户唯一标识",
  "nickname": "用户昵称",
  "avatar_url": "头像URL",
  "like_num": 5,
  "timestamp": 1675000000
}
```

> **like_num 是上游 2 秒聚合后的数据**，不是单次点赞

> **[项目示范]** 点赞推力值(每赞2推力,持续3秒)是本项目的设定。新游戏可自定义点赞效果。

**我们的处理逻辑**（`Room.handleDouyinLike`）：
1. 游戏不在 running 状态 → 忽略
2. 玩家未加入阵营 → 忽略（点赞不触发自动加入）
3. 推力 = `like_num × 2`（每赞2推力），**临时推力，持续3秒后自动衰减**
4. **不广播 gift_received**，只更新推力（由 GameEngine tick 广播）

### 5.4 粉丝团 (live_fansclub)

> 通常不需要处理。如有需要可参考抖音文档中的payload格式。

---

## 六、服务器架构

### 6.1 模块关系

```
index.js（入口）
├── express HTTP 服务器
│   ├── POST /api/douyin/push      → DouyinAPI.handlePushData()
│   ├── POST /api/douyin/task/*    → DouyinAPI.startTasks/stopTasks()
│   ├── GET  /health, /stats, /gifts
│   └── GET  /room/:id, /room/:id/rankings
├── WebSocket 服务器
│   └── ws.on('message') → RoomManager.routeMessage()
├── DouyinAPI 实例（签名验证 + Token管理 + 数据解析）
│   ├── onComment → roomManager.routeDouyinComment()
│   ├── onGift    → roomManager.routeDouyinGift()
│   └── onLike    → roomManager.routeDouyinLike()
└── RoomManager 实例（房间生命周期管理）
    └── Room 实例（每个直播间一个）
        ├── GameEngine（状态机 + 推力 + 橘子位置）
        ├── PlayerManager（玩家数据 + 排行榜 + 持久化）
        └── BarrageSimulator（开发测试用模拟器）
```

### 6.2 房间生命周期

```
客户端 join_room → getOrCreateRoom() → Room.addClient()
                                           ↓
                                      status = 'active'
                                           ↓
客户端全部断开 → Room.removeClient() → _enterPaused()
                                           ↓
                                      status = 'paused'
                                      (GameEngine.pause() 停tick保状态)
                                           ↓
                                      30分钟无连接
                                           ↓
                                      Room.destroy()
                                      status = 'destroyed'
```

> 房间创建不仅来自 WebSocket 客户端，抖音推送数据也会自动创建房间
> `routeDouyinComment/Gift/Like` 内部都调用 `getOrCreateRoom(roomId)`

### 6.3 游戏状态机

> **[项目示范]** 以下状态机设计特定于"卡皮巴拉对决"的推橘子玩法。新游戏需设计自己的状态机，但idle→countdown→running→settlement→idle的基本模式可参考。

```
idle → countdown(3s) → running → settlement → idle
              ↑                       │
              └──── reset_game ───────┘
```

- **running 状态**: 每 200ms tick 一次
- **tick 逻辑**: 计算速度 → 平滑过渡 → 累加位移 → 检查到终点 → 广播
- **橘子运动**: 速度驱动 + tanh sigmoid 非线性映射（详见 MEMORY.md）

### 6.4 弹幕游戏通用组件架构

> **[通用]** 以下是弹幕互动游戏的通用组件模式，不依赖具体游戏逻辑。换个游戏仍然需要这些层。

```
数据推送接收层 (DouyinAPI)
    ↓ 消息解析 + 签名验证 + 去重
数据路由层 (RoomManager)
    ↓ 按直播间ID路由到对应房间
业务处理层 (Room)
    ↓ 玩家管理 + 游戏逻辑 + 状态机
数据持久化层 (PlayerManager)
    ↓ 按房间隔离的玩家数据 + 排行榜
实时同步层 (WebSocket)
    ↓ 广播到所有连接的客户端
客户端渲染层 (Unity)
```

**通用设计要点**:

1. **多房间隔离**: 每个主播直播间 = 独立Room实例，数据完全隔离，互不影响
2. **数据持久化**:
   - 当前使用JSON文件（原子写入 + .bak备份），适合小规模
   - 规模大时可引入SQLite（单机）或MongoDB（分布式）
   - 持久化时机: 每局结算时写入，不要每次操作都写（性能问题）
3. **自动测试**: BarrageSimulator模式可模拟弹幕/礼物数据，不需要真实直播环境
4. **服务器权威模式(Server Authority)**: 客户端不做本地预测，只接收服务器状态（踩过预测过冲的坑）
5. **房间生命周期**: active → paused(无客户端) → destroyed(超时)，暂停期间保留游戏状态但停止tick
6. **客户端协议**: 定义好消息类型和数据结构，服务端和客户端各自解析同一份协议

### 6.5 游戏业务逻辑注意事项

> **[通用]** 以下注意事项适用于所有弹幕互动游戏。

1. **所有互动必须在游戏运行状态才处理**: `state === 'running'` 检查放在每个handler最前面
2. **未加入阵营的处理策略**:
   - 评论: 按关键词加入阵营
   - 礼物: 随机分配阵营（**不拒绝付费用户**，这是底线）
   - 点赞: 忽略（低价值操作不触发自动加入）
3. **异步数据的幂等性**: msg_id去重防止重复处理（抖音可能重试推送）
4. **推力/分数计算应在服务端**: 防作弊，客户端只做展示
5. **数据链路完整性**: 新增字段时必须检查全链路（推送→存储→广播→客户端协议→UI），漏任何一环数据都会丢失
6. **回调必须快速响应**: 先返回HTTP 200再异步处理数据，确保不触发平台熔断

---

## 七、WebSocket 消息协议

### 7.1 客户端 → 服务器

| 消息类型 | 用途 | data 字段 |
|---------|------|----------|
| `join_room` | 加入房间 | `{ roomId }` |
| `heartbeat` | 心跳 | 无 |
| `start_game` | 开始游戏 | 无 |
| `reset_game` | 重置游戏 | 无 |
| `toggle_sim` | 模拟器开关 | `{ enabled, showcase? }` |
| `test_barrage` | 测试弹幕 | `{ playerId, playerName, content }` |
| `test_gift` | 测试礼物 | `{ playerId, playerName, camp, giftId, count }` |
| `ranking_query` | 查询历史排行 | `{ period: "weekly"|"monthly"|... }` |

### 7.2 服务器 → 客户端（广播）

所有消息都带 `roomId` 和 `timestamp` 字段。

| 消息类型 | 触发时机 | data 字段 |
|---------|---------|----------|
| `game_state` | 状态变更/客户端连接 | `{ state, leftForce, rightForce, orangePos, remainingTime, leftCount, rightCount }` |
| `force_update` | 每 200ms tick / 每次收到礼物 | `{ leftForce, rightForce, orangePos, remainingTime }` |
| `countdown` | 开局3秒 / 最后30秒 | `{ remainingTime }` |
| `player_joined` | 评论加入 / 送礼自动加入 | `{ playerId, playerName, avatarUrl, camp, totalLeft, totalRight }` |
| `gift_received` | 收到礼物 | `{ playerId, playerName, camp, giftId, giftName, forceValue, isSummon, unitId, giftCount, tier }` |
| `ranking_update` | 排行变化 | `{ left: [...], right: [...] }` |
| `game_ended` | 游戏结束 | 完整结算数据（winner, reason, MVP, 排行等） |
| `room_destroyed` | 房间销毁 | `{ roomId, reason }` |

---

## 八、配置文件

**文件**: `Server/config/default.json`

> **[项目示范]** 以下配置值（阵营关键词、游戏时长等）特定于"卡皮巴拉对决"。新游戏需根据自身玩法调整。

```json
{
  "server": {
    "httpPort": 8080,
    "wsPort": 8081,
    "heartbeatInterval": 30000
  },
  "game": {
    "matchDuration": 600,        // 每局10分钟
    "countdownWarning": 30,
    "maxCapybaraCount": 100
  },
  "room": {
    "pauseTimeout": 1800000,     // 30分钟无连接后销毁
    "cleanupInterval": 300000,   // 5分钟清理一次
    "maxRooms": 200
  },
  "camps": {
    "left": {
      "name": "香橙温泉",
      "color": "#FF8C00",
      "keywords": ["1", "111", "左", "香橙"]
    },
    "right": {
      "name": "柚子温泉",
      "color": "#ADFF2F",
      "keywords": ["2", "222", "右", "柚子"]
    }
  }
}
```

---

## 九、部署 & 运维

### 9.0 远程服务器连接

**SSH连接**:
```bash
ssh root@212.64.26.65
```

**从本地Claude Code操作服务器**:
```bash
# 查看日志（最常用）
ssh root@212.64.26.65 "pm2 logs dm-kpbl-server --lines 100"

# 查看进程状态
ssh root@212.64.26.65 "pm2 status"

# 重启服务
ssh root@212.64.26.65 "pm2 restart dm-kpbl-server"

# 上传代码（Windows本地 → 远程Linux）
scp -r D:\claude\DM_kpbl\Server\src\* root@212.64.26.65:/opt/dm_kpbl/src/

# 上传后必须重启
ssh root@212.64.26.65 "pm2 restart dm-kpbl-server"
```

> **注意**: Windows的scp路径使用反斜杠，但远程路径使用正斜杠。
> **绝不操作** `/opt/dm_kpbl/data/` 目录！里面是玩家花真金白银的数据。

### 9.1 远程服务器信息

| 项目 | 值 |
|------|-----|
| IP | 212.64.26.65 |
| 代码路径 | `/opt/dm_kpbl/src/` |
| 配置路径 | `/opt/dm_kpbl/config/default.json` |
| 数据路径 | `/opt/dm_kpbl/data/rooms/{roomId}/` |
| PM2 进程 | `dm-kpbl-server` |
| 端口 | 8081 (HTTP + WebSocket 共用) |

### 9.2 部署命令

```bash
# 上传代码
scp -r Server/src/* root@212.64.26.65:/opt/dm_kpbl/src/

# 重启服务
ssh root@212.64.26.65 "pm2 restart dm-kpbl-server"

# ⚠️ 绝不动 /opt/dm_kpbl/data/ 目录！里面是玩家花真金白银的数据
```

### 9.3 健康检查 & 监控

```bash
# 健康检查
curl http://212.64.26.65:8081/health

# 全局统计（含抖音推送计数）
curl http://212.64.26.65:8081/stats

# 指定房间状态
curl http://212.64.26.65:8081/room/{roomId}

# 指定房间排行榜
curl http://212.64.26.65:8081/room/{roomId}/rankings

# PM2 日志
ssh root@212.64.26.65 "pm2 logs dm-kpbl-server --lines 100"
```

---

## 十、上架前 Checklist

### 必须完成

- [x] 在抖音开放平台创建小玩法应用，获取 appId / appSecret
- [x] 开通能力1（评论弹幕）和能力2（礼物+点赞）
- [x] 配置推送回调地址为 `https://kpbl.jiuqian2025.com/api/douyin/push`（**必须HTTPS**）
- [x] 配置推送密钥（PUSH_SECRET）
- [x] 在服务器上配置 `.env`（DOUYIN_APP_ID / DOUYIN_APP_SECRET / DOUYIN_PUSH_SECRET）
- [x] 部署HTTPS + WSS（Nginx + Let's Encrypt）
- [x] 重启 PM2 进程 `pm2 restart dm-kpbl-server`
- [x] 验证 Token 获取: `curl https://kpbl.jiuqian2025.com/stats` 检查 `douyin.tokenValid`
- [x] 端到端测试: 评论"1"→加入阵营 ✅ 礼物→推力 ✅ 点赞→临时推力 ✅ 666→推力升级 ✅
- [x] 签名验证测试: 检查日志中无 `signature verification failed` ✅

### 可选优化

- [x] HTTPS 反向代理（Nginx + Let's Encrypt）— 已完成
- [ ] 防火墙只开放必要端口
- [ ] 日志告警（错误率 > 5% 时通知）
- [ ] 抖音推送 IP 白名单（如有提供）
- [ ] 清理调试日志减少生产日志量

---

## 十一、踩坑记录 & 关键注意事项

### 已踩过的坑

### 🔴 致命级（导致数据完全不通）

| 坑 | 正确做法 |
|----|---------|
| **roomId大整数精度丢失** — JSON.parse截断 | 从原始JSON用正则提取: `rawResponse.match(/"room_id"\s*:\s*(\d+)/)` |
| **推送数据格式与文档不符** — body没有msg_type | msg_type/roomid在HTTP Header中，做header-based routing fallback |
| **礼物用gift_value判断种类** — 折叠导致值变化 | 用 **sec_gift_id** 精确匹配，不用 gift_value |
| **task/start不检查返回值** — 错误被吞掉 | 检查 `err_no===0`，失败任务后台重试 |
| **.env缺少PUSH_SECRET** — 签名验证失败 | 配置 DOUYIN_PUSH_SECRET（优先于APP_SECRET） |

### 🟡 中等级（影响功能但不致命）

| 坑 | 正确做法 |
|----|---------|
| body 格式以为是 `{ event, data }` | 实际是 `{ msg_type, payload }`，payload 是 JSON 字符串 |
| 签名算法以为是 SHA256 → hex | 实际是 **MD5 → Base64** |
| Token 接口用 open.douyin.com | 小玩法用 **minigame.zijieapi.com** |
| Token 参数用 client_key/client_secret | 小玩法用 **appid/secret** |
| 签名验证用 JSON.stringify(body) | 必须用 express verify 捕获的**原始 rawBody 字符串** |
| express.json不设limit | 设置 `limit: '1mb'`，防大批量推送被截断 |

### 🟢 常识级（容易搞混）

| 坑 | 正确做法 |
|----|---------|
| gift_value 以为是抖币 | 实际单位是**分**（1分 = 0.01元 = 0.1抖币） |
| like_num 以为是单次点赞 | 是上游 **2秒聚合** 的累计数 |
| test 礼物当正式处理 | `item.test === true` 是抖音自查工具数据，需**过滤** |
| 回调返回错误码 | 必须**始终返回 `{ err_no: 0 }`**，否则触发熔断 |

### 需要注意的设计决策

1. **未加入阵营的用户送礼** → 随机分配阵营（不会拒绝）
2. **未加入阵营的用户点赞** → 忽略（不产生推力）
3. **game_state !== 'running'** → 所有弹幕/礼物/点赞都忽略
4. **签名验证**: pushSecret 优先 → appSecret 降级 → 无密钥则跳过（开发环境方便但生产需配置）
5. **camps.keywords 硬编码**: PlayerManager 中关键词是硬编码的，修改 config 不会生效，需改代码

---

## 十二、文件索引

| 文件路径 | 用途 |
|---------|------|
| `Server/src/index.js` | 服务器入口，HTTP路由 + WebSocket + 抖音回调 |
| `Server/src/DouyinAPI.js` | 抖音API封装（Token + 任务 + 签名 + 解析） |
| `Server/src/Room.js` | 独立房间（含抖音弹幕/礼物/点赞处理） |
| `Server/src/RoomManager.js` | 房间生命周期 + 抖音数据路由 |
| `Server/src/GameEngine.js` | 游戏状态机 + 速度驱动橘子运动 |
| `Server/src/PlayerManager.js` | 玩家管理 + 排行 + 数据持久化 |
| `Server/src/BarrageSimulator.js` | 开发测试用模拟器 |
| `Server/src/GiftConfig.js` | 6种礼物配置定义 |
| `Server/config/default.json` | 服务器配置 |
| `docs/gift_config.md` | 礼物详细说明文档 |

---

## 十三、Nginx反向代理配置（HTTPS+WSS）

抖音推送回调**必须HTTPS**，WebSocket也需要WSS。以下是生产环境的Nginx配置要点：

```nginx
# /etc/nginx/sites-available/kpbl.jiuqian2025.com
server {
    listen 80;
    server_name kpbl.jiuqian2025.com;
    return 301 https://$host$request_uri;  # 强制HTTPS
}

server {
    listen 443 ssl;
    server_name kpbl.jiuqian2025.com;

    ssl_certificate /etc/letsencrypt/live/kpbl.jiuqian2025.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/kpbl.jiuqian2025.com/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:8081;
        proxy_http_version 1.1;

        # ⚠️ 关键: WebSocket Upgrade headers
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";

        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # 超时设置（WebSocket长连接需要较长超时）
        proxy_read_timeout 86400s;
        proxy_send_timeout 86400s;
    }
}
```

**SSL证书安装**:
```bash
# 安装 certbot
apt install certbot python3-certbot-nginx

# 获取证书
certbot --nginx -d kpbl.jiuqian2025.com

# 自动续签（certbot安装时自动配置了timer）
systemctl status certbot-renew.timer
```

> **⚠️ 踩坑**: Nginx WebSocket proxy 必须同时设置 `Upgrade` 和 `Connection` headers，缺一不可。

---

## 十四、npm依赖与项目初始化

```json
{
  "dependencies": {
    "express": "^4.21.2",    // HTTP服务器
    "ws": "^8.18.0",         // WebSocket
    "dotenv": "^16.4.7"      // 环境变量
  },
  "devDependencies": {
    "nodemon": "^3.1.9",     // 热重载开发
    "jest": "^29.7.0"        // 单元测试
  },
  "engines": { "node": ">=18.0.0" }
}
```

**关键**: HTTP和WebSocket共用同一端口（8081），通过 `http.createServer(app)` + `new WebSocket.Server({ server })` 实现。不需要开两个端口。

**PM2首次启动**:
```bash
cd /opt/dm_kpbl
pm2 start src/index.js --name dm-kpbl-server
pm2 save  # 保存进程列表，开机自启
```

---

## 十五、排查自检清单（Debug Checklist）

> 🔧 当数据不通时，按此顺序逐项排查。每一项都是实际踩过的坑。

### 阶段1: 服务器基础
- [ ] 服务器能正常访问: `curl https://域名/health` 返回 `{status: "ok"}`
- [ ] `.env` 已配置 DOUYIN_APP_ID、DOUYIN_APP_SECRET、DOUYIN_PUSH_SECRET
- [ ] PM2 进程正常运行: `pm2 status` 状态为 online
- [ ] `/stats` 接口 `douyin.tokenValid=true`

### 阶段2: 抖音配置
- [ ] 抖音开放平台小玩法应用已上线/审核通过
- [ ] 能力1（评论弹幕）和能力2（礼物+点赞）已开通
- [ ] 推送回调地址已配置为 `https://域名/api/douyin/push`（**必须HTTPS**）
- [ ] 推送密钥（PUSH_SECRET）已配置

### 阶段3: 开播与init
- [ ] 主播已开播且直播间可见小摇杆（小玩法已挂载）
- [ ] 直播伴侣正确传递了token给exe客户端
- [ ] `POST /api/douyin/init` 返回 `success=true`
- [ ] roomId 不是 `...000` 结尾（**排除JS大整数精度丢失**）

### 阶段4: task/start 推送任务
- [ ] 3种task/start(comment/gift/like)全部返回 `err_no=0`
- [ ] 如果 `err_no=5003019`: 检查直播间在播 + 小玩法已挂载 + 能力已开通 + roomId正确
- [ ] `GET /api/douyin/tasks` 可看到活跃任务列表

### 阶段5: 数据推送接收
- [ ] 发评论"1"，PM2日志出现 `Push received`
- [ ] 日志不含 `Invalid push data`（如有→检查header-based routing）
- [ ] 签名验证通过（日志不含 `signature verification failed`）

### 阶段6: 游戏内生效
- [ ] Unity客户端已通过WebSocket连接到对应roomId
- [ ] 游戏状态为 `running`（非running时所有数据被忽略！）
- [ ] 评论"1"→加入左营、"2"→加入右营
- [ ] 点赞→临时推力（3秒）
- [ ] "666"/"6"→推力升级（5秒）
- [ ] 送礼物→sec_gift_id正确映射→永久推力

---

## 十六、新游戏接入快速指南（一遍成功版）

> 🚀 基于卡皮巴拉对决踩坑经验总结，后续新游戏照此清单执行。

1. **【开放平台配置】** 创建小玩法应用 → 获取appId/appSecret → 开通能力1(评论)+能力2(礼物点赞) → 配置推送回调URL(HTTPS) → 获取PUSH_SECRET
2. **【服务器部署】** 配置.env(APP_ID/SECRET/PUSH_SECRET) → 确保HTTPS+WSS(Nginx+certbot) → 验证/health可访问 → 验证access_token能获取
3. **【代码关键检查点】**
   - ①roomId用正则从原始JSON提取（防精度丢失）
   - ②push端点做header-based routing兼容
   - ③礼物用sec_gift_id映射（不是gift_value）
   - ④签名用MD5→Base64 + rawBody + pushSecret优先
   - ⑤回调始终返回 `{err_no:0}`
   - ⑥express.json设置 `limit:'1mb'` + verify回调保存rawBody
   - ⑦task/start检查err_no===0，失败自动重试
4. **【开播前测试】** 开播 → 确认小摇杆出现 → 直播伴侣启动exe → 检查init返回success → 3个task/start全部err_no=0 → roomId不以000结尾
5. **【端到端验证】** 评论"1"→加入 | 礼物→推力 | 点赞→临时推力 | "666"→推力升级
6. **【获取sec_gift_id】** 保留Unknown日志 → 开播送每种礼物各一次 → 从日志提取ID → 填入GIFT_ID_MAP → 重新部署

---

## 十七、Unity客户端接入要点

> 客户端在抖音接入中承担3个关键职责：解析Token、调用init、WebSocket连接。以下是必须处理的地方。

### 17.1 命令行Token解析（NetworkManager.cs）

直播伴侣启动exe时传入token，Unity需要从命令行参数获取：

```csharp
private void ParseCommandLineToken()
{
    string[] args = Environment.GetCommandLineArgs();
    for (int i = 0; i < args.Length; i++)
    {
        string arg = args[i];
        // 支持两种格式: -token=xxx 或 -token xxx
        if (arg.StartsWith("-token=", StringComparison.OrdinalIgnoreCase))
        {
            douyinToken = arg.Substring(7);
        }
        else if (arg.Equals("-token", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            douyinToken = args[i + 1];
        }
    }
}
```

> **⚠️ 关键**: 在 `Awake()` 中调用，不能延迟。直播伴侣传token的时机是启动exe的瞬间。

### 17.2 调用服务器init接口

拿到token后，先调HTTP接口获取roomId，再连WebSocket：

```csharp
private async Task<bool> DouyinInitAsync(string token)
{
    string url = $"{httpUrl}/api/douyin/init";
    string jsonBody = $"{{\"token\":\"{token}\"}}";
    // UnityWebRequest POST → 返回 {success, roomId, anchorName, startedTypes}
    // 成功后 CurrentRoomId = response.roomId
}
```

**流程顺序（严格）**:
1. `ParseCommandLineToken()` → 获取token
2. `DouyinInitAsync(token)` → HTTP POST获取roomId
3. `ConnectAsync(wsUrl + "?roomId=" + roomId)` → WebSocket连接
4. 发送 `join_room` 消息

> **无token降级**: 如果没有token（本地开发），直接连WebSocket加入 `default` 房间。

### 17.3 WebSocket消息协议（客户端必须处理的消息类型）

| 消息类型 | 触发时机 | 客户端处理 |
|---------|---------|-----------|
| `game_state` | 连接时/状态变更 | 同步完整游戏状态（state, forces, orangePos, counts） |
| `force_update` | 每200ms | 更新推力、橘子位置、倒计时（**最频繁的消息**） |
| `player_joined` | 评论加入/送礼加入 | 生成基础单位、更新人数 |
| `gift_received` | 收到礼物 | 播放礼物动画、生成召唤单位（如isSummon=true） |
| `force_boost` | 发"666"/"6" | 显示推力升级特效 |
| `countdown` | 开局3秒/最后30秒 | 显示倒计时UI |
| `ranking_update` | 排行变化 | 更新排行榜 |
| `game_ended` | 游戏结束 | 显示结算画面（winner, MVP, 排行等） |

### 17.4 客户端关键设计模式

```
服务器权威（Server Authority）:
- 客户端纯被动接收，不做任何本地模拟
- 橘子位置由 force_update.orangePos 驱动
- 客户端只做跟随（serverFollowSpeed=4）
- 不要做客户端预测（踩过坑，预测会过冲）

线程安全:
- WebSocket接收在子线程，Unity API只能在主线程调用
- 用 ConcurrentQueue 将消息从接收线程传递到主线程Update()处理

自动重连:
- 最多3次重试，每次间隔3秒
- 退出时发送 leave_room

心跳:
- 每30秒发送 heartbeat 消息保持连接
```

### 17.5 关键文件清单

| 文件 | 职责 |
|------|------|
| `Scripts/Core/NetworkManager.cs` | WebSocket连接、Token解析、init调用、消息收发 |
| `Scripts/Core/MessageProtocol.cs` | 所有消息类型的数据结构定义 |
| `Scripts/Core/GameManager.cs` | 消息分发处理、游戏状态机 |
| `Scripts/Systems/GiftHandler.cs` | 礼物处理、召唤单位生成 |
| `Scripts/Systems/OrangeController.cs` | 橘子位置跟随、边界检测 |
| `Scripts/Systems/ForceSystem.cs` | 推力数据管理 |
| `Scripts/Config/GameConfig.cs` | 服务器URL配置（ScriptableObject） |

### 17.6 新游戏客户端接入Checklist

- [ ] 实现命令行 `-token=xxx` 解析（Awake中）
- [ ] 实现 `POST /api/douyin/init` HTTP调用
- [ ] 实现 WebSocket 连接 + `join_room` 发送（带roomId）
- [ ] 处理 `game_state` / `force_update` / `player_joined` / `gift_received` 消息
- [ ] 实现心跳（30秒间隔）
- [ ] 实现自动重连（3次/3秒间隔）
- [ ] 主线程安全（ConcurrentQueue跨线程传递消息）
- [ ] 无token时降级到default房间（开发测试用）
- [ ] JSON解析不依赖JsonUtility（灵活性不够），自定义解析或用Newtonsoft.Json

---

## 十八、里程碑记录 🎉

### 2026-02-13: 首次端到端验证通过 🎊

历史性时刻！卡皮巴拉对决成为我们第一个成功接入抖音直播数据的游戏。

**验证通过的完整链路**:
- ✅ 评论"1" → 加入左营(香橙温泉) → 永久推力+10
- ✅ 评论"2" → 加入右营(柚子温泉) → 永久推力+10
- ✅ 评论"666"/"6" → 推力升级 → 临时推力+3(5秒)
- ✅ 点赞 → 临时推力(每赞×2, 持续3秒)
- ✅ 仙女棒礼物 → sec_gift_id正确映射 → fairy_wand → 永久推力+10
- ✅ WebSocket实时同步 → Unity客户端即时渲染

**修复的3个关键问题**:
1. roomId大整数精度丢失 (JS Number.MAX_SAFE_INTEGER)
2. 推送数据格式文档与实际不符 (header-based routing)
3. 礼物映射从gift_value改为sec_gift_id

**技术栈**: Node.js + Express + WebSocket + PM2 | Unity C# | 抖音小玩法API
**开发工具**: Claude Code (AI辅助全栈开发)
**服务器**: 212.64.26.65 | 域名: kpbl.jiuqian2025.com (HTTPS+WSS)

---

## 十九、官方Unity SDK对比分析（2026-02-13）

> 📌 基于抖音官方文档 `developer.open-douyin.com/docs/resource/zh-CN/interaction/develop/unity-sdk/` 的完整分析。
> 目标: 后续新游戏在开发初期就能做出正确的架构选择。

### 19.1 官方SDK提供的三种接入架构

| 架构 | 说明 | 适用场景 |
|------|------|---------|
| **抖音云托管 + 长连接网关** | 服务器部署在抖音云，数据通过长连接网关传输 | 需要对指令数据做定制处理的玩法 |
| **抖音云托管 + 指令直推** | 抖音云有服务器，但消息直推到客户端SDK | 追求低延迟+有服务器结算 |
| **无服务器 + 指令直推** | 不需要服务器，数据直推到SDK | 简单玩法，追求极致低延迟 |

### 19.2 我们的架构 vs 官方SDK架构

**我们当前架构（"自建服务器+HTTP推送"）:**
```
观众操作 → 抖音服务器 → HTTP POST推送到我们服务器 → 服务器处理 → WebSocket推到Unity客户端
```

**我们没有使用官方Unity SDK**（LiveOpenSDK），而是自建了完整的服务器侧处理链路。

**差异对比:**

| 功能 | 官方SDK方式 | 我们的方式 | 差异影响 |
|------|-----------|----------|---------|
| 数据接收 | SDK客户端直接接收（直推） | 服务器HTTP回调→WebSocket中转 | 多一跳，延迟略高（但可控） |
| RoomInfo获取 | `Sdk.GetRoomInfoService().WaitForRoomInfoAsync` | 服务器 `POST GetRoomInfo` + 客户端解析roomId | 功能等价，我们手动实现 |
| 推送任务启停 | `Sdk.GetMessagePushService().StartPushTaskAsync` | 服务器 `POST task/start` | 功能等价 |
| 消息监听 | `Sdk.GetMessagePushService().OnMessage` | WebSocket `onmessage` 回调 | 功能等价 |
| **履约ACK上报** | `Sdk.GetMessageAckService().ReportAck` | ❌ **我们没有实现** | ⚠️ 平台后续可能强制要求 |
| **礼物进阶互动** | `Sdk.GetRoundApi().UpdateRoundStatusInfoAsync` | ❌ **我们没有实现** | 对局状态同步、阵营上报 |
| **用户战绩排行榜** | `Sdk.GetRoundApi().UpdateUserRoundListInfoAsync` | ❌ **我们没有实现** | 平台级排行（非必须） |
| **观众一键同玩** | SDK内置 | ❌ 我们用评论关键词 | 官方方案UX更好 |
| **用户快捷选队** | SDK内置 | ❌ 我们用评论关键词 | 官方方案UX更好 |
| SDK初始化 | `Sdk.Initialize(appId)` | 手动解析token+HTTP init | 功能等价 |
| 线程安全 | `Sdk.DefaultSynchronizationContext` | 自己用ConcurrentQueue | 功能等价 |
| 连接状态监听 | `GetMessagePushService().OnConnectionStateChanged` | WebSocket状态检查 | 功能等价 |
| MultiPushType | SinglePush/HTTPWithSDK/DyCloudWithSDK | HTTP推送(等价HTTPWithSDK的HTTP侧) | 我们只收HTTP侧 |

### 19.3 🔴 我们缺失的关键功能

#### 1. 履约ACK上报（强烈建议接入）

**官方文档原文要点**: 开发者务必接入，平台后续会**强制要求**不接入的玩法无法审核通过。

**作用**: 告知抖音平台"我已经收到并处理了这条互动消息"，平台用此统计消息处理是否正常、是否超时。

**当前状态**: 我们用自建服务器接收数据，没有通过SDK上报ACK。

**后续方案选择**:
- **方案A**: 引入官方 LiveOpenSDK，用 `MultiPushType.HTTPWithSDK`（HTTP+SDK双推模式），客户端SDK自动上报ACK
- **方案B**: 在服务器端实现ACK上报API（如果抖音提供服务端ACK接口）
- **方案C**: 暂不接入，等平台强制要求时再集成SDK

> **建议**: 新游戏从一开始就集成 LiveOpenSDK，用 HTTPWithSDK 双推模式。既有服务器做逻辑，又满足ACK上报要求。

#### 2. 礼物进阶互动（Round API）

| API | 功能 | 必要性 |
|-----|------|-------|
| `UpdateRoundStatusInfoAsync` | 同步对局状态（开始/结束） | 建议接入 |
| `UpdateUserGroupInfoAsync` | 上报用户阵营数据 | 建议接入 |
| `UpdateUserRoundListInfoAsync` | 上报用户对局数据 | 可选 |
| `UpdateRoundRankListInfoAsync` | 上报对局榜单 | 可选 |
| `UpdateRoundResultInfoAsync` | 完成对局上报 | 建议接入 |

**作用**: 让抖音平台知道对局状态，进而支持"礼物进阶互动效果"（礼物面板显示阵营等信息）。

#### 3. 观众一键同玩 & 用户快捷选队

官方SDK提供了原生的UI组件让观众快速加入阵营，比我们用评论关键词的方式UX更好。但这需要集成 LiveOpenSDK。

### 19.4 新游戏架构决策建议

```
🎯 推荐架构: 自建服务器 + LiveOpenSDK（HTTPWithSDK双推模式）

理由:
1. 自建服务器: 保持对游戏逻辑的完全控制（推力计算、排行、持久化）
2. LiveOpenSDK: 满足ACK履约上报（平台即将强制要求）
3. HTTPWithSDK双推: 服务器收HTTP做逻辑，客户端SDK收推送做ACK
4. 两条链路互备: HTTP推送到服务器做游戏逻辑，SDK侧仅负责ACK+RoomInfo

初始化顺序（新游戏）:
1. SDK初始化: Sdk.Initialize(appId)
2. 获取RoomInfo: Sdk.GetRoomInfoService().WaitForRoomInfoAsync
3. 启动推送: Sdk.GetMessagePushService().StartPushTaskAsync("live_comment", MultiPushType.HTTPWithSDK)
4. 注册消息监听: Sdk.GetMessagePushService().OnMessage += OnMessage（用于ACK上报）
5. 连接自己的服务器: WebSocket连接（游戏逻辑走这条路径）
6. 对局管理: Sdk.GetRoundApi() 上报对局状态
```

### 19.5 官方SDK关键类/接口速查

| 类/接口 | 用途 | 版本 |
|---------|------|------|
| `LiveOpenSdk.Instance` | SDK全局实例 | 2.0.0 |
| `Sdk.Initialize(appId)` | 初始化（必须首先调用） | 2.0.0 |
| `Sdk.Env` | 环境参数（初始化前设置） | 2.0.0 |
| `Sdk.DefaultSynchronizationContext` | 线程同步上下文 | 2.0.0 |
| `Sdk.GetRoomInfoService().WaitForRoomInfoAsync` | 获取直播间信息（有重试） | 2.0.0 |
| `Sdk.GetMessagePushService().StartPushTaskAsync(msgType, pushType)` | 启动推送任务 | 2.0.0 |
| `Sdk.GetMessagePushService().StopPushTaskAsync(msgType)` | 停止推送任务 | 2.0.0 |
| `Sdk.GetMessagePushService().OnMessage` | 消息接收回调 | 2.0.0 |
| `Sdk.GetMessagePushService().OnConnectionStateChanged` | 连接状态变化 | 2.0.0 |
| `Sdk.GetMessageAckService().ReportAck` | 履约ACK上报 | 2.0.0 |
| `Sdk.GetRoundApi().UpdateRoundStatusInfoAsync` | 同步对局状态 | 2.1.0 |
| `Sdk.GetRoundApi().UpdateUserGroupInfoAsync` | 上报阵营数据 | 2.1.0 |
| `Sdk.GetDyCloudApi().WebSocket` | 抖音云WebSocket | 2.0.0 |

**PushMessageTypes**: `live_gift`, `live_comment`, `live_like`, `live_fansclub`
**MultiPushType**: `SinglePush=1`(仅SDK), `HTTPWithSDK=2`(HTTP+SDK双推), `DyCloudWithSDK=3`(抖音云+SDK)

### 19.6 SDK接入环境要求

- Unity 2020+ LTS (2020.3.x, 2021.3.x, ...)
- 脚本后端: Mono 或 IL2CPP
- C# 版本: 8.0 以上（Unity 2020 默认 C# 8.0）
- 安装工具: **BGDT** (ByteGame Develop Tools) — 通过 `com.bytedance.bgdt-cp.unitypackage` 安装
- SDK包名: **LiveOpenSDK** (通过BGDT安装)

### 19.7 官方文档页面索引

| 页面 | URL路径 | 关键内容 |
|------|--------|---------|
| 概览 | `.../unity-sdk/overview` | 三种架构选择、功能列表 |
| API概览 | `.../live-unity-sdk-support/api-overview` | 全部API一览表 |
| SDK | `.../live-unity-sdk-support/sdk` | Initialize/Env/LogSource等基础API |
| 直播间数据 | `.../live-unity-sdk-support/live-room-data` | GetRoomInfo API |
| 指令直推能力 | `.../live-unity-sdk-support/direct-push-ability` | StartPushTask/OnMessage/ConnectionState |
| 互动数据履约上报 | `.../live-unity-sdk-support/ack-ability` | **ReportAck（即将强制）** |
| 礼物进阶互动效果 | `.../live-unity-sdk-support/gift-interaction-upgrade` | Round API（对局/阵营/榜单） |
| 用户战绩与排行榜 | `.../live-unity-sdk-support/user-ranking` | 平台级排行 |
| Unity SDK接入 | `.../unity-sdk/unity-sdk-access` | BGDT安装、环境要求 |
| 常见问题 | `.../unity-sdk/faq` | 常见坑和解答 |

> **文档基础URL**: `https://developer.open-douyin.com/docs/resource/zh-CN/interaction/develop/`

---

## 二十、ACK履约上报接入（2026-02-13）

### 20.1 ACK机制说明

抖音数据推送的ACK是**隐式**的 — 通过HTTP响应码确认：
- 推送回调返回 **HTTP 2XX** = 成功ACK
- 返回非2XX 或 响应超时 = 推送失败
- **连续10次失败** → 触发熔断，平台直接丢弃数据若干秒
- 超时阈值：评论/点赞 **2秒**，礼物 **3秒**

> **无需额外的ACK API调用**，只要确保push端点快速返回200即可。

### 20.2 我们的实现方式

**核心策略: 先返回200，再异步处理数据**

```
index.js push端点:
  1. 签名验证（失败也返回200，只记日志，避免误触熔断）
  2. 立即 res.json({ err_no: 0, err_msg: 'ok' })  ← ACK确认
  3. 异步处理推送数据（不阻塞HTTP响应）
```

### 20.3 msg_id去重

- DouyinAPI.js 维护 `_seenMsgIds` Map（msg_id → timestamp）
- `_processItem()` 处理前检查msg_id是否已处理过
- 每分钟清理5分钟前的过期msg_id
- 统计: `stats.duplicatesSkipped`, `stats.pushesReceived`

### 20.4 头像链路修复

抖音推送每条消息都包含 `avatar_url` 和 `nickname`，完整链路：
1. **DouyinAPI.js** → 提取 avatar_url ✅（已有）
2. **Room.js** → 传递给 PlayerManager.joinCamp ✅（已修复，增加avatarUrl参数）
3. **PlayerManager.js** → 存储到玩家数据 ✅（已修复，joinCamp增加avatarUrl）
4. **getRankings/buildCampRankings** → 返回值包含avatarUrl ✅（已修复）
5. **playerStats** → 持久化存储avatarUrl ✅（已修复）
6. **客户端 MessageProtocol.cs** → RankingEntry/PlayerJoinedData增加avatarUrl ✅
7. **AvatarLoader.cs** → URL下载+LRU缓存 ✅（新增）
8. **PlayerListUI.cs** → 当局Top3显示头像 ✅
9. **RankingPanelUI.cs** → 持久化排行榜加载真实头像 ✅

### 20.5 昵称清洗

抖音昵称可能包含特殊字符、emoji、控制字符等：
- `_sanitizeNickname()`: 去空格 → 过滤控制字符 → 限20字符
- 保留emoji（Unity TMP Chinese Font fallback支持emoji渲染）
- 空/无效昵称降级为"匿名用户"
