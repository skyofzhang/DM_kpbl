/**
 * 玩家管理模块
 * 管理玩家加入阵营、贡献值统计、排行榜、历史数据持久化
 *
 * 数据安全策略:
 * 1. 每局结算后立即写盘（matchHistory + playerStats）
 * 2. 写盘使用 先写临时文件→原子rename 防止写入中断导致文件损坏
 * 3. 启动时自动加载历史数据，加载失败尝试从备份恢复
 * 4. 每次写盘同时保留一份 .bak 备份
 */

const fs = require('fs');
const path = require('path');

// 数据根目录
const DATA_ROOT = path.join(__dirname, '..', 'data');

// ==================== 升级系统常量 ====================

// 累积仙女棒数量 → 等级阈值 (Lv.1 ~ Lv.10)
const UPGRADE_THRESHOLDS = [1, 11, 77, 265, 785, 1305, 2304, 3618, 6246, 10188];

// 各等级推力效果值（策划表绝对值）
const LEVEL_FORCE_TABLE = {
  // 点赞效果(3秒临时推力)
  likeForce:  [5, 9, 16, 40, 90, 140, 240, 330, 480, 600],
  // 666效果(5秒临时推力)
  boostForce: [20, 36, 64, 160, 360, 560, 960, 1320, 1920, 2400],
  // 基础推力加成(仙女棒每次永久推力)
  baseForce:  [10, 15, 32, 80, 180, 280, 480, 660, 1000, 1440]
};

class PlayerManager {
  /**
   * @param {string} [roomId] - 房间ID，用于数据隔离。不传则使用根目录（向后兼容）
   */
  constructor(roomId) {
    this.roomId = roomId || null;

    // 数据目录: data/ 或 data/rooms/{roomId}/
    if (this.roomId) {
      this.dataDir = path.join(DATA_ROOT, 'rooms', this.roomId);
    } else {
      this.dataDir = DATA_ROOT;
    }
    this.matchHistoryFile = path.join(this.dataDir, 'matchHistory.json');
    this.playerStatsFile = path.join(this.dataDir, 'playerStats.json');
    this.players = new Map(); // playerId -> { id, name, camp, contribution, joinTime, fairyWandCount, upgradeLevel }
    this.campKeywords = {
      left: ['1', '111', '左', '香橙'],
      right: ['2', '222', '右', '柚子']
    };

    // 历史排行数据
    this.matchHistory = [];          // 每局结算记录
    this.playerStats = new Map();    // playerId -> { name, totalScore, wins, matches, lastActive }

    // 启动时加载持久化数据
    this._ensureDataDir();
    this._loadFromDisk();
  }

  // ==================== 文件持久化 ====================

  /**
   * 确保数据目录存在
   */
  _ensureDataDir() {
    try {
      if (!fs.existsSync(this.dataDir)) {
        fs.mkdirSync(this.dataDir, { recursive: true });
        console.log(`[PM${this.roomId ? ':' + this.roomId : ''}] Data directory created: ${this.dataDir}`);
      }
    } catch (e) {
      console.error(`[PM${this.roomId ? ':' + this.roomId : ''}] Failed to create data directory: ${e.message}`);
    }
  }

  /**
   * 原子写文件：先写 .tmp，再 rename，防止写入中断损坏数据
   */
  _atomicWrite(filePath, data) {
    const tmpFile = filePath + '.tmp';
    const bakFile = filePath + '.bak';

    try {
      const jsonStr = JSON.stringify(data, null, 2);

      // 1. 写入临时文件
      fs.writeFileSync(tmpFile, jsonStr, 'utf8');

      // 2. 如果原文件存在，先备份
      if (fs.existsSync(filePath)) {
        try {
          fs.copyFileSync(filePath, bakFile);
        } catch (e) {
          console.warn(`[PM] Backup failed (non-critical): ${e.message}`);
        }
      }

      // 3. 原子替换（rename 在同一文件系统上是原子操作）
      fs.renameSync(tmpFile, filePath);

      return true;
    } catch (e) {
      console.error(`[PM] Atomic write failed for ${filePath}: ${e.message}`);
      // 清理临时文件
      try { if (fs.existsSync(tmpFile)) fs.unlinkSync(tmpFile); } catch (_) {}
      return false;
    }
  }

