/**
 * Room - 独立游戏房间
 * 每个主播直播间对应一个Room，拥有独立的GameEngine + PlayerManager + BarrageSimulator
 *
 * 生命周期:
 *   create → active(有WS客户端) → paused(WS断开) → destroyed(超时清理)
 *
 * 数据隔离:
 *   每个Room的PlayerManager独立，数据持久化带roomId前缀
 */

const GameEngine = require('./GameEngine');
const PlayerManager = require('./PlayerManager');
const BarrageSimulator = require('./BarrageSimulator');
const { getGift } = require('./GiftConfig');

class Room {
  /**
   * @param {string} roomId - 房间ID（通常=抖音直播间ID）
   * @param {object} gameConfig - 游戏配置
   */
  constructor(roomId, gameConfig) {
    this.roomId = roomId;
    this.gameConfig = gameConfig;
    this.createdAt = Date.now();
    this.lastActiveAt = Date.now();

    // 状态: active | paused | destroyed
    this.status = 'active';

    // WebSocket客户端集合（一个房间可能有多个观察客户端）
    this.clients = new Set();

    // 核心模块（每个房间独立实例）
    this.playerManager = new PlayerManager(roomId);
    this.gameEngine = new GameEngine(
      gameConfig,
      (msg) => this.broadcast(msg),
      this.playerManager
    );
    this.simulator = new BarrageSimulator(
      this.gameEngine,
      this.playerManager,
      (playerId, playerName, camp, giftId, giftCount) => this.processGift(playerId, playerName, camp, giftId, giftCount),
      (msg) => this.broadcast(msg),
      {
        // Room级回调，审核演示模式需要模拟666/点赞
        handleComment: (id, name, avatar, content) => this.handleDouyinComment(id, name, avatar, content),
        handleLike: (id, name, avatar, likeNum) => this.handleDouyinLike(id, name, avatar, likeNum)
      }
    );

    // 暂停超时定时器
    this._pauseTimer = null;
    // 默认暂停后30分钟销毁
    this.pauseTimeout = 30 * 60 * 1000;

    console.log(`[Room:${roomId}] Created`);
  }

  // ==================== 客户端管理 ====================

  /**
   * 添加WebSocket客户端到房间
   */
  addClient(ws) {
    this.clients.add(ws);
    this.lastActiveAt = Date.now();

    // 如果是从暂停恢复，取消销毁定时器并恢复游戏引擎
    if (this.status === 'paused') {
      this._cancelPauseTimer();
      this.status = 'active';
      console.log(`[Room:${this.roomId}] Resumed from pause (${this.clients.size} clients)`);

      // 恢复游戏引擎tick循环（如果游戏在暂停前正在running）
      if (this.gameEngine.state === 'running') {
        this.gameEngine.resume();
        console.log(`[Room:${this.roomId}] Game engine resumed`);
      }
    }

    // 发送当前游戏状态给新客户端
    this._sendState(ws);

    console.log(`[Room:${this.roomId}] Client added (total: ${this.clients.size})`);
  }

  /**
   * 移除WebSocket客户端
   */
  removeClient(ws) {
    this.clients.delete(ws);
    console.log(`[Room:${this.roomId}] Client removed (remaining: ${this.clients.size})`);

    // 所有客户端断开 → 进入暂停状态
    if (this.clients.size === 0) {
      this._enterPaused();
    }
  }

  /**
   * 进入暂停状态 - 停止模拟器，保留游戏状态
   * 注意：暂停状态仍然可以接收抖音推送数据（评论/礼物/点赞），
   * 因为主播可能只是游戏exe重启但直播还在继续
   */
  _enterPaused() {
    this.status = 'paused';
    this.simulator.stop();

    // 如果游戏正在运行，暂停游戏引擎（不重置）
    if (this.gameEngine.state === 'running') {
      console.log(`[Room:${this.roomId}] Game paused (was running)`);
      // 停止tick定时器但保留状态
      this.gameEngine.pause();
    }

    console.log(`[Room:${this.roomId}] Paused - will destroy in ${this.pauseTimeout / 60000} minutes`);

    // 启动销毁定时器
    this._startPauseTimer();
  }

  /**
   * 启动暂停销毁定时器
   */
  _startPauseTimer() {
    this._cancelPauseTimer();
    this._pauseTimer = setTimeout(() => {
      this.destroy();
    }, this.pauseTimeout);
  }

  /**
   * 当收到抖音推送数据时刷新暂停计时器
   * 有推送说明直播还在继续，不应该销毁房间
   */
  refreshPauseTimer() {
    this.lastActiveAt = Date.now();
    if (this.status === 'paused' && this._pauseTimer) {
      this._startPauseTimer();
    }
  }

