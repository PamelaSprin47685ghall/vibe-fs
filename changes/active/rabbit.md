> **状态**：Active — 本文件为变更工作记录，不是当前产品规范。当前产品语义仅以 `docs/` 正式层为准。
> 原始 Proposal 已冻结于下方；后续事实仅追加于 Active work / Blockers / Final outcome。

# Proposal G4R-CE — Semantic CE Vocabulary

## 将军赶路：把生产程序重写成自说明的 F# 语义语言

**Status:** Proposed
**Priority:** P0 / G4R prerequisite
**Scope:** CE DSL / Manager / Reviewer / Finality / Fallback / Recovery / Reconciliation / Host composition / Temporal proof
**Depends on:** G4R One Physical World proposal
**Blocks:** G4 Exit

---

# 0. Executive Decision

本 Change 不发明第二套 workflow framework。

不造 AST。

不造 interpreter。

不造 `ReliableFlowBuilder` 黑盒。

不把程序重新压成几十个 `Decision` case。

我们保留 F# 自己作为 DSL：

```fsharp
task {
    let! ...
    do! ...
    match! ...
    return! ...
}
```

但增加一层正式设计纪律：

> **复杂时序可以隐藏；隐藏之前必须先获得一个足够准确的语义名字。**

因此生产代码最终分成四种东西：

```text
Business CE
    讲故事

Semantic Vocabulary
    给复杂时序一个领域名字

Port Decorator
    给一次能力逐层增加 observation / normalization / physical policy

Physical Adapter
    真的碰 OpenCode / Git / process / timer
```

最终原则：

> **CE 负责故事；Vocabulary 负责定理；Decorator 负责能力；Port 负责物理。**

以及：

> **任何聪明都必须有名字；任何名字都必须有 law。**

---

# 1. 当前代码的具体病灶

## 1.1 `TurnCompletionProgram.fs` 已经成为多 bounded-context 混合层

当前：

```text
Application/Reconciliation/TurnCompletionProgram.fs
```

同时拥有：

```text
missing-final-report repair
interaction repair
ProviderRetry suppression
coverage-before-retry
fallback advance
loop-kill bridge
abort
terminal materialisation
join guard
ordinary completion
```

更严重的是 `awaitCoverageBeforeRetry` 目前仍直接：

```fsharp
let budgetMs = 2000
let sliceMs = 25
let deadline = DateTimeOffset.UtcNow.AddMilliseconds(...)

...
Promise.race(journalChange, sliceTimer 25)
...
DateTimeOffset.UtcNow >= deadline
```

也就是一个业务恢复语义仍然由 **25ms polling + wall clock** 推动。

这必须拆掉。

---

## 1.2 `FinalityController.fs` 在错误的层，而且承担过多语义

当前：

```text
Infrastructure/OpenCode/Tools/FinalityController.fs
```

拥有：

```text
FinalityOutcome
cohort enlistment
reviewer race
cancellation
record readiness
REVISE short circuit
sibling steering
blessing
tree validation
durable resume
```

其自己已经描述：一个 FinalityRequest 会 enlist 历史 reviewer + 新 reviewer，任一 REVISE 立即关闭，全员 dual-PERFECT 后才 blessing。

这显然不是 “OpenCode tool adapter”。

这是完整 Application workflow。

当前还在内部实现通用并发 race：

```text
concurrentAllOrShortCircuit
TaskCompletionSource
ResizeArray
ref remaining
ref shortCircuitWinner
```

最终这里必须只剩 Tool adapter。

---

## 1.3 `HostSignalBootstrap` 仍在做 workflow multiplexing

当前 composition root：

```text
SyncDelegate HandleTurn → bool
ReviewerWorkflow → reviewerHandled bool
ManagerWorkflow → managerHandled bool
否则 TurnCompletionProgram
```

这意味着 Host composition 知道：

```text
谁是 Reviewer
谁是 Manager
哪个 workflow 可以消费 terminal
谁 fallback 到 generic program
```

这些知识应该压进一个明确的：

```text
TurnWorkflow.observe
```

Host 只负责：

```text
wire
→ reconcile
→ prepare physical runtime
→ hand observation to Application
```

---

## 1.4 physical single-flight 仍然待在 Application

`SessionRecoveryWorkflow.Coordinator` 当前拥有：

```fsharp
gate
Dictionary<string, Task<FamilyRecovery>>
lock
inflight
```

但正式 DSL ownership 已经规定：

```text
Application = CE workflow
Session     = runtime single-flight / physical resource ownership
```

因此 Coordinator 必须下沉 Session。

---

## 1.5 时间还没有成为统一 capability

例如 Child Recovery 直接：

```fsharp
HandleController.recordAbandon ... DateTimeOffset.UtcNow
```

这会让 temporal proof 无法完全掌控输入。

必须消灭。

---

# 2. 目标源码长什么样

最终一个业务文件应该接近：

```fsharp
let rec runManagerLife env life =
    task {
        do! ManagerBackground.awaitSettled env life

        let! activation =
            ManagerActivation.ensureAccepted env life

        let! work =
            ManagerLabor.performResiliently env activation

        let! judgement =
            ReviewerCohort.reviewUntilSettled env work

        match judgement with
        | Revision feedback ->
            let! revised = ManagerLabor.revise env feedback
            return! runManagerLife env revised.Life

        | Confirmed witness ->
            return!
                ManagerFinality.finalizeWhenSafe
                    env
                    life
                    witness
    }
```

注意：

这里：

```text
awaitSettled
ensureAccepted
performResiliently
reviewUntilSettled
finalizeWhenSafe
```

内部都可以非常复杂。

可以递归。

可以 race。

可以 durable recovery。

可以 exactly-once claim。

可以 fallback。

可以 virtual deadline。

**调用点不追这些小兔。**

因为名字已经声明了承诺。

---

# 3. 新正式条款

修改：

```text
docs/what/dsl-structured-program.md
```

新增：

## Target DSL-013 — Semantic Vocabulary

定义：

> 业务 CE 可以调用内部包含复杂时序的具名 Vocabulary。Vocabulary 的名字必须描述完整业务承诺，而不是实现动作。

