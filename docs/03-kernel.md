# 03 — Kernel 纯规则层

## 职责

Kernel 承载**宿主无关**的稳定语义：状态机、事件 fold、工具元数据、权限规则、提示词片段、纯解析与纯算法。源码位于 `src/Kernel/`，按子目录与顶层文件组织；文件数量以架构门禁和目录为准，不在文档中维护易失的手工统计。

## 子系统地图

| 簇 | 路径 | 核心内容 |
| :--- | :--- | :--- |
| Review | `ReviewSession/` | `Types`、`StateMachine`、`Registry`、`Query`、`Effects`、`Facade` |
| Nudge | `Nudge/` | 推导（`NudgeDerivation`）、TODO 状态、retry、submit_review hooks |
| EventSourcing | `EventSourcing/` | `EventKind`、`EventEnvelope`、`EventPayload`、`SessionState`、`Fold`、`FoldApply` |
| Subsession | `Subsession/` | `State`、`Command`、`Decision`、`Policy`、`Fold`、`SubsessionProjection`、`TranscriptDecision`、`TypeClassify` |
| Wanxiangzhen | `Wanxiangzhen/` | `Dag`、`SquadEvent`、`Scheduler`、`FfDecision`、`SquadTask`、`SquadTaskTransition`、`SquadUpdateIdAssign`、`SquadConfig`、`SquadPrompts`、`EventLogParse` |
| Fallback | `FallbackKernel/` | `Types`、`Decision`、`Recovery`（完美平方）、`StateMachine` |
| Fallback 续命 | `Fallback/` | `Continuation.fs`（定义租约与续命请求类型） |
| Fallback 辅助 | 顶层 | `FallbackRuntimeFlags`、`FallbackRuntimeLifecycle`、`FallbackSubagentGate` |
| 工具 SSOT | `ToolCatalog/` | `ToolSpec`、`Registry`、`Classification`、分族 FileIO/Web/Search/Subagent/Executor/Review |
| Backlog | `Backlog/`、`WorkBacklog.fs` | 从事件/参数投影待办展示 |
| 消息语义 | `Messaging.fs`、`MessageTransformPolicy.fs`、`ReviewReplayPolicy.fs` | 角色、部件、去重、replay 策略 |
| 子代理元数据 | `SubagentIntents.fs`、`SubagentToolPolicy.fs` | 意图、策略，非 spawn |
| 方法论元数据 | `Methodology/` | `Catalog.fs` 聚合六个领域条目模块；`Registry.fs` 派生 schema 与枚举 |
| 提示词 | `CapsPrelude`、`CapsFormat`、`PromptFragments`、`LoopMessages`、`ReviewPrompts/`、`SearchPrompts`、`SubagentPrompts`、`OmpPrompts` | 宝典/铁律与片段 SSOT |
| 纯算法 | `FuzzyQuery`、`FuzzyPath`、`FuzzyFormat`、`Executor`、`ExecutorStrip`、`TreeSitterKernel`、`Domain`、`Yaml`、`PatchParser`、`CapsSynthPolicy` | 无 IO |
| 宿主命名 / 适配 | `HostTools.fs`、`HostAdapter.fs` | 工具名映射；适配接口供 Runtime 委派 |
| 权限 | `ToolPermission` | 角色 → 工具语义 |
| 其他 | `Config`、`ToolCopy`、`ToolArgs`、`ToolResult`、`ToolOutputInfo`、`ToolOutputInfoTypes`、`ToolOutputInfoParse`、`ToolExecutionStatus`、`ToolContext`、`ToolCatalogParams`、`WebFetchGuard`、`ReviewVerdict`、`WarnTdd`、`FinishReason`、`SessionGateDemand`、`SessionLoop`、`ContextBudget`、`WorkBacklog` | 横切 |

## ReviewSession 状态机（概念）

状态 DU 消除非法组合；转移在 `StateMachine.fs`。典型状态：

- `Inactive` / `Active(task)` / `Locked(task, reviewerId)` / `Accepted` / `NeedsRevision(feedback)`

命令侧：`Activate`、`Submit`、`Lock`、`Unlock`、`Accept`、`RequestRevision` 等。  
**发出的事件种类**与 PRD 表一致，见 [06-review-and-nudge.md](./06-review-and-nudge.md)。