  /**
   * 安全读取文件：主文件损坏时尝试 .bak 备份
   */
  _safeRead(filePath) {
    // 尝试主文件
    try {
      if (fs.existsSync(filePath)) {
        const raw = fs.readFileSync(filePath, 'utf8');
        const data = JSON.parse(raw);
        return data;
      }
    } catch (e) {
      console.warn(`[PM] Main file corrupted (${filePath}): ${e.message}`);
    }

    // 主文件失败，尝试备份
    const bakFile = filePath + '.bak';
    try {
      if (fs.existsSync(bakFile)) {
        console.log(`[PM] Trying backup file: ${bakFile}`);
        const raw = fs.readFileSync(bakFile, 'utf8');
        const data = JSON.parse(raw);
        console.log(`[PM] Recovered from backup: ${bakFile}`);
        return data;
      }
    } catch (e) {
      console.error(`[PM] Backup also corrupted (${bakFile}): ${e.message}`);
    }

    return null;
  }

  /**
   * 启动时从磁盘加载数据
   */
  _loadFromDisk() {
    const tag = `[PM${this.roomId ? ':' + this.roomId : ''}]`;

    // 加载对局历史
    const historyData = this._safeRead(this.matchHistoryFile);
    if (Array.isArray(historyData)) {
      this.matchHistory = historyData;
      console.log(`${tag} Loaded ${this.matchHistory.length} match history records`);
    } else {
      this.matchHistory = [];
      console.log(`${tag} No match history found, starting fresh`);
    }

    // 加载玩家累计统计
    const statsData = this._safeRead(this.playerStatsFile);
    if (statsData && typeof statsData === 'object' && !Array.isArray(statsData)) {
      this.playerStats = new Map(Object.entries(statsData));
      console.log(`${tag} Loaded ${this.playerStats.size} player stats records`);
    } else {
      this.playerStats = new Map();
      console.log(`${tag} No player stats found, starting fresh`);
    }
  }

  /**
   * 将数据写入磁盘（每局结算后调用）
   */
  _saveToDisk() {
    const tag = `[PM${this.roomId ? ':' + this.roomId : ''}]`;

    // 保存对局历史
    const histOk = this._atomicWrite(this.matchHistoryFile, this.matchHistory);

    // 保存玩家统计（Map → Object）
    const statsObj = {};
    for (const [id, stats] of this.playerStats.entries()) {
      statsObj[id] = stats;
    }
    const statsOk = this._atomicWrite(this.playerStatsFile, statsObj);

    if (histOk && statsOk) {
      console.log(`${tag} Data saved to disk. History: ${this.matchHistory.length}, Players: ${this.playerStats.size}`);
    } else {
      console.error(`${tag} WARNING: Data save partially failed!`);
    }
  }

  // ==================== 当局玩家管理 ====================

  // 根据弹幕内容判断阵营
  parseCamp(content) {
    const trimmed = content.trim();
    if (this.campKeywords.left.includes(trimmed)) return 'left';
    if (this.campKeywords.right.includes(trimmed)) return 'right';
    return null;
  }

  // 玩家加入阵营
  joinCamp(playerId, playerName, avatarUrl, camp) {
    if (this.players.has(playerId)) {
      return { success: false, reason: 'already_joined' };
    }
    this.players.set(playerId, {
      id: playerId,
      name: this._sanitizeNickname(playerName),
      avatarUrl: avatarUrl || '',
      camp,
      contribution: 0,
      totalForce: 0,         // 本局累计推力（用于荣誉卡片和玩家数据面板显示）
      joinTime: Date.now(),
      fairyWandCount: 0,    // 本局累积仙女棒数量
      upgradeLevel: 1        // 当前升级等级 1~10
    });

    // 更新 lastActive（小时榜过滤用）：玩家加入也算活跃
    if (this.playerStats.has(playerId)) {
      this.playerStats.get(playerId).lastActive = Date.now();
    }

    return {
      success: true,
      camp,
      totalLeft: this.getCampCount('left'),
      totalRight: this.getCampCount('right')
    };
  }

