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
| 会话压缩时 backlog | `session.compacting` 钩子 → `BacklogSession.GetOrRebuildBacklog` → `buildBacklogText` → `{ context: string[] }` | `src/Omp/SessionCompacting.fs`，`OmpSessionCompactingTests` |
| `tool.execute.before` | `tool_call` 钩子 → `HookExecute.applyPreExecuteHook`（patch 参数归一 + `_ui` 标签注入） | `src/Omp/SessionLifecycleHooks.fs`，`src/Omp/HookExecute.fs` |
| `before_agent_start` 工具过滤 | 系统提示补丁 + `applyActiveToolFilterForMainSession`（复用 `session_start` 逻辑） | `src/Omp/SessionLifecycleHooks.fs` |
| Runner nudge | `hasRunningRunnerJob(sessionId)` 注入 `NudgeContext`；`agentEndHandler` 发 `wanxiangshu-runner-reminder` | `src/Omp/NudgeRuntime.fs`，`SessionLifecycleHooks.fs` |
| 生命周期钩子测试 | `extensionRegistersLifecycleHooks` + `toolCallHookCanBeInvoked` + `sessionCompactingHookCanBeInvoked` | `tests/OmpPluginTests.fs` |
| compacting 测试 | 5 个测试覆盖空消息/有消息/report 保护/synthetic 剥离 | `tests/OmpSessionCompactingTests.fs` |
| `tool_call` block 能力 | 主会话阻断 child-only 工具（defense-in-depth）；`toolCallHandler` 返回 `{block, reason}` | `src/Omp/SessionLifecycleHooks.fs`，`Kernel.OmpSessionTools.isChildOnlyTool` |
| 每轮工具可见性 | `turn_start` 钩子 → `applyActiveToolFilterForMainSession`（子代理回合后恢复主会话工具集） | `src/Omp/SessionLifecycleHooks.fs`，`SessionLifecycle.fs` |

## 仍缺失（无对等接线）

| 能力 | Opencode/Mimo | OMP 现状 | 验收建议 |
|------|---------------|----------|----------|
| stealth-browser MCP | 插件 `mcp` + AgentConfig | OMP 无 MCP 注入入口；`browser` 检查 `getAllTools`，无宿主时返回错误文本 | 宿主约束：Pi 不暴露等价 client 获取入口 |
| `command.execute.before` 统一入口 | loop-review 等与命令生态 | 有 `/loop`、`loop-review` 命令 + `tool_call` block 覆盖工具级前钩；缺统一命令前链但语义已部分覆盖 | 依赖 Pi 是否暴露统一命令前钩 |

## 弱等价（有实现，语义不完全同）

| 能力 | 差异 |
|------|------|
| `system.transform` | 行为等价：OMP `before_agent_start` + `systemPrompt` 链式替换 = Opencode `chat.system.transform` |
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

1. **P0**：无（核心能力已追平）。
2. **P1**：browser MCP 可选接线（依赖 Pi 暴露 MCP 配置入口或用户 env 自配）。
3. **P2**：`command.execute.before` 统一入口（依赖 Pi 暴露命令前钩）。

## 参考

- compacting：`src/Omp/SessionCompacting.fs`，`tests/OmpSessionCompactingTests.fs`
- HookExecute：`src/Omp/HookExecute.fs`，`tests/OmpHookExecuteTests.fs`
- NudgeRuntime：`src/Omp/NudgeRuntime.fs`，`tests/OmpPluginTestsAgentEnd.fs`
- Opencode 钩子：`src/Opencode/PluginCore.fs` `registerHooks`
- OMP 生命周期：`src/Omp/SessionLifecycle.fs`，`SessionLifecycleHooks.fs`
- 测试：`tests/OmpPluginTests*.fs`，`tests/OmpToolResultEventTests.fs`
- tool_call block + turn_start：`tests/OmpPluginTests.fs`（`toolCallBlocksChildOnlyInMainSession`，`turnStartRestoresMainSessionTools`）