允许：

```text
reviewUntilPerfect
publishEventually
recoverDurably
awaitChildrenSettled
finalizeWhenSafe
fallbackAcross
```

拒绝：

```text
executeSafe
process
handle
doRetry
runReliable
withPolicy
continue2
```

判据：

```text
只看调用点名字 + 参数 + 返回类型，
reviewer 是否能够合理知道调用者在等待什么语义？
```

不能：

REVISE。

---

## Target DSL-014 — Semantic Compression

> 已被独立 proof 完整覆盖的机械时序允许被 Vocabulary 压缩。

也就是说：

```fsharp
let! result = publishEventually ...
```

可以隐藏：

```text
read head
rebase
review
CAS
target moved
rebase again
review again
...
```

但：

```text
publishEventually
```

必须拥有自己的 proof。

---

## Target DSL-015 — Decorator Boundary

Decorator 分两类。

### Transparent Decorator

不改变业务 trace 集：

```text
diagnostics
metrics
causal observation
protocol normalization
exception normalization
```

可以自由叠加。

### Semantic Decorator

改变业务 trace 集：

```text
retry
fallback
recovery
dedupe
claim
deadline policy
```

必须满足以下之一：

```text
A. 自身已经是有正式 law 的 Semantic Vocabulary；
B. 在业务 CE 调用点拥有明确语义名字。
```

禁止匿名 middleware 魔法。

---

# 4. Slice 0 — RED + Formal Docs

在写 production 代码前先落：

```text
changes/active/g4r-semantic-ce-vocabulary.md
```

并修改：

```text
docs/what/dsl-structured-program.md
docs/shape/dsl-structured-program.md
docs/how/dsl-structured-program.md
docs/proof/dsl-structured-program.md

docs/what/flow.md
docs/how/flow.md
docs/shape/flow.md
docs/proof/flow.md
```

`how` 中把当前：

```text
Evidence
→ Decision
→ exhaustive match
→ effect
```

降级成一种可用形式，而不是唯一理想形式。

正式推荐改成：

```text
typed evidence / capability
→ semantic vocabulary
→ CE bind / recursion / higher-order composition
→ effect
```

现有文档本来已经允许用有界递归表达 retry，并禁止递归参数保存 Stage。

我们只是把它提升成主设计法。

---

# 5. Slice 1 — Universal Temporal Capability

## 5.1 新文件

新增：

```text
src/Wanxiangshu/Kernel/Temporal.fs
```

只定义 capability contract：

```fsharp
type IDeadlineHandle =
    abstract Delay: Task<unit>
    abstract Cancel: unit -> unit

type ITimerPort =
    abstract Delay: milliseconds:int -> IDeadlineHandle
    abstract Dispose: unit -> unit

type IClockPort =
    abstract UtcNow: unit -> DateTimeOffset
```

这里**没有** Node timer。

没有 `setTimeout`。

没有 Fable JS。

---

## 5.2 修改 `Process/PtyTiming.fs`

当前其中同时定义 interface 和 Node implementation。

改为：

```text
Kernel/Temporal.fs
    contract

Process/PtyTiming.fs
    nodeTimerPort
    nodeClockPort
    virtualTimerPort
    virtualClockPort
```

保留兼容 module function 可以一轮过渡，但 Exit 前删除重复 contract。

---

## 5.3 扩充 `Session/CausalAwait.fs`

现有 `awaitTask / awaitUnit / race` 保留。

新增一个机制 Vocabulary：

```fsharp
CausalAwait.untilSignalOrDeadline
```

形状：

```fsharp
untilSignalOrDeadline
    observer
    descriptor
    deadline
    tryRead
    awaitSignal
```

语义：

```text
先 tryRead
成功 → return

否则等待：
    real signal
    OR
    one deadline

signal → 重读
deadline → DeadlineExpired
```

重要：

```text
只有一个 deadline handle
没有 slice timer
没有 polling interval
没有 UtcNow loop
```

---

# 6. Slice 2 — 拆除 `TurnCompletionProgram`

目标：

```text
DELETE
src/Wanxiangshu/Application/Reconciliation/TurnCompletionProgram.fs
```

不是缩小。

是删除。

---

## 6.1 新 `TerminalReporter.fs`

新增：

```text
Application/Reconciliation/TerminalReporter.fs
```

迁入当前：

```text
completeAgent
```

职责只有：

```text
ReconciledTurn
→ AgentRunResult
→ XTrace capture
→ NotifyTerminal
```

API：

```fsharp
TerminalReporter.complete
```

不允许引用：

```text
Fallback
Manager
Reviewer
Finality
JoinGuard
IdleRepair
LoopSensor
```

---

## 6.2 新 `InteractionRepairWorkflow.fs`

新增：

```text
Application/Reconciliation/InteractionRepairWorkflow.fs
```

迁入：

```text
isRecoveryContinue
sendRepair
trySendIdleRepair
```

公开 Vocabulary：

```fsharp
repairMissingFinalReport
repairIncompleteInteraction
```

调用点应该读成：

```fsharp
return!
    InteractionRepairWorkflow.repairMissingFinalReport
        env
        context
```

而不是：

```fsharp
trySendIdleRepair ... "missing-final-report"
```

字符串：

```text
"missing-final-report"
"interaction-repair"
```

不得继续承担业务语义。

---

## 6.3 新 `ProviderRecoveryWorkflow.fs`

新增：

```text
Application/Recovery/ProviderRecoveryWorkflow.fs
```

迁入：

```text
sessionHasCoverage
expectsCoverage
awaitCoverageBeforeRetry
continueAfterProviderFailure
continueAfterLoopKill
continueAfterOrdinaryFailure
```

重命名 Vocabulary：

```fsharp
awaitRecoveryMaterial
continueAfterConfirmedFailure
continueAfterLoopKill
```

其中：

```text
awaitCoverageBeforeRetry
```

彻底删除。

替代：

```fsharp
awaitRecoveryMaterial
```

实现必须：

```text
journal predicate
+ journal change signal
+ one injectable deadline
```

零 25ms slice。

零 `UtcNow`.

