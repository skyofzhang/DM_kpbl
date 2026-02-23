/**
 * 重置所有玩家的streakPoints为0（清除旧算法的污染数据）
 * 保留 currentStreak 和 bestStreak（这些是有效的）
 *
 * 用法: node reset_sp.js [room_dir]
 * 默认: /opt/dm_kpbl/data/rooms/default/
 */
const fs = require('fs');
const path = require('path');

const roomDir = process.argv[2] || '/opt/dm_kpbl/data/rooms/default';
const statsFile = path.join(roomDir, 'playerStats.json');

console.log(`Reading: ${statsFile}`);
const data = JSON.parse(fs.readFileSync(statsFile, 'utf8'));

let resetCount = 0;
for (const [pid, stats] of Object.entries(data)) {
  if (stats.streakPoints && stats.streakPoints !== 0) {
    console.log(`  ${pid}: streakPoints ${stats.streakPoints} → 0`);
    stats.streakPoints = 0;
    resetCount++;
  }
}

console.log(`\nReset ${resetCount} players' streakPoints to 0`);

// Write back
fs.writeFileSync(statsFile, JSON.stringify(data, null, 2), 'utf8');
console.log('Saved.');
