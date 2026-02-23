/**
 * 礼物配置模块
 * 定义所有礼物类型、推力值、是否触发召唤
 */

const GIFTS = {
  // 仙女棒 - 升级型，增加基础推力（永久提升）
  fairy_wand: {
    id: 'fairy_wand',
    name: '仙女棒',
    price: 0.1,            // 1抖币 = 0.1元
    type: 'upgrade',       // upgrade = 增强自身推力
    forceValue: 10,        // 基础推力加成（截图显示+10）
    isSummon: false,
    unitId: null,
    duration: 0,
    tier: '1'              // 数字tier，客户端用于选择动画和通知样式
  },
  // 能力药丸 - 召唤奔跑水豚（普通单位）
  ability_pill: {
    id: 'ability_pill',
    name: '能力药丸',
    price: 10,
    type: 'summon',
    forceValue: 343,       // 召唤奔跑水豚（推力343）
    isSummon: true,
    unitId: '3111_RunCapy',
    duration: 60,
    tier: '2'
  },
  // 甜甜圈 - 召唤甜甜圈战士（稀有单位）
  donut: {
    id: 'donut',
    name: '甜甜圈',
    price: 52,
    type: 'summon',
    forceValue: 808,       // 召唤甜甜圈战士（推力808）
    isSummon: true,
    unitId: '3131_DonutWarrior',
    duration: 120,
    tier: '3'
  },
  // 能量电池 - 召唤电池骑士（稀有单位）
  battery: {
    id: 'battery',
    name: '能量电池',
    price: 99,
    type: 'summon',
    forceValue: 1415,      // 召唤电池骑士（推力1415）
    isSummon: true,
    unitId: '3151_BatteryKnight',
    duration: 180,
    tier: '4'
  },
  // 爱的爆炸 - 召唤水豚之神（传说单位）
  love_blast: {
    id: 'love_blast',
    name: '爱的爆炸',
    price: 199,
    type: 'summon',
    forceValue: 2679,      // 召唤水豚之神（推力2679）
    isSummon: true,
    unitId: '3161_CapyGod',
    duration: 240,
    tier: '5'
  },
  // 神秘空投 - 召唤宇宙豚王（传说单位）
  mystery_drop: {
    id: 'mystery_drop',
    name: '神秘空投',
    price: 520,
    type: 'summon',
    forceValue: 6988,      // 召唤宇宙豚王（推力6988）
    isSummon: true,
    unitId: '3171_WildRanger',
    duration: 300,
    tier: '6'
  }
};

// 按价格排序的礼物ID列表（用于随机选择）
const GIFT_IDS = Object.keys(GIFTS);

function getGift(giftId) {
  return GIFTS[giftId] || null;
}

function getAllGifts() {
  return GIFTS;
}

function getRandomGiftId() {
  // 权重：便宜礼物出现概率更高
  const weights = {
    fairy_wand: 40,
    ability_pill: 30,
    donut: 15,
    battery: 8,
    love_blast: 5,
    mystery_drop: 2
  };
  const totalWeight = Object.values(weights).reduce((a, b) => a + b, 0);
  let rand = Math.random() * totalWeight;
  for (const [id, weight] of Object.entries(weights)) {
    rand -= weight;
    if (rand <= 0) return id;
  }
  return 'fairy_wand';
}

module.exports = { GIFTS, GIFT_IDS, getGift, getAllGifts, getRandomGiftId };