---

## 6.4 新 `OrdinaryTurnWorkflow.fs`

新增：

```text
Application/Reconciliation/OrdinaryTurnWorkflow.fs
```

它拥有普通：

```text
TurnUnknown
TurnInProgress
TurnNeedsContinuation
TurnAborted
TurnFailed
TurnCompleted
```

的业务语言。

目标：

```fsharp
let observe env context =
    task {
        match context.Turn.Observation, context.Turn.Outcome with
        | Some TurnUnknown, _ ->
            return!
                InteractionRepairWorkflow.repairMissingFinalReport env context

        | _, TurnFailed error ->
            return!
                ProviderRecoveryWorkflow.continueAfterConfirmedFailure
                    env
                    context
                    error

        | _, TurnAborted reason ->
            return!
                AbortWorkflow.conclude env context reason

        | _, TurnCompleted ->
            return!
                TerminalReporter.complete env context.Turn

        ...
    }
```

这一个 match 合法。

因为：

```text
TurnOutcome
```

是世界真实结果，不是 program counter。

---

## 6.5 新 `TurnWorkflow.fs`

新增：

```text
Application/Reconciliation/TurnWorkflow.fs
```

这成为 reconciled turn 的唯一 Application 入口：

```fsharp
TurnWorkflow.observe
```

内部：

```text
SyncDelegate-owned
Reviewer
Manager
Ordinary
```

按 bounded context 委派。

Host 不再认识三个 `handled bool`。

---

# 7. Slice 3 — Reconciler 退回“观测稳定器”

修改：

```text
Domain/ReconcileProgram.fs
Application/Reconciliation/Reconciler.fs
```

当前 `ReconcileDecision` 中：

```text
RepairMissingFinalReport
```

删除。

因为这是业务效果名字。

替换成纯 observation vocabulary，例如：

```fsharp
type ReconcileDecision =
    | Reread of ...
    | PublishStableObservation
    | StopPass
```

或者统一：

```text
Publish
```

使 `ReconcileProgram` 只回答：

```text
这个 observation 是否足够稳定，可以交给业务？
```

不回答：

```text
业务应该发哪个 repair prompt？
```

当前 `Reconciler` 甚至有注释说明 `RepairMissingFinalReport` 发布 Unknown turn 后交给 TurnCompletion 执行 repair。

这一跳本身就证明边界命名错了。

改后：

```text
Reconciler
→ stable ReconciledTurnContext
→ TurnWorkflow.observe
```

到此结束。

---

# 8. Slice 4 — Manager Vocabulary

当前：

```text
Application/Manager/ManagerWorkflow.fs
```

保留作为故事文件。

但拆出四组 Vocabulary。

---

## 8.1 `ManagerActivation.fs`

把：

```text
Application/Reconciliation/ManagerLifecycleGate.fs
```

移动/重构为：

```text
Application/Manager/ManagerActivation.fs
```

迁入：

```text
hasPendingActivation
shouldActivate
sendActivation
```

公开 API：

```fsharp
ManagerActivation.ensureAccepted
```

语义：

```text
如果 planning 已合法完成且 Activation 尚未建立，
完成 exactly-once activation；
否则返回已有 activation / no-op。
```

不要暴露：

```text
shouldActivate : bool
```

作为主要 Application API。

bool 可以内部存在。

业务调用点应该是动词。

---

## 8.2 `ManagerBackground.fs`

新增：

```text
Application/Manager/ManagerBackground.fs
```

迁入：

```text
TerminalPolicy.outstandingBackground 的 Manager 使用
HostJoinGuard.nudge 的业务选择
```

公开：

```fsharp
ManagerBackground.ensureSettled
```

返回真实单次结果：

```fsharp
type BackgroundSettlement =
    | Settled
    | Deferred
```

这是函数结果，不是长期状态。

---

## 8.3 `ManagerIdle.fs`

新增：

```text
Application/Manager/ManagerIdle.fs
```

迁入：

```text
currentLife
idleAlreadyClaimed
manager-idle dedupe
quiescence admission
trySendIdleManagerEncouragement
```

公开：

```fsharp
ManagerIdle.encourageLabor
```

名字表达业务承诺。

---

## 8.4 `ManagerJobHandoff.fs`

新增：

```text
Application/Manager/ManagerJobHandoff.fs
```

迁入：

```text
tryJobProgress
CandidateReady
RebasedCandidateReady
PublishClaimed
Published
Failed
Abandoned
```

公开：

```fsharp
ManagerJobHandoff.completeIfTransferred
```

让 ManagerWorkflow 不再自己知道 Orchestrator progress case 全集。

---

## 8.5 最终 `ManagerWorkflow.fs`

目标缩成故事：

```fsharp
let observe env context =
    task {
        let turn = context.Turn

        match! ManagerJobHandoff.completeIfTransferred env turn with
        | Transferred ->
            return ()

        | ManagerOwnsTurn ->
            match turn.Outcome with
            | TurnCompleted ->
                match! ManagerBackground.ensureSettled env turn with
                | Deferred ->
                    return ()

                | Settled ->
                    let! activation =
                        ManagerActivation.ensureAccepted env turn

                    match activation with
                    | ActivationDeferred ->
                        return ()

                    | ActivationReady life ->
                        return!
                            ManagerIdle.encourageLabor
                                env
                                context
                                life

            | _ ->
                return ()
    }
```

如果 Active Finality 需要 defer：

封进：

```fsharp
ManagerFinality.awaitOrDefer
```

不要再把：

```text
ActiveFinality open?
```

散在 ManagerWorkflow。

---

# 9. Slice 5 — Reviewer Vocabulary

Reviewer 当前方向总体正确，不大拆。

当前已经明确由 `ReviewerWorkflow` 独占 continuation，且读取 durable witness，而不是保存 stage。

保留这个思想。

---

## 9.1 移动 `ReviewerGuardState.fs`

从：

```text
Application/Reconciliation/ReviewerGuardState.fs
```

到：

```text
Application/Review/ReviewerEvidence.fs
```

因为这不是 reconciliation concern。

公开：

