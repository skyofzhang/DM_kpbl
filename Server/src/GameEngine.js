/**
 * 游戏引擎模块
 * 管理游戏状态机、计时器、推力计算、胜负判定
 *
 * 核心设计：橘子用【速度驱动】而非【位置映射】
 * - 推力比决定速度方向和大小
 * - 速度用 sigmoid 非线性映射，有硬上限
 * - 每tick最大位移限制，保证画面舒适自然
 * - 画面第一原则：即使碾压性领先也是平滑加速，不会瞬移
 */

class GameEngine {
  constructor(config, broadcast, playerManager) {
    this.config = config;
    this.broadcast = broadcast;
    this.playerManager = playerManager;

    // 状态: idle → waiting → countdown → running → settlement → idle
    this.state = 'idle';
    this.leftForce = 0;
    this.rightForce = 0;
    this.orangePos = 0;           // -100 ~ +100
    this.remainingTime = config.matchDuration || 3600;
    this.matchDuration = config.matchDuration || 3600;

    this.timer = null;
    this.countdownTimer = null;
    this.settlementTimer = null;

    // === 橘子运动参数 ===
    this.winThreshold = 100;

    // 速度驱动参数（每200ms为1tick）
    // 最大速度: 0.21单位/tick → 到终点最少 100/0.21/5 ≈ 95秒（碾压局）
    // 最小可感知速度: 0.018单位/tick → 到终点需 100/0.018/5 ≈ 1111秒（势均力敌）
    this.maxSpeedPerTick = 0.21;     // 每tick最大位移（硬上限）— 从0.3再降30%
    this.minSpeedPerTick = 0.018;    // 最小可感知速度 — 从0.025再降30%
    this.speedSmoothFactor = 0.15;   // 速度变化平滑（0~1，越小越平滑）

    // 当前速度（平滑过渡用）
    this.currentSpeed = 0;

    // === 临时推力系统 ===
    // 存储带时限的推力条目: { camp, value, expireAt }
    // 点赞和666推力升级都是临时推力，到期后自动扣除
    this.tempForces = [];
  }

  // 获取当前状态
  getState() {
    return {
      state: this.state,
      leftForce: Math.round(this.leftForce),
      rightForce: Math.round(this.rightForce),
      orangePos: Math.round(this.orangePos * 100) / 100,
      remainingTime: Math.round(this.remainingTime)
    };
  }

  // 开始游戏
  startGame() {
    // 如果处于结算状态，先自动重置
    if (this.state === 'settlement') {
      console.log('[GameEngine] Auto-reset from settlement state');
      this.reset();
    }
    if (this.state !== 'idle' && this.state !== 'waiting') return;

    this.state = 'countdown';
    this.broadcast({ type: 'game_state', timestamp: Date.now(), data: { ...this.getState(), state: 'countdown' } });

    // 3秒倒计时
    let count = 3;
    this.countdownTimer = setInterval(() => {
      this.broadcast({ type: 'countdown', timestamp: Date.now(), data: { remainingTime: count } });
      count--;
      if (count < 0) {
        clearInterval(this.countdownTimer);
        this._enterRunning();
      }
    }, 1000);
  }

  _enterRunning() {
    this.state = 'running';
    this.remainingTime = this.matchDuration;
    this.leftForce = 0;
    this.rightForce = 0;
    this.orangePos = 0;
    this.currentSpeed = 0;
    this.tempForces = [];

    this.broadcast({ type: 'game_state', timestamp: Date.now(), data: this.getState() });

    // 每200ms tick一次（高频同步位置，让客户端更平滑）
    this._tickCounter = 0;
    this.timer = setInterval(() => {
      this._tick();
    }, 200);
  }

