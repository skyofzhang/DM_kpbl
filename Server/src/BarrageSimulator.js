/**
 * 弹幕模拟器
 * 自动生成假弹幕/礼物事件用于测试
 *
 * 默认模式(start): 轮流送礼测试（3~5秒间隔，低级→高级循环6种礼物）
 * 展示模式(startShowcase): 6种礼物轮流送，间隔5秒
 * 审核演示(startReviewDemo): 完整功能覆盖，按时间线编排，适合录屏提交审核
 */

const { getRandomGiftId, getGift } = require('./GiftConfig');

// 随机中文名生成
const SURNAMES = ['张', '李', '王', '赵', '刘', '陈', '杨', '黄', '周', '吴'];
const NAMES = ['小明', '大壮', '美美', '强强', '欢欢', '乐乐', '豆豆', '甜甜', '阿宝', '旺财',
               '铁柱', '翠花', '狗蛋', '二狗', '建国', '秀英', '春花', '富贵', '大力', '小花'];

function randomName() {
  return SURNAMES[Math.floor(Math.random() * SURNAMES.length)] +
         NAMES[Math.floor(Math.random() * NAMES.length)];
}

// 6种礼物从低到高
const GIFT_CYCLE = ['fairy_wand', 'ability_pill', 'donut', 'battery', 'love_blast', 'mystery_drop'];

class BarrageSimulator {
  constructor(gameEngine, playerManager, processGift, broadcast, roomCallbacks) {
    this.gameEngine = gameEngine;
    this.playerManager = playerManager;
    this.processGift = processGift;
    this.broadcast = broadcast;
    this.enabled = false;
    this.playerCounter = 0;

    // Room级别的回调（用于模拟666/点赞等需要经过Room处理的事件）
    // roomCallbacks = { handleComment, handleLike }
    this.roomCallbacks = roomCallbacks || {};

    // 100个固定假人池：保证跨局同一批ID，数据可追溯
    this.playerPool = [];
    this._initFixedPlayerPool(100);

    // 轮流送礼索引（默认模式和展示模式共用）
    this.giftCycleIndex = 0;

    // 定时器
    this.joinTimer = null;
    this.giftTimer = null;
    this.showcaseTimer = null;
    this.reviewTimer = null;

    // 模式标记
    this.showcaseMode = false;
    this.reviewMode = false;
  }

  /**
   * 初始化固定假人池：sim_001 ~ sim_100
   * 每个假人有固定ID和名字，跨局复用以支持连胜/SP累积
   */
  _initFixedPlayerPool(count) {
    this.playerPool = [];
    // DiceBear API 免费头像（多种风格，HTTPS PNG格式，与抖音头像URL格式一致）
    const styles = ['adventurer', 'avataaars', 'bottts', 'fun-emoji', 'lorelei'];
    for (let i = 1; i <= count; i++) {
      const id = `sim_${String(i).padStart(3, '0')}`;
      const name = `${SURNAMES[i % SURNAMES.length]}${NAMES[i % NAMES.length]}`;
      const style = styles[i % styles.length];
      const avatarUrl = `https://api.dicebear.com/7.x/${style}/png?seed=${id}&size=128`;
      this.playerPool.push({ id, name, avatarUrl });
    }
    this.playerCounter = count;
    console.log(`[BarrageSimulator] 初始化${count}个固定假人池（含头像）`);
  }

  /**
   * 默认模拟模式：
   * - 先加入6个玩家（左右各3）
   * - 然后每3~5秒轮流送1个礼物（从仙女棒到神秘空投循环）
   * - 同时每2秒缓慢加入新玩家（30%概率）
   */
  start() {
    if (this.enabled) return;
    this.enabled = true;
    this.showcaseMode = false;
    this.reviewMode = false;
    this.giftCycleIndex = 0;
    console.log('[SIM] 弹幕模拟器已开启（轮流送礼模式：3~5秒间隔，低→高循环）');

    // 先加入6个玩家
    for (let i = 0; i < 6; i++) {
      this._simulateJoin();
    }

    // 轮流送礼
    this._scheduleCycleGift();
    // 缓慢加入新玩家
    this._scheduleJoin();
  }

  /**
   * 展示模式：6种礼物按顺序轮流送，间隔5秒，每次1个
   */
  startShowcase() {
    if (this.enabled) this.stop();
    this.enabled = true;
    this.showcaseMode = true;
    this.reviewMode = false;
    this.giftCycleIndex = 0;
    console.log('[SIM] 礼物展示模式已开启 - 6种礼物轮流送，间隔5秒');

    // 先快速加入6个玩家（左右各3）
    for (let i = 0; i < 6; i++) {
      this._simulateJoin();
    }

    // 开始轮流送礼
    this._scheduleShowcaseGift();
  }

