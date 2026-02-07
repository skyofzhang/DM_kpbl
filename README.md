# 卡皮巴拉对决 (Capybara Duel)

抖音直播弹幕互动游戏 - 基于羊羊对决玩法的卡皮巴拉主题换皮项目

## 项目信息

| 项目 | 信息 |
|------|------|
| 项目代号 | CapybaraDuel / KPBL |
| 平台 | 抖音直播 |
| 架构 | Node.js Server + Unity Client |
| Unity版本 | 2022.3.47f1c1 LTS |
| 渲染管线 | Built-In Render Pipeline (3D) |

## 目录结构

```
DM_kpbl/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/           # GameManager, NetworkManager
│   │   ├── Entity/         # PlayerData, Capybara, Camp
│   │   ├── Systems/        # OrangeController, CapybaraSpawner
│   │   ├── UI/             # UI管理
│   │   └── Config/         # GameConfig
│   ├── Models/             # 3D模型 (羊羊对决资源)
│   ├── Prefabs/            # 预制体
│   ├── Scenes/             # 场景
│   └── Resources/          # 动态加载资源
├── Server/
│   ├── src/                # Node.js 源码
│   └── config/             # 服务端配置
└── Docs/                   # 文档
```

## 核心玩法

1. **弹幕加入**: 观众发送 "1/左" 或 "2/右" 加入阵营
2. **礼物加速**:
   - 泡澡小黄鸭(0.1抖币): 升级系统，提升基础推力
   - 柚子(10抖币): 召唤奔跑水豚
   - 更高级礼物: 召唤更强单位
3. **推动橘子**: 双方推力差决定橘子移动方向
4. **获胜条件**: 橘子到达对方终点或30分钟后偏向方获胜

## 快速开始

```bash
# 克隆仓库
git clone https://github.com/skyofzhang/KPBL.git
cd KPBL

# 启动服务端
cd Server
npm install
npm run dev

# Unity打开 Assets 目录
```

## 知识库

详见 Notion 弹幕游戏知识库 (12层架构)

## 许可证

私有项目 - 仅限授权成员