  /**
   * 计算目标速度（非线性映射）
   * 输入：推力比 ratio（-1 ~ +1，正=左方领先）
   * 输出：目标速度（-maxSpeed ~ +maxSpeed）
   *
   * 使用 sigmoid 映射：
   * - ratio接近0时 → 速度很小（势均力敌，拉锯）
   * - ratio越大 → 速度越大，但收敛到上限（碾压也不会瞬移）
   * - 中间有一个"加速带"让优势方逐渐拉开
   */
  _calcTargetSpeed() {
    const total = this.leftForce + this.rightForce;
    if (total < 10) return 0; // 双方都没推力时不动

    // 推力比：-1（右方碾压）~ +1（左方碾压）
    const diff = this.leftForce - this.rightForce;
    const ratio = diff / total; // -1 ~ +1

    // 死区：推力比极小 且 绝对差值也小时才认为势均力敌
    const absDiff = Math.abs(diff);
    if (Math.abs(ratio) < 0.02 && absDiff < 100) return 0;

    // sigmoid 映射：ratio → speed
    // 使用 tanh 作为 sigmoid，自然映射到 [-1, +1]
    // steepness 控制加速曲线的陡峭程度
    const steepness = 3.0;
    const normalizedSpeed = Math.tanh(ratio * steepness);

    // 映射到实际速度范围
    let absSpeed = Math.abs(normalizedSpeed) * this.maxSpeedPerTick;
    const direction = Math.sign(normalizedSpeed);

    // 保底最小速度：只要有推力差就要动，差值越大保底越高
    // 解决高推力拉锯时(如5万vs5万+1000)橘子卡住不动的问题
    if (absDiff > 0) {
      // 基于绝对差值的保底速度（对数缩放，避免线性爆炸）
      // absDiff=100 → 0.003, absDiff=1000 → 0.006, absDiff=10000 → 0.008
      const floorSpeed = Math.min(this.minSpeedPerTick * 0.5, Math.log10(absDiff + 1) * 0.002);
      absSpeed = Math.max(absSpeed, floorSpeed);
    }

    return direction * absSpeed;
  }

  _tick() {
    if (this.state !== 'running') return;

    this._tickCounter++;

    // 每5次tick（即每秒, 200ms*5=1s）减少1秒倒计时
    if (this._tickCounter % 5 === 0) {
      this.remainingTime -= 1;

      // 广播倒计时（最后30秒每秒广播）
      if (this.remainingTime <= 30) {
        this.broadcast({ type: 'countdown', timestamp: Date.now(), data: { remainingTime: this.remainingTime } });
      }

      // 时间到 → 结算（先看橘子位置，中线时比推力，均等则平局）
      if (this.remainingTime <= 0) {
        let winner;
        if (this.orangePos > 0.01) {
          winner = 'left';   // 橘子偏向右侧终点 = 左方推得更远
        } else if (this.orangePos < -0.01) {
          winner = 'right';  // 橘子偏向左侧终点 = 右方推得更远
        } else {
          // 橘子在中线附近，比较总推力
          winner = this.leftForce > this.rightForce ? 'left' :
                   this.rightForce > this.leftForce ? 'right' : 'draw';
        }
        this._endGame(winner, 'timeout');
        return;
      }
    }

    // === 临时推力过期检查 ===
    this._expireTempForces();

    // === 速度驱动位移 ===
    const targetSpeed = this._calcTargetSpeed();

    // 平滑过渡到目标速度（不会突变）
    this.currentSpeed += (targetSpeed - this.currentSpeed) * this.speedSmoothFactor;

    // 硬上限保护
    this.currentSpeed = Math.max(-this.maxSpeedPerTick, Math.min(this.maxSpeedPerTick, this.currentSpeed));

    // 应用位移
    this.orangePos += this.currentSpeed;
    this.orangePos = Math.max(-100, Math.min(100, this.orangePos));

    // 检查是否到达端点
    if (this.orangePos >= this.winThreshold) {
      this._endGame('left', 'reached_end');
      return;
    } else if (this.orangePos <= -this.winThreshold) {
      this._endGame('right', 'reached_end');
      return;
    }

    // 每次tick都广播位置+剩余时间
    this.broadcast({
      type: 'force_update',
      timestamp: Date.now(),
      data: {
        leftForce: Math.round(this.leftForce),
        rightForce: Math.round(this.rightForce),
        orangePos: Math.round(this.orangePos * 100) / 100,
        remainingTime: Math.round(this.remainingTime)
      }
    });
  }

  // 添加永久推力（来自礼物或加入阵营，当局不衰减）
  addForce(camp, value) {
    if (this.state !== 'running') return;

    if (camp === 'left') {
      this.leftForce += value;
    } else {
      this.rightForce += value;
    }

    // 不再瞬间改变 orangePos！
    // 位置变化完全由 _tick() 中的速度驱动系统控制
    // 只广播推力变化（让UI立即更新推力数字）
    this.broadcast({
      type: 'force_update',
      timestamp: Date.now(),
      data: {
        leftForce: Math.round(this.leftForce),
        rightForce: Math.round(this.rightForce),
        orangePos: Math.round(this.orangePos * 100) / 100,
        remainingTime: Math.round(this.remainingTime)
      }
    });
  }

