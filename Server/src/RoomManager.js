/**
 * RoomManager - 房间生命周期管理
 *
 * 职责:
 * 1. 创建/查找/销毁房间
 * 2. 定期清理超时房间
 * 3. 提供全局房间统计
 *
 * 房间创建策略:
 * - WebSocket客户端连接时指定roomId → 自动创建或加入已有房间
 * - 抖音弹幕推送到达时 → 自动创建或查找房间
 * - 默认roomId = "default"（测试/开发用）
 */

const Room = require('./Room');

class RoomManager {
  /**
   * @param {object} gameConfig - 游戏配置（传给每个Room的GameEngine）
   */
  constructor(gameConfig) {
    this.gameConfig = gameConfig;

    // roomId → Room
    this.rooms = new Map();

    // 清理定时器（每5分钟检查超时房间）
    this.cleanupInterval = setInterval(() => {
      this._cleanup();
    }, 5 * 60 * 1000);

    // 默认暂停超时: 30分钟
    this.pauseTimeout = 30 * 60 * 1000;

    console.log('[RoomManager] Initialized');
  }

  // ==================== 房间操作 ====================

  /**
   * 获取或创建房间
   * @param {string} roomId
   * @returns {Room}
   */
  getOrCreateRoom(roomId) {
    if (!roomId) roomId = 'default';

    let room = this.rooms.get(roomId);

    if (room) {
      // 如果房间被标记为destroyed，删除并重建
      if (room.status === 'destroyed') {
        this.rooms.delete(roomId);
        room = null;
      }
    }

    if (!room) {
      room = new Room(roomId, this.gameConfig);
      room.pauseTimeout = this.pauseTimeout;
      this.rooms.set(roomId, room);
      console.log(`[RoomManager] Room created: ${roomId} (total: ${this.rooms.size})`);
    }

    return room;
  }

  /**
   * 获取房间（不自动创建）
   * @param {string} roomId
   * @returns {Room|null}
   */
  getRoom(roomId) {
    const room = this.rooms.get(roomId);
    if (room && room.status !== 'destroyed') return room;
    return null;
  }

  /**
   * 删除房间
   */
  destroyRoom(roomId) {
    const room = this.rooms.get(roomId);
    if (room) {
      room.destroy();
      this.rooms.delete(roomId);
      console.log(`[RoomManager] Room destroyed: ${roomId} (remaining: ${this.rooms.size})`);
    }
  }

  // ==================== WebSocket客户端路由 ====================

  /**
   * WebSocket客户端连接 → 加入房间
   * @param {WebSocket} ws - WebSocket连接
   * @param {string} roomId - 房间ID
   */
  handleClientConnect(ws, roomId) {
    const room = this.getOrCreateRoom(roomId);
    ws._roomId = roomId; // 在ws对象上标记roomId，方便后续查找
    room.addClient(ws);
    return room;
  }

  /**
   * WebSocket客户端断开 → 从房间移除
   * @param {WebSocket} ws
   */
  handleClientDisconnect(ws) {
    const roomId = ws._roomId;
    if (!roomId) return;

    const room = this.rooms.get(roomId);
    if (room) {
      room.removeClient(ws);
    }
  }

  /**
   * 路由客户端消息到对应房间
   * @param {WebSocket} ws
   * @param {object} msg - 解析后的消息对象
   */
  routeMessage(ws, msg) {
    const roomId = ws._roomId;
    if (!roomId) {
      console.warn('[RoomManager] Message from client without roomId');
      return;
    }

    const room = this.rooms.get(roomId);
    if (!room || room.status === 'destroyed') {
      console.warn(`[RoomManager] Message for non-existent room: ${roomId}`);
      return;
    }

    room.handleMessage(ws, msg);
  }

  // ==================== 抖音数据路由 ====================

