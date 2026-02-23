# Claude Code × Unity × Coplay MCP 快速指南

> **用途**: 新 Claude Code 对话框启动后，快速配置 Coplay MCP 并掌握 Unity 远程操作方法。
>
> **最后更新**: 2026-02-17

---

## 一、前置条件

| 项目 | 要求 | 本机状态 |
|------|------|----------|
| Unity | 2022+ 已打开项目 | ✅ 2022.3.47f1c1 |
| Coplay 插件 | Unity Package Manager 已安装并登录 | ✅ `Packages/Coplay/` |
| Python | 3.11+ | ✅ `C:\Users\Administrator\AppData\Local\Programs\Python\Python312\` |
| uvx | uv 包管理器 | ✅ `C:\Users\Administrator\AppData\Local\Programs\Python\Python312\Scripts\uvx.exe` v0.10.0 |

---

## 二、MCP 配置（一次性）

### 2.1 项目级 `.mcp.json`（已创建）

文件位置: `D:\claude\DM_kpbl\.mcp.json`

```json
{
  "mcpServers": {
    "coplay-mcp": {
      "command": "uvx",
      "args": ["--python", ">=3.11", "coplay-mcp-server@latest"],
      "env": {
        "MCP_TOOL_TIMEOUT": "720000"
      }
    }
  }
}
```

> ⚠️ **重要**: MCP 服务器只在 Claude Code **启动时**加载。修改 `.mcp.json` 后必须**重启 Claude Code 会话**才生效。

### 2.2 权限配置

需要在全局或项目的 `settings.json` 中允许 Coplay 工具。添加到 `permissions.allow` 数组中：

```
mcp__coplay-mcp__*
```

或者逐个添加（完整工具列表见第四节）。

### 2.3 验证 MCP 是否生效

在 Claude Code 中运行以下命令测试：

```
请用 coplay MCP 列出当前打开的 Unity 编辑器
```

如果返回编辑器信息，说明配置成功。如果工具不存在，检查：
1. Unity 是否已打开并且 Coplay 插件已登录
2. `.mcp.json` 路径是否在项目根目录
3. 是否重启了 Claude Code 会话

---

## 三、常用工作流

### 3.1 编译检查

```
// 用 Coplay 检查 Unity 是否有编译错误
调用 mcp__coplay-mcp__get_console_logs 获取控制台日志
```

### 3.2 截图检查 UI

```
// 截取当前 Game 视图
调用 mcp__coplay-mcp__take_screenshot

// 截取特定 Canvas/Panel
先用 mcp__coplay-mcp__find_objects_by_name 找到对象
再用 mcp__coplay-mcp__take_screenshot 截图
```

### 3.3 场景操作

```
// 查找场景中的对象
mcp__coplay-mcp__find_objects_by_name { "name": "GameUIPanel" }

// 获取对象属性
mcp__coplay-mcp__get_component_properties { "objectPath": "Canvas/GameUIPanel", "componentType": "RectTransform" }