```text
continuationOpen
verdictSubmitted
confirmationPending
confirmed
```

---

## 9.2 新 `ReviewerContinuation.fs`

迁入当前：

```text
ensureContinuation
HostReviewGuard requestPerfectConfirmation
HostReviewGuard nudgeReviewer
```

改成两个有意义的 Vocabulary：

```fsharp
ensureVerdictSubmitted
ensurePerfectConfirmed
```

然后 ReviewerWorkflow 读起来应该是：

```fsharp
let observe env turn =
    task {
        if ReviewerEvidence.confirmed env turn then
            return! ReviewerTerminal.completeConfirmed env turn

        elif ReviewerEvidence.confirmationPending env turn then
            return! ReviewerContinuation.ensurePerfectConfirmed env turn

        elif not (ReviewerEvidence.verdictSubmitted env turn) then
            return! ReviewerContinuation.ensureVerdictSubmitted env turn

        else
            return! ReviewerTerminal.completeRevision env turn
    }
```

---

# 10. Slice 6 — ReviewController 上移

当前：

```text
Session/ReviewController.fs
```

没有物理 single-flight 所有权。

它实际拥有：

```text
verdict judgement
challenge issuance
confirmed witness
single writer
```

移动：

```text
Application/Review/VerdictWorkflow.fs
```

`VerdictTool.fs` 直接调用：

```fsharp
VerdictWorkflow.submit
```

`Session/ReviewController.fs`

删除。

`ReviewTypes.fs` 的 `GitTreePort` 若只服务 review，可移动：

```text
Application/Review/Ports.fs
```

---

# 11. Slice 7 — HostReviewProgram 上移

当前：

```text
Infrastructure/OpenCode/Orchestration/HostReviewProgram.fs
```

本身已经是：

```text
fork reviewer
open barrier
await reviewer
read durable witness
recursive await until verdict becomes decisive
```

这就是 Application CE。

移动为：

```text
Application/Review/ReviewBarrierWorkflow.fs
```

保留它现在非常好的递归结构：

```fsharp
let rec awaitWitness () =
    task {
        let! terminal = ...
        match readOutcome ... with
        | Confirmed -> ...
        | RevisionRequired -> ...
        | Pending -> return! awaitWitness ()
    }
```

物理内容通过：

```fsharp
type ReviewHostPort =
    {
        ForkReviewer: ...
        AwaitReviewer: ...
    }
```

注入。

`Infrastructure/OpenCode/Orchestration/ReviewRunner.fs`

只负责构造这个 port。

---

# 12. Slice 8 — Finality 大拆迁

目标：

```text
DELETE
Infrastructure/OpenCode/Tools/FinalityController.fs
```

最终 Infrastructure 不能拥有 Finality lifecycle。

---

## 12.1 新目录

新增：

```text
Application/Finality/
    Types.fs
    Ports.fs
    RecordWorkflow.fs
    CohortWorkflow.fs
    RevisionWorkflow.fs
    BlessingWorkflow.fs
    FinalityWorkflow.fs
```

---

## 12.2 `Types.fs`

迁入：

```text
FinalityOutcome
EnlistedMember
```

如果内部需要 reviewer 单轮结果，则定义为真实结果，例如：

```fsharp
type MemberJudgement =
    | Confirmed of WorkRecord
    | RevisionRequired of WorkRecord
    | Unavailable of reason:string
```

禁止：

```text
Stage
Phase
NextAction
WaitingFor...
```

---

## 12.3 `Ports.fs`

定义 Application 需要的真实能力：

```fsharp
type FinalityReviewerPort =
    {
        Enlist:
            FinalityReviewerRequest
                -> Task<Result<EnlistedMember, string>>

        AwaitTerminal:
            SessionId
                -> Task<Result<unit, string>>

        SendRevisionSteer:
            SessionId
                -> string
                -> Task<Result<unit, string>>
    }

type FinalityTreePort =
    {
        ReadManagerTree:
            SessionId
                -> Result<GitTreeHash, string>
    }
```

不要把：

```text
ToolRuntimeScope
ManagedAgent
OpenCode API
Directory registry
```

泄漏到 Application。

---

## 12.4 `RecordWorkflow.fs`

迁入：

```text
hasRenderedWorkLog
materializeRecord
coverageCanAdvance
recordReadiness
awaitRecordReady
awaitBlessingRecords
awaitDurableSiblingRecords
```

公开 Vocabulary：

```fsharp
awaitCanonicalWorkRecord
awaitCanonicalCohortRecords
```

内部使用：

```text
AgentJournal.awaitChangeFrom
CausalAwait
```

不 polling。

---

## 12.5 `CohortWorkflow.fs`

迁入：

```text
CancelToken
raceWithCancel
driveMember
concurrentAllOrShortCircuit
enlistMember
```

但重命名为业务语言。

公开：

```fsharp
reviewUntilFirstRevisionOrAllConfirmed
```

这一个函数可以内部拥有：

```text
TCS
cooperative cancellation
parallel fan-out
short circuit
```

调用者不需要知道。

这就是本 proposal 所说的**合法 semantic compression**。

它自己的 temporal tests 必须穷尽：

```text
Revision first
Revision last
simultaneous confirmations
revision after sibling confirmation
cancel before next effect
all confirmed
```

---

## 12.6 `RevisionWorkflow.fs`

迁入：

```text
stagePrimaryRejectionRecord
sealFinalityRejected
commitSiblingSteerFacts
sendSiblingSteerContinuations
concludeRejectionAccountingSiblings
replaySiblingSteer
steerSiblingRevisions
pendingRevision
durableRevisionSiblings
resumeDurableRevise
```

压成三个 Vocabulary：

```fsharp
rejectWithCanonicalRecord
steerRevisionSiblings
resumeRejectedRequest
```

---

## 12.7 `BlessingWorkflow.fs`

迁入：

```text
treeUnchanged
concludeBlessing
```

公开：

```fsharp
blessIfTreeUnchanged
```

内部承担：

```text
collect canonical records
re-read tree
append FinalityBlessed
```

---

## 12.8 `FinalityWorkflow.fs`

只保留故事。

公开 API 只有：