  /**
   * 添加临时推力（持续duration秒后自动衰减）
   * 用于：点赞(2推力/赞, 3秒)、推力升级666(3推力, 5秒)
   */
  addTempForce(camp, value, durationSec) {
    if (this.state !== 'running') return;

    // 立即加上推力
    if (camp === 'left') {
      this.leftForce += value;
    } else {
      this.rightForce += value;
    }

    // 记录到期时间，到期后在tick中自动扣除
    this.tempForces.push({
      camp,
      value,
      expireAt: Date.now() + durationSec * 1000
    });

    // 广播推力变化
    this.broadcast({
      type: 'force_update',
      timestamp: Date.now(),
      data: {
        leftForce: Math.round(this.leftForce),
        rightForce: Math.round(this.rightForce),
        orangePos: Math.round(this.orangePos * 100) / 100,
        remainingTime: Math.round(this.remainingTime)
      }
    });
  }

  /**
   * 清理过期的临时推力，从总推力中扣除
   */
  _expireTempForces() {
    if (this.tempForces.length === 0) return;

    const now = Date.now();
    const remaining = [];

    for (const tf of this.tempForces) {
      if (now >= tf.expireAt) {
        // 到期，扣除推力（不低于0）
        if (tf.camp === 'left') {
          this.leftForce = Math.max(0, this.leftForce - tf.value);
        } else {
          this.rightForce = Math.max(0, this.rightForce - tf.value);
        }
      } else {
        remaining.push(tf);
      }
    }

    this.tempForces = remaining;
  }

  _endGame(winner, reason) {
    if (this.state === 'settlement') return;
    this.state = 'settlement';

    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }

    // 先计算连胜变化（写入每个玩家的_streakBet/_streakGain），再生成结算数据
    if (this.playerManager) {
      this.playerManager.calculateStreakChanges(winner);
    }

    // 生成完整结算数据（此时连胜字段已就绪）
    const settlementData = this.playerManager
      ? this.playerManager.buildSettlementData(winner, reason, this.leftForce, this.rightForce)
      : { winner, reason, leftForce: Math.round(this.leftForce), rightForce: Math.round(this.rightForce) };

    this.broadcast({
      type: 'game_ended',
      timestamp: Date.now(),
      data: settlementData
    });

    // 存储本局结算到历史排行（SP已计算完毕，只做存档）
    if (this.playerManager) {
      this.playerManager.saveMatchHistory(settlementData);
    }

    // 不自动重置，等客户端发送 reset_game 指令
    // （让用户有足够时间查看结算数据）
  }

  // 暂停游戏（保留状态，停止定时器）
  pause() {
    if (this.state !== 'running') return;

    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
    // state保持'running'，恢复时继续tick
    console.log(`[GameEngine] Paused (remaining: ${this.remainingTime}s, forces: L=${Math.round(this.leftForce)} R=${Math.round(this.rightForce)}, pos=${this.orangePos.toFixed(2)})`);
  }

  // 恢复游戏（从暂停状态恢复tick）
  resume() {
    if (this.state !== 'running') return;
    if (this.timer) return; // 已经在运行

    this._tickCounter = 0;
    this.timer = setInterval(() => {
      this._tick();
    }, 200);

    this.broadcast({ type: 'game_state', timestamp: Date.now(), data: this.getState() });
    console.log(`[GameEngine] Resumed (remaining: ${this.remainingTime}s)`);
  }

  // 重置游戏
  reset() {
    if (this.timer) clearInterval(this.timer);
    if (this.countdownTimer) clearInterval(this.countdownTimer);
    if (this.settlementTimer) clearTimeout(this.settlementTimer);

    this.state = 'idle';
    this.leftForce = 0;
    this.rightForce = 0;
    this.orangePos = 0;
    this.currentSpeed = 0;
    this.remainingTime = this.matchDuration;
    this.tempForces = [];

    this.broadcast({ type: 'game_state', timestamp: Date.now(), data: this.getState() });
  }
}

module.exports = GameEngine;