// 修改对象属性
mcp__coplay-mcp__set_component_property { "objectPath": "Canvas/GameUIPanel", ... }
```

### 3.4 执行菜单命令

```
// 执行编辑器菜单（如保存场景、运行编辑器脚本）
mcp__coplay-mcp__execute_menu_item { "menuPath": "CapybaraDuel/Update Battle UI Art (Safe)" }
```

### 3.5 运行/停止 Play Mode

```
mcp__coplay-mcp__enter_play_mode
mcp__coplay-mcp__exit_play_mode
```

### 3.6 保存场景

```
// ⚠️ 重要: 用 SaveCurrentScene.cs 菜单保存，不要用 Coplay 的 save_scene
// Coplay save_scene 会另存到错误路径！
mcp__coplay-mcp__execute_menu_item { "menuPath": "CapybaraDuel/Save Current Scene" }
```

---

## 四、Coplay MCP 工具完整列表（86个）

### 层级 & 场景管理
- `find_objects_by_name` — 按名称查找对象
- `find_objects_by_tag` — 按 Tag 查找
- `find_objects_by_layer` — 按 Layer 查找
- `find_objects_by_component` — 按组件类型查找
- `get_hierarchy` — 获取层级树
- `get_children` — 获取子物体
- `create_game_object` — 创建空物体
- `delete_game_object` — 删除物体
- `rename_game_object` — 重命名
- `set_parent` — 设置父物体
- `duplicate_game_object` — 复制物体
- `set_active` — 启用/禁用物体
- `get_selected_objects` — 获取当前选中对象
- `select_objects` — 选中指定对象

### Transform
- `get_transform` — 获取 Transform 信息
- `set_transform` — 设置 Position/Rotation/Scale

### 组件
- `get_components` — 获取物体上的所有组件
- `get_component_properties` — 获取组件属性
- `set_component_property` — 设置组件属性
- `add_component` — 添加组件
- `remove_component` — 移除组件

### 资源
- `find_assets` — 搜索项目资源
- `get_asset_info` — 获取资源信息
- `import_asset` — 导入资源
- `create_material` — 创建材质
- `create_prefab` — 创建 Prefab
- `instantiate_prefab` — 实例化 Prefab

### UGUI
- `create_canvas` — 创建 Canvas
- `create_ui_element` — 创建 UI 元素(Button/Text/Image 等)
- `set_ui_text` — 设置文字内容
- `set_ui_image` — 设置图片
- `get_rect_transform` — 获取 RectTransform
- `set_rect_transform` — 设置 RectTransform

### 编辑器控制
- `execute_menu_item` — 执行菜单命令
- `enter_play_mode` — 进入播放模式
- `exit_play_mode` — 退出播放模式
- `take_screenshot` — 截图
- `get_console_logs` — 获取控制台日志
- `clear_console` — 清空控制台
- `save_scene` — 保存场景 (⚠️ 本项目不用这个，用 SaveCurrentScene.cs)
- `open_scene` — 打开场景
- `undo` — 撤销
- `redo` — 重做

### 脚本 & 代码
- `execute_code` — 在 Unity 编辑器中执行 C# 代码
- `get_script_source` — 获取脚本源码
- `compile_check` — 检查编译状态

### 高级任务
- `coplay_task` — 让 Coplay 内部 AI 执行复杂任务（需要较长 timeout）

---

## 五、项目特定注意事项

### 场景保存
- **必须用** `CapybaraDuel/Save Current Scene` 菜单保存
- **不要用** Coplay 的 `save_scene` 工具（会另存到错误路径）
- 对应脚本: `Assets/Editor/SaveCurrentScene.cs`

### 编辑器脚本菜单
| 菜单路径 | 功能 |
|----------|------|
| `CapybaraDuel/1. Generate Scene Objects` | 生成场景基础对象 |
| `CapybaraDuel/2. Update Scene (Safe)` | 安全更新场景（不覆盖手动调整） |
| `CapybaraDuel/3. Build All Prefabs` | 重建所有 Prefab |
| `CapybaraDuel/Update Battle UI Art (Safe)` | 更新战斗界面美术资源 |
| `CapybaraDuel/Update GM Panel (Safe)` | 更新 GM 面板 |
| `CapybaraDuel/Save Current Scene` | 保存当前场景 |
| `CapybaraDuel/Build Windows (抖音上架)` | 打包 Windows 版本 |

### 代码默认值 = 场景值
- Unity 序列化系统：如果 C# 字段有 `[SerializeField]`，场景保存的值优先于代码默认值
- 如果代码默认值与场景值不一致，重新生成 Prefab 或者新建对象时会用错误的值
- **改代码默认值时必须同步检查场景值**

### 排行榜坐标
- 排行榜的坐标是用户手动在 Inspector 中校准的
- **严禁任何代码（包括编辑器脚本）覆盖排行榜坐标**
- 详细坐标值记录在 `dm_kpbl_history.md` 开头

### UI 隐藏方式
- **禁止在 Awake() 中 SetActive(false)**: 会阻止 OnEnable() 执行
- **正确做法**: 用 `CanvasGroup.alpha=0` 隐藏，保持 GameObject active

---

## 六、典型自检流程

新会话开始后，执行以下 Coplay 自检：

```
1. 检查编译状态
   → mcp__coplay-mcp__get_console_logs (查看是否有编译错误)

2. 检查场景对象完整性
   → mcp__coplay-mcp__get_hierarchy (查看层级结构)
   → mcp__coplay-mcp__find_objects_by_name { "name": "GameUIPanel" }
   → mcp__coplay-mcp__find_objects_by_name { "name": "BottomBar" }

3. 截图验证 UI 布局
   → mcp__coplay-mcp__take_screenshot (截取 Game 视图)

4. 检查关键组件引用
   → mcp__coplay-mcp__get_component_properties 检查 GameControlUI 的按钮引用
   → mcp__coplay-mcp__get_component_properties 检查 TopBarUI 的 UI 元素引用
```

---

## 七、故障排除

### Coplay MCP 工具不可用
1. 确认 Unity 已打开且 Coplay 插件已登录
2. 确认 `.mcp.json` 在项目根目录 (`D:\claude\DM_kpbl\.mcp.json`)
3. **重启 Claude Code 会话**（MCP 只在启动时加载）
4. 检查 `uvx` 是否可用: `C:\Users\Administrator\AppData\Local\Programs\Python\Python312\Scripts\uvx.exe --version`

### 编译错误
- 先用 `get_console_logs` 查看错误
- 常见原因: 命名空间缺失、引用丢失、API 变更
- 修复后用 `compile_check` 确认

### 场景丢失
- 如果场景对象缺失，运行 `CapybaraDuel/2. Update Scene (Safe)` 补充
- **不要用** `1. Generate Scene Objects`（会重新生成，覆盖手动调整）

---

## 八、快速启动 Checklist

新 Claude Code 会话启动后：

- [ ] 读取 `CLAUDE.md` 了解项目架构
- [ ] 读取 `MEMORY.md` 了解当前状态和待办
- [ ] 确认 Coplay MCP 工具可用（调用任意工具测试）
- [ ] 用 `get_console_logs` 检查编译状态
- [ ] 用 `take_screenshot` 截图确认 UI 状态
- [ ] 确认不要覆盖排行榜坐标、场景手动调整的值