```fsharp
FinalityWorkflow.start
FinalityWorkflow.resume
```

理想形状：

```fsharp
let start env manager life request =
    task {
        let! cohort =
            FinalityCohort.enlistRequiredReviewers env manager life request

        match!
            FinalityCohort.reviewUntilFirstRevisionOrAllConfirmed
                env
                cohort
        with
        | RevisionRequired rejection ->
            return!
                FinalityRevision.rejectAndSteer
                    env
                    request
                    rejection

        | AllConfirmed confirmations ->
            return!
                FinalityBlessing.blessIfTreeUnchanged
                    env
                    request
                    confirmations
    }
```

读这个文件的人不再需要看：

```text
TaskCompletionSource
ref remaining
barrier wait loop
record materialization
directory registration
```

---

## 12.9 Infrastructure replacement

新增：

```text
Infrastructure/OpenCode/Tools/FinalityHostPort.fs
```

它只把：

```text
ToolRuntimeScope
Session host
Directory registry
ManagedAgent
```

适配成：

```text
FinalityReviewerPort
FinalityTreePort
```

然后：

```text
FinalityTool.fs
```

只做：

```text
decode tool args
validate immediate tool contract
build ports
call FinalityWorkflow.start/resume
render tool result
```

---

# 13. Slice 9 — Fallback 分层

当前 `FallbackController` 有一个很好的性质：

> 它是 `FallbackCursorAdvanced` / `FallbackExhausted` 唯一 writer。

这个性质必须保留。

但它最终不应该继续由 Session 业务 Host 直接调用。

---

## 13.1 第一阶段：先做 dependency inversion

`Session/EnforcerHost.fs` 当前直接调用：

```text
FallbackController.recordConfirmedFailure
```

改成注入 capability：

```fsharp
type ConfirmedFailurePort =
    SessionId
        -> ProviderRunIdentity
        -> string
        -> Result<RecoveryAdmission, string>
```

定义真实单次结果：

```fsharp
type RecoveryAdmission =
    | ContinueRecovery
    | RecoveryExhausted
```

EnforcerHost 只问：

```text
这次已确认 failure 后，还允许不允许自动 recovery？
```

不知道 cursor。

不知道预算怎么推进。

不知道 writer 在哪。

---

## 13.2 第二阶段：移动 controller

等 Session 无调用者后：

```text
Session/FallbackController.fs
```

移动为：

```text
Application/Recovery/FallbackLedger.fs
```

职责：

```text
confirmed failure
→ durable dedupe
→ cursor advance/exhaust
→ RecoveryAdmission
```

---

## 13.3 `DurableFallback.fs`

当前是纯 durable cursor 查询。

移动：

```text
Application/Recovery/FallbackEvidence.fs
```

公开：

```text
currentCursor
currentSide
mayContinue
effectiveAgent
```

---

## 13.4 Provider recovery story

最终：

```fsharp
let continueAfterConfirmedFailure env turn failure =
    task {
        let! admission =
            FallbackLedger.recordConfirmedFailure
                env
                turn
                failure

        match admission with
        | RecoveryExhausted ->
            return!
                TerminalReporter.fail
                    env
                    turn
                    failure

        | ContinueRecovery ->
            do!
                RecoveryMaterial.awaitAvailable
                    env
                    turn.SessionId

            return!
                ProviderContinuation.resumeLogicalRun
                    env
                    turn
    }
```

这就是漂亮 DSL。

---

# 14. Slice 10 — Family Recovery 所有权修正

## 14.1 `SessionRecoveryWorkflow.fs`

保留：

```text
recoverFamilyDirect
child-first recursion
```

这是很好的 CE。

---

## 14.2 `mergeOutcomes` 下沉 Domain

当前 Application 自己用优先级：

```text
Blocked
> Waiting
> Recovered
> other
```

把它移动：

```text
Domain/SessionRecovery.fs
```

命名：

```fsharp
SessionRecovery.combine
```

并增加 algebra tests：

```text
Blocked dominates
Waiting dominates ready
combine associative
empty identity
order independent where semantics permit
```

---

## 14.3 Coordinator 下沉 Session

删除：

```text
SessionRecoveryWorkflow.Coordinator
```

新增：

```text
Session/FamilyRecoveryCoordinator.fs
```

它拥有：

```text
Dictionary<root, Task<FamilyRecovery>>
lock
single-flight
```

API：

```fsharp
FamilyRecoveryCoordinator.runOnce
```

它接受：

```fsharp
SessionId -> Task<FamilyRecovery>
```

也就是它完全不知道 family recovery 怎么做。

它只知道 physical single-flight。

这是很典型的 decorator：

```text
bare recovery CE
→ single-flight decorator
```

语义逐层增强，但 business workflow 不被污染。

---

# 15. Slice 11 — Child Recovery 时间注入

修改：

```text
Application/Reconciliation/ChildRecoveryWorkflow.fs
```

Ports 增加：

```fsharp
Clock: IClockPort
```

把：

```fsharp
DateTimeOffset.UtcNow
```

改成：

```fsharp
ports.Clock.UtcNow()
```

测试全部使用 deterministic clock。

---

# 16. Slice 12 — Orchestrator 作为正面范本，不大修

`Application/Orchestration/Program.fs` 不应该被这轮顺手推翻。

它现在已经有正确结构：

```fsharp
rebase
→ review
→ record
→ publish
→ TargetMoved
→ return! recursion
```

而且 `TargetMoved` 被明确建模为真实 publish attempt outcome，不是假 error。

当前 `rebaseReviewPublish` 在 target moved 时直接：

```fsharp
return! rebaseReviewPublish ... (round + 1)
```

已经是我们想要的风格。

本 Change 只做两个语义命名改进：

```text
rebaseReviewPublish
→ publishEventually

resume
→ resumeFromDurableFacts
```

这样调用点：

```fsharp
return! publishEventually deps job 0
```

直接说明完整承诺。

不要为追求统一而重写已经正确的递归。

**将军赶路不追小兔。**

---

# 17. Slice 13 — Port Decorator 设计规范

不建立：

