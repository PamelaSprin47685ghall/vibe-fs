# 02 — 系统架构

## 三层模型

```text
┌──────────────────────────────────────────────────────────────────┐
│  Host Adapters (volatile)                                         │
│  Opencode/  Mimocode/  Mux/  Omp/                                  │
│  — hook 注册、schema 生成、宿主对象原地写字段纪律                   │
│  — SubsessionHostAdapter（ISubsessionHost 实现）                    │
└──────────────────────────────┬───────────────────────────────────┘
                               │ obj ↔ codec
┌──────────────────────────────▼───────────────────────────────────┐
│  Shell (side effects)                                             │
│  FS / 网络 / 子进程 / MCP / EventLog / MessageTransform           │
│  ToolExecute / SubagentSpawn / RuntimeScope                       │
│  SubsessionActor / SubsessionService / SubsessionEventRouter       │
│  FallbackRuntimeState / FallbackEventBridge / NudgeRuntime         │
└──────────────────────────────┬───────────────────────────────────┘
                               │ 强类型命令/事件
┌──────────────────────────────▼───────────────────────────────────┐
│  Kernel (pure rules)                                              │
│  ReviewSession / Nudge / EventLog.Fold / WorkBacklog              │
│  Subsession/（Decision、Types、Policy、Fold、TranscriptDecision）  │
│  FallbackKernel/（StateMachine、Decision、Recovery）               │
│  ToolCatalog / ToolPermission / Methodology 元数据                │
└──────────────────────────────────────────────────────────────────┘
```

**判定法则**：去掉 Node 与宿主 `obj` 后仍成立的逻辑 → Kernel；否则 → Shell 或 Host。

## 模块依赖纪律（架构测试强制执行）

| 规则 | 探针示例 |
| :--- | :--- |
| Kernel 规则可直接执行 | Kernel 单元测试与编译器边界 |
| 宿主边界稳定 | 宿主行为与 codec 契约测试 |
| Nudge loop 态必须事件 fold | Nudge 事件溯源行为测试 |
| Hook output 经 codec | Hook 输入输出契约测试 |
| Subsession 状态机纯函数 | Kernel/Subsession 单元测试 |

完整列表见 [17-build-test-verify.md](./17-build-test-verify.md)。

## 公开 JavaScript 入口

| 入口文件 | npm 路径 | 宿主 |
| :--- | :--- | :--- |
| `src/Mux/Plugin.fs` | `wanxiangshu` → `.` | Mux（默认 main） |
| `src/Omp/Plugin.fs` | `wanxiangshu/omp` | oh-my-pi |
| `src/Opencode/Plugin.fs` | （包内构建产物） | OpenCode |
| `src/Opencode/PluginMimo.fs` | （包内构建产物） | Mimocode |
| `src/Opencode/PluginMimoTui.fs` | TUI 辅助 | Mimocode sidebar todo |
| `src/Opencode/PluginWanxiangzhen.fs` | `wanxiangshu/wanxiangzhen` | 万象阵 |

共享装配逻辑：**OpenCode 系** → `Opencode/PluginCore.fs`；**OMP** → `Omp/PluginCore.fs`。

## Host 枚举与工具命名

`Kernel.HostTools.Host` = `Opencode | Mimocode | Mux | Omp`。

同一概念在不同宿主上的**工具名**可能不同，例如：

- 待办写入：OpenCode/Mux/OMP → `todowrite`；Mimocode → `task`
- 子代理任务工具：Mimocode 侧 `actor` 映射为 canonical `task`

`normalizeToolName` / `normalizeToolNameForMux` 在权限分类前统一 canonical 名。

## 可变状态安放

- **允许**：`Shell.RuntimeScope` 派生实例（iterator store、scope 级队列等）
- **允许**：`Shell.SubsessionActorRegistry` 模块级 actor 注册表
- **允许**：`Shell.FallbackRuntimeState` 每个 session 的可变状态
- **禁止**：Kernel 模块级可变；跨 session 裸全局（架构测试 `noDuplicateStateHolder`）
- **事件日志**：`EventLogStore` 进程内缓存 = fold 投影，**非**第二 SSOT；磁盘 NDJSON 为先

## 数据平面 vs 控制平面

| 平面 | 内容 |
| :--- | :--- |
| 控制平面 | 命令校验、FSM 转移、是否 append 事件 |
| 数据平面 | NDJSON 行、git（万象阵）、宿主 message 数组 |
| 展示平面 | YAML front-matter、caps prelude、Magic todo UI |

展示平面**不得**作为 review/todo/Subsession 的 SSOT（见 `05-event-sourcing`）。

## 子系统概要

### Subsession Actor（子会话隔离）

子代理（Coder、Investigator、Browser、Meditator）通过 `SubsessionActor` 轻量 Actor 消息泵运行，提供完整的错误隔离、降级恢复和超时保护。每个子会话拥有独立的 `SerialQueue`、Fallback 状态机、事件溯源和 NDJSON 持久化。

详见 [11-subagents.md](./11-subagents.md) § SubsessionActor。

### Fallback 运行时（模型降级）

`FallbackEventBridge` 编排模型降级全流程：宿主事件翻译 → FSM 转移 → 续命六阶段生命周期 → 门闩防并发。`FallbackRuntimeState` 维护每个 session 的降级状态、续命租约、门闩标志。