  /**
   * 清洗昵称: 去空格、限长度、过滤控制字符
   * 保留emoji（Unity TMP_FontAsset Chinese Font fallback支持emoji渲染）
   */
  _sanitizeNickname(name) {
    if (!name || typeof name !== 'string') return '匿名用户';
    // 去首尾空格
    let cleaned = name.trim();
    // 过滤控制字符(U+0000~U+001F, U+007F~U+009F)，保留emoji和其他Unicode
    cleaned = cleaned.replace(/[\x00-\x1F\x7F-\x9F]/g, '');
    // 限制长度（20字符，按Unicode字符计算，emoji算1个）
    if ([...cleaned].length > 20) {
      cleaned = [...cleaned].slice(0, 20).join('');
    }
    return cleaned || '匿名用户';
  }

  // 获取玩家
  getPlayer(playerId) {
    return this.players.get(playerId) || null;
  }

  /**
   * 增加贡献值和推力累计
   * @param {string} playerId
   * @param {number} contribution - 贡献分（基于推力值，非金额）
   * @param {number} [force=0] - 本次推力值（用于per-player推力显示）
   */
  addContribution(playerId, contribution, force = 0) {
    const player = this.players.get(playerId);
    if (!player) return;
    player.contribution += contribution;
    if (force > 0) player.totalForce += force;

    // 更新 lastActive（小时榜过滤用）：送礼=活跃行为
    if (this.playerStats.has(playerId)) {
      this.playerStats.get(playerId).lastActive = Date.now();
    }
  }

  // 获取阵营人数
  getCampCount(camp) {
    let count = 0;
    for (const p of this.players.values()) {
      if (p.camp === camp) count++;
    }
    return count;
  }

  // 获取阵营排行榜（Top N）
  getRankings(camp, topN = 4) {
    const campPlayers = [];
    for (const p of this.players.values()) {
      if (p.camp === camp) {
        const stats = this.playerStats.get(p.id);
        const streak = stats ? (stats.currentStreak || 0) : 0;
        const bet = Math.floor(streak * 0.5);
        campPlayers.push({ id: p.id, name: p.name, avatarUrl: p.avatarUrl || '', contribution: p.contribution, streakBet: bet });
      }
    }
    campPlayers.sort((a, b) => b.contribution - a.contribution);
    return campPlayers.slice(0, topN);
  }

  // 获取双方排行榜（附带连胜信息）
  getAllRankings(topN = 4) {
    return {
      left: this.getRankings('left', topN),
      right: this.getRankings('right', topN),
      streakInfo: this.getStreakInfo()
    };
  }

  /**
   * 获取左右阵营的连胜池信息（各阵营所有玩家投注之和）
   * 投注 = floor(currentStreak * 0.5)
   * @returns {{ left: { streak: number }, right: { streak: number } }}
   */
  getStreakInfo() {
    let leftTotal = 0;
    let rightTotal = 0;
    for (const p of this.players.values()) {
      const stats = this.playerStats.get(p.id);
      if (!stats) continue;
      this._ensurePeriodFields(stats);
      const bet = Math.floor((stats.currentStreak || 0) * 0.5);
      if (p.camp === 'left') leftTotal += bet;
      else rightTotal += bet;
    }
    return { left: { streak: leftTotal }, right: { streak: rightTotal } };
  }

  // 获取总人数
  getTotalCount() {
    return this.players.size;
  }

  // 重置当局玩家（不清除历史数据）
  reset() {
    this.players.clear();
  }

  // ==================== 升级系统 ====================

