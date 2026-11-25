# Unity MCP 接入说明（Unity 2022.3.62f3c1 / 团结引擎）

## 结论：选用 CoderGamester/mcp-unity 1.5.0

| 项 | 值 |
| --- | --- |
| 引擎侧包 | `com.gamelovers.mcp-unity` **1.5.0**（UPM，MIT） |
| 官方最低版本 | 包清单声明 `"unity": "2022.3"`；C# 里对 Unity 6 才有的 API 全部带 `#if UNITY_6000_0_OR_NEWER` 或 asmdef `versionDefines`（`MCP_UNITY_ENTITY_ID_API`）回退，因此 2022.3 走的是兼容分支 |
| 链路 | DSH ⇄(stdio)⇄ Node MCP Server ⇄(WebSocket, 8090)⇄ Unity Editor 插件 |
| 为什么不用别的 | `IvanMurzak/Unity-MCP` 需要 UPM + NuGet/.NET 常驻进程，China 版编辑器上依赖链更重；npm 上的 `unity-mcp-server` 明确要求 Unity 6 |

## 已落地的改动（5 处）

1. `Remiehzla/Packages/com.gamelovers.mcp-unity/`
   内嵌（vendored）包，1.5.0 全量代码（58 个 Editor C# 文件 + 内置 `websocket-sharp.dll` + `Server~` 源码）。
   内嵌而不是 git URL，是为了不依赖 Unity 打开工程时的 GitHub 连通性。
2. `Remiehzla/ProjectSettings/McpUnitySettings.json`
   `Port=8090`、`RequestTimeoutSeconds=10`、`AutoStartServer=true`、`AllowRemoteConnections=false`、`AllowPackageInstallation=false`。
3. `Remiehzla/Library/McpUnity/bridge-token`
   项目专属 64 位十六进制令牌（已预生成，Unity 启动时直接复用；`Library/` 已在 `.gitignore` 内，不入库）。
4. `Tools/mcp-unity-server/`
   由 `Server~` 构建的 Node MCP 服务：`build/index.js` + `node_modules`。
   已实测通过 MCP `initialize` 握手并返回 `tools/list`（27 个工具），不依赖 Unity 是否已启动。
5. `%USERPROFILE%\.dsh\profiles\desktop\cordis.patch.yml`
   追加 `mcp-unity` 条目（`@deepseek-ai/dsh-mcp-client`，stdio，`serverName: unity`）。
   原文件备份在同目录 `cordis.patch.yml.bak-before-mcp-unity`。

## 首次接通：三步

1. **打开 Unity 工程** `Remiehzla`（2022.3.62f3c1）。
   首次导入会拉取三个依赖并编译：`com.unity.nuget.newtonsoft-json@3.2.1`、`com.unity.editorcoroutines@1.0.0`、`com.unity.test-framework@1.3.3`（会把工程里原来的 1.1.33 提到 1.3.3）。
2. **启动编辑器侧服务**：菜单 `Tools > MCP Unity > Server Window` → `Start Server`。
   因为 `AutoStartServer=true`，正常情况下打开工程就会自启；窗口里出现绿色 `Connected` 即就绪。
3. **重启 DSH 桌面端**，新开会话。
   工具会以 `mcp__unity__<tool>` 形式出现。

验证一句话：让 Agent 调 `mcp__unity__get_scene_info`，返回真实场景名/路径即打通（能看到工具列表 ≠ 已连通）。

## 可用的能力（摘要）

- 场景：`get_scene_info` `load_scene` `save_scene` `create_scene` `delete_scene` `unload_scene` `get_scenes_hierarchy`
- 对象：`get_gameobject` `update_gameobject` `duplicate_gameobject` `delete_gameobject` `reparent_gameobject` `select_gameobject`，以及 `transform` 工具组
- 组件/材质：`update_component`、`create_material` `assign_material` `modify_material` `get_material_info`
- 编辑器：`execute_menu_item` `recompile_scripts` `get_console_logs` `send_console_log` `get_play_mode_status` `set_play_mode_status` `run_tests`
- 资源：`unity://logs` `unity://scenes-hierarchy` `unity://assets` `unity://packages` `unity://tests/{mode}` `unity://menu-items` 等

安全默认：`add_package`（可执行第三方 Editor 代码）默认关闭，需要时在 Server Window 勾选 *Allow Package Installation*。

## 排障

| 现象 | 处理 |
| --- | --- |
| 工具都在，调用报 `ECONNREFUSED` | Unity 里服务没启动 / 端口不一致。看 `Tools > MCP Unity > Server Window`，默认 8090 |
| 认证失败（401 / handshake 拒绝） | `Library/McpUnity/bridge-token` 与编辑器侧不一致。Server Window 里 Regenerate 令牌，然后重启 DSH |
| 重启 DSH 后没有 `mcp__unity__*` | 看 DSH 日志里 `mcp-unity` 插件是否激活失败；确认 `Tools/mcp-unity-server/build/index.js` 与 Node 路径仍在 |
| 升级后桥接连不上 | 1.5.0 有意拒绝 1.4.x 的 Node 桥接，Unity 包与 Node 服务必须同版本 |
| 2022.3 下编译报错 | 说明该版本用到了 2022.3 没有的 API：可回退 `com.gamelovers.mcp-unity@1.4.0`，同时用 1.4.0 的 `Server~` 重建 Node 服务 |

## 回滚

1. 删除 `cordis.patch.yml` 末尾的 `mcp-unity` 条目（或直接恢复 `cordis.patch.yml.bak-before-mcp-unity`），重启 DSH。
2. 删除 `Remiehzla/Packages/com.gamelovers.mcp-unity`、`Remiehzla/ProjectSettings/McpUnitySettings.json`、`Remiehzla/Library/McpUnity`。

## 升级 / 重建

```powershell
# 1) 引擎侧（二选一）
#    a. 覆盖 Packages/com.gamelovers.mcp-unity 为新版本
#    b. 改用 UPM：Packages/manifest.json 里加
#       "com.gamelovers.mcp-unity": "https://github.com/CoderGamester/mcp-unity.git"

# 2) Node 侧重建
cd D:\UnityProjects\Gelan2\Tools\mcp-unity-server
npm install --ignore-scripts --cache D:\UnityProjects\Gelan2\.tmp-npm-cache
node node_modules\typescript\bin\tsc -p tsconfig.json
node scripts\copy-ui.mjs
```

## 附：本次环境备注

- 该工程的 `Remiehzla/` 目录原先没有给当前账号「完全控制」，DSH 沙箱无法在工程内写文件；已用官方诊断脚本补权限（报告与回滚命令在 `.dsh-acl-recovery\`）。
- 沙箱内 `npm` 会因为管道受限报 `EPERM`：安装依赖必须加 `--ignore-scripts`（本服务不需要 postinstall）。
