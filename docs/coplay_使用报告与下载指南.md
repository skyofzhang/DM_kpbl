# Coplay 使用报告与下载指南

> 面向 Unity 的 AI 助手，支持编辑器内对话与 MCP 远程控制。  
> 文档版本：2026-02-11 | 适用项目：卡皮巴拉对决 (DM_kpbl)

---

## 一、产品概述

### 1.1 是什么

**Coplay** 是面向 Unity 的 AI 助手，提供两类使用方式：

| 模式 | 入口 | 说明 |
|------|------|------|
| **Coplay 插件** | Unity 编辑器内 | 在 Unity 里直接与 AI 对话，操作场景、Prefab、资源 |
| **Coplay MCP** | Cursor / Claude Code / VS Code | 通过 MCP 协议，让外部 AI 调用 Unity 的 86 个工具 |

### 1.2 核心能力

- **86 个 Unity 工具**：层级管理、资源编辑、场景控制、材质、脚本、UGUI、网格、性能分析等  
- **深度上下文**：可理解当前场景、选中对象、Prefab、层级结构  
- **多模型**：支持主流 LLM，按需切换  
- **可选能力**：3D 模型生成、精灵图生成、管道录制（视版本而定）

### 1.3 版本与费用（截至 2025–2026）

| 项目 | 说明 |
|------|------|
| **Coplay 插件 免费版** | 功能全开，有额度限制，适合试用与小项目 |
| **Coplay Pro** | $20/月，约 $40 额度/月，优先支持 |
| **Coplay MCP** | **Beta 期间免费**，使用你已有的 Cursor/Claude 订阅 |
| **Enterprise** | 定制报价，VPC/本地部署等 |