  /**
   * 累加仙女棒并计算升级
   * @param {string} playerId
   * @param {number} count - 本次仙女棒数量（giftCount）
   * @returns {{ oldLevel: number, newLevel: number, leveledUp: boolean, fairyWandCount: number }}
   */
  addFairyWand(playerId, count) {
    const player = this.players.get(playerId);
    if (!player) return { oldLevel: 1, newLevel: 1, leveledUp: false, fairyWandCount: 0 };

    const oldLevel = player.upgradeLevel;
    player.fairyWandCount += count;

    // 根据累积仙女棒数量计算新等级
    let newLevel = 1;
    for (let i = UPGRADE_THRESHOLDS.length - 1; i >= 0; i--) {
      if (player.fairyWandCount >= UPGRADE_THRESHOLDS[i]) {
        newLevel = i + 1; // 阈值数组index 0~9 对应 Lv.1~Lv.10
        break;
      }
    }
    player.upgradeLevel = newLevel;

    return {
      oldLevel,
      newLevel,
      leveledUp: newLevel > oldLevel,
      fairyWandCount: player.fairyWandCount
    };
  }

  /**
   * 获取玩家当前等级
   * @param {string} playerId
   * @returns {number} 1~10, 未找到返回1
   */
  getPlayerLevel(playerId) {
    const player = this.players.get(playerId);
    return player ? player.upgradeLevel : 1;
  }

  /**
   * 获取指定等级的推力效果值
   * @param {number} level - 1~10
   * @returns {{ baseForce: number, likeForce: number, boostForce: number }}
   */
  getLevelForceValues(level) {
    const idx = Math.max(0, Math.min(9, level - 1)); // clamp to 0~9
    return {
      baseForce: LEVEL_FORCE_TABLE.baseForce[idx],
      likeForce: LEVEL_FORCE_TABLE.likeForce[idx],
      boostForce: LEVEL_FORCE_TABLE.boostForce[idx]
    };
  }

  // 随机给一个已加入的玩家（用于模拟器）
  getRandomPlayer() {
    const arr = Array.from(this.players.values());
    if (arr.length === 0) return null;
    return arr[Math.floor(Math.random() * arr.length)];
  }

  // ==================== 结算数据生成 ====================

  /**
   * 生成完整结算数据（供GameEngine._endGame调用）
   */
  buildSettlementData(winner, reason, leftForce, rightForce) {
    const allPlayers = Array.from(this.players.values());
    const scorePool = allPlayers.reduce((sum, p) => sum + p.contribution, 0);

    // MVP: 贡献最高的玩家
    let mvp = null;
    const sorted = [...allPlayers].sort((a, b) => b.contribution - a.contribution);
    if (sorted.length > 0 && sorted[0].contribution > 0) {
      const m = sorted[0];
      mvp = {
        playerId: m.id,
        playerName: m.name,
        avatarUrl: m.avatarUrl || '',
        camp: m.camp,
        totalContribution: Math.round(m.contribution)
      };
    }

    // 各阵营Top100排行
    const leftRankings = this._buildCampRankings('left', 100);
    const rightRankings = this._buildCampRankings('right', 100);

    // 积分分配: Top6 按 30%/25%/20%/12%/8%/5%
    const ratios = [0.30, 0.25, 0.20, 0.12, 0.08, 0.05];
    const topPlayers = sorted.filter(p => p.contribution > 0).slice(0, ratios.length);
    const scoreDistribution = topPlayers.map((p, i) => ({
      rank: i + 1,
      playerName: p.name,
      avatarUrl: p.avatarUrl || '',
      contribution: Math.round(p.contribution),
      coins: Math.round(scorePool * ratios[i])
    }));

    return {
      winner,
      reason,
      leftForce: Math.round(leftForce),
      rightForce: Math.round(rightForce),
      mvp,
      leftRankings,
      rightRankings,
      scorePool: Math.round(scorePool),
      scoreDistribution,
      streakInfo: this.getStreakInfo()
    };
  }

