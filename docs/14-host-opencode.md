# 14 — OpenCode / Mimocode 宿主

## 入口

| 文件 | 用途 |
| :--- | :--- |
| `Opencode/Plugin.fs` | OpenCode 插件入口 |
| `Opencode/PluginMimo.fs` | Mimocode 入口 |
| `Opencode/PluginMimoTui.fs` | TUI：`/subagents`、task→sidebar todo |
| `Opencode/PluginCore.fs` | 共享装配 |
| `Opencode/PluginCoreHooks.fs` | Hook 注册 |
| `Opencode/PluginCoreServices.fs` | 服务与依赖 |

## 工具

- `Opencode/Tools.fs`：总表 + `registerMethodologyTools`
- 分族：`SearchTools.fs`、`SubagentTools.fs`、`MimoTodoTool.fs`、`ExecutorTool.fs`、`PtySpawn.fs`、`PtyIo.fs` 等
- **PTY**：`pty_*` 五工具注册于 `Tools.fs`；运行时依赖 npm `opencode-pty`（根 `package.json`）
- **Executor**：Zod schema 含必填 `max_bytes`
- Schema：**Zod**（禁止在 hook 文件直接 import Zod，架构测试）
- 契约：`e2e/OpencodePluginTests.fs`（`pty_spawn` 注册与 execute）

## 消息

- `Opencode/MessageTransform.fs` → `MessageTransformPipeline`
- Codec：`Shell/Opencode*Codec` 系列

## Session 事件与 Nudge

- `Shell/OpencodeSessionEventCodec.fs`、`OpencodeSessionEventNudge.fs`
- `Opencode/NudgeEffect.fs`

## Fallback 续命

- `Opencode/Fallback/ContinuationHost.fs`：`IContinuationHost` 的 OpenCode 实现，使用 `continuationPayload`（`"\u200B"`）作为 `createFallbackContinuationPromptBody` 的文本参数
- `Opencode/Fallback/ActionExecutor.fs`：`IActionExecutor` 实现，`SendContinue` 使用内联 `"\u200B"` 替代旧 `zwsChar` 私有常量
- `Opencode/Fallback/EventTranslator.fs`：`IEventTranslator` 实现，`isNewUserMessageImpl` 不再依赖 `isSyntheticText` 嗅探（由 `ContinuationHost` 的 metadata 机制替代）

## Mimocode 命名差异

经 `HostTools`：`task` = todowrite，`actor` = task 子代理工具。

## 上游

参见 `../opencode`；**禁止改上游**（`AGENTS.md`）。适配仅在本仓库 `Opencode/` + Shell。

## 相关文档

- [02-architecture.md](./02-architecture.md)
- [10-message-transform.md](./10-message-transform.md)
- [08-tools-and-permissions.md](./08-tools-and-permissions.md)
- [12-fallback.md](./12-fallback.md)