参考：[Coplay 定价页](https://coplay.dev/pricing)

---

## 二、下载与安装指南

### 2.1 环境要求

| 项目 | 要求 |
|------|------|
| **Unity** | 2022 及以上（推荐）；2021 可用 beta 分支，非长期支持 |
| **Git** | 已安装（Package Manager 从 git 拉包） |
| **网络** | 可访问 GitHub、Coplay 服务 |
| **Coplay MCP 额外** | Python 3.11+、`uv` 包管理器 |

### 2.2 安装 Coplay Unity 插件（必选）

1. 打开 Unity 项目（如 `D:\claude\DM_kpbl`）。
2. 菜单 **Window → Package Manager**。
3. 左上角点击 **+**，选择 **Add package from git URL…**。
4. 按 Unity 版本粘贴 URL：
   - **Unity 2022 及以上**（本项目适用）：
     ```
     https://github.com/CoplayDev/coplay-unity-plugin.git#beta
     ```
   - **Unity 2021**：
     ```
     https://github.com/CoplayDev/coplay-unity-plugin.git#beta-unity-2021
     ```
5. 点击 **Add**，等待安装完成（约 1 分钟）。
6. 安装完成后，按提示在 Unity 内登录/注册 Coplay 账号并完成认证。

官方安装说明：[Installation - Coplay Documentation](https://docs.coplay.dev/getting-started/installation)

### 2.3 安装 Coplay MCP（在 Cursor 里用 AI 操作 Unity）

**前提**：Unity 里已安装并登录 Coplay 插件；本机已安装 **Python 3.11+** 和 **uv**。

#### 安装 Python 3.11+

- Windows：从 [python.org](https://www.python.org/downloads/) 下载安装，勾选 “Add Python to PATH”。
- 安装 `uv`：
  ```bash
  pip install uv
  ```

#### 在 Cursor 中配置 MCP

1. 打开 Cursor，进入 MCP 配置（如 **Settings → MCP** 或对应配置文件）。
2. 在 `mcpServers` 中新增：

```json
{
  "mcpServers": {
    "coplay-mcp": {
      "autoApprove": [],
      "disabled": false,
      "timeout": 720,
      "type": "stdio",
      "command": "uvx",
      "args": ["--python", ">=3.11", "coplay-mcp-server@latest"]
    }
  }
}
```

3. 保存后重启 Cursor。  
4. **验证**：在 Cursor 中对 AI 说：「用 Coplay MCP 列出当前打开的 Unity 编辑器」。若返回编辑器列表，则配置成功。

说明：`timeout: 720` 为 12 分钟，适合长时间任务（如 `coplay_task`）；若只做简单查询可改为 60。

官方 MCP 说明：[Coplay MCP Quick Start](https://docs.coplay.dev/coplay-mcp/guide)

### 2.4 其他客户端（可选）

| 客户端 | 配置要点 |
|--------|----------|
| **Claude Desktop** | Settings → Developer → Edit Config，在 `mcpServers` 中加入上述 `coplay-mcp` 配置，并设置 `MCP_TOOL_TIMEOUT=720000`。 |
| **Claude Code** | 运行：`claude mcp add --scope user --transport stdio coplay-mcp --env MCP_TOOL_TIMEOUT=720000 -- uvx --python ">=3.11" coplay-mcp-server@latest` |
| **VS Code** | 命令面板 → MCP: Add Server → stdio，命令填 `uvx --python >=3.11 coplay-mcp-server@latest`，标识填 `coplay-mcp`。 |

---

## 三、使用说明

### 3.1 在 Unity 内使用 Coplay 插件

1. Unity 中打开 Coplay 面板（安装后通常有专用菜单或窗口）。
2. 用自然语言描述任务，例如：
   - 「在场景里创建一个空物体，命名为 SpawnPoint」
   - 「给当前选中的物体添加 Rigidbody」
   - 「把 Main Camera 的 clear flags 改成 Solid Color」
3. 查看 AI 建议的变更，确认后应用或修改后再应用。

适合：日常在编辑器里搭场景、调 Prefab、改组件，无需切到 Cursor。

### 3.2 在 Cursor 中通过 Coplay MCP 使用

1. 确保 Unity 项目已打开，且 Coplay 插件已登录。
2. 在 Cursor 对话中直接向 AI 下达与 Unity 相关的指令，例如：
   - 「用 Coplay MCP 列出当前打开的 Unity 编辑器」
   - 「在 Hierarchy 里创建一个 Canvas，下面放一个 Button」
   - 「把 Orange 的 Position Y 设为 0」
3. AI 会通过 Coplay MCP 调用对应工具，在 Unity 中执行操作。

适合：边写代码边改场景、批量操作、与 Cursor 代码修改配合使用。

### 3.3 与本项目（DM_kpbl）的适配

| 项目 | 说明 |
|------|------|
| **Unity 版本** | 2022.3.47f1c1 → 使用 `#beta` 分支即可 |
| **渲染管线** | URP → 支持 |
| **冲突风险** | 插件主要为 Editor 扩展，不参与构建，一般不影响现有脚本与场景；建议先备份或新分支再安装 |

---

## 四、参考链接

| 内容 | 链接 |
|------|------|
| 官网 | https://coplay.dev |
| 安装文档 | https://docs.coplay.dev/getting-started/installation |
| Coplay MCP 快速开始 | https://docs.coplay.dev/coplay-mcp/guide |
| 定价 | https://coplay.dev/pricing |
| Discord 社区 | https://discord.gg/y4p8KfzrN4 |
| Unity 插件 Git | https://github.com/CoplayDev/coplay-unity-plugin |

---

## 五、简要结论

- **下载/安装**：无需单独“下载 exe”，通过 Unity Package Manager 从 Git URL 安装插件；MCP 通过 `uvx` 自动拉取 `coplay-mcp-server`。
- **使用**：编辑器内用 Coplay 面板；Cursor/Claude 用 Coplay MCP，同一套 86 个工具。
- **费用**：插件免费版 + MCP Beta 免费，即可在 Cursor 中无额外 Coplay 费用使用；Pro 按需订阅。
- **本项目**：Unity 2022.3 + URP，按上述步骤安装即可；建议先装插件并登录，再配置 Cursor MCP。

以上内容可作为团队内部的 Coplay 使用报告与下载指南使用；若有版本或定价变动，以官网与官方文档为准。
