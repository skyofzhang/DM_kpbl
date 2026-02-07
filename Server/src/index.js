/**
 * 卡皮巴拉对决 - Node.js 服务端
 *
 * 职责:
 * 1. 接收抖音直播弹幕/礼物事件
 * 2. 解析并转换为游戏指令
 * 3. WebSocket同步到Unity客户端
 */

const WebSocket = require('ws');
const express = require('express');
require('dotenv').config();

const PORT = process.env.PORT || 8080;
const WS_PORT = process.env.WS_PORT || 8081;

// 游戏状态
const gameState = {
  state: 'idle', // idle, waiting, running, settlement, ended
  leftForce: 0,
  rightForce: 0,
  orangePosition: 0, // -10 ~ 10
  remainingTime: 1800, // 30分钟
  players: new Map(),
  rankings: []
};

// Express HTTP服务
const app = express();
app.use(express.json());

app.get('/health', (req, res) => {
  res.json({ status: 'ok', timestamp: Date.now() });
});

app.get('/state', (req, res) => {
  res.json({
    state: gameState.state,
    leftForce: gameState.leftForce,
    rightForce: gameState.rightForce,
    orangePosition: gameState.orangePosition,
    playerCount: gameState.players.size
  });
});

app.listen(PORT, () => {
  console.log(`[KPBL] HTTP server running on port ${PORT}`);
});

// WebSocket服务
const wss = new WebSocket.Server({ port: WS_PORT });

// 广播消息到所有客户端
function broadcast(message) {
  const data = JSON.stringify(message);
  wss.clients.forEach(client => {
    if (client.readyState === WebSocket.OPEN) {
      client.send(data);
    }
  });
}

// 发送游戏状态
function sendGameState(ws) {
  ws.send(JSON.stringify({
    type: 'game_state',
    timestamp: Date.now(),
    data: {
      state: gameState.state,
      leftForce: gameState.leftForce,
      rightForce: gameState.rightForce,
      orangePosition: gameState.orangePosition,
      remainingTime: gameState.remainingTime
    }
  }));
}

// 处理礼物事件
function processGift(playerId, playerName, camp, giftId, giftCount) {
  // TODO: 实现礼物处理逻辑
  const forceValue = giftCount * 10; // 临时计算

  if (camp === 'left') {
    gameState.leftForce += forceValue;
  } else {
    gameState.rightForce += forceValue;
  }

  // 计算橘子位置
  const forceDiff = gameState.leftForce - gameState.rightForce;
  gameState.orangePosition = Math.max(-10, Math.min(10, forceDiff * 0.01));

  broadcast({
    type: 'gift_received',
    timestamp: Date.now(),
    data: {
      playerId,
      playerName,
      camp,
      giftId,
      giftCount,
      forceValue,
      isSummon: false,
      summonCount: 0
    }
  });

  broadcast({
    type: 'force_update',
    timestamp: Date.now(),
    data: {
      leftForce: gameState.leftForce,
      rightForce: gameState.rightForce,
      forceDiff,
      orangePosition: gameState.orangePosition
    }
  });
}

wss.on('connection', (ws) => {
  console.log('[KPBL] Unity client connected');

  // 发送初始状态
  sendGameState(ws);

  ws.on('message', (message) => {
    try {
      const msg = JSON.parse(message.toString());
      console.log('[KPBL] Received:', msg.type);

      switch (msg.type) {
        case 'heartbeat':
          ws.send(JSON.stringify({ type: 'heartbeat', timestamp: Date.now() }));
          break;
        case 'start_game':
          gameState.state = 'running';
          broadcast({ type: 'game_state', data: { state: 'running' } });
          break;
        case 'test_gift':
          // 测试用
          processGift(msg.data.playerId, msg.data.playerName, msg.data.camp, msg.data.giftId, msg.data.count);
          break;
      }
    } catch (e) {
      console.error('[KPBL] Message parse error:', e);
    }
  });

  ws.on('close', () => {
    console.log('[KPBL] Unity client disconnected');
  });

  ws.on('error', (err) => {
    console.error('[KPBL] WebSocket error:', err);
  });
});

console.log(`[KPBL] WebSocket server running on port ${WS_PORT}`);
console.log('[KPBL] Server started successfully');
