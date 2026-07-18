# 03 — Kernel 纯规则层

## 职责

Kernel 承载**宿主无关**的稳定语义：状态机、事件 fold、工具元数据、权限规则、提示词片段、纯解析与纯算法。模块约 **92** 个 `.fs` 文件，按子目录与顶层文件组织。

## 子系统地图

| 簇 | 路径 | 核心内容 |
| :--- | :--- | :--- |
| Review | `ReviewSession/` | `Types`、`StateMachine`、`Registry`、`Query`、`Effects`、`Facade` |
| Nudge | `Nudge/` | 推导（`NudgeDerivation`）、TODO 状态、retry、submit_review hooks |
| EventLog | `EventLog/` | `Types`（全部 kind 常量）、`Fold`（`foldNudgeSnapshot`、`ownerAndLeaseFolder` 等）、`FallbackInjectionFold`、`ReviewLoopFold`、`ReviewVerdictWire` |
| Subsession | `Subsession/` | `Types`（9 种 SubsessionState）、`Decision`（~873 行纯决策函数）、`Policy`（Fallback 策略）、`Fold`（`SessionSafetyProjection`）、`TranscriptDecision`、`PartTypeClassify` |
| Wanxiangzhen | `Wanxiangzhen/` | `Dag`、`SquadEvent`、`Scheduler`、`FfDecision`、`SquadTask`、`SquadTaskTransition`、`SquadUpdateIdAssign`、`SquadConfig`、`SquadPrompts`、`EventLogParse` |
| Fallback | `FallbackKernel/` | `Types`、`Decision`、`Recovery`（完美平方）、`StateMachine` |
| Fallback 辅助 | 顶层 | `FallbackRuntimeFlags`、`FallbackRuntimeLifecycle`、`FallbackSubagentGate` |
| 工具 SSOT | `ToolCatalog/` | `ToolSpec`、`Registry`、`Classification`、分族 FileIO/Web/Search/Subagent/Executor/Review |
| Backlog | `BacklogProjectionCore`、`BacklogProjection`、`WorkBacklog` | 从事件/参数投影待办展示 |
| 消息语义 | `Messaging`、`Message`、`MessageTransformPolicy`、`ReviewReplayPolicy` | 角色、部件、去重、replay 策略 |
| 子代理元数据 | `Subagent`、`SubagentIntents`、`SubagentToolPolicy` | 意图、策略，非 spawn |
| 方法论元数据 | `Methodology/`、`MethodologyCatalog` | `select_methodology` 枚举与 todowrite 文案 |
| 提示词 | `CapsPrelude`、`CapsFormat`、`PromptFragments`、`LoopMessages`、`ReviewPrompts/`、`SearchPrompts`、`SubagentPrompts`、`OmpPrompts` | 宝典/铁律与片段 SSOT |
| 纯算法 | `FuzzyQuery`、`FuzzyPath`、`FuzzyFormat`、`Executor`、`ExecutorStrip`、`TreeSitterKernel`、`Domain`、`Yaml`、`PatchParser`、`CapsSynthPolicy` | 无 IO |
| 宿主命名 / 适配 | `HostTools`、`HostAdapter` | 工具名映射；`IHostAdapter` 供 `SubagentDispatcher` |
| 权限 | `ToolPermission` | 角色 → 工具语义 |
| 其他 | `Config`、`ToolCopy`、`ToolArgs`、`ToolResult`、`ToolOutputInfo`、`ToolOutputInfoTypes`、`ToolOutputInfoParse`、`ToolExecutionStatus`、`ToolContext`、`ToolCatalogParams`、`WebFetchGuard`、`ReviewVerdict`、`WarnTdd`、`FinishReason`、`SessionGateDemand`、`SessionLoop`、`ContextBudget`、`WorkBacklog` | 横切 |

## ReviewSession 状态机（概念）

状态 DU 消除非法组合；转移在 `StateMachine.fs`。典型状态：

- `Inactive` / `Active(task)` / `Locked(task, reviewerId)` / `Accepted` / `NeedsRevision(feedback)`

命令侧：`Activate`、`Submit`、`Lock`、`Unlock`、`Accept`、`RequestRevision` 等。  
**发出的事件种类**与 PRD 表一致，见 [06-review-and-nudge.md](./06-review-and-nudge.md)。

## EventLog Fold（纯函数）

`Kernel/EventLog/Fold.fs` 是核心折叠引擎，`applyEvent` 将单行事件应用到 `SessionState` 积分：

| 函数/组件 | 输出 |
| :--- | :--- |
| `applyEvent` | 主折叠入口，逐个事件更新全部投影 |
| `foldReviewTask` | 当前 loop task `string option` |
| `foldWorkBacklogSnapshot` | 最新 backlog 快照 |
| `foldNudgeDedup` | 已派发锚点集合等 |
| `foldNudgeSnapshot` | nudge 决策用聚合快照（含 todos、lastAssistantText、reviewLoop、pendingNudge 等） |
| `foldSubagents` | 子代理投影（`childId → SubagentState`） |
| `foldFallbackInjection` | 降级注入状态 |
| `ownerAndLeaseFolder` | 会话拥有者、续命租约、nudge 租约、compaction 状态 |
| `foldEventStream` | 通用 fold 骨架 |

