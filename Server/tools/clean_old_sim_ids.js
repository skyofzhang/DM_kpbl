/**
 * 清洗旧格式的模拟器玩家数据
 * 旧格式: sim_1, sim_2, ... sim_66（无零填充）
 * 新格式: sim_001, sim_002, ... sim_100（3位零填充）
 *
 * 操作：删除所有旧格式sim_X数据，保留新格式sim_XXX和真实玩家数据
 *
 * 用法: node clean_old_sim_ids.js [room_dir]
 */
const fs = require('fs');
const path = require('path');

const roomDir = process.argv[2] || '/opt/dm_kpbl/data/rooms/default';
const statsFile = path.join(roomDir, 'playerStats.json');

console.log(`Reading: ${statsFile}`);
const data = JSON.parse(fs.readFileSync(statsFile, 'utf8'));

const oldIds = [];
const newIds = [];
const realIds = [];

for (const pid of Object.keys(data)) {
  // 旧格式: sim_1 ~ sim_99 (1-2位数字，无零填充)
  if (/^sim_\d{1,2}$/.test(pid)) {
    oldIds.push(pid);
  }
  // 新格式: sim_001 ~ sim_100 (3位零填充)
  else if (/^sim_\d{3}$/.test(pid)) {
    newIds.push(pid);
  }
  // 真实玩家
  else {
    realIds.push(pid);
  }
}

console.log(`\n统计:`);
console.log(`  旧格式 sim_X (待删除): ${oldIds.length} 个`);
console.log(`  新格式 sim_XXX (保留):  ${newIds.length} 个`);
console.log(`  真实玩家 (保留):        ${realIds.length} 个`);

// 删除旧格式
for (const pid of oldIds) {
  delete data[pid];
}

console.log(`\n已删除 ${oldIds.length} 个旧格式ID`);

// 同时重置所有新格式的数据为干净状态
let resetCount = 0;
for (const pid of newIds) {
  const stats = data[pid];
  if (stats.streakPoints > 0 || stats.currentStreak > 0) {
    stats.streakPoints = 0;
    stats.currentStreak = 0;
    resetCount++;
  }
}
console.log(`重置 ${resetCount} 个新格式假人的SP和连胜`);

// 写回
fs.writeFileSync(statsFile, JSON.stringify(data, null, 2), 'utf8');
console.log(`\n保存完成。剩余 ${Object.keys(data).length} 条记录`);
