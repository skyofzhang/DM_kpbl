# 任务：GameUIPanel 战斗界面重构 — 梳理报告

## 参考文件
- 策划案流程图: `D:\claude\DM_kpbl\美术界面资源\主界面\whiteboard_exported_image.pdf`
- 策划案高清拆分: `whiteboard_top.png` / `whiteboard_bottom.png`
- 期望效果图: 用户提供的截图（见对话记录）
- 场景: `Assets/Scenes/MainScene.unity` → `Canvas/GameUIPanel`
- 切换脚本: `Assets/Editor/TogglePanel.cs`

---

## 一、功能已有，只需美术表现层修改（安全，不动代码逻辑）

| 模块 | 当前状态 | 策划案要求 | 改动内容 | 涉及脚本 |
|---|---|---|---|---|
| **推力值文字** | 显示"+XXX 推力" | 显示"推力:9271" + 连胜数 | 修改TMP格式和位置 | TopBarUI.cs 的 leftForceText/rightForceText 文字格式 |
| **距离标记** | "← 柚子终点 XX.Xm" / "XX.Xm 香橙终点 →" | 显示距离数字（如35.98米/82.98米） | 修改TMP文字格式 | TopBarUI.cs 的 leftEndMarker/rightEndMarker |
| **积分池样式** | "积分池 XXXX" 已有万单位 | "积分池:X.XX万" | 微调文字格式 | TopBarUI.cs 的 scorePoolText |
| **倒计时样式** | "MM:SS" 已实现 | 同，但位置可能不同 | 调位置/字号 | TopBarUI.cs 的 timerText |
| **玩家面板布局** | 木质牌匾大面板（底部） | 紧凑横排：圆形头像+编号+名字+推力 | 改UI布局/美术资源，不改数据逻辑 | PlayerListUI.cs 的容器布局 |
| **TopBar整体位置** | 代码强制下移60px | 策划案顶部居中 | 调整EnsureLayout()的偏移参数 | TopBarUI.cs |

**风险等级：低** — 只改显示格式和位置，不动数据流

---

## 二、功能没有，需要新增

| 模块 | 策划案要求 | 复杂度 | 说明 |
|---|---|---|---|
| **结束按钮（左上角）** | 主播可提前结束比赛 | 中 | 需新增UI按钮 + 调用GameManager结束逻辑 |
| **设置按钮（右上角）** | 音效/特效/资源开关 | 中 | 需新增设置面板 + 音效/特效/资源的开关逻辑 |
| **顶部提示文字** | "橙方差9999推力反击" | 低 | 新增TMP，数据可从ForceSystem计算得出 |
| **连胜显示** | 左右各显示连胜数 | 中 | 需服务器提供连胜数据，或从persistent_ranking提取 |
| **底部礼物详细介绍区** | 大面积礼物美术图展示区 | 中 | 新增UI面板 + 美术资源 |
| **贴纸按钮（右下角）** | 点击展开/收起贴纸面板，可拖动 | 高 | 当前只有文字说明，无交互逻辑，需完整开发 |
| **LOGO装饰框** | 中间精美装饰框 | 低 | 替换美术资源 |

**风险等级：中** — 新增模块不影响现有功能，但需要额外美术资源和部分新代码

---

## 三、功能实现逻辑需要修改

| 模块 | 当前逻辑 | 策划案要求 | 改动 | 风险 |
|---|---|---|---|---|
| **⭐ 角力进度条（核心）** | 左右各一个Image.fillAmount，orangePos(-45~+45)线性映射 | 双向箭头动画 >>>橘子<<<，血条互相挤压效果 | 需重写进度条渲染逻辑：从两个独立fill改为双向拉锯动画 | **高** |
| **进度条美术效果** | 纯色fill | 需要箭头动画、进度变化特效、颜色渐变 | 新增动画/特效组件 | 中 |
| **玩家头像显示** | 运行时创建30x30小图，文字中嵌贡献值 | 圆形头像框+独立编号+名字+推力值分离显示 | 重写PlayerListUI的渲染逻辑 | **高** |
| **运行时布局修正** | EnsureLayout()强制下移60px，EnsureCampPanelLayout()移到底部 | 策划案中位置不同 | 修改或移除强制布局代码 | 中 |

**风险等级：高** — 改动核心数据显示逻辑，需仔细验证不破坏现有功能

---

## 四、核心：角力进度条规则和美术效果优化

### 当前实现
```
数据流: 服务器 force_update(每200ms) → ForceSystem.UpdateForce → TopBarUI.HandleForceUpdate
计算: leftFill = Clamp01(orangePos / 45), rightFill = Clamp01(-orangePos / 45)
渲染: progressBarLeft.fillAmount = leftFill (Image Filled模式)
```

### 策划案要求
1. 双方造成互相挤压血条效果（不是两个独立的fill）
2. 血条上有方向箭头动画 >>> <<<
3. 血条进度变化需要平滑过渡（当前可能是跳变的）
4. 积分从进度条中计算（"本局2方阵营的总积分，结算则从这里瓜分"）
5. 6主界面的数字、尺量做滚动

### 建议修改方案
- **保留ForceSystem数据层不动**（数据流正确）
- **重写TopBarUI的进度条渲染部分**：
  - 方案A：改用Slider + 自定义动画（简单）
  - 方案B：改用自定义Shader实现双向挤压效果（效果好但复杂）
  - 方案C：两个Image叠加 + DOTween平滑过渡（推荐，兼顾效果和可维护性）
- **新增箭头动画**：在进度条上方叠加箭头Sprite序列帧

---

## 五、完整事件依赖图（修改时参考）

```
GameManager (中心枢纽)
  ├── OnStateChanged ────→ UIManager (面板切换)
  │                   └──→ AnnouncementUI (比赛开始公告)
  ├── OnCountdownTick ───→ TopBarUI (倒计时)
  ├── OnScorePoolUpdated → TopBarUI (积分池)
  ├── OnGameEnded ───────→ AnnouncementUI + SettlementUI
  └── OnPersistentRankingReceived → RankingPanelUI

ForceSystem
  └── OnForceUpdated ────→ TopBarUI (推力+拉力条+距离) ⭐核心修改点

CampSystem
  ├── OnPlayerJoined ────→ PlayerListUI (人数)
  │                   └──→ PlayerJoinNotificationUI
  └── OnVIPJoined ───────→ VIPAnnouncementUI

RankingSystem
  └── OnRankingsUpdated ─→ PlayerListUI (Top3排行) ⭐需改渲染

GiftHandler
  └── OnGiftReceived ────→ GiftNotificationUI + GiftAnimationUI
```

---

## 六、建议执行顺序

| 阶段 | 内容 | 风险 | 预估 |
|---|---|---|---|
| **Phase 1** | 美术表现层修改（文字格式、位置、样式） | 低 | 1-2小时 |
| **Phase 2** | 角力进度条重写（核心） | 高 | 3-4小时 |
| **Phase 3** | 玩家面板布局重构 | 高 | 2-3小时 |
| **Phase 4** | 新增模块（结束/设置按钮、提示文字、连胜） | 中 | 2-3小时 |
| **Phase 5** | 底部礼物区 + 贴纸功能 | 中 | 3-4小时 |

每个Phase完成后截图验证，确认无regression再进入下一阶段。
