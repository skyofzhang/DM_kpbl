# UI Layout Snapshot - 2026-02-10 (2026-02-13 标注重构区域)
# 用户手动调整后的 UI 坐标记录
# ⚠️ 标注 [重构] 的区域将在 GameUIPanel 战斗界面重构中修改

## GameUIPanel (pos=(0,0) size=(0,0) stretch) ⚠️ [重构中]

### TopBar (pos=(0,-79) size=(0,400)) [重构] Phase 1+2
- TopBarBg: pos=(0,0) size=(0,368) [重构] 位置上移至顶部
- ProgressBarLeft: pos=(34.999973,59) size=(-70.00005,0) [重构] Phase 2 改为双向挤压血条
- ProgressBarRight: pos=(-35,59) size=(-70,0) [重构] Phase 2 改为双向挤压血条
- LeftForceText: pos=(-220,-80) size=(280,55) [重构] Phase 1 改格式为"推力:9271"
- RightForceText: pos=(220,-80) size=(280,55) [重构] Phase 1 改格式为"推力:9271"
- ScorePoolText: pos=(0,-118) size=(300,45) [重构] Phase 1 微调格式
- ScorePoolLabel: pos=(0,78) size=(250,38)
- TimerText: pos=(0,-200) size=(260,65) [重构] Phase 1 调位置/字号
- PosIndicator: pos=(0,288) size=(0,0)
- LeftEndMarker: pos=(0,159.00012) size=(0,0) [重构] Phase 1 改距离数字格式
- RightEndMarker: pos=(0,159) size=(0,0) [重构] Phase 1 改距离数字格式
- CenterMarker: pos=(0,0) size=(0,0)
- [新增] 结束按钮（左上角）Phase 4
- [新增] 设置按钮（右上角）Phase 4
- [新增] 顶部提示文字 Phase 4
- [新增] 连胜显示 Phase 4
- [新增] LOGO装饰框 Phase 4

### BottomBar (pos=(0,0) size=(0,50))
- [新增] 底部礼物详细介绍区 Phase 5
- [新增] 贴纸按钮（右下角）Phase 5

### Left/Right Player Lists [重构] Phase 3
- LeftPlayerList: pos=(0,-309) size=(0,0) [重构] 改为紧凑横排布局
- RightPlayerList: pos=(0,-309) size=(0,0) [重构] 改为紧凑横排布局
- LeftListTitle: pos=(0,-67) size=(0,0)
- RightListTitle: pos=(0,-69) size=(0,0)

### Notifications
- JoinNotification: pos=(0,0) size=(0,0)
- GiftNotification: pos=(0,0) size=(0,0)
- VIPAnnouncement: pos=(0,0) size=(0,0)

## SettlementPanel (pos=(0,0) size=(0,0))
- Title: pos=(0,810) size=(600,80)
- WinnerText: pos=(0,880) size=(800,70)
- MVPArea: pos=(0,362) size=(500,420)
- MVPAvatar: pos=(0,-10) size=(140,140)
- MVPAvatarFrame: pos=(0,100) size=(260,260)
- MVPNameBar: pos=(0,-55) size=(460,55)
- MVPLabel: pos=(0,-100) size=(200,55)
- MVPContribution: pos=(0,-121) size=(350,40)
- LeftRankColumn: pos=(-265,-230) size=(480,640)
- RightRankColumn: pos=(265,-230) size=(480,640)
- LeftRankTitle: pos=(0,285) size=(400,36)
- RightRankTitle: pos=(0,285) size=(400,36)
- ScoreDistArea: pos=(0,-650) size=(960,200)
- BtnRestart: pos=(0,-849) size=(300,80)
- BtnClose: pos=(0,-751) size=(300,80)

## RankingPanel (pos=(0,0) size=(0,0))
- PanelFrame: pos=(0,-35) size=(800,930)
- RankingBg: pos=(0,6) size=(1080,900)
- ListArea: pos=(0,-349) size=(950,680)
- Top3 Area: pos(0,455/415/380) for nums/names/scores
- Tabs: Tab_0(-300,720) Tab_1(-100,720) Tab_2(100,720) Tab_3(300,720)

## MainMenuPanel (pos=(0,0) size=(0,0))
- Logo: pos=(0,602) size=(900,650)
- BtnStartGame: pos=(0,200) size=(500,110)
- BtnLeaderboard: pos=(0,60) size=(500,110)
- BtnGiftDesc: pos=(0,-80) size=(500,110)
- BtnRuleDesc: pos=(0,-220) size=(500,110)
- BtnStickerSettings: pos=(200,-360) size=(280,80)

## Control Buttons (ButtonGroup)
- ButtonGroup: pos=(0,-35) size=(600,700)
- BtnStart: pos=(-270,0) size=(150,38)
- BtnReset: pos=(70,0) size=(150,38)
- BtnSimulate: pos=(-100,0) size=(150,38)
- BtnConnect: pos=(-440,0) size=(150,38)
- BtnGMLeft: pos=(240,0) size=(150,38)
- BtnGMRight: pos=(410,0) size=(150,38)