  /**
   * ==================== 审核演示模式 ====================
   * 完整功能覆盖，按时间线编排所有功能点，适合录屏提交平台审核
   *
   * 时间线（约2.5分钟完成所有功能展示，之后持续运行直到对局结束）：
   *
   * Phase 1 [0~15s]  玩家入场
   *   - 快速加入20个玩家(左右各10)，展示入场通知
   *   - 每0.6秒加入1个，展示加入动画和阵营分配
   *
   * Phase 2 [15~45s] 6种礼物逐一展示
   *   - 每种礼物间隔4秒，从tier1仙女棒到tier6神秘空投
   *   - 左右阵营交替送礼，展示不同模型召唤
   *
   * Phase 3 [45~75s] 升级系统展示
   *   - 选一个玩家连续送仙女棒，从Lv.1升到Lv.5+
   *   - 展示升级通知弹窗 + 头顶光圈变化
   *
   * Phase 4 [75~95s] 666指令+点赞展示
   *   - 多个玩家发送666，展示推力加成弹窗
   *   - 多个玩家点赞，展示点赞推力弹窗
   *
   * Phase 5 [95~150s] 混合高频互动
   *   - 模拟真实直播场景，混合送礼+666+点赞+新人加入
   *   - 让橘子明显移动，展示推力差效果
   *   - 之后持续高频互动直到对局结束（自然结算）
   */
  startReviewDemo() {
    if (this.enabled) this.stop();
    this.enabled = true;
    this.showcaseMode = false;
    this.reviewMode = true;
    this._reviewStepIndex = 0;
    this._reviewTimers = [];

    // 随机选择偏向阵营（70%的礼物给这一侧，加速推进，防止拉锯浪费录屏时间）
    this._reviewBiasCamp = Math.random() < 0.5 ? 'left' : 'right';
    this._reviewBiasRatio = 0.70; // 偏向侧获得70%的礼物

    console.log('[SIM] ===== 审核演示模式启动 =====');
    console.log(`[SIM] 偏向阵营: ${this._reviewBiasCamp} (${this._reviewBiasRatio * 100}%礼物偏向)`);
    console.log('[SIM] 时间线: P1入场(0~15s) → P2礼物展示(15~45s) → P3升级(45~75s) → P4指令(75~95s) → P5混合互动(95s+)');

    this._buildReviewTimeline();
  }