`SessionState` 聚合所有投影（EventCount 等 30+ 字段），`emptySessionState` 提供零值。

Payload 在 Kernel 层多为 `Map<string,string>`（与 Shell codec 解耦）。

## Subsession 决策引擎

`Kernel/Subsession/Decision.fs` 实现 `decide(state, cmd)` 纯函数（~873 行，约 200+ 状态-命令匹配对）：

- **9 种状态**：Available、Dispatching、CancellingDispatch、ReconcilingUnknownDispatch、Running、Draining、IssuingAbort、AwaitingAbortSettle、ReconcilingAbortSettle、Poisoned
- **12 种命令**：StartRun、DispatchAccepted、DispatchRejected、TurnErrorObserved、SessionIdleObserved、EvidenceUpdated、DispatchStatusResolved、CancelRequested、TurnDeadlineExpired、AbortDeadlineExpired、AbortConfirmed、AbortHostAccepted、AbortRequestFailed、SessionClosed
- **决策结果**：`Decided { NextState; Events; Effects }` 或 `NoChange(reason)`

详见 [11-subagents.md](./11-subagents.md) § SubsessionActor。

## FallbackKernel

| 模块 | 职责 |
| :--- | :--- |
| `Types.fs` | `FallbackModel`、`FallbackChain`、`FallbackConfig`、`ErrorInput`、`ErrorClass`、`FallbackPhase`、`FallbackAction`、`FallbackLifecycle`、`SessionFallbackState`、`FallbackEvent` |
| `Decision.fs` | `classifyError`：Abort→Ignore、401-403→ImmediateFallback、429/5xx→RetrySame、Exhausted |
| `Recovery.fs` | 完美平方启发式 |
| `StateMachine.fs` | `transition(state, evt, cfg, chain)` → `(newState, action)` |

辅助模块：
- `Kernel/FallbackRuntimeFlags.fs`：`FallbackConsumedStatus`、`FallbackSessionGateFlag`
- `Kernel/FallbackRuntimeLifecycle.fs`：`FallbackContinueMode`、`FallbackTaskCompletion`
- `Kernel/FallbackSubagentGate.fs`：`needFallbackContinue`、`isSubagentSettledFromObservation`

详见 [12-fallback.md](./12-fallback.md)。

## Nudge 决策

`Kernel/Nudge/`：

| 模块 | 职责 |
| :--- | :--- |
| `Nudge.fs` | `NudgeAction` DU、`skipsTodo`/`skipsReview` 抑制标记检测 |
| `NudgeDerivation.fs` | `deriveAction` 从 `SessionSnapshot` 推导 action、`selectNudgePrompt` |
| `NudgeSnapshotSource.fs` | `NudgeSnapshotSource` 类型、`workStateFromSource` |
| `TodoStatus.fs` | `TodoStatus` DU、`isTerminal`、`isSyntheticAssistantAgent` |
| `SubmitReviewHooks.fs` | `isSubmitReviewWipProgressOutput` |
| `RetryProgress.fs` | `isRetryProgressEvent`、`isRetryProgressPart` |
| `Types.fs` | `SessionWorkState`（7 轴）、`NudgeBlockStatus`、`SessionSnapshot`、`SendOutcome` |

详见 [06-review-and-nudge.md](./06-review-and-nudge.md) § Nudge。

## ToolCatalog

`ToolCatalog.Registry.all` 列出核心工具 spec（coder、inspector、read、write、fuzzy_*、web*、submit_review、return_reviewer、executor*、continue、pty_* 等）。
**description、paramDocs、requiredFields** 为各宿主生成 schema 的 SSOT；宿主层禁止复制一份描述文案（架构测试 guard）。

## WorkBacklog 与 Kernel

`WorkBacklog` / `BacklogProjectionCore` 定义：

- 待办项形状、五份 `completedWorkReport` 字段约束（与 Shell codec 校验衔接）
- 从 committed 事件或参数构造**展示用**结构

真相仍在 NDJSON `work_backlog_committed`，见 [07-work-backlog.md](./07-work-backlog.md)。

## 提示词与宝典

用户可见「Kolmogorov 宝典 / 铁律」类长文本的 SSOT 在 **`CapsPrelude`**（及 `CapsFormat` 组装）。MessageTransform 只**引用** Shell 缓存组装结果，不在宿主目录复制 caps 正文；行为测试验证最终注入内容。

## 修改 Kernel 的检查清单

1. 是否引入 `Dyn` / `open Shell` / `DateTime.Now`？→ 禁止
2. 新状态是否用 DU + 穷举匹配？
3. 可预见业务失败是否 `Result` 分支而非异常？
4. 单文件是否逼近 300 行？→ 拆模块
5. 对应 `tests/*Tests.fs` 或架构探针是否更新？

## 源码入口（推荐阅读顺序）

1. `ReviewSession/StateMachine.fs` + `Types.fs`
2. `EventLog/Fold.fs` + `Types.fs`
3. `Nudge/`（与 `Nudge.fs` 顶层）
4. `BacklogProjectionCore.fs`
5. `ToolCatalog/Registry.fs` + `ToolPermission.fs`
6. `HostTools.fs`
7. `Subsession/Types.fs` + `Decision.fs`（子会话子系统）
