# Claude 插件一览与项目推荐

> 基于 Claude Code 官方市场与文档整理 | 项目：卡皮巴拉对决 (DM_kpbl) | 更新：2026-02-11

---

## 一、Claude 官方市场插件总表

以下为 **Anthropic 官方市场** (`claude-plugins-official`) 中常见插件分类与说明，便于快速浏览。实际列表以 Claude 内 **Browse plugins / Discover** 为准。

### 1. 代码智能（LSP）

| 插件名 | 语言 | 需安装的二进制 | 作用 |
|--------|------|----------------|------|
| `csharp-lsp` | C# | `csharp-ls` | 跳转定义、查找引用、编辑后即时类型/错误诊断 |
| `typescript-lsp` | TypeScript/JavaScript | `typescript-language-server` + `typescript` | 同上，适合 Node 服务端 |
| `pyright-lsp` | Python | `pyright` 或 `pyright-langserver` | Python 代码智能 |
| `clangd-lsp` | C/C++ | `clangd` | C/C++ 代码智能 |
| `gopls-lsp` | Go | `gopls` | Go 代码智能 |
| `rust-analyzer-lsp` | Rust | `rust-analyzer` | Rust 代码智能 |
| `jdtls-lsp` | Java | `jdtls` | Java 代码智能 |
| `kotlin-lsp` | Kotlin | `kotlin-language-server` | Kotlin 代码智能 |
| `lua-lsp` | Lua | `lua-language-server` | Lua 代码智能 |
| `php-lsp` | PHP | `intelephense` | PHP 代码智能 |
| `swift-lsp` | Swift | `sourcekit-lsp` | Swift 代码智能 |

### 2. 外部集成（MCP）

| 插件名 | 类别 | 简介 |
|--------|------|------|
| `figma` | 设计 | 读取 Figma 设计文件、组件信息、Design Tokens，设计转代码，缩小设计与实现差距 |
| `notion` | 项目管理/知识库 | 连接 Notion，读写页面与数据库 |
| `github` | 源码管理 | 与 GitHub 仓库/PR/Issue 等集成 |
| `gitlab` | 源码管理 | 与 GitLab 集成 |
| `atlassian` | 项目管理 | Jira、Confluence 等 |
| `asana` | 项目管理 | Asana 任务与项目 |
| `linear` | 项目管理 | Linear 事项管理 |
| `firebase` | 基础设施 | Firestore、Auth、Cloud Functions、Hosting、Storage 等 |
| `vercel` | 基础设施 | Vercel 部署与配置 |
| `supabase` | 基础设施 | Supabase 后端服务 |
| `slack` | 沟通 | Slack 频道与消息 |
| `sentry` | 监控 | 错误与性能监控 |

### 3. 开发工作流

| 插件名 | 简介 |
|--------|------|
| **综合开发流程**（名称以 Discover 为准） | 代码库探索、架构设计、质量审查等专用 Agent，约 71.6K+ 安装 |
| `commit-commands` | Git 提交流程：commit、push、创建 PR 等 |
| `pr-review-toolkit` | 专用于 Pull Request 审查的 Agent |
| `agent-sdk-dev` | 使用 Claude Agent SDK 开发的工具 |
| `plugin-dev` | 创建与调试自定义插件 |

### 4. 输出风格

| 插件名 | 作用 |
|--------|------|
| `explanatory-output-style` | 对实现选择做教学式解释 |
| `learning-output-style` | 交互式学习模式，帮助巩固技能 |

### 5. 其他常见插件（社区/截图可见）

| 插件名 | 简介 |
|--------|------|
| **Firecrawl** | 网页抓取与爬取，将网站转为 Markdown 或结构化数据 |

---

## 二、安装方式速查

- 在 Claude Code 中打开插件管理：输入 **`/plugin`**，切到 **Discover** 浏览并安装。
- 命令行安装（以官方市场为例）：
  ```bash
  /plugin install <插件名>@claude-plugins-official
  ```