  _buildCampRankings(camp, count) {
    const campPlayers = [];
    for (const p of this.players.values()) {
      if (p.camp === camp && p.contribution > 0) {
        campPlayers.push(p);
      }
    }
    campPlayers.sort((a, b) => b.contribution - a.contribution);
    return campPlayers.slice(0, count).map((p, i) => ({
      rank: i + 1,
      playerId: p.id,
      playerName: p.name,
      avatarUrl: p.avatarUrl || '',
      contribution: Math.round(p.contribution),
      totalForce: Math.round(p.totalForce || 0),   // 个人累计推力
      streakBet: p._streakBet || 0,     // 本局投入的连胜数
      streakGain: p._streakGain || 0    // 连胜变化（正=瓜分获得，负=损失）
    }));
  }

  // ==================== 连胜结算（在buildSettlementData之前调用） ====================

  /**
   * 计算所有玩家的连胜变化，写入 p._streakBet / p._streakGain
   * 必须在 buildSettlementData() 之前调用，否则排行数据中连胜字段为0
   *
   * === 设计规则（2026-02-21 v5 — 纯连胜数模型） ===
   * 只有一个数值: currentStreak（连胜数 = 连续胜利累计场次 + 瓜分所得）
   * 1. 加入时投注: bet = floor(currentStreak * 0.5), currentStreak -= bet
   * 2. 胜方: 退还bet + 按贡献瓜分败方池 + 本局胜利+1
   * 3. 败方: bet没收 + currentStreak额外-1（最低0）
   * 4. 战斗UI阵营总连胜 = 阵营所有玩家bet之和
   */
  calculateStreakChanges(winner) {
    const INVEST_RATIO = 0.5;

    const allPlayers = Array.from(this.players.values());

    // ============ 阶段1: 收集投注 + 扣除（escrow） ============
    // bet = floor(currentStreak * 50%)
    let leftBetTotal = 0, rightBetTotal = 0;
    const playerBets = new Map();

    for (const p of allPlayers) {
      if (p.contribution <= 0) continue;
      if (!this.playerStats.has(p.id)) {
        this.playerStats.set(p.id, {
          name: p.name, avatarUrl: p.avatarUrl || '',
          totalScore: 0, wins: 0, matches: 0, lastActive: Date.now()
        });
      }
      const stats = this.playerStats.get(p.id);
      this._ensurePeriodFields(stats);

      // 投注 = 连胜数的50%（向下取整）
      const streak = stats.currentStreak || 0;
      const bet = Math.floor(streak * INVEST_RATIO);

      // Escrow: 先从连胜数扣除投注
      stats.currentStreak = streak - bet;

      playerBets.set(p.id, bet);
      if (p.camp === 'left') leftBetTotal += bet;
      else rightBetTotal += bet;
    }

    // ============ 阶段2: 计算池子 ============
    const loserBetTotal = (winner === 'left') ? rightBetTotal : leftBetTotal;
    let winnerTotalContrib = 0;
    for (const p of allPlayers) {
      if (p.camp === winner && p.contribution > 0) winnerTotalContrib += p.contribution;
    }

    console.log(`[PM] 连胜结算v5: winner=${winner}, leftBet=${leftBetTotal}, rightBet=${rightBetTotal}, loserPool=${loserBetTotal}, winnerContrib=${winnerTotalContrib}`);

    // ============ 阶段3: 结算每个玩家 ============
    for (const p of allPlayers) {
      if (p.contribution <= 0) continue;

      const stats = this.playerStats.get(p.id);
      const bet = playerBets.get(p.id) || 0;

      // 更新通用统计
      stats.name = p.name;
      stats.avatarUrl = p.avatarUrl || stats.avatarUrl;
      stats.totalScore += p.contribution;
      stats.matches += 1;
      stats.lastActive = Date.now();

      this._ensurePeriodFields(stats);
      stats.weeklyScore += p.contribution;
      stats.monthlyScore += p.contribution;

      if (p.camp === winner) {
        // === 胜方 ===
        stats.wins += 1;
        stats.weeklyWins += 1;
        stats.monthlyWins += 1;

        // 1. 退还本金（escrow已扣，现在加回来）
        stats.currentStreak += bet;

        // 2. 按贡献瓜分败方连胜池
        const poolShare = winnerTotalContrib > 0
          ? Math.floor(loserBetTotal * (p.contribution / winnerTotalContrib))
          : 0;
        stats.currentStreak += poolShare;

        // 3. 本局胜利+1连胜
        stats.currentStreak += 1;

        if (stats.currentStreak > (stats.bestStreak || 0)) {
          stats.bestStreak = stats.currentStreak;
        }

        // 记录显示数据: 投入X连胜, 赢得Y连胜
        p._streakBet = bet;
        p._streakGain = poolShare + 1; // 瓜分 + 胜利奖励

      } else {
        // === 败方 ===
        // 投注已在Phase1扣除，不退还
        // 额外-1连胜（最低0）
        stats.currentStreak = Math.max(0, stats.currentStreak - 1);

        // 记录显示数据: 投入X连胜, 损失总计
        p._streakBet = bet;
        p._streakGain = -(bet + 1); // 投注被没收 + 额外扣1
      }
    }
  }