  /**
   * 取消暂停销毁定时器
   */
  _cancelPauseTimer() {
    if (this._pauseTimer) {
      clearTimeout(this._pauseTimer);
      this._pauseTimer = null;
    }
  }

  /**
   * 销毁房间 - 清理所有资源
   */
  destroy() {
    this._cancelPauseTimer();
    this.simulator.stop();
    this.gameEngine.reset();
    this.status = 'destroyed';

    // 关闭所有残留客户端
    for (const ws of this.clients) {
      try {
        ws.send(JSON.stringify({
          type: 'room_destroyed',
          timestamp: Date.now(),
          data: { roomId: this.roomId, reason: 'timeout' }
        }));
        ws.close(1000, 'room_destroyed');
      } catch (e) { }
    }
    this.clients.clear();

    console.log(`[Room:${this.roomId}] Destroyed`);
  }

  // ==================== 消息广播 ====================

  /**
   * 向房间内所有客户端广播消息
   */
  broadcast(message) {
    if (this.clients.size === 0) return;

    // 注入roomId（让客户端知道消息来自哪个房间）
    message.roomId = this.roomId;
    const data = JSON.stringify(message);

    for (const client of this.clients) {
      try {
        if (client.readyState === 1) { // WebSocket.OPEN
          client.send(data);
        }
      } catch (e) {
        console.warn(`[Room:${this.roomId}] Broadcast error: ${e.message}`);
      }
    }
  }

  /**
   * 发送当前游戏状态给指定客户端
   */
  _sendState(ws) {
    try {
      ws.send(JSON.stringify({
        type: 'game_state',
        roomId: this.roomId,
        timestamp: Date.now(),
        data: {
          ...this.gameEngine.getState(),
          leftCount: this.playerManager.getCampCount('left'),
          rightCount: this.playerManager.getCampCount('right')
        }
      }));

      // 如果有正在进行的游戏，也发送当前排行榜
      if (this.gameEngine.state === 'running') {
        ws.send(JSON.stringify({
          type: 'ranking_update',
          roomId: this.roomId,
          timestamp: Date.now(),
          data: this.playerManager.getAllRankings()
        }));
      }
    } catch (e) {
      console.warn(`[Room:${this.roomId}] Send state error: ${e.message}`);
    }
  }

  // ==================== 礼物处理 ====================

  /**
   * 处理礼物事件（来自抖音推送或模拟器）
   */
  processGift(playerId, playerName, camp, giftId, giftCount) {
    const gift = getGift(giftId);
    if (!gift) return;

    // 获取玩家头像URL
    const playerInfo = this.playerManager.getPlayer(playerId);
    const avatarUrl = playerInfo ? (playerInfo.avatarUrl || '') : '';

    if (gift.type === 'upgrade') {
      // ===== 仙女棒(tier1): 累积升级 + 等级推力 =====
      const upgradeResult = this.playerManager.addFairyWand(playerId, giftCount);
      const levelValues = this.playerManager.getLevelForceValues(upgradeResult.newLevel);

      // 推力使用当前等级的基础推力加成值（非固定10）
      const totalForce = levelValues.baseForce * giftCount;
      this.gameEngine.addForce(camp, totalForce);

      // 累加贡献值（基于推力值，非金额，解决仙女棒价格0.1导致贡献过低的问题）
      this.playerManager.addContribution(playerId, totalForce, totalForce);

      // 广播礼物事件（保留原有字段兼容客户端）
      this.broadcast({
        type: 'gift_received',
        timestamp: Date.now(),
        data: {
          playerId,
          playerName,
          avatarUrl,
          camp,
          giftId: gift.id,
          giftName: gift.name,
          forceValue: totalForce,
          isSummon: gift.isSummon,
          unitId: gift.unitId,
          giftCount,
          tier: gift.tier || 'basic'
        }
      });

      // 如果升级了，广播升级事件
      if (upgradeResult.leveledUp) {
        this.broadcast({
          type: 'player_upgrade',
          timestamp: Date.now(),
          data: {
            playerId,
            playerName,
            avatarUrl,
            camp,
            oldLevel: upgradeResult.oldLevel,
            newLevel: upgradeResult.newLevel,
            fairyWandCount: upgradeResult.fairyWandCount
          }
        });
      }
    } else {
      // ===== 召唤型礼物(tier2~6): 限时推力（参考羊羊对决，推力有持续时间）=====
      const totalForce = gift.forceValue * giftCount;
      const giftDuration = gift.duration || 60; // 从GiftConfig读取持续时间（秒）
      this.gameEngine.addTempForce(camp, totalForce, giftDuration);
      // 贡献基于推力值（而非金额），推力越大贡献越高
      this.playerManager.addContribution(playerId, totalForce, totalForce);

      this.broadcast({
        type: 'gift_received',
        timestamp: Date.now(),
        data: {
          playerId,
          playerName,
          avatarUrl,
          camp,
          giftId: gift.id,
          giftName: gift.name,
          forceValue: totalForce,
          isSummon: gift.isSummon,
          unitId: gift.unitId,
          giftCount,
          tier: gift.tier || 'basic'
        }
      });
    }

    // 广播排行榜更新
    this.broadcast({
      type: 'ranking_update',
      timestamp: Date.now(),
      data: this.playerManager.getAllRankings()
    });

    this.lastActiveAt = Date.now();
  }