  /**
   * 构建审核演示时间线
   */
  _buildReviewTimeline() {
    const schedule = (delayMs, fn, label) => {
      const timer = setTimeout(() => {
        if (!this.enabled || !this.reviewMode) return;
        if (this.gameEngine.state !== 'running') return;
        console.log(`[SIM-审核] ${label}`);
        fn();
      }, delayMs);
      this._reviewTimers.push(timer);
    };

    let t = 0; // 累计时间(ms)

    // ========== Phase 1: 玩家入场 [0~15s] ==========
    console.log('[SIM-审核] Phase 1: 玩家入场（20人，每0.6秒1人）');
    for (let i = 0; i < 20; i++) {
      const camp = i % 2 === 0 ? 'left' : 'right';
      schedule(t, () => this._simulateJoinCamp(camp), `P1: 玩家${i + 1}加入${camp}阵营`);
      t += 600;
    }

    // ========== Phase 2: 6种礼物逐一展示 [15~45s] ==========
    t = 15000;
    console.log('[SIM-审核] Phase 2: 6种礼物展示（每4秒1种）');
    const giftNames = ['仙女棒', '能力药丸', '甜甜圈', '能量电池', '爱的爆炸', '神秘空投'];
    for (let i = 0; i < GIFT_CYCLE.length; i++) {
      const giftId = GIFT_CYCLE[i];
      const giftName = giftNames[i];
      const camp = i % 2 === 0 ? 'left' : 'right'; // 交替阵营
      schedule(t, () => {
        const player = this._getPlayerByCamp(camp);
        if (player) {
          this.processGift(player.id, player.name, player.camp, giftId, 1);
        }
      }, `P2: ${giftName}(tier${i + 1}) → ${camp}阵营`);
      t += 4000;
    }

    // ========== Phase 3: 升级系统展示 [45~75s] ==========
    t = 45000;
    console.log('[SIM-审核] Phase 3: 升级系统展示（连续仙女棒升级）');

    // 选定一个左阵营和一个右阵营的玩家做升级展示
    // 升级阈值: [1, 11, 77, 265, 785, ...] → Lv2需要11根，Lv3需要77根
    // 快速展示：先送10根(接近Lv2)→再送2根(升Lv2)→再大量升到Lv3→Lv4
    const upgradeBatches = [
      { count: 10, delay: 0, note: '送10根仙女棒(接近Lv2)' },
      { count: 2,  delay: 2000, note: '再送2根→升级到Lv.2!' },
      { count: 30, delay: 4000, note: '送30根(累计42)' },
      { count: 36, delay: 6000, note: '再送36根→升级到Lv.3!(累计78)' },
      { count: 90, delay: 8000, note: '送90根(累计168)' },
      { count: 98, delay: 10000, note: '再送98根→升级到Lv.4!(累计266)' },
    ];

    for (const batch of upgradeBatches) {
      schedule(t + batch.delay, () => {
        // 选左阵营第一个玩家
        const player = this._getPlayerByCamp('left');
        if (player) {
          this.processGift(player.id, player.name, player.camp, 'fairy_wand', batch.count);
        }
      }, `P3: 左阵营${batch.note}`);
    }

    // 同时给右阵营一个玩家也升一些级（平衡展示）
    schedule(t + 3000, () => {
      const player = this._getPlayerByCamp('right');
      if (player) {
        this.processGift(player.id, player.name, player.camp, 'fairy_wand', 12);
      }
    }, 'P3: 右阵营送12根仙女棒→Lv.2');

    schedule(t + 9000, () => {
      const player = this._getPlayerByCamp('right');
      if (player) {
        this.processGift(player.id, player.name, player.camp, 'fairy_wand', 66);
      }
    }, 'P3: 右阵营再送66根→Lv.3');

    // ========== Phase 4: 666指令 + 点赞展示 [75~95s] ==========
    t = 75000;
    console.log('[SIM-审核] Phase 4: 666指令+点赞展示');

    // 666指令：4个玩家发送666
    for (let i = 0; i < 4; i++) {
      const camp = i % 2 === 0 ? 'left' : 'right';
      schedule(t + i * 2500, () => {
        const player = this._getPlayerByCamp(camp);
        if (player && this.roomCallbacks.handleComment) {
          this.roomCallbacks.handleComment(player.id, player.name, player.avatarUrl || '', '666');
        }
      }, `P4: ${camp}阵营玩家发送666`);
    }

    // 点赞：3个玩家点赞（不同数量）
    const likeCounts = [5, 12, 30];
    for (let i = 0; i < 3; i++) {
      schedule(t + 12000 + i * 2500, () => {
        const camp = i % 2 === 0 ? 'left' : 'right';
        const player = this._getPlayerByCamp(camp);
        if (player && this.roomCallbacks.handleLike) {
          this.roomCallbacks.handleLike(player.id, player.name, player.avatarUrl || '', likeCounts[i]);
        }
      }, `P4: 点赞×${likeCounts[i]}`);
    }

    // ========== Phase 5: 混合高频互动 [95s+] ==========
    t = 95000;
    console.log('[SIM-审核] Phase 5: 混合高频互动（持续到对局结束）');

    // 先给一方大量推力，让橘子明显移动（展示推力差效果）
    schedule(t, () => {
      const player = this._getPlayerByCamp('left');
      if (player) {
        // 送一个高级礼物让橘子明显偏移
        this.processGift(player.id, player.name, player.camp, 'love_blast', 1);
      }
    }, 'P5: 左阵营送爱的爆炸→橘子大幅偏移');

    schedule(t + 5000, () => {
      const player = this._getPlayerByCamp('right');
      if (player) {
        // 右阵营反击
        this.processGift(player.id, player.name, player.camp, 'battery', 2);
      }
    }, 'P5: 右阵营送2个能量电池反击');

    // 启动持续混合互动（每2秒一个随机事件）
    schedule(t + 10000, () => {
      this._startMixedLoop();
    }, 'P5: 启动持续混合互动循环');

    // 持续加入新玩家（每3秒一个）
    schedule(t + 5000, () => {
      this._startSlowJoinLoop();
    }, 'P5: 启动持续玩家加入');
  }

