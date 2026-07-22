# 03 — Kernel 纯规则层

## 职责

`src/Kernel/` 承载**宿主无关**的稳定语义：状态机、事件 fold、工具元数据、权限规则、提示词片段、纯解析与纯算法。判定法则：去掉 Node 与宿主 `obj` 后仍成立的逻辑 → Kernel。

## 子系统地图

| 簇 | 路径 | 核心内容 |
| :--- | :--- | :--- |
| ReviewSession | `ReviewSession/` | `Types`（5 状态 DU + 命令）、`StateMachine.fs`（穷举转移）、`Registry`、`Query`、`Effects` |
| Review | `Review/` | `ReviewLoopFold.fs`、`ReviewProjection.fs`、`ReviewReportBuffer.fs`、`ReviewVerdictWire.fs` |
| Nudge | `Nudge/` | `NudgeProjection.fs`、`NudgeSnapshotProjection.fs`、`NudgeSnapshotSource.fs`、`TodoStatus.fs`、`SubmitReviewHooks.fs`、`Types.fs`（8 工作状态 DU） |
| Nudge（顶层） | `Nudge.fs` | `NudgeAction` DU、`ofString`/`toString`、skip 正则 |
| EventSourcing | `EventSourcing/` | `EventKind.fs`（52 个事件 kind 常量）、`EventEnvelope.fs`（`WanEvent` 类型）、`EventPayload.fs`、`SessionState.fs`（28 轴投影）、`Fold.fs`（主折叠引擎）、`FoldApply.fs` |
| Subsession | `Subsession/` | `State.fs`（9 状态 DU）、`Command.fs`（28 命令）、`Decision.fs`（纯函数 `decide`）、`Policy.fs`、`Fold.fs`、`SubsessionProjection.fs`、`TranscriptDecision.fs`、`TypeClassify.fs`、`Abort.fs`、`Cancellation.fs`、`Dispatch.fs` 等 24 文件 |
| Wanxiangzhen | `Wanxiangzhen/` | `Dag.fs`、`SquadEvent.fs`（8 事件 DU + fold）、`Scheduler.fs`、`FfDecision.fs`、`SquadConfig.fs`、`SquadPrompts.fs`、`SquadTask.fs`、`SquadUpdateIdAssign.fs` |
| FallbackKernel | `FallbackKernel/` | `Types.fs`（`SessionFallbackState`、`LeaseStatus`、`SessionOwner` 等）、`Decision.fs`（`classifyError`）、`Recovery.fs`（完美平方启发式）、`StateMachine.fs` |
| Fallback 续命 | `Fallback/Continuation.fs` | `ContinuationRequest`、`ContinuationState`、`ContinuationEvent` 等类型 |
| Fallback 辅助 | 顶层 | `FallbackRuntimeFlags.fs`、`FallbackRuntimeLifecycle.fs`、`FallbackSubagentGate.fs` |
| ToolCatalog | `ToolCatalog/` | `ToolSpec.fs`、`Registry.fs`（`all` 列出核心工具）、`Classification.fs`、`FileIO.fs`、`Subagent.fs`、`Search.fs`、`Web.fs`、`Executor.fs`、`Review.fs` |
| SessionControl | `SessionControl/` | `Event.fs`、`Projection.fs`（续命/compact 投影）、`LeaseTransitions.fs`、`HumanTurn.fs`、`LeaseIdentity.fs`、`State.fs`、`EventOrder.fs` |
| Methodology | `Methodology/` | `Catalog.fs`（聚合 6 个条目模块）、`Registry.fs`（enum 派生）、`Schema.fs`、`Api.fs`、`Logic.fs`、`MathematicalReasoning.fs`、`Optimization.fs`、`SystemsEngineering.fs`、`CriticalInquiry.fs`、`ProblemTransformation.fs` |
| 提示词 | `CapsPrelude.fs`、`CapsSynthPolicy.fs`、`PromptFragments.fs`、`LoopMessages.fs`、`ReviewPrompts/` | 宝典/铁律 SSOT |
| HostTools | `HostTools.fs` | `Host` DU、工具名映射（`normalizeToolNameForMux` 等） |
| 权限 | `ToolPermission.fs` | 角色 → 工具语义规则矩阵 |
| Subagent 元数据 | `SubagentIntents.fs`、`SubagentToolPolicy.fs` | 意图、策略（非 spawn 本身） |
| 纯算法 | `FuzzyQuery.fs`、`FuzzyPath.fs`、`FuzzyFormat.fs`、`ExecutorStrip.fs`、`TreeSitterKernel.fs`、`PatchParser.fs`、`Yaml.fs` | 无 IO |
| 横切 | `Config.fs`、`ToolArgs.fs`、`ToolResult.fs`、`ToolContext.fs`、`Messaging.fs`、`ReviewVerdict.fs` | 通用类型 |

## ReviewSession 状态机

```fsharp
type ReviewState =
    | Inactive
    | Active of task: string
    | Locked of task: string * reviewerId: string
    | Accepted
    | NeedsRevision of feedback: string
```

转移在 `StateMachine.fs`，穷举匹配。命令：`Activate`、`Submit`、`Lock`、`Unlock`、`Accept`、`RequestRevision`。

## EventSourcing Fold

`SessionState` 是 28 轴复合投影，`applyEvent` 是主折叠入口。各轴独立维护：`ReviewLoopFold`、`NudgeDedup`/`NudgeSnapshot`、`SubsessionProjection`、`HumanTurn`、`Projection`（续命/compact 状态机）。`emptySessionState` 提供零值。

## Subsession 决策引擎

- 9 种状态：`Available` → `Dispatching` → `Running` → `Draining` / `IssuingAbort` → `AwaitingAbortSettle` → `ReconcilingAbortSettle` → `Poisoned`
- 28 种命令（`Command.fs`）
- `decide(nowMs, state, cmd)` 纯函数（`Decision.fs`）
- 恢复协议：`SubsessionReconcile.reconcile` 在重启时扫描未完成 run → 原子持久化 `SessionPoisoned` + `TurnFinished` + `RunFinished`

## 修改 Kernel 的检查清单

1. 是否引入 `Dyn` / `open Runtime` / `DateTime.Now`？→ 禁止
2. 新状态是否用 DU + 穷举匹配？
3. 可预见业务失败是否 `Result` 分支而非异常？
4. 单文件是否逼近 300 行？→ 拆模块
5. 对应 `tests/*Tests.fs` 是否更新？