  // ==================== 历史排行持久化 ====================

  /**
   * 保存本局结算到历史记录（每局结束调用，自动写盘）
   * 注意：连胜计算已在 calculateStreakChanges() 中完成，此处只做存档
   */
  saveMatchHistory(settlementData) {
    this.matchHistory.push({
      ...settlementData,
      timestamp: Date.now()
    });

    if (this.matchHistory.length > 100) {
      this.matchHistory.shift();
    }

    console.log(`[PM${this.roomId ? ':' + this.roomId : ''}] Match history saved. Total matches: ${this.matchHistory.length}, Players tracked: ${this.playerStats.size}`);

    // 立即写盘保护数据
    this._saveToDisk();
  }

  // ==================== 周期重置辅助 ====================

  /**
   * 获取当前ISO周号 (1~53)，以周一为周起始
   * 返回 "YYYY-WW" 格式，如 "2026-08"
   */
  _getCurrentWeekKey() {
    const now = new Date();
    // ISO周: 以周一为起始，1月4日所在周为第1周
    const jan4 = new Date(now.getFullYear(), 0, 4);
    const dayOfYear = Math.floor((now - new Date(now.getFullYear(), 0, 1)) / 86400000) + 1;
    const jan4DayOfWeek = jan4.getDay() || 7; // 周日=7
    const weekNum = Math.ceil((dayOfYear + jan4DayOfWeek - 1) / 7);
    return `${now.getFullYear()}-W${String(weekNum).padStart(2, '0')}`;
  }

  /**
   * 获取当前月份 key
   * 返回 "YYYY-MM" 格式，如 "2026-02"
   */
  _getCurrentMonthKey() {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
  }

  /**
   * 懒重置：检查玩家的周/月分数是否需要归零
   * 在查询和写入时调用，确保数据时效性
   */
  _ensurePeriodFields(stats) {
    const currentWeek = this._getCurrentWeekKey();
    const currentMonth = this._getCurrentMonthKey();

    // 初始化缺失字段（兼容旧数据）
    if (stats.weeklyScore === undefined) stats.weeklyScore = 0;
    if (stats.monthlyScore === undefined) stats.monthlyScore = 0;
    if (stats.weeklyWins === undefined) stats.weeklyWins = 0;
    if (stats.monthlyWins === undefined) stats.monthlyWins = 0;
    // 连胜字段（2026-02-18新增，v5纯连胜数模型）
    if (stats.currentStreak === undefined) stats.currentStreak = 0;
    if (stats.bestStreak === undefined) stats.bestStreak = 0;
    // lastActive: 旧数据可能没有此字段，默认为当前时间（确保小时榜能查到）
    if (stats.lastActive === undefined) stats.lastActive = Date.now();
    // 兼容: 旧数据中的streakPoints迁移到currentStreak
    if (stats.streakPoints !== undefined && stats.streakPoints > 0) {
      stats.currentStreak = Math.max(stats.currentStreak, stats.streakPoints);
      delete stats.streakPoints;
    }

    // 周重置（含连胜榜）
    if (stats.lastResetWeek !== currentWeek) {
      stats.weeklyScore = 0;
      stats.weeklyWins = 0;
      // 连胜榜跟随周榜重置
      stats.currentStreak = 0;
      stats.bestStreak = 0;
      stats.lastResetWeek = currentWeek;
    }

    // 月重置
    if (stats.lastResetMonth !== currentMonth) {
      stats.monthlyScore = 0;
      stats.monthlyWins = 0;
      stats.lastResetMonth = currentMonth;
    }
  }

