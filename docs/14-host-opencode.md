# 14 — OpenCode / Mimocode 宿主

## 入口

| 文件 | 用途 |
| :--- | :--- |
| `src/Hosts/OpenCode/Plugin.fs` | OpenCode 插件入口 |
| `src/Hosts/OpenCode/PluginMimo.fs` | Mimocode 入口 |
| `src/Hosts/OpenCode/PluginMimoTui.fs` | TUI：`/subagents`、task → sidebar todo |
| `src/Hosts/OpenCode/PluginComposition.fs` | 共享装配 |
| `src/Hosts/OpenCode/PluginHooks.fs` | Hook 注册 |
| `src/Hosts/OpenCode/PluginServices.fs` | 服务与依赖 |

## 工具

`src/Hosts/OpenCode/Tools.fs`：总表 + `registerMethodologyTools`。分族：`SearchTools.fs`、`SubagentTools.fs`、`MimoTodoTool.fs`、`ExecutorTool.fs`、`PtySpawn.fs`、`PtyReadTool.fs`、`PtyWriteTool.fs` 等。

- **PTY**：`pty_*` 五工具注册于 `Tools.fs`；运行时依赖 npm `opencode-pty`
- **Executor**：Zod schema 含必填 `max_bytes`
- **Schema**：**Zod**（架构测试禁止在 hook 文件直接 import Zod）

## 消息

- `src/Hosts/OpenCode/MessageTransformPipeline.fs` → Runtime message transform
- Codec：`src/Hosts/OpenCode/MessagingCodec.fs` 与 `src/Runtime/Messaging/Opencode*.fs`

## Session 事件与 Nudge

- `src/Runtime/Messaging/OpencodeSessionEventCodec.fs`
- `src/Hosts/OpenCode/NudgeEffect.fs`、`NudgeTrigger.fs`

## Fallback 续命

唯一物理路径：`IActionExecutor.SendContinue` → `SessionDispatcher` → 宿主 `session.prompt`。

- `src/Hosts/OpenCode/Fallback/ActionExecutor.fs`：`SendContinue` → `SessionDispatcher`
- `src/Hosts/OpenCode/Fallback/Hook.fs`：事件入 Runtime Coordinator
- `src/Hosts/OpenCode/Fallback/EventTranslator.fs`：宿主事件翻译
- `src/Hosts/OpenCode/ChatHooksClassification.fs`：`chat.message` → `recordHostAcceptedContinuation`

## Subsession Host Adapter

`src/Hosts/OpenCode/SubsessionHostAdapter.fs`、`SubsessionHostAdapterOps.fs`、`SubsessionHostAdapterTypes.fs`：实现 `ISubsessionHost`，管理子会话生命周期。

## Mimocode 命名差异

经 `HostTools`：`actor` = task 子代理工具。待办写入（todowrite/task）已由宿主原生实现。

## 上游

参见 `../opencode`；**禁止改上游**（`AGENTS.md`）。
