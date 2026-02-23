# 卡皮巴拉对决 (Capybara Duel) — DM_kpbl

## ⚡ 新会话启动指令（必须优先执行）
> 新对话框打开后，在做任何任务之前，必须按顺序执行以下步骤：
> 1. 读取全局记忆: `C:\Users\Administrator\.claude\projects\D--claude\memory\MEMORY.md`
> 2. 读取技术状态: `C:\Users\Administrator\.claude\projects\D--claude\memory\dm_kpbl_current_state.md`
> 3. 读取 Coplay 指南: `D:\claude\DM_kpbl\docs\claude_unity_coplay_guide.md`
> 4. 确认 Coplay MCP 可用（调用 `get_unity_editor_state`）
> 5. 编译检查（`check_compile_errors`）
> 6. 截图自检（`capture_scene_object`）
> 7. 向用户汇报当前状态 + 待完成列表，等待指示

## 项目概述
抖音直播间互动游戏：两队观众通过刷礼物推动大橘子，橘子到边即分胜负。
- Unity 2022.3.47f1c1 + URP
- Node.js WebSocket 服务器
- 目标平台：抖音直播互动插件

## 架构

### Unity 客户端
```
Assets/Scripts/
├── Core/
│   ├── GameManager.cs       — 全局状态机 (Idle→Countdown→Running→Settlement)
│   ├── NetworkManager.cs    — WebSocket 通信
│   └── MessageProtocol.cs   — 消息协议定义
├── Systems/
│   ├── ForceSystem.cs       — 推力计算 + orangePos 管理
│   ├── OrangeController.cs  — 橘子位置/旋转/到边检测
│   ├── CapybaraSpawner.cs   — 卡皮巴拉生成管理
│   ├── CampSystem.cs        — 阵营管理
│   ├── GiftHandler.cs       — 礼物处理
│   ├── BarrageSimulator.cs  — 本地模拟器（仅Editor/开发版）
│   ├── AudioManager.cs      — 音效管理器（空壳待填充）
│   ├── RankingSystem.cs     — 排行系统
│   └── OrangeFollowCamera.cs
├── UI/
│   ├── UIManager.cs         — UI 面板切换
│   ├── TopBarUI.cs          — 顶栏：推力/拉力条/计时器/积分池/距离指示
│   ├── SettlementUI.cs      — 结算面板
│   ├── MainMenuUI.cs        — 主菜单
│   ├── AnnouncementUI.cs    — 全屏公告（比赛开始/胜利等）
│   ├── GameControlUI.cs     — 调试控制栏
│   ├── PlayerListUI.cs      — 玩家列表
│   ├── RankingPanelUI.cs    — 排行榜面板
│   ├── PlayerJoinNotificationUI.cs
│   ├── GiftNotificationUI.cs
│   └── VIPAnnouncementUI.cs
├── Entity/
│   ├── Capybara.cs          — 卡皮巴拉实体
│   ├── PlayerData.cs        — 玩家数据
│   └── Camp.cs              — 阵营定义
├── Config/
│   ├── GameConfig.cs        — ScriptableObject 配置
│   └── CharacterDatabase.cs — 角色数据库
└── VFX/
    ├── OrangeSpeedHUD.cs      — 橘子头顶速度HUD（场景预创建，运行时只更新文本）
    ├── OrangeDustTrail.cs     — 橘子移动烟尘尾迹
    ├── CapybaraCampEffect.cs  — 阵营菲涅尔边缘光
    ├── FootDustManager.cs     — 脚底尘埃管理器（单例）
    ├── UnitVisualEffect.cs    — 单位缩放+发光+颜色分级
    ├── VFXSpawner.cs          — 粒子效果生成
    └── CameraShake.cs         — 摄像机抖动
```

### Node.js 服务器（多房间架构 2026-02-12）
```
server/src/
├── index.js             — 入口，多房间WebSocket路由 + 抖音HTTP回调 + 向后兼容
├── Room.js              — 独立游戏房间(GameEngine+PlayerManager+BarrageSimulator)
├── RoomManager.js       — 房间生命周期(创建/查找/暂停/销毁)
├── DouyinAPI.js         — 抖音开放平台(access_token/任务管理/推送解析/签名验证)
├── GameEngine.js        — 游戏引擎：状态机/推力/橘子位置 + pause/resume
├── PlayerManager.js     — 玩家管理 + 按房间隔离数据持久化
├── BarrageSimulator.js  — 服务端弹幕模拟（轮流送礼模式）
└── GiftConfig.js        — 礼物配置（6种礼物）
```