  // ==================== 抖音弹幕/礼物/点赞处理 ====================

  /**
   * 处理抖音评论（加入阵营 + 推力升级）
   * 关键词分两类:
   *   - 阵营关键词(1/2/左/右等): 加入阵营，临时推力+10(30秒)
   *   - 推力升级关键词(666/6): 已加入玩家给己方阵营临时推力+3，持续5秒
   */
  handleDouyinComment(secOpenId, nickname, avatarUrl, content) {
    // 有推送到来就刷新活跃时间（即使游戏不在running，直播仍然活着）
    this.lastActiveAt = Date.now();
    if (this.gameEngine.state !== 'running') return;

    const trimmed = content.trim();

    // 先检查是否为推力升级关键词（666/6）
    if (trimmed === '666' || trimmed === '6') {
      const player = this.playerManager.getPlayer(secOpenId);
      if (!player) return; // 未加入阵营的忽略

      // 推力: 根据玩家升级等级获取666效果值（Lv1=20, Lv5=360, Lv10=2400）
      const level = this.playerManager.getPlayerLevel(secOpenId);
      const levelValues = this.playerManager.getLevelForceValues(level);
      const boostForce = levelValues.boostForce;
      const boostDuration = 5; // 秒
      this.gameEngine.addTempForce(player.camp, boostForce, boostDuration);
      this.playerManager.addContribution(secOpenId, boostForce, boostForce);

      // 广播推力升级事件（让客户端显示特效）
      this.broadcast({
        type: 'force_boost',
        timestamp: Date.now(),
        data: {
          playerId: secOpenId,
          playerName: nickname,
          camp: player.camp,
          forceValue: boostForce,
          duration: boostDuration,
          keyword: trimmed
        }
      });

      this.lastActiveAt = Date.now();
      return;
    }

    // 阵营关键词处理
    const camp = this.playerManager.parseCamp(content);
    if (!camp) return; // 不是加入阵营的关键词

    const result = this.playerManager.joinCamp(secOpenId, nickname, avatarUrl, camp);
    if (result.success) {
      this.gameEngine.addForce(camp, 10); // 加入基础推力（永久）
      this.playerManager.addContribution(secOpenId, 10, 10);

      const vipInfo = this.playerManager.getVipInfo(secOpenId);
      this.broadcast({
        type: 'player_joined',
        timestamp: Date.now(),
        data: {
          playerId: secOpenId,
          playerName: nickname,
          avatarUrl: avatarUrl || '',
          camp,
          totalLeft: result.totalLeft,
          totalRight: result.totalRight,
          isVip: vipInfo?.isVip || false,
          vipRank: vipInfo?.vipRank || 0,
          vipTitle: vipInfo?.vipTitle || '',
          vipType: vipInfo?.vipType || ''
        }
      });

      // 排行榜更新
      this.broadcast({
        type: 'ranking_update',
        timestamp: Date.now(),
        data: this.playerManager.getAllRankings()
      });
    }

    this.lastActiveAt = Date.now();
  }