  /**
   * 混合互动循环（Phase 5）
   * 每2秒执行一个随机事件：送礼(50%) / 666(25%) / 点赞(25%)
   */
  _startMixedLoop() {
    if (!this.enabled || !this.reviewMode) return;

    const doMixed = () => {
      if (!this.enabled || !this.reviewMode) return;
      if (this.gameEngine.state !== 'running') return;

      const rand = Math.random();
      // 使用偏向比例决定阵营（加速一侧推进）
      const biasRatio = this._reviewBiasRatio || 0.5;
      const biasCamp = this._reviewBiasCamp || 'left';
      const camp = Math.random() < biasRatio ? biasCamp : (biasCamp === 'left' ? 'right' : 'left');
      const player = this._getPlayerByCamp(camp);
      if (!player) return;

      if (rand < 0.50) {
        // 送礼（按权重随机选择）
        const giftId = getRandomGiftId();
        const gift = getGift(giftId);
        // 偏向阵营额外送双倍数量（加速推进）
        const count = (player.camp === biasCamp && Math.random() < 0.4) ? 2 : 1;
        console.log(`[SIM-审核] 混合: ${player.name} 送 ${gift?.name || giftId}×${count}`);
        this.processGift(player.id, player.name, player.camp, giftId, count);
      } else if (rand < 0.75) {
        // 666
        if (this.roomCallbacks.handleComment) {
          console.log(`[SIM-审核] 混合: ${player.name} 发送666`);
          this.roomCallbacks.handleComment(player.id, player.name, player.avatarUrl || '', '666');
        }
      } else {
        // 点赞
        const likeNum = Math.floor(Math.random() * 20) + 1;
        if (this.roomCallbacks.handleLike) {
          console.log(`[SIM-审核] 混合: ${player.name} 点赞×${likeNum}`);
          this.roomCallbacks.handleLike(player.id, player.name, player.avatarUrl || '', likeNum);
        }
      }
    };

    // 每2秒执行一次
    const loop = () => {
      if (!this.enabled || !this.reviewMode) return;
      doMixed();
      this.reviewTimer = setTimeout(loop, 2000);
    };
    loop();
  }

  /**
   * 持续缓慢加入新玩家（Phase 5）
   */
  _startSlowJoinLoop() {
    if (!this.enabled || !this.reviewMode) return;

    const loop = () => {
      if (!this.enabled || !this.reviewMode) return;
      if (this.gameEngine.state === 'running') {
        this._simulateJoin();
      }
      this.joinTimer = setTimeout(loop, 3000);
    };
    loop();
  }

  /**
   * 获取指定阵营的一个随机玩家
   */
  _getPlayerByCamp(camp) {
    const players = [];
    for (const [id, p] of this.playerManager.players) {
      if (p.camp === camp) players.push(p);
    }
    if (players.length === 0) return null;
    return players[Math.floor(Math.random() * players.length)];
  }

  /**
   * 加入指定阵营
   */
  _simulateJoinCamp(camp) {
    const available = this.playerPool.filter(p => !this.playerManager.players.has(p.id));
    if (available.length === 0) return;
    const picked = available[Math.floor(Math.random() * available.length)];

    const avatarUrl = picked.avatarUrl || '';
    const result = this.playerManager.joinCamp(picked.id, picked.name, avatarUrl, camp);
    if (result.success) {
      this.gameEngine.addForce(camp, 10);
      this.playerManager.addContribution(picked.id, 10);

      const vipInfo = this.playerManager.getVipInfo(picked.id);
      this.broadcast({
        type: 'player_joined',
        timestamp: Date.now(),
        data: {
          playerId: picked.id,
          playerName: picked.name,
          avatarUrl,
          camp,
          totalLeft: result.totalLeft,
          totalRight: result.totalRight,
          isVip: vipInfo?.isVip || false,
          vipRank: vipInfo?.vipRank || 0,
          vipTitle: vipInfo?.vipTitle || '',
          vipType: vipInfo?.vipType || ''
        }
      });

      this.broadcast({
        type: 'ranking_update',
        timestamp: Date.now(),
        data: this.playerManager.getAllRankings()
      });
    }
  }

  stop() {
    this.enabled = false;
    this.showcaseMode = false;
    this.reviewMode = false;
    if (this.joinTimer) clearTimeout(this.joinTimer);
    if (this.giftTimer) clearTimeout(this.giftTimer);
    if (this.showcaseTimer) clearTimeout(this.showcaseTimer);
    if (this.reviewTimer) clearTimeout(this.reviewTimer);
    this.joinTimer = null;
    this.giftTimer = null;
    this.showcaseTimer = null;
    this.reviewTimer = null;

    // 清理审核模式的所有定时器
    if (this._reviewTimers) {
      for (const t of this._reviewTimers) clearTimeout(t);
      this._reviewTimers = [];
    }

    console.log('[SIM] 弹幕模拟器已关闭');
  }