```text
DecoratorBase
MiddlewarePipeline
IWorkflowDecorator
ReliableFlow<T>
```

这种框架。

采用局部 module decorator。

例如：

```fsharp
module ManagerPort =

    let withCausalObservation observer inner =
        {
            inner with

                AwaitManager =
                    fun jobId ->
                        CausalAwait.awaitTask
                            observer
                            (Waits.manager jobId)
                            (inner.AwaitManager jobId)
        }
```

composition：

```fsharp
let manager =
    rawManagerPort
    |> ManagerPort.withCausalObservation waits
```

允许继续加：

```text
raw
→ protocol-normalized
→ causal-observed
→ metrics
```

只要这些 decorator 不改变业务 trace。

---

# 18. Slice 14 — HostSignalBootstrap 最终形状

当前 handled bool chain 全删。

目标：

```fsharp
let onTurn context =
    task {
        let turn = context.Turn

        let! recovery =
            recoveryCoordinator.Ensure turn.SessionId

        match recovery with
        | FamilyBlocked _ ->
            return ()

        | FamilyWaiting _
        | FamilyReady _ ->
            do!
                TurnRuntimePreparation.prepare
                    physicalRuntime
                    turn

            return!
                TurnWorkflow.observe
                    application
                    context
    }
```

Host composition 不再：

```text
match Role.Reviewer
call ReviewerWorkflow
bool

match Role.Manager
call ManagerWorkflow
bool

else generic
```

它只进入一个 Application program。

---

# 19. `TurnRuntimePreparation` 的位置

当前 `TurnCompletionProgram.prepareTurn` 同时：

```text
dispose Executor runtime
ensure child authority
```

拆成：

```text
Infrastructure/OpenCode/Host/TurnRuntimePreparation.fs
```

只保留 physical cleanup：

```text
DisposeExecutorRuntime
```

Prompt authority 相关逻辑搬到：

```text
Application/Prompting/ChildPromptAuthority.fs
```

因为：

```text
谁拥有 prompt authority
```

不是 runtime cleanup。

---

# 20. Fsproj 精确调整

`Wanxiangshu.fsproj` 按 dependency 顺序新增：

```text
Kernel/Temporal.fs

Application/Recovery/FallbackEvidence.fs
Application/Recovery/FallbackLedger.fs
Application/Recovery/ProviderRecoveryWorkflow.fs

Application/Review/Ports.fs
Application/Review/ReviewerEvidence.fs
Application/Review/VerdictWorkflow.fs
Application/Review/ReviewerContinuation.fs
Application/Review/ReviewBarrierWorkflow.fs
Application/Review/ReviewerWorkflow.fs

Application/Manager/ManagerActivation.fs
Application/Manager/ManagerBackground.fs
Application/Manager/ManagerIdle.fs
Application/Manager/ManagerJobHandoff.fs
Application/Manager/ManagerWorkflow.fs

Application/Finality/Types.fs
Application/Finality/Ports.fs
Application/Finality/RecordWorkflow.fs
Application/Finality/CohortWorkflow.fs
Application/Finality/RevisionWorkflow.fs
Application/Finality/BlessingWorkflow.fs
Application/Finality/FinalityWorkflow.fs

Application/Reconciliation/TerminalReporter.fs
Application/Reconciliation/InteractionRepairWorkflow.fs
Application/Reconciliation/OrdinaryTurnWorkflow.fs
Application/Reconciliation/TurnWorkflow.fs

Session/FamilyRecoveryCoordinator.fs

Infrastructure/OpenCode/Host/TurnRuntimePreparation.fs
Infrastructure/OpenCode/Tools/FinalityHostPort.fs
```

删除 compile entries：

```text
Session/FallbackController.fs
Session/ReviewController.fs

Application/Reconciliation/ManagerLifecycleGate.fs
Application/Reconciliation/ReviewerGuardState.fs
Application/Reconciliation/TurnCompletionProgram.fs

Infrastructure/OpenCode/Orchestration/HostReviewProgram.fs
Infrastructure/OpenCode/Tools/FinalityController.fs
```

注意：

FallbackController 删除项在 dependency inversion slice 完成后才执行。

---

# 21. 测试迁移：不要保留“大判定表测试”

当前：

```text
tests/unit/reconciliation/turn-completion-program.test.mjs
```

自己就宣称测试一个“大 decision table”，包含 repair、probe hijack、abort、fallback、join、planning、finality、manager handoff、encouragement 等。

这正是 production 混层的镜像。

删除这个文件。

拆成：

```text
tests/unit/temporal/reconciliation/
    interaction-repair.test.mjs
    ordinary-turn.test.mjs
    terminal-reporter.test.mjs
    turn-routing.test.mjs

tests/unit/temporal/recovery/
    provider-failure.test.mjs
    recovery-material.test.mjs
    fallback-dedupe.test.mjs
    fallback-permutations.test.mjs

tests/unit/temporal/manager/
    activation.test.mjs
    background-settlement.test.mjs
    idle-occasion.test.mjs
    job-handoff.test.mjs

tests/unit/temporal/review/
    reviewer-continuation.test.mjs
    barrier-workflow.test.mjs
    cohort-race.test.mjs

tests/unit/temporal/finality/
    rejection.test.mjs
    sibling-steer.test.mjs
    blessing.test.mjs
    durable-resume.test.mjs
    cohort-permutations.test.mjs

tests/unit/temporal/recovery/
    family-recovery.test.mjs
    family-single-flight.test.mjs
```

---

# 22. Temporal test 的统一写法

所有测试使用：

```text
real production Vocabulary
+
fake/deterministic ports
```

不复制业务实现。

例如：

```fsharp
reviewUntilFirstRevisionOrAllConfirmed
```

测试输入不是 timer：

```text
after 20ms reviewer A
after 21ms reviewer B
```

而是：

```text
Trace A:
    A Confirmed
    B Revision

Trace B:
    B Revision
    A Confirmed

Trace C:
    A ConfirmationPending
    B Revision
    A Confirmed
```

然后断言：

```text
same rejection
same durable facts
no sibling challenge after cancellation point
```

---

# 23. Finality Cohort 必须做 bounded exhaustive permutation

