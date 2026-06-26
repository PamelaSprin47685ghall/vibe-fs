# REQ.md — OMP 与 Opencode/Mimo 能力对齐

## 目的与范围

- **对照基准**：`src/Opencode/`（OpenCode / Mimocode 成熟实现）、`../oh-my-pi` 扩展契约（只读参考）。
- **目标宿主**：`src/Omp/`（`wanxiangshuExtension` / Pi `on(...)` 接线）。
- **原则**：功能等效、实现可不同；禁止改 `../oh-my-pi` 上游（见 `AGENTS.md`）。

## 已追平（本轮）

| 项 | 说明 | 代码锚点 |
|----|------|----------|
| `tool_result` 事件形状 | Pi 使用 `input` / `toolCallId` / `content: TextContent[]`，非 `args` + 字符串 `content` | `src/Omp/ToolResultEvent.fs`，`SessionLifecycleHooks`，`MessageTransform` |
| `todowrite` 工作报告捕获 | `CaptureReport(callId, completedWorkReport)` | `SessionLifecycleHooks.toolResultHandler` |
| backlog 投影回退 | tool state 无 report 时用 projection | `src/Shell/BacklogSessionCodec.fs`（含 `Omp` host） |
| 工具面注册 | 子代理、fuzzy、web、executor、review、KG、53×methodology、`todowrite` | `Omp/Tools.fs`，`OmpPluginTests` |

## 仍缺失（无对等接线）

| 能力 | Opencode/Mimo | OMP 现状 | 验收建议 |
|------|---------------|----------|----------|
| 会话压缩时 backlog | `experimental.session.compacting` + `BacklogSession` | 无 Pi compacting 钩子；仅 `context`/`before_context` 折叠 | 压缩前后 `completedWorkReport` 仍在合成上下文 |
| 每轮 chat 工具可见性 | `chat.message` 按 agent 改 `tools` | 主要 `session_start` 一次 `filterOmpMainSessionActiveTools` | 子代理回合后工具集与 Opencode 策略一致 |
| `tool.execute.before` | 执行前 `_ui`、patch 参数归一 | 仅在 `tool_result` 后处理（UI 可能已展示旧 args） | 若 Pi 暴露等价钩子则前移；否则文档化限制 |
| 事件驱动 nudge 全状态机 | `SessionLifecycleObserver` + `NudgeState` + `session.prompt` | `agent_end` loop/todo + 简化 `NudgeRuntime` | idle/retry/abort 与 Opencode 同等不重复、不泄漏 |
| Runner nudge | 内核 `NudgeRunner`（Opencode 亦 StandDown） | `hasActiveRunner` 恒 false，从不推 | 产品若要「executor 空闲提醒」需定义 OMP 检测源 |
| stealth-browser MCP | 插件 `mcp` + AgentConfig | 不注入；`browser` 无宿主则报错 | 可选：文档或 env 引导用户自配 Pi MCP |
| `command.execute.before` 统一入口 | loop-review 等与命令生态 | 有 `/loop`、`loop-review` 命令，无统一 before 链 | 命令级行为与 Opencode 对齐清单 |
| `experimental.chat.system.transform` | 系统提示变换 | caps 走 context 合成 + `before_agent_start` 剥 AGENTS | 行为等价即可，不必同名钩子 |

## 弱等价（有实现，语义不完全同）

| 能力 | 差异 |
|------|------|
| 消息变换 / review replay | Opencode client 拉 history；OMP `entries` + `IfStoreEmpty`；agent 来自 `sessionManager.agentName` |
| KG bookkeeper | OMP `tool_result` + `isChildSession`；Opencode `tool.execute.after` + `ChildAgentRegistry` |
| todowrite 命名 | Mimocode `task` vs OMP `todowrite`；折叠/合并规则按 `Host` 分支 |
| Executor | Opencode 同会话队列；OMP 子 workspace + `executor_wait`/`executor_abort` |
| Review 预检 | Opencode `command.execute.before`；OMP `loop-review` 命令 |
| AgentConfig | Opencode 含 MCP map；OMP 可选 `getConfig`/`setConfig`，并 disable 部分 Pi 原生 agent |

## 宿主约束

- 不得修改 `../oh-my-pi`；仅参考 `packages/coding-agent` 中 `ToolResultEvent` 等类型。
- `src/Omp/` 禁止引用 `Wanxiangshu.Opencode` / `Wanxiangshu.Mux`（架构测试门禁）。

## 建议优先级

1. **P0**：compacting 时 backlog（若 Pi 提供钩子或等价事件）。
2. **P1**：每轮工具策略（`chat.message` 等价物或 `agent_start`/`turn_start`）。
3. **P2**：nudge 与 Opencode `NudgeState` 对齐（在 Pi 事件可用前提下）。
4. **P3**：browser MCP 可选接线、`tool.execute.before` 前移（依赖宿主 API）。

## 参考

- Opencode 钩子：`src/Opencode/PluginCore.fs` `registerHooks`
- OMP 生命周期：`src/Omp/SessionLifecycle.fs`，`SessionLifecycleHooks.fs`
- 测试：`tests/OmpPluginTests*.fs`，`tests/OmpToolResultEventTests.fs`