- 作用域：**User**（仅自己）、**Project**（写进 `.claude/settings.json`，团队共享）、**Local**（仅本仓库、gitignore）。

---

## 三、结合本项目的踩坑与需求推荐

以下结合你项目里的 **CLAUDE.md**、**Notion 知识库**、**UI 与联调** 等实际情况做推荐。

### 项目与踩坑摘要

| 维度 | 内容 |
|------|------|
| 技术栈 | Unity 2022.3 (C#) + Node.js (JS) + WebSocket，URP |
| 痛点 | 结算/排行榜等 UI：**背景美术对、数据排版对不上**（重叠、行距不足） |
| 文档与协作 | Notion 知识库（直播弹幕游戏知识库）、CLAUDE.md 记录教训 |
| 教训示例 | UI 不在 Awake 里 SetActive(false)；SettlementUI 时序需 OnEnable 回补；客户端预测参数易被覆盖；Prefab 需重建等 |
| 当前阶段 | 约 70%，待：联调、美术补充、细节优化 |

### 推荐插件及理由

| 优先级 | 插件 | 理由 |
|--------|------|------|
| ⭐⭐⭐ | **Notion** | 知识库已在用，接上后 Claude 可直接读/写 Notion 页面，对齐策划、协议、踩坑记录，减少“只改代码不看文档”的偏差。 |
| ⭐⭐⭐ | **csharp-lsp** | Unity 脚本全是 C#，LSP 提供即时代码诊断与跳转，改 UI 脚本/实体时减少漏改和编译后才发现的错误。 |
| ⭐⭐⭐ | **Figma** | 若有 UI 设计稿，可让 Claude 按设计稿的尺寸/间距做“数据排版”，减轻**数据与美术对不齐**的问题；若没有 Figma，可降为可选。 |
| ⭐⭐ | **GitHub** | 仓库在 GitHub (skyofzhang/DM_kpbl)，便于在 Claude 内查 PR/Issue、配合提交与发布。 |
| ⭐⭐ | **commit-commands** | 规范 commit/PR 流程，适合多人或多环境（本地/服务器）协作。 |
| ⭐ | **typescript-lsp** | 若经常改 Node 服务端 (Server/src)，可装；当前量不大可先不装。 |
| ❌ 暂不推荐 | **Firebase** | 当前后端为自建 Node + 文件持久化，无 Firebase 需求可不装。 |

### 与“数据排版对不上美术”的配合方式

- **Figma**：有设计稿时，让 Claude 按 Figma 的组件与间距来写/调 UGUI 布局（RectTransform、锚点、间距），再在 Unity 里用 Coplay 微调，效果更好。
- **Notion**：把 UI 规范（例如：贡献榜行高、排名圆圈与文字间距、积分瓜分列宽）写在 Notion，让 Claude 读 Notion 后再改 SettlementUI/RankingPanelUI，保证逻辑与文档一致。
- **Coplay**：继续用 Coplay 在 Unity 里按实际美术资源微调布局，解决“底图对、数据错位”的最后一公里。

---

## 四、推荐安装顺序（实操）

1. **Notion** → 接上现有知识库，方便 Claude 查协议、阶段、踩坑。
2. **csharp-lsp** → 本机安装好 `csharp-ls` 后，在 Claude 中安装 `csharp-lsp`，提升 C# 编辑质量。
3. **Figma**（若有设计稿）→ 设计转代码 + 排版规范，再配合 Coplay 精修。
4. **GitHub** + **commit-commands** → 按需启用，规范提交流程。

---

## 五、参考链接

| 内容 | 链接 |
|------|------|
| Claude Code 发现与安装插件 | https://code.claude.com/docs/en/discover-plugins |
| 插件参考（结构/规范） | https://code.claude.com/docs/en/plugins-reference |
| 中文文档（发现和安装插件） | https://claudecn.com/docs/claude-code/discover-plugins/ |

---

*说明：插件名称与数量以 Claude 内 Browse plugins / Discover 列表为准，本文档按官方文档与常见用法整理，便于你对照和选型。*