## 远程服务器
- IP: `212.64.26.65`
- 代码路径: `/opt/dm_kpbl/src/`
- 配置: `/opt/dm_kpbl/config/default.json` (wsPort: 8081, matchDuration: 600, room配置)
- 数据: `/opt/dm_kpbl/data/rooms/{roomId}/` (match_history + player_stats，**不可丢失**）
- PM2 进程: `dm-kpbl-server`
- 部署: `scp → pm2 restart dm-kpbl-server`

## 关键设计决策

### 橘子控制器 (OrangeController) — 2026-02-12更新
- **重量感移动**: `Mathf.SmoothDamp` (smoothTime=2.0, **maxSpeed=2**, **maxAcceleration=1.5**)
- **旋转系统**: 自转(spin) + 倾斜(tilt) 双系统
  - 自转: baseSpinSpeed=20°/s, 移动时 ×8 倍
  - 倾斜: maxTiltAngle=40°, 仅移动时倾斜
  - 组合: `_baseRotation * tilt * spin`
- **必须保存 _baseRotation**: Awake() 中存储 FBX 原始旋转，否则模型会陷入地面
- 位置范围: -45 ~ +45, 到边阈值 2%
- **纯服务器跟随**: serverFollowSpeed=4（已移除客户端预测）
- **推力晃动**: wobbleAmplitude=0.00007, wobbleFrequency=0.8Hz
- ⚠️ **代码默认值必须与场景值一致**：wobbleMaxAmplitude代码默认曾为0.08导致场景值丢失回退。已修正为0.00007
- ⚠️ **场景保存**: 用 `Assets/Editor/SaveCurrentScene.cs` 而非 Coplay save_scene（后者会另存到错误路径）

### UI 事件订阅
- **禁止在 Awake() 中 SetActive(false)**: 会阻止 OnEnable() 执行，导致事件永远不订阅
- 正确做法: 用 CanvasGroup.alpha=0 隐藏，保持 GameObject active
- SettlementUI 有时序问题: OnGameEnded 在面板激活前触发，需要在 OnEnable 中回补 LastEndedData

### OrangeSpeedHUD（场景化 2026-02-12）
- **场景预创建**: OrangeSpeedHUD_Root (World Space Canvas) + BG (Image) + MainText (TMP)
- **运行时职责**: 仅更新文本+跟随橘子位置+Billboard面向摄像机
- **跟随机制**: Start()记录_initialOffset，LateUpdate用偏移跟随（尊重用户Inspector调整）
- **显示**: `{speed:F2} 米/秒` + 方向箭头(>>/<<) + 数字滚动 + 呼吸脉冲
- **字体**: LiberationSans SDF + 运行时Chinese Font fallback
- **集成**: SceneGenerator.CreateOrUpdateSpeedHUD() + SceneUpdater(仅null时创建)

### 距离显示 (TopBarUI)
- 左右终点标记：← 柚子终点 XX.Xm / XX.Xm 香橙终点 →
- 中心标记：当前偏移位置
- posIndicatorText：始终可见，显示最近终点距离+方向箭头
- GOAL_DIST = 45（⚠️ 当前TopBarUI中仍是30，待Task5修复为45）
- **倒计时 (2026-02-12修复)**: force_update 每200ms附带 remainingTime，客户端实时更新

### ⭐ GameUIPanel 战斗界面重构 (2026-02-13 规划)
- **任务文档**: `docs/task_gameui_redesign.md`（完整梳理、分阶段计划）
- **策划案**: `美术界面资源/主界面/whiteboard_exported_image.pdf` + `whiteboard_top.png` / `whiteboard_bottom.png`
- **切换脚本**: `Assets/Editor/TogglePanel.cs`（显示GameUIPanel） / `RestorePanel.cs`（恢复MainMenuPanel）
- **修改范围**:
  - Phase 1: 美术表现层（文字格式/位置/样式）— 低风险
  - Phase 2: 角力进度条重写（核心，双向挤压血条）— 高风险
  - Phase 3: 玩家面板布局重构（圆形头像+紧凑横排）— 高风险
  - Phase 4: 新增模块（结束/设置按钮、提示文字、连胜）— 中风险
  - Phase 5: 底部礼物区 + 贴纸功能 — 中风险
- **不动的系统**: PlayerJoinNotificationUI / GiftNotificationUI / GiftAnimationUI / VIPAnnouncementUI
- **核心修改点**: TopBarUI.cs（进度条渲染）、PlayerListUI.cs（布局重构）
- **数据层不动**: ForceSystem.cs 数据流保持不变，只改UI渲染

### 结算流程
- 到边 → 胜利公告(4s + 0.5s淡出) → 延迟5s进入结算面板
- 结算面板需要: MVP + 双列10行排行 + 积分瓜分

### 服务器状态机
- idle → countdown(3s) → running → settlement → idle
- 已修复: settlement 状态下收到 start_game 会自动 reset

## 数据持久化 ⚠️ 关键
- 玩家花费真金白银，服务器重启**绝不能丢失数据**
- `/opt/dm_kpbl/data/` 下的文件是核心资产
- 部署前务必确认数据文件不受影响

## 参考资料
- 羊羊对决分析报告: `D:\claude\yangyang_report.md`（从 Notion 提取）
- UI 坐标快照: `docs/ui_layout_snapshot.md`（手动调整后的坐标记录）
- **抖音接入指南**: `docs/douyin_integration_guide.md`（签名算法/API/踩坑/上架checklist，避免反复查官方文档）
- **⭐ Claude+Unity+Coplay指南**: `docs/claude_unity_coplay_guide.md`（MCP配置/工具列表/自检流程/项目规则）

## 反推弹开系统 (Capybara.cs)
- `_bounceVelX`: 每个单位的X方向弹开冲量
- 当 boundary 推动单位（pos被截断）时，产生 `bounceImpulse` 反向冲量
- 冲量通过 ApplySeparation 向后排传递（前排→后排，衰减70%）
- `bounceFriction` 控制冲量衰减速度
- `urgency` 因子：空间不足时 retreatLerpSpeed 最多 ×3

## GIF 礼物动画系统
- GIF 不能直接用于 Unity，需要转为 PNG 帧序列
- **转换工具**: `Assets/Editor/GifToSpriteConverter.cs` (菜单 Tools > Convert GIFs to Sprites)
- **备选方案**: 用 ezgif.com 手动拆帧
- **GIF 输入**: `Assets/Art/GiftGifs/tier1.gif ~ tier6.gif`
- **帧序列输出**: `Assets/Resources/GiftAnimations/tier1/ ~ tier6/` (frame_000.png...)
- **播放器**: `Assets/Scripts/UI/GiftAnimationUI.cs`
- **6个tier礼物映射**（2026-02-11更新）：
  - Tier1: 仙女棒（0.1抖币，推力10）- 永久提升基础推力
  - Tier2: 能力药丸（10抖币，推力343）- 召唤奔跑水豚
  - Tier3: 甜甜圈（52抖币，推力808）- 召唤甜甜圈战士
  - Tier4: 能量电池（99抖币，推力1415）- 召唤电池骑士
  - Tier5: 爱的爆炸（199抖币，推力2679）- 召唤水豚之神
  - Tier6: 神秘空投（520抖币，推力6988）- 召唤宇宙豚王
- 不做队列，同时显示，最多5个同屏
- 没有帧资源时显示 fallback 占位动画（色块+文字+缩放弹入）

## 橘子晃动系统
- wobbleAmplitude=0.00007, wobbleFrequency=0.8Hz, wobbleForceThreshold=100

## 物理碰撞移除
**问题**：PrefabGenerator 给橘子和卡皮巴拉都添加了 Collider，导致单位无法穿过橘子，形成弧形堆积。
**解决**：注释掉 `PrefabGenerator.cs` 中的 Collider 创建代码
- 游戏逻辑是代码直接控制 `transform.position`，不是物理引擎驱动
- 单位间距由 separation 系统保证，不需要 Collider
- **重新生成Prefab**: Unity 菜单 `CapybaraDuel/3. Build All Prefabs`

## 待完成 (2026-02-19 更新)
1. 设置面板功能实现（BtnSettings 点击后的面板）
2. AudioManager 音效素材填充
3. 后续美术表现批次（烟尘VFX等）

### 近期已完成里程碑
- ✅ 连胜系统全栈 (#61) + bug修复 (#62) + 胜点SP系统 (#63)
- ✅ 战斗UI重构5阶段 + 8轮视觉反馈迭代 (#32~#60)
- ✅ WebM视频系统 tier1-6 (#59/#63)
- ✅ Notion技术文档/策划案同步 (#58/#63)
- 完整历史见 `MEMORY.md` 的已完成汇总 (#20~#63)

## Coplay MCP 使用
- **配置文件**: `.mcp.json`（项目根目录，已配置 uvx 方式）
- **权限**: 全局 settings.json 已添加 `mcp__coplay-mcp__*`
- **详细指南**: `docs/claude_unity_coplay_guide.md`
- **⚠️ 每次新会话必须**: 确认 Coplay MCP 工具可用 → 编译检查 → 截图自检
- **⚠️ 保存场景**: 用 `CapybaraDuel/Save Current Scene` 菜单，不用 Coplay save_scene