  /**
   * 处理抖音礼物
   * @param {string} secOpenId - 用户openid
   * @param {string} nickname - 用户昵称
   * @param {string} secGiftId - 抖音礼物ID（需要映射到我们的giftId）
   * @param {number} giftNum - 礼物数量
   * @param {number} giftValue - 礼物价值（分）
   */
  handleDouyinGift(secOpenId, nickname, avatarUrl, secGiftId, giftNum, giftValue) {
    this.lastActiveAt = Date.now();
    if (this.gameEngine.state !== 'running') return;

    // 查找该玩家已加入的阵营
    const player = this.playerManager.getPlayer(secOpenId);
    if (!player) {
      // 玩家未加入阵营，随机分配一个
      const camp = Math.random() < 0.5 ? 'left' : 'right';
      this.playerManager.joinCamp(secOpenId, nickname, avatarUrl, camp);
      const vipInfo2 = this.playerManager.getVipInfo(secOpenId);
      this.broadcast({
        type: 'player_joined',
        timestamp: Date.now(),
        data: {
          playerId: secOpenId,
          playerName: nickname,
          avatarUrl: avatarUrl || '',
          camp,
          totalLeft: this.playerManager.getCampCount('left'),
          totalRight: this.playerManager.getCampCount('right'),
          isVip: vipInfo2?.isVip || false,
          vipRank: vipInfo2?.vipRank || 0,
          vipTitle: vipInfo2?.vipTitle || '',
          vipType: vipInfo2?.vipType || ''
        }
      });
    }

    const playerData = this.playerManager.getPlayer(secOpenId);
    if (!playerData) return;

    // 将抖音 sec_gift_id 映射到我们的游戏礼物ID
    const mappedGiftId = this._mapDouyinGift(secGiftId);
    this.processGift(secOpenId, nickname, playerData.camp, mappedGiftId, giftNum);
  }

  /**
   * 处理抖音点赞
   * @param {string} secOpenId - 用户openid
   * @param {string} nickname - 用户昵称
   * @param {number} likeNum - 2秒内聚合的点赞数
   *
   * 设计文档: 每赞基础推力2，持续3秒（临时推力）
   */
  handleDouyinLike(secOpenId, nickname, avatarUrl, likeNum) {
    this.lastActiveAt = Date.now();
    if (this.gameEngine.state !== 'running') return;

    const player = this.playerManager.getPlayer(secOpenId);
    if (!player) return; // 未加入阵营的点赞忽略

    // 点赞推力: 根据玩家升级等级获取点赞效果值（Lv1=5, Lv5=90, Lv10=600）
    const level = this.playerManager.getPlayerLevel(secOpenId);
    const levelValues = this.playerManager.getLevelForceValues(level);
    const forcePerLike = levelValues.likeForce;
    const likeDuration = 3; // 秒
    const totalForce = likeNum * forcePerLike;
    this.gameEngine.addTempForce(player.camp, totalForce, likeDuration);
    this.playerManager.addContribution(secOpenId, totalForce, totalForce);

    // 广播点赞推力事件（让客户端显示提示）
    this.broadcast({
      type: 'like_boost',
      timestamp: Date.now(),
      data: {
        playerId: secOpenId,
        playerName: nickname,
        camp: player.camp,
        likeNum: likeNum,
        forcePerLike: forcePerLike,
        forceValue: totalForce,
        duration: likeDuration
      }
    });
    this.lastActiveAt = Date.now();
  }

  /**
   * 将抖音 sec_gift_id 映射到我们的游戏礼物ID
   * 用 sec_gift_id 精确匹配（不用 gift_value，因为抖音会折叠2秒内多个礼物导致总价值变化）
   *
   * 抖音后台配置的6种专属礼物 sec_gift_id:
   *   仙女棒(1抖)   → fairy_wand
   *   能力药丸(10抖)  → ability_pill
   *   甜甜圈(52抖)   → donut
   *   能量电池(99抖)  → battery
   *   爱的爆炸(199抖) → love_blast
   *   神秘空投(520抖) → mystery_drop
   */
  _mapDouyinGift(secGiftId) {
    // sec_gift_id → 游戏礼物ID 映射表
    const GIFT_ID_MAP = {
      'n1/Dg1905sj1FyoBlQBvmbaDZFBNaKuKZH6zxHkv8Lg5x2cRfrKUTb8gzMs=': 'fairy_wand',       // 仙女棒(1抖)
      '28rYzVFNyXEXFC8HI+f/WG+I7a6lfl3OyZZjUS+CVuwCgYZrPrUdytGHu0c=': 'ability_pill',    // 能力药丸(10抖)
      'PJ0FFeaDzXUreuUBZH6Hs+b56Jh0tQjrq0bIrrlZmv13GSAL9Q1hf59fjGk=': 'donut',           // 甜甜圈(52抖)
      'IkkadLfz7O/a5UR45p/OOCCG6ewAWVbsuzR/Z+v1v76CBU+mTG/wPjqdpfg=': 'battery',         // 能量电池(99抖)
      'gx7pmjQfhBaDOG2XkWI2peZ66YFWkCWRjZXpTqb23O/epru+sxWyTV/3Ufs=': 'love_blast',      // 爱的爆炸(199抖)
      'pGLo7HKNk1i4djkicmJXf6iWEyd+pfPBjbsHmd3WcX0Ierm2UdnRR7UINvI=': 'mystery_drop',    // 神秘空投(520抖)
    };

    const mapped = GIFT_ID_MAP[secGiftId];
    if (mapped) {
      console.log(`[Room:${this.roomId}] Gift mapped: ${secGiftId.substring(0, 20)}... → ${mapped}`);
      return mapped;
    }

    // 未识别的礼物ID，记录日志（用于新游戏获取sec_gift_id）
    console.warn(`[Room:${this.roomId}] ⚠️ Unknown sec_gift_id: ${secGiftId}, treating as fairy_wand`);
    return 'fairy_wand';
  }