  /**
   * 获取要转发的目标房间列表
   *
   * 策略（按优先级）：
   * 1. 精确匹配: 推送roomId对应的房间
   * 2. 默认房间: default房间有在线客户端时也转发
   * 3. 回退策略: 如果精确匹配的房间没有客户端，找当前有客户端的房间转发
   *    （场景：主播不关播重开，抖音分配新roomId，但客户端连着旧roomId）
   *
   * 同时刷新房间的暂停计时器（有推送说明直播还活着）
   */
  _getTargetRooms(roomId) {
    const rooms = [];

    // 1. 精确匹配的房间
    const exactRoom = this.rooms.get(roomId);
    if (exactRoom && exactRoom.status !== 'destroyed') {
      rooms.push(exactRoom);
      exactRoom.refreshPauseTimer();
    }

    // 2. default房间有在线客户端时也转发
    if (roomId !== 'default') {
      const defaultRoom = this.rooms.get('default');
      if (defaultRoom && defaultRoom.status !== 'destroyed' && defaultRoom.clients.size > 0) {
        rooms.push(defaultRoom);
      }
    }

    // 3. 回退策略：如果精确匹配的房间没有客户端连接（或房间不存在），
    //    找有在线客户端的房间转发推送数据
    //    场景：直播中途关播重开→新roomId，但客户端仍连着旧roomId的房间
    const hasClientRoom = rooms.some(r => r.clients.size > 0);
    if (!hasClientRoom) {
      for (const [rid, room] of this.rooms.entries()) {
        if (rid === roomId || rid === 'default') continue;
        if (room.status !== 'destroyed' && room.clients.size > 0) {
          rooms.push(room);
          room.refreshPauseTimer();
          console.log(`[RoomManager] ⚠️ Push for room ${roomId} routed to active room ${rid} (fallback)`);
          break; // 只转发到一个有客户端的房间
        }
      }
    }

    // 4. 如果完全没有匹配到任何房间，创建一个（保持原有行为兼容）
    if (rooms.length === 0) {
      const newRoom = this.getOrCreateRoom(roomId);
      rooms.push(newRoom);
    }

    return rooms;
  }

  /**
   * 抖音评论推送 → 路由到房间
   */
  routeDouyinComment(roomId, secOpenId, nickname, avatarUrl, content) {
    for (const room of this._getTargetRooms(roomId)) {
      room.handleDouyinComment(secOpenId, nickname, avatarUrl, content);
    }
  }

  /**
   * 抖音礼物推送 → 路由到房间
   */
  routeDouyinGift(roomId, secOpenId, nickname, avatarUrl, secGiftId, giftNum, giftValue) {
    for (const room of this._getTargetRooms(roomId)) {
      room.handleDouyinGift(secOpenId, nickname, avatarUrl, secGiftId, giftNum, giftValue);
    }
  }

  /**
   * 抖音点赞推送 → 路由到房间
   */
  routeDouyinLike(roomId, secOpenId, nickname, avatarUrl, likeNum) {
    for (const room of this._getTargetRooms(roomId)) {
      room.handleDouyinLike(secOpenId, nickname, avatarUrl, likeNum);
    }
  }

  // ==================== 清理 & 统计 ====================

  /**
   * 清理超时房间
   * 注意：即使房间处于paused状态，只要最近有推送数据到达（lastActiveAt刷新），
   * 就不应该销毁——因为直播还在继续，只是exe客户端临时断开了
   */
  _cleanup() {
    const now = Date.now();
    let cleaned = 0;

    for (const [roomId, room] of this.rooms.entries()) {
      if (room.status === 'destroyed') {
        this.rooms.delete(roomId);
        cleaned++;
        continue;
      }

      // paused房间：只有 lastActiveAt 超过 pauseTimeout 才销毁
      // 有推送数据到来会刷新 lastActiveAt，所以直播还在播的房间不会被清理
      if (room.status === 'paused') {
        const inactiveDuration = now - room.lastActiveAt;
        if (inactiveDuration > this.pauseTimeout) {
          room.destroy();
          this.rooms.delete(roomId);
          cleaned++;
        }
      }
    }

    if (cleaned > 0) {
      console.log(`[RoomManager] Cleanup: removed ${cleaned} rooms (remaining: ${this.rooms.size})`);
    }
  }

  /**
   * 获取全局统计
   */
  getStats() {
    const stats = {
      totalRooms: this.rooms.size,
      activeRooms: 0,
      pausedRooms: 0,
      totalClients: 0,
      totalPlayers: 0,
      rooms: []
    };

    for (const [roomId, room] of this.rooms.entries()) {
      if (room.status === 'destroyed') continue;

      const info = room.getInfo();
      stats.rooms.push(info);

      if (room.status === 'active') stats.activeRooms++;
      else if (room.status === 'paused') stats.pausedRooms++;

      stats.totalClients += info.clientCount;
      stats.totalPlayers += info.playerCount;
    }

    return stats;
  }

  /**
   * 关闭所有房间（服务器关闭时调用）
   */
  shutdown() {
    clearInterval(this.cleanupInterval);
    for (const [roomId, room] of this.rooms.entries()) {
      room.destroy();
    }
    this.rooms.clear();
    console.log('[RoomManager] Shutdown complete');
  }
}

module.exports = RoomManager;
