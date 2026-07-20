# 14 — OpenCode / Mimocode 宿主

## 入口

| 文件 | 用途 |
| :--- | :--- |
| `src/Hosts/OpenCode/Plugin.fs` | OpenCode 插件入口 |
| `src/Hosts/OpenCode/PluginMimo.fs` | Mimocode 入口 |
| `src/Hosts/OpenCode/PluginMimoTui.fs` | TUI：`/subagents`、task→sidebar todo |
| `src/Hosts/OpenCode/PluginComposition.fs` | 共享装配 |
| `src/Hosts/OpenCode/PluginHooks.fs` | Hook 注册 |
| `src/Hosts/OpenCode/PluginServices.fs` | 服务与依赖 |

## 工具

- `src/Hosts/OpenCode/Tools.fs`：总表 + `registerMethodologyTools`
- 分族：`SearchTools.fs`、`SubagentTools.fs`、`MimoTodoTool.fs`、`ExecutorTool.fs`、`PtySpawn.fs`、`PtyIo.fs` 等
- **PTY**：`pty_*` 五工具注册于 `Tools.fs`；运行时依赖 npm `opencode-pty`（根 `package.json`）
- **Executor**：Zod schema 含必填 `max_bytes`
- Schema：**Zod**（禁止在 hook 文件直接 import Zod，架构测试）
- 契约：`e2e/OpencodePluginTests.fs`（`pty_spawn` 注册与 execute）

## 消息

- `src/Hosts/OpenCode/MessageTransformPipeline.fs` → Runtime message transform
- Codec：`src/Hosts/OpenCode/MessagingCodec.fs` 与 `src/Runtime/Messaging/`

## Session 事件与 Nudge

- `src/Runtime/Messaging/OpencodeSessionEventCodec.fs`
- `src/Hosts/OpenCode/NudgeEffect.fs`

## Fallback 续命

唯一物理路径见 [CONTINUATION_PATH.md](./CONTINUATION_PATH.md)。

- `src/Hosts/OpenCode/Fallback/ActionExecutor.fs`：`IActionExecutor.SendContinue` → `SessionDispatcher`
- `src/Hosts/OpenCode/Fallback/Hook.fs`：事件入 Runtime Coordinator
- `src/Hosts/OpenCode/Fallback/EventTranslator.fs`：宿主事件翻译
- `src/Hosts/OpenCode/ChatHooksClassification.fs`：`chat.message` → `recordHostAcceptedContinuation`

## Mimocode 命名差异

经 `HostTools`：`task` = todowrite，`actor` = task 子代理工具。

## 上游

参见 `../opencode`；**禁止改上游**（`AGENTS.md`）。适配仅在本仓库 `src/Hosts/OpenCode/` + `src/Runtime/`。

## 相关文档

- [02-architecture.md](./02-architecture.md)
- [10-message-transform.md](./10-message-transform.md)
- [08-tools-and-permissions.md](./08-tools-and-permissions.md)
- [12-fallback.md](./12-fallback.md)