  /**
   * 获取持久化排行数据（供ranking_query查询）
   * period: "weekly" | "monthly" | "hourly" | "streak" | "total"
   */
  getHistoryRankings(period) {
    const stats = Array.from(this.playerStats.values());

    // 先确保所有记录的周期字段是最新的
    for (const s of stats) {
      this._ensurePeriodFields(s);
    }

    let sorted;

    switch (period) {
      case 'streak':
        // 连胜榜：按当前连胜排序（展示活跃连胜，非历史最高）
        sorted = stats
          .filter(s => (s.currentStreak || 0) > 0)
          .sort((a, b) => (b.currentStreak || 0) - (a.currentStreak || 0));
        break;
      case 'hourly':
        const oneHourAgo = Date.now() - 3600000;
        sorted = stats
          .filter(s => s.lastActive > oneHourAgo)
          .sort((a, b) => b.totalScore - a.totalScore);
        break;
      case 'weekly':
        sorted = stats
          .filter(s => s.weeklyScore > 0)
          .sort((a, b) => b.weeklyScore - a.weeklyScore);
        break;
      case 'monthly':
        sorted = stats
          .filter(s => s.monthlyScore > 0)
          .sort((a, b) => b.monthlyScore - a.monthlyScore);
        break;
      case 'total':
      default:
        sorted = stats.sort((a, b) => b.totalScore - a.totalScore);
        break;
    }

    const entries = sorted.slice(0, 100).map((s, i) => {
      // 根据period选择显示的分数
      let score;
      switch (period) {
        case 'weekly': score = Math.round(s.weeklyScore); break;
        case 'monthly': score = Math.round(s.monthlyScore); break;
        default: score = Math.round(s.totalScore); break;
      }
      return {
        rank: i + 1,
        playerId: '',
        playerName: s.name,
        avatarUrl: s.avatarUrl || '',
        score,
        wins: period === 'weekly' ? (s.weeklyWins || 0) :
              period === 'monthly' ? (s.monthlyWins || 0) : s.wins,
        streak: s.bestStreak || 0,           // 历史最高连胜
        currentStreak: s.currentStreak || 0  // 当前连胜
      };
    });

    return { period, entries };
  }

  /**
   * 获取玩家VIP信息（周榜/月榜前20名）
   * 用于 player_joined 广播时附带VIP入场数据
   * @param {string} playerId
   * @returns {null | { isVip: boolean, vipRank: number, vipTitle: string, vipType: string }}
   */
  getVipInfo(playerId) {
    const stats = this.playerStats.get(playerId);
    if (!stats) return null;

    this._ensurePeriodFields(stats);

    // 计算周榜排名
    let weeklyRank = 0;
    if (stats.weeklyScore > 0) {
      let higher = 0;
      for (const [, s] of this.playerStats) {
        this._ensurePeriodFields(s);
        if (s.weeklyScore > stats.weeklyScore) higher++;
      }
      weeklyRank = higher + 1; // 1-based
    }

    // 计算月榜排名
    let monthlyRank = 0;
    if (stats.monthlyScore > 0) {
      let higher = 0;
      for (const [, s] of this.playerStats) {
        this._ensurePeriodFields(s);
        if (s.monthlyScore > stats.monthlyScore) higher++;
      }
      monthlyRank = higher + 1;
    }

    // 取最佳排名（周榜或月榜，哪个排名更高用哪个）
    let vipRank = 0;
    let vipTitle = '';
    let vipType = '';

    if (weeklyRank > 0 && weeklyRank <= 20) {
      vipRank = weeklyRank;
      vipTitle = `周榜第${weeklyRank}名`;
      vipType = 'weekly';
    }
    if (monthlyRank > 0 && monthlyRank <= 20 &&
        (vipRank === 0 || monthlyRank < vipRank)) {
      vipRank = monthlyRank;
      vipTitle = `月榜第${monthlyRank}名`;
      vipType = 'monthly';
    }

    if (vipRank === 0) return null;
    return { isVip: true, vipRank, vipTitle, vipType };
  }