这是第一批最值得 pure temporal 化的地方。

对小 cohort：

```text
R1
R2
R3
```

枚举：

```text
Confirmed / Revision
```

和事件排列。

至少证明：

```text
第一 REVISE 决定业务结果

已启动 sibling 可以完成当前无副作用 step
但不得开始下一个 side effect

durable sibling REVISE 不丢

all confirmed 才 blessing

tree moved 永不 blessing

同一 durable rejection resume 不重复事实
```

这比当前真实 concurrent race 强得多。

---

# 24. 新 Static Ratchets

修改：

```text
scripts/checks/dsl-ownership.mjs
tests/unit/verify/dsl-ownership.test.mjs
tests/unit/verify/dsl-ownership-ratchet.test.mjs
```

增加永久规则。

## 24.1 Obsolete controller absence

Exit 后必须不存在：

```text
Application/Reconciliation/TurnCompletionProgram.fs
Infrastructure/OpenCode/Tools/FinalityController.fs
Session/ReviewController.fs
Application/Reconciliation/ManagerLifecycleGate.fs
Application/Reconciliation/ReviewerGuardState.fs
```

---

## 24.2 Raw time gate

以下目录：

```text
Domain/
Application/
Session/
```

禁止：

```text
DateTimeOffset.UtcNow
DateTime.Now
DateTime.UtcNow
setTimeout
timerTask
```

除精确 allowlist 的 physical implementation。

---

## 24.3 Infrastructure business workflow gate

Infrastructure 禁止新增包含：

```text
business lifecycle DU
durable business retry recursion
cohort business decision
Manager/Reviewer/Finality state transition
```

自动门最低限度使用路径+已知类型 ratchet。

不要企图仅靠启发式 scanner “证明”全部语义。

人工 proof 仍拥有最后判断。

---

## 24.4 Vocabulary naming review

这项不做愚蠢的单词黑名单。

在 `docs/proof/dsl-structured-program.md` 增加强制 review 表：

每个新增 Application public function 回答：

```text
1. 它的名字声明了什么业务承诺？
2. 它隐藏了哪些时序？
3. 哪个 temporal proof 证明这些时序？
4. 它改变 trace 集，还是 transparent decorator？
5. crash 后从什么 durable evidence 重入？
```

回答不出：

REVISE。

---

# 25. Proof Matrix

最终每个高阶 Vocabulary 都有对应 proof：

```text
ManagerActivation.ensureAccepted
→ exactly-once activation traces

ManagerBackground.ensureSettled
→ completion / join / wake permutations

ManagerIdle.encourageLabor
→ independent idle occasions / stale permit

ReviewerContinuation.ensurePerfectConfirmed
→ first PERFECT / challenge / second PERFECT

ReviewBarrierWorkflow.reverify
→ verdict absence / revision / confirmation

FallbackLedger.recordConfirmedFailure
→ dedupe / AABB / exhaustion

ProviderRecoveryWorkflow.continueAfterConfirmedFailure
→ failure → material → continuation

FinalityCohort.reviewUntilFirstRevisionOrAllConfirmed
→ cohort interleavings

FinalityRevision.rejectAndSteer
→ sibling accounting / replay

FinalityBlessing.blessIfTreeUnchanged
→ tree movement / canonical records

SessionRecoveryWorkflow.recoverFamilyDirect
→ closure orders / missing evidence

FamilyRecoveryCoordinator.runOnce
→ physical single-flight only

Orchestrator.publishEventually
→ target movement recursion
```

---

# 26. 与唯一 Long Stroke E2E 的对应

唯一真实 E2E 不再测试这些 Vocabulary 的组合排列。

它只真实调用它们。

故事：

```text
ManagerActivation.ensureAccepted
→ Manager labor
→ child
→ ManagerBackground.ensureSettled
→ provider fail
→ ProviderRecoveryWorkflow.continueAfterConfirmedFailure
→ Reviewer workflow
→ REVISE
→ FinalityRevision.rejectAndSteer
→ repair
→ review all confirmed
→ publishEventually
→ target moved
→ publishEventually recursion
→ final blessing
→ second suicide
→ clean terminal
```

因此 E2E 读起来甚至应该像 Vocabulary 的展示程序。

这非常重要。

**Production CE 与 E2E narrative 使用同一种语言。**

---

# 27. Landing 顺序

严格按下面落，不得同时大爆炸修改。

```text
S0  docs + RED gates

S1  Kernel Temporal contracts
    CausalAwait signal/deadline vocabulary
    Clock injection

S2  TurnCompletion split
    TurnWorkflow introduced
    old TurnCompletion deleted

S3  Reconciler semantic repair decision removed

S4  Manager vocabulary extraction

S5  Reviewer evidence / continuation extraction
    ReviewController moved

S6  HostReviewProgram → Application ReviewBarrierWorkflow

S7  Finality ports
    Finality business split
    old Infrastructure FinalityController deleted

S8  FamilyRecovery Coordinator ownership move

S9  EnforcerHost fallback port inversion
    FallbackController → Application

S10 Orchestrator semantic naming only

S11 HostSignalBootstrap collapse to one Application entry

S12 temporal proof migration

S13 one Long Stroke E2E migration

S14 static ratchet + <10s gate
```

每个 Slice：

```text
RED
→ production
→ targeted temporal proof
→ npm run check
→ commit
```

不得同时跨两个 ownership plane 做半成品。

---

# 28. 明确不做

不要：

```text
自定义 Flow AST
WorkflowCommand
WorkflowInterpreter

CurrentStage
NextAction
ManagerPhase
FinalityStage
RecoveryStage

全局 Middleware framework
IOC container
DecoratorBase<T>
magic ReliableFlowBuilder

为了“统一风格”重写已经漂亮的 Orchestrator recursion

把所有 private helper 都升级为公共 Vocabulary

为每一行代码创造抽象
```

Vocabulary 的价值来自：

```text
压缩真正复杂且反复出现的语义
```

不是：

```text
函数越多越高级
```

---

# 29. “将军赶路”代码审查法

Review 一个 workflow 时，只按两层阅读。