  // ==================== 默认模式：轮流送礼 ====================

  /**
   * 默认模式的礼物调度：3~5秒间隔，从低级到高级循环
   */
  _scheduleCycleGift() {
    if (!this.enabled || this.showcaseMode || this.reviewMode) return;
    // 3~5秒随机间隔
    const delay = 3000 + Math.random() * 2000;
    this.giftTimer = setTimeout(() => {
      if (this.gameEngine.state === 'running') {
        this._simulateCycleGift();
      }
      this._scheduleCycleGift();
    }, delay);
  }

  /**
   * 送出当前轮次的礼物（固定1个，从低到高循环）
   */
  _simulateCycleGift() {
    const player = this.playerManager.getRandomPlayer();
    if (!player) return;

    const giftId = GIFT_CYCLE[this.giftCycleIndex];
    const gift = getGift(giftId);
    if (!gift) return;

    console.log(`[SIM] 轮流送礼 ${this.giftCycleIndex + 1}/6: ${player.name}(${player.camp}) 送出 ${gift.name} (推力${gift.forceValue})`);
    this.processGift(player.id, player.name, player.camp, giftId, 1);

    // 推进到下一个礼物（循环）
    this.giftCycleIndex = (this.giftCycleIndex + 1) % GIFT_CYCLE.length;
  }

  // ==================== 展示模式 ====================

  _scheduleShowcaseGift() {
    if (!this.enabled || !this.showcaseMode) return;
    this.showcaseTimer = setTimeout(() => {
      if (this.gameEngine.state === 'running') {
        this._simulateShowcaseGift();
      }
      this._scheduleShowcaseGift();
    }, 5000);
  }

  _simulateShowcaseGift() {
    const player = this.playerManager.getRandomPlayer();
    if (!player) return;

    const giftId = GIFT_CYCLE[this.giftCycleIndex];
    const gift = getGift(giftId);
    if (!gift) return;

    console.log(`[SIM-展示] 第${this.giftCycleIndex + 1}/6: ${player.name} 送出 ${gift.name} (${giftId})`);
    this.processGift(player.id, player.name, player.camp, giftId, 1);

    this.giftCycleIndex = (this.giftCycleIndex + 1) % GIFT_CYCLE.length;
  }

  // ==================== 玩家加入 ====================

  _scheduleJoin() {
    if (!this.enabled || this.reviewMode) return;
    this.joinTimer = setTimeout(() => {
      // 默认模式下缓慢加入（30%概率，每2秒）
      if (this.gameEngine.state === 'running' && Math.random() < 0.3) {
        this._simulateJoin();
      }
      this._scheduleJoin();
    }, 2000);
  }

  _simulateJoin() {
    let playerId, playerName;

    // 从100个固定假人中随机选一个当前不在游戏中的
    const available = this.playerPool.filter(p => !this.playerManager.players.has(p.id));
    if (available.length === 0) {
      // 所有假人都已在游戏中，跳过
      return;
    }
    const picked = available[Math.floor(Math.random() * available.length)];
    playerId = picked.id;
    playerName = picked.name;

    const camp = Math.random() < 0.5 ? 'left' : 'right';
    const avatarUrl = picked.avatarUrl || '';
    const result = this.playerManager.joinCamp(playerId, playerName, avatarUrl, camp);
    if (result.success) {
      // 加入时给一个基础推力
      this.gameEngine.addForce(camp, 10);
      this.playerManager.addContribution(playerId, 10);

      const vipInfo = this.playerManager.getVipInfo(playerId);
      this.broadcast({
        type: 'player_joined',
        timestamp: Date.now(),
        data: {
          playerId,
          playerName,
          avatarUrl,
          camp,
          totalLeft: result.totalLeft,
          totalRight: result.totalRight,
          isVip: vipInfo?.isVip || false,
          vipRank: vipInfo?.vipRank || 0,
          vipTitle: vipInfo?.vipTitle || '',
          vipType: vipInfo?.vipType || ''
        }
      });

      this.broadcast({
        type: 'ranking_update',
        timestamp: Date.now(),
        data: this.playerManager.getAllRankings()
      });
    }
  }

  isEnabled() {
    return this.enabled;
  }
}

module.exports = BarrageSimulator;