  /**
   * 获取当前参与玩家的完整数据面板
   * 包含每个玩家在各榜单的排名位置 + 当局贡献 + 连胜数据
   * @returns {{ players: Array, totalCount: number }}
   */
  getPlayerDataPanel() {
    const allStats = Array.from(this.playerStats.entries()); // [[playerId, stats], ...]

    // 确保所有记录的周期字段是最新的
    for (const [, s] of allStats) {
      this._ensurePeriodFields(s);
    }

    // 预排序各榜单，构建 playerId→rank 映射（一次排序，多次O(1)查找）
    const weeklyRankMap = new Map();
    [...allStats]
      .filter(([, s]) => s.weeklyScore > 0)
      .sort((a, b) => b[1].weeklyScore - a[1].weeklyScore)
      .forEach(([id], i) => weeklyRankMap.set(id, i + 1));

    const monthlyRankMap = new Map();
    [...allStats]
      .filter(([, s]) => s.monthlyScore > 0)
      .sort((a, b) => b[1].monthlyScore - a[1].monthlyScore)
      .forEach(([id], i) => monthlyRankMap.set(id, i + 1));

    const streakRankMap = new Map();
    [...allStats]
      .filter(([, s]) => (s.currentStreak || 0) > 0)
      .sort((a, b) => (b[1].currentStreak || 0) - (a[1].currentStreak || 0))
      .forEach(([id], i) => streakRankMap.set(id, i + 1));

    const oneHourAgo = Date.now() - 3600000;
    const hourlyRankMap = new Map();
    [...allStats]
      .filter(([, s]) => s.lastActive > oneHourAgo)
      .sort((a, b) => b[1].totalScore - a[1].totalScore)
      .forEach(([id], i) => hourlyRankMap.set(id, i + 1));

    // 遍历当局玩家，组装完整数据
    const result = [];
    for (const p of this.players.values()) {
      const stats = this.playerStats.get(p.id);
      if (!stats) {
        // 无历史数据的新玩家
        result.push({
          playerId: p.id,
          playerName: p.name,
          avatarUrl: p.avatarUrl || '',
          camp: p.camp,
          contribution: Math.round(p.contribution),
          totalForce: Math.round(p.totalForce || 0),
          weeklyRank: 0, monthlyRank: 0, streakRank: 0, hourlyRank: 0,
          currentStreak: 0, weeklyScore: 0, monthlyScore: 0
        });
        continue;
      }

      this._ensurePeriodFields(stats);

      result.push({
        playerId: p.id,
        playerName: p.name,
        avatarUrl: stats.avatarUrl || '',
        camp: p.camp,
        contribution: Math.round(p.contribution),
        totalForce: Math.round(p.totalForce || 0),
        weeklyRank: weeklyRankMap.get(p.id) || 0,
        monthlyRank: monthlyRankMap.get(p.id) || 0,
        streakRank: streakRankMap.get(p.id) || 0,
        hourlyRank: hourlyRankMap.get(p.id) || 0,
        currentStreak: stats.currentStreak || 0,
        weeklyScore: Math.round(stats.weeklyScore || 0),
        monthlyScore: Math.round(stats.monthlyScore || 0)
      });
    }

    // 按贡献值降序排列
    result.sort((a, b) => b.contribution - a.contribution);

    return { players: result, totalCount: this.players.size };
  }
}

module.exports = PlayerManager;