详见 [12-fallback.md](./12-fallback.md)。

### Nudge 运行时

`NudgeRuntime` 通过 `tryClaimNudgeDispatch` 在 `EventLogStore` 锁内执行原子 Claim，防止多路并发重复派发 nudge。`NudgeSnapshotState` 从事件流 fold 出决策所需快照。

详见 [06-review-and-nudge.md](./06-review-and-nudge.md) § Nudge。

## 演进路线（类型安全与去重）

已识别问题与分阶段目标：

| 问题 | 状态 | 实现在 |
| :--- | :--- | :--- |
| 宿主 `obj` 渗入内核 | 已落地 | Shell DTO + 边界 decode |
| 内存与盘双写风险 | 已落地 | 突变仅经 EventLog append，投影只读 fold |
| 三宿主重复 spawn/fuzzy | 已落地 | spawn 用 `IHostAdapter` + `SubagentDispatcher`；fuzzy 分 Kernel 规则 + Shell 后端 |
| 魔法字符串错误 | 已落地 | `DomainError` DU |
| 巨型 SessionLifecycleObserver | 已落地 | 拆为 Progress / Fallback / Nudge 观察片 |
| 子代理错误隔离 | 已落地 | `SubsessionActor` 消息泵 + `SubsessionState` 9 种状态 |
| ContinuationLease 原子 claim | 已落地 | `PendingLease` + `LeaseStatus` ADT + `TryTransitionPendingLease` 原子门闩 |
| Nudge 并发去重 | 已落地 | `tryClaimNudgeDispatch` 事件级 Claim |
| Compaction 事件溯源 | 已落地 | `compaction_*` 事件 + `ownerAndLeaseFolder` + `ReplayCompactionState` |
| 上下文预算 R 参数 | 已落地 | 动态防过度触发，`classifyPressure` 公式 |
| 控制字段软合规 | 已落地 | 三层防线（schema 软提示 → before 原地删除 → after 批评+还原） |
| TransformState 三段缓存 | 已落地 | `MessageTransformStack` 管理 Caps/Backlog/Top slot 引用，revision/key 驱动 |
| SessionEpoch/CausalityContext/TurnIdentity | 已落地 | `Kernel/Domain.fs` 类型定义 + `matchesCausality` 过滤 |
| Investigator CAPS 注入 | 已落地 | `CapsInjectionPolicy` 独立于 backlog projection |
| 并行工具提示 | 已落地 | `ParallelHintPolicy` 独立，单工具调用后稳定注入 |
| 首轮 context budget 误触 | 已落地 | `UsageConfidence` 限制，首次观测只校准不触发 |
| Warn 字段 after 还原 | 已落地 | 三宿主 after hook 调用 `restoreWarnToArgs` |
| Per-session serial mailbox | 已落地 | `SerialQueue` + `FallbackEventBridge.createHandler` 每 session 独立队列 |
| 取消粘性 + CancelEpisode | 已落地 | `FallbackRuntimeState.UpdateState` 在 Cancelled 时清除所有门禁 |
| Subsession dispatch/reconcile | 已落地 | `Dispatching → Running → Draining → ReconcilingUnknownDispatch → ReconcilingAbortSettle` 状态机 |
| UserAbortObserved 事件 | 已落地 | `user_abort_observed` 事件 + `eventKindUserAbortObserved` fold |
| 事件日志 NDJSON 前缀完整性 | 已落地 | `EventLogFiles` 首行损坏截断、快照指纹校验 |
| NudgeBlockStatus 门禁 | 已落地 | `Kernel/Nudge/Types.fs` `NudgeBlockStatus` (Blocked/Allowed) |

### 设计文档映射

PRD-00 描述的 Flow-first 架构以以下等价形式实现，而非字面 `IAsyncEnumerable`/`Channel`/`scanCommit`：

| PRD 概念 | 实现等价物 |
| :--- | :--- |
| `Channel<SessionInput>` | `SerialQueue` per-session mailbox + `FallbackEventBridge.createHandler` |
| `scanCommit` | `SerialQueue.Enqueue` → `handleEvent` → `appendEvent` → `UpdateState` |
| `Effect Flow` | `executeContinuationIntent` 在队列外 fire-and-forget，结果通过 `Post` 回队列 |
| `IAsyncEnumerable` | JS `Promise` chain + `SerialQueue` |
| `HumanTurnProjection` | `SessionState.LatestHumanTurn` + `humanTurnFolder` |
| `CancellationProjection` | `SessionState.CancelGeneration` + `user_abort_observed` fold |
| `ContinuationProjection` | `SessionState.PendingLease` + `ownerAndLeaseFolder` |
| `CompactionProjection` | `SessionState.ActiveCompaction` + `ReplayCompactionState` |
| `ContextBudgetProjection` | `ContextBudgetStore` 内存状态 + `classifyPressure` |
| `ReviewProjection` | `ReviewLoopFold` + `SessionState.ReviewTask` |

迁移策略：分阶段、每步 `npm run build-and-test` 全绿；禁止大爆炸重写。当前真相以 **四套宿主目录 + 行为/契约测试** 为准。

## 相关文档

- Kernel 模块族：[03-kernel.md](./03-kernel.md)
- Shell 边界：[04-shell.md](./04-shell.md)
- SSOT 总表：[18-glossary-and-ssot-map.md](./18-glossary-and-ssot-map.md)