  // ==================== WebSocket消息处理 ====================

  /**
   * 处理来自客户端的WebSocket消息
   */
  handleMessage(ws, msg) {
    this.lastActiveAt = Date.now();

    switch (msg.type) {
      case 'heartbeat':
        ws.send(JSON.stringify({ type: 'heartbeat', roomId: this.roomId, timestamp: Date.now() }));
        break;

      case 'start_game':
        console.log(`[Room:${this.roomId}] Start game`);
        this.gameEngine.startGame();
        break;

      case 'reset_game':
        console.log(`[Room:${this.roomId}] Reset game`);
        this.simulator.stop();
        this.gameEngine.reset();
        this.playerManager.reset();
        break;

      case 'toggle_sim':
        if (msg.data && msg.data.enabled) {
          if (msg.data.review) {
            this.simulator.startReviewDemo();
          } else if (msg.data.showcase) {
            this.simulator.startShowcase();
          } else {
            this.simulator.start();
          }
        } else {
          this.simulator.stop();
        }
        this.broadcast({
          type: 'sim_status',
          timestamp: Date.now(),
          data: { enabled: this.simulator.isEnabled() }
        });
        break;

      case 'test_barrage':
        if (msg.data) {
          const camp = this.playerManager.parseCamp(msg.data.content);
          if (camp) {
            const result = this.playerManager.joinCamp(msg.data.playerId, msg.data.playerName, '', camp);
            if (result.success) {
              this.gameEngine.addForce(camp, 10); // 加入基础推力（永久）
              this.playerManager.addContribution(msg.data.playerId, 10, 10);
              const vipInfo3 = this.playerManager.getVipInfo(msg.data.playerId);
              this.broadcast({
                type: 'player_joined',
                timestamp: Date.now(),
                data: {
                  playerId: msg.data.playerId,
                  playerName: msg.data.playerName,
                  avatarUrl: '',
                  camp,
                  totalLeft: result.totalLeft,
                  totalRight: result.totalRight,
                  isVip: vipInfo3?.isVip || false,
                  vipRank: vipInfo3?.vipRank || 0,
                  vipTitle: vipInfo3?.vipTitle || '',
                  vipType: vipInfo3?.vipType || ''
                }
              });
            }
          }
        }
        break;

      case 'test_gift':
        if (msg.data) {
          this.processGift(
            msg.data.playerId, msg.data.playerName,
            msg.data.camp, msg.data.giftId, msg.data.count || 1
          );
        }
        break;

      case 'ranking_query':
        if (msg.data && msg.data.period) {
          const rankData = this.playerManager.getHistoryRankings(msg.data.period);
          ws.send(JSON.stringify({
            type: 'persistent_ranking',
            roomId: this.roomId,
            timestamp: Date.now(),
            data: rankData
          }));
        }
        break;

      case 'player_data_query':
        console.log(`[Room:${this.roomId}] Player data panel query`);
        const panelData = this.playerManager.getPlayerDataPanel();
        ws.send(JSON.stringify({
          type: 'player_data_panel',
          roomId: this.roomId,
          timestamp: Date.now(),
          data: panelData
        }));
        break;

      case 'settlement_report':
        console.log(`[Room:${this.roomId}] Settlement report received from client`);
        break;

      case 'leave_room':
        // 客户端主动离开房间（不做额外处理，ws.close会触发removeClient）
        console.log(`[Room:${this.roomId}] Client requested leave`);
        break;

      default:
        console.log(`[Room:${this.roomId}] Unknown message: ${msg.type}`);
    }
  }

  // ==================== 状态查询 ====================

  /**
   * 获取房间信息摘要
   */
  getInfo() {
    return {
      roomId: this.roomId,
      status: this.status,
      clientCount: this.clients.size,
      gameState: this.gameEngine.state,
      playerCount: this.playerManager.getTotalCount(),
      leftCount: this.playerManager.getCampCount('left'),
      rightCount: this.playerManager.getCampCount('right'),
      simulatorEnabled: this.simulator.isEnabled(),
      createdAt: this.createdAt,
      lastActiveAt: this.lastActiveAt
    };
  }
}

module.exports = Room;