## EventLog Fold（纯函数）

`src/Kernel/EventSourcing/Fold.fs` 是核心折叠引擎，`applyEvent` 将单行事件应用到 `SessionState` 积分：

| 函数/组件 | 输出 |
| :--- | :--- |
| `applyEvent` | 主折叠入口，逐个事件更新全部投影 |
| `foldReviewTask` | 当前 loop task `string option` |
| `foldWorkBacklogSnapshot` | 最新 backlog 快照 |
| `foldNudgeDedup` | 已派发锚点集合等 |
| `foldNudgeSnapshot` | nudge 决策用聚合快照（含 todos、lastAssistantText、reviewLoop、pendingNudge 等） |
| `foldSubagents` | 子代理投影（`childId → SubagentState`） |
| `foldEpisode` / `SessionControl.Projection` | 会话代数、取消代数、continuation episode 与 owner/lease 投影 |
| `foldEventStream` | 通用 fold 骨架 |

`SessionState` 聚合各投影轴，`emptySessionState` 提供零值；旧的 `FallbackInjection` 字段和独立 `FallbackInjectionFold` 不属于当前模型。

Payload 在 Kernel 层多为 `Map<string,string>`（与 Runtime codec 解耦）。

## Subsession 决策引擎

`src/Kernel/Subsession/Decision.fs` 实现 `decide(state, cmd)` 纯函数：

- **10 种状态**：Available、Dispatching、CancellingDispatch、ReconcilingUnknownDispatch、Running、Draining、IssuingAbort、AwaitingAbortSettle、ReconcilingAbortSettle、Poisoned
- **命令**：定义于 `src/Kernel/Subsession/Command.fs`，覆盖 StartRun、dispatch/turn evidence、cancel/deadline、abort/quiescence、physical close 与 SessionClosed
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
- `src/Kernel/FallbackRuntimeFlags.fs`：`FallbackConsumedStatus`、`FallbackSessionGateFlag`
- `src/Kernel/FallbackRuntimeLifecycle.fs`：`FallbackContinueMode`、`FallbackTaskCompletion`
- `src/Kernel/FallbackSubagentGate.fs`：`needFallbackContinue`、`isSubagentSettledFromObservation`

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

`src/Kernel/ToolCatalog/Registry.fs` 的 `all` 列出核心工具 spec（coder、inspector、browser、continue、read、write、swap、fuzzy_*、web*、submit_review、return_reviewer、executor、pty_* 等）。
**description、paramDocs、requiredFields** 为各宿主生成 schema 的 SSOT；宿主层禁止复制一份描述文案（架构测试 guard）。

## WorkBacklog 与 Kernel

`WorkBacklog.fs` 与 `Backlog/` 定义：

- 待办项形状、五份 `completedWorkReport` 字段约束（与 Shell codec 校验衔接）
- 从 committed 事件或参数构造**展示用**结构

真相仍在 NDJSON `work_backlog_committed`，见 [07-work-backlog.md](./07-work-backlog.md)。

## 提示词与宝典

用户可见「Kolmogorov 宝典 / 铁律」类长文本的 SSOT 在 **`src/Kernel/CapsPrelude.fs`**。MessageTransform 只引用 Runtime 缓存组装结果，不在宿主目录复制 caps 正文；行为测试验证最终注入内容。

## 修改 Kernel 的检查清单

1. 是否引入 `Dyn` / `open Runtime` / `DateTime.Now`？→ 禁止
2. 新状态是否用 DU + 穷举匹配？
3. 可预见业务失败是否 `Result` 分支而非异常？
4. 单文件是否逼近 300 行？→ 拆模块
5. 对应 `tests/*Tests.fs` 或架构探针是否更新？

## 源码入口（推荐阅读顺序）

1. `ReviewSession/StateMachine.fs` + `Types.fs`
2. `EventSourcing/Fold.fs` + `EventKind.fs`
3. `Nudge/`（与 `Nudge.fs` 顶层）
4. `Backlog/BacklogProjection.fs` + `WorkBacklog.fs`
5. `ToolCatalog/Registry.fs` + `ToolPermission.fs`
6. `HostTools.fs`
7. `Subsession/Types.fs` + `Decision.fs`（子会话子系统）