第一遍只读：

```text
Business CE
```

应该能回答：

```text
这个角色的一生是什么？
失败以后去哪？
什么时候等待？
什么时候完成？
```

如果必须打开 Host / Timer / Journal helper 才能回答：

REVISE。

第二遍只在怀疑某个承诺时进入 Vocabulary。

例如怀疑：

```text
publishEventually
```

才打开它。

然后只需要回答：

```text
TargetMoved 是否真的重新 review？
```

不需要同时理解 Manager Activation。

这才是局部推理。

---

# 30. 最终源码审美标准

坏：

```fsharp
match state with
| Some x when pending && not sent && ...
```

坏：

```fsharp
let! result = executeSafe env thing
```

坏：

```fsharp
reliableFlow {
    let! result = run thing
}
```

好：

```fsharp
let! activation =
    ManagerActivation.ensureAccepted env life

let! work =
    ManagerLabor.performResiliently env activation

let! judgement =
    ReviewerCohort.reviewUntilSettled env work

match judgement with
| Revision feedback ->
    return! reviseAndContinue env feedback

| Confirmed witness ->
    return! finalizeWhenSafe env witness
```

内部再复杂：

都可以。

因为每一个复杂概念都有名字。

---

# 31. G4R-CE Exit Criteria

## Formal

```text
[ ] DSL-013 Semantic Vocabulary
[ ] DSL-014 Semantic Compression
[ ] DSL-015 Decorator Boundary
[ ] flow/shape/how/proof 全对齐
```

## Structural

```text
[ ] TurnCompletionProgram deleted
[ ] FinalityController deleted from Infrastructure
[ ] ReviewController no longer in Session
[ ] family recovery Coordinator no longer in Application
[ ] HostReviewProgram no longer in Infrastructure
[ ] HostSignalBootstrap has one Application turn entry
```

## Time

```text
[ ] Domain/Application/Session no raw UtcNow
[ ] semantic waits no polling
[ ] all deadline capability injected
```

## CE

```text
[ ] ManagerWorkflow reads as Manager story
[ ] ReviewerWorkflow reads as Reviewer story
[ ] FinalityWorkflow reads as Finality story
[ ] Orchestrator remains recursive CE
[ ] recovery re-enters normal vocabulary
```

## Vocabulary

```text
[ ] every high-level Vocabulary has explicit semantic contract
[ ] every trace-changing Vocabulary has temporal proof
[ ] transparent decorators do not change observable business traces
```

## Proof

```text
[ ] old turn-completion decision-table test deleted
[ ] Finality cohort permutations deterministic
[ ] fallback permutations deterministic
[ ] manager unhappy permutations deterministic
[ ] recovery crash cuts deterministic
```

## Physical

```text
[ ] exactly one E2E
[ ] exactly one OpenCode startup
[ ] Long Stroke includes adversity
```

## Performance

```text
[ ] semantic full suite < 10s
[ ] no timeout inflation
[ ] no retry-until-pass
[ ] no wall-clock race proof
```

---

# 32. Final Outcome

本 Change 完成后，工程师看到的生产代码不应该像：

```text
一个聪明框架在运行
```

而应该像：

```text
Manager 在工作
Reviewer 在审判
Finality 在收敛
Orchestrator 在发布
Recovery 在恢复
```

实现细节仍然存在。

race 仍然存在。

fallback 仍然存在。

durability 仍然存在。

并发仍然存在。

但它们都被关进一个个有名字、有 contract、有 temporal proof 的 Vocabulary。

调用者不再追逐：

```text
timer
TCS
bool
pending
which writer
which callback
which event arrived first
```

除非他正在维护那个 Vocabulary 本身。

这才是：

> **将军赶路，不追小兔。**

也是“明显没有 Bug”在生产代码侧真正应该达到的形态。

---

## Active work

> Original proposal 原文冻结于上方；后续事实只追加于 Active work / Amendments / Blockers / Final outcome。

**Started**: 2026-08-10  
**Work origin**: 用户明确启动 `changes/proposed/rabbit.md`（G4R-CE Semantic CE Vocabulary），并要求与 G4R One World 最大并行。

**Specification impact**（正式 docs 将先于 production 改写）：
- `docs/{what,shape,how,proof}/dsl-structured-program.md` → DSL-013 Semantic Vocabulary / DSL-014 Semantic Compression / DSL-015 Decorator Boundary
- `docs/{what,shape,how,proof}/flow.md` → Evidence→Decision 降级为可用形式；主设计法改为 typed evidence/capability → semantic vocabulary → CE composition → effect
- 静态门禁：obsolete controller absence、Domain/Application/Session raw-time allowlist（S0 RED scaffolding；Exit 后 harden）
- Cross-Change：Depends on `changes/active/test.md`（G4R One World）；Blocks G4 Exit

## Remaining work

- [x] **S0 Formal docs + RED** — DSL-013/014/015 in what/shape/how/proof; flow docs demote Evidence→Decision; `scripts/checks/g4r-ce-vocabulary.mjs` soft phase + `tests/unit/verify/g4r-ce-vocabulary.test.mjs` (10/10)
- [x] **S1 Temporal capability** — Kernel/Temporal.fs + PtyTiming adapters; CausalAwait.untilSignalOrDeadline + 4 theorems (`tests/unit/temporal/until-signal-or-deadline.test.mjs`); awaitCoverageBeforeRetry rewrite deferred to S2 ProviderRecovery
- [ ] **S2 TurnCompletion split/delete** — TurnWorkflow / OrdinaryTurn / InteractionRepair / ProviderRecovery / TerminalReporter
- [ ] **S3–S11** — Reconciler / Manager / Reviewer / Finality / Fallback / FamilyRecovery / Host collapse（按 proposal landing 顺序）
- [ ] **S12–S14** — temporal proof migration；Long Stroke vocabulary narrative；static ratchet + <10s gate

## Completion criteria

以 Original proposal §31 G4R-CE Exit Criteria 全部勾选为准（Formal / Structural / Time / CE / Vocabulary / Proof / Physical / Performance），且不得缩减批准范围。

## Blockers

（无）
