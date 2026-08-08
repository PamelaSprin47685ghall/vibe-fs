# Proposal：时序所有权清算、Direct-CE 真迁移、Join Attempt 隔离与 Canary 可信度重建

**Status:** Proposed
**Priority:** P0 / architectural correctness
**Scope:** Review / Student–Teacher / Join interruption / Esc-abort / Reconcile repair / Turn completion ownership / Canary causal proof / DSL static enforcement
**原则:** 君子不立危墙。任何依赖“这个 race 大概碰不到”“这个 bool 通常不会同时成立”“再加一个 dedupe 就行”“测试虽然挂但不是本 Change 引入”的实现，一律视为未完成。

---

# 0. 裁决：上一轮“CE DSL 迁移”在关键时序上没有真正完成

本 Change 不把当前问题定义成六个独立 bug。

当前观察到的：

1. Reviewer 的

   ```text
   # Your previous response did not submit a verdict.
   # Continue the review and submit PERFECT or REVISE with the verdict tool.
   ```

   出现重复物理发送；

2. Student–Teacher 测试失败；

3. 多个 canary 处于勉强通过、依赖时序运气的状态；

4. 用户按 Esc 后系统又自动发送裸 `#`；

5. 用户消息不仅能打断正在等待的 `join()`，还会污染未来尚未开始的 `join()`；

6. 大量业务时序仍然挤在 `TurnCompletionProgram` 和长期 mutable runtime cell 中；

不是六件事。

它们共同证明：

> **项目把“Direct CE”做成了源码表面要求，却没有真正把业务时序所有权交给 CE 调用结构。**

正式规范已经明确要求：

```text
业务流程 = F# computation expression
           + let!/do!
           + match
           + return!
           + 递归
           + 高阶组合

F# 调用栈就是流程栈。
```

禁止 `CurrentStage`、`NextAction`、`Running` 等程序计数器。

但当前生产实现仍然大量采用：

```text
Host event
→ Reconcile
→ TurnEnd / HandleTurn
→ 读取很多 State/Pending/bool
→ 计算“现在到底处于哪个阶段”
→ 再决定下一个动作
```

这仍是状态机。

只不过：

```text
StateMachine.execute(state, event)
```

被拆成了：

```text
if ...
match ...
Pending...
State...
bool...
TurnCompleted...
```

所以看起来不像状态机而已。

**本 Change 的目标不是“再把测试修绿”。**

目标是：

> **把时序程序从状态字段和 TurnEnd 判定表中搬回 F# CE 的程序结构本身。**

---

# 1. 严厉审计结论

## 1.1 最不可接受的问题不是 Bug 多，而是旧思维被包装成“DSL 已完成”

正式 FLOW 条款已经写得非常清楚：

```text
Domain      = 纯 Evidence / Decision / Projection
Application = CE workflow / direct ports
Session     = physical single-flight / resource ownership
```

然而 Student–Teacher 当前真正的核心对象却是：

```fsharp
type private StudentRunCell =
    { SessionId: SessionId
      LogicalRunId: LogicalRunId
      Agent: string
      Tier: AgentTier
      mutable State: StudentTeacher.RunState
      mutable TeacherSessionId: SessionId option
      mutable Waiter: TaskCompletionSource<Result<string, string>> option
      mutable PendingTeacherReturn: PendingTeacherReturn option
      mutable TeacherReturnHandoff: bool
      mutable TouchedSkillDocuments: Map<string, string>
      mutable PendingFinal: (ProviderRunIdentity * string) option }
```

这不是“物理资源 owner”。

这是一个完整的业务程序计数器集合。

至少存在：

```text
State
× TeacherSession
× Waiter
× PendingTeacherReturn
× TeacherReturnHandoff
× PendingFinal
```

多个正交控制轴。

然后 `HandleTurn` 再从：

```text
TurnOutcome × State × PendingReturn × Waiter × PendingFinal
```

反推出程序下一步。

这恰好就是本项目 DSL 宪法试图消灭的东西。

**不能因为字段叫 `RunState` 而不是 `CurrentStage`，就说它不是程序计数器。**

---

## 1.2 DSL 静态门存在一个足以让整个 StudentRunCell 漏过去的机械漏洞

当前 `state-product` scanner 的字段规则是：

```js
const fieldLine = /^\s*(\w+)\s*:\s*([^=\[\]{}]+?)\s*$/
```

即只识别：

```fsharp
Foo: SomeType
```

却不识别：

```fsharp
mutable Foo: SomeType
```

与此同时现有 mutable gate 重点匹配的是：

```text
let mutable
```

而不是 record field 的：

```text
mutable Foo:
```

因此当前最危险的一类状态机恰好可以从两道门之间穿过去：

```fsharp
mutable State: ...
mutable PendingX: ... option
mutable SomeFlag: bool
```

StudentRunCell 正是这种形态。

所以这里不能再说：

> “DSL gate 已经证明生产代码符合 Direct CE。”

没有证明。

门漏了。

而且漏的是最关键的形态。

---

## 1.3 `TurnCompletionProgram` 已经变成事实上的第二运行时

当前文件自己宣称它是：

> “The one production path that turns a reconciled turn into side effects”

而在 `TurnCompleted` 一条路径中，它继续计算：

```text
joinOutstanding
managerPlanning
finalityOutstanding
managerJobHandedOff
managerShouldContinue
lifeArchived
completionDeferred
```

然后再按 Role 做下一层分支。

Reviewer confirmation、missing verdict、Manager activation、Manager idle、Finality 等仍在这个 terminal 大漏斗里发生。

这不是 CE workflow。

这是：

> **在 TurnEnd 的螺蛳壳里重新实现一台业务解释器。**

即使里面全部写成 `task {}`，它也仍然是一台状态机。

`task {}` 不是免死金牌。

---

## 1.4 Join 当前实现直接违反正式 EXEC-017

正式语义已经写明：

> External-user ingress interrupts **only the current wait**.

并且：

> 不 cancel mailbox/runtime/session/child。

但当前 `JoinInterruptRegistry` 却实现：

```text
SignalUserMessage(session)
    └─ 当前没有 waiter
       → pendingUserMessage.Add(session)

未来：
Register(session, interrupt)
    → consume pendingUserMessage
    → UserMessageArrived
```

代码注释甚至明确称之为：

> Signal-before-Register race latch.

并且永久 unit test 明确冻结：

```text
SignalUserMessage
BEFORE
Register

→ future Register MUST wake
```

这是明确的：

```text
formal spec = 只打断当前 wait
production  = 可以打断未来 wait
test        = 保护 production 的错误行为
```

不是理解偏差。

是实现和测试共同违反规范。

---

## 1.5 Esc 后出现 `#` 不是神秘 Host 行为

当前生产明确定义：

```fsharp
let MissingFinalReportInstructions = [ "" ]
```

并说明 `SyntheticToml.comment ""` 渲染为：

```text
#
```

因此用户 Esc 后看到自动 `#`，正确调查方向不是：

> “是不是 OpenCode UI 自己加了一个 #？”

而是：

```text
operator abort
与
idle-derived missing-final-report capability
没有建立强排斥
```

当前 Host signal adapter 甚至把：

```text
MessageAbortedError
AbortError
```

直接丢掉，不形成独立 abort signal。

于是可能发生：

```text
旧 idle / late idle
→ mint QuiescencePermit
→ Esc
→ abort 没有立刻撤销 repair capability
→ reconcile 看到 Unknown / NeedsContinuation
→ MissingFinalReport
→ "#"
```

这里不能再靠：

```text
希望 snapshot 先看到 TurnAborted
```

正确性必须来自能力撤销。

---

## 1.6 Canary 当前存在“测试自己给挂死路径续命”的明确实例

VERIFY-004 的正式原则是：

> watchdog 只应由证明因果链确实向前走了一步的事件续期；有字节移动不算。

但 Student–Teacher canary 当前存在：

```js
while (runtime.unmetMust().length > 0) {
    await sleep(50)
    watchdog.advance({
        reason: 'student-teacher-must',
        blocking: true
    })
}
```

随后又：

```js
while (final message 尚未出现) {
    GET messages
    sleep(50)
    watchdog.advance({
        reason: 'student-final-message',
        blocking: true
    })
}
```

也就是说：

```text
没有新事实
没有新 expectation
没有新 terminal
仅仅“又 poll 了一次”

→ blocking causal progress
```

这与 VERIFY-004 的精神直接冲突。

这种 canary 即使绿，也不能证明系统没有停滞。

---

## 1.7 “基线本来就挂，所以本 Change 可以 Close”不得再次出现

历史 Completed Change 明确留下：

```text
EXEC_025_three_teacher 在全量 unit 下挂起
HEAD 基线同样 timeout
→ 非本 Change 回归
→ 不阻塞 close
```

同时：

```text
npm run check 全量
→ 未作为 close 条件
```

对于普通局部 Change，这种“不是我引入的”判断有时可以讨论。

但对于一个声称：

```text
重构 Host 时序
修改 Join
修改 Manager continuation
修改 Prompt/reconcile
修改 DSL proof
```

的 P0 架构 Change：

> **留下已知 hanging lifecycle test，然后宣布时序改造 Completed，是不可接受的。**

Baseline broken 不是 correctness proof。

它只能证明：

```text
坏问题不是今天才有
```

不能证明：

```text
坏问题可以继续存在
```

本 Change 禁止重复这种 Close 标准。

---

# 2. 本 Change 的唯一目标架构

以后业务时序只允许：

```text
Durable Facts / Host Observation
            ↓
Projection / typed evidence / capability
            ↓
Application recursive CE
    ├─ let!
    ├─ do!
    ├─ match
    ├─ return!
    ├─ bounded recursion
    ├─ higher-order traversal
    └─ race / short-circuit
            ↓
typed ports
            ↓
Durable Facts / physical result
```

注意：

**不要再把整个 workflow 压成一个巨大 `Decision DU`。**

允许小型真实领域判断：

```fsharp
ReviewWitness
ReviewerOutcome
PromptAcceptance
FamilyRecovery
```

禁止这种“漂亮一点的程序计数器”：

```fsharp
ManagerTurnDecision =
    | Activate
    | WaitForChildren
    | WaitForFinality
    | Encourage
    | Complete

StudentStage =
    | Learning
    | TeacherReturning
    | CompileDispatching
    | Compiling
    | Finalizing
```

因为：

```text
名字再漂亮
仍然等价于 pc = 0/1/2/3/4
```

Domain 负责真相与规则。

**Application CE 本身才是程序。**

---

# 3. 强制建立五个独立时序 Owner

本 Change 完成以后至少形成：

```text
ReviewerWorkflow
StudentTeacherWorkflow
ManagerWorkflow
OrdinaryTurnWorkflow
JoinWaitScope
```

禁止继续：

```text
TurnCompletionProgram
    └─ 什么都管
```

---

# 4. Reviewer：只允许一个 continuation writer

## 4.1 当前错误

当前 Reviewer terminal 可以由 `TurnCompletionProgram` 判断：

```text
pendingConfirmation
→ requestPerfectConfirmation

没有 verdict
→ nudgeReviewer
```

而 `HostReviewGuard` 自己又维护：

```text
PendingClaims
nudgeKeys
processNudgeKeys
```

其中 process key 还包含 `RuntimeId`。

这是一种典型错误方向：

> 多 writer 已经存在，于是不断增加 dedupe 层，希望多 writer 看起来像一个 writer。

**拒绝。**

---

## 4.2 新 owner

新增建议：

```text
Application/Review/ReviewerWorkflow.fs
```

唯一职责：

```fsharp
let rec reconcileReviewer env observation =
    task {
        let! review = env.LoadReviewEvidence observation.SessionId

        match ReviewWitness.observe review with
        | Revision revision ->
            return! env.RecordRevision revision

        | Confirmed witness ->
            return! env.CompleteReviewer witness

        | FirstPerfect challenge ->
            do! env.EnsureChallengeSent challenge
            return ()

        | NoVerdict ->
            do! env.EnsureVerdictRequestSent observation
            return ()
    }
```

重点不是上面具体函数名。

重点是：

```text
ReviewerWorkflow
```

是唯一业务 owner。

---

## 4.3 FinalityController 必须降权

Finality 以后只可以：

```text
enlist reviewer
open barrier
send INITIAL assignment
wait facts
aggregate cohort
short-circuit REVISE
collect confirmed witnesses
```

**绝对禁止：**

```text
FinalityController → reviewer missing-verdict nudge
FinalityController → skeptical challenge
FinalityController → second verdict continuation
```

Finality 不推动 Reviewer “进入下一阶段”。

Finality 只等待 Reviewer workflow 写出的事实。

---

## 4.4 HostReviewGuard 降为 transport primitive 或删除

如果保留：

```text
HostReviewGuard.send...
```

它只能是：

```text
ReviewerWorkflow 调用的发送 primitive
```

不得再拥有业务判定。

更理想的是：

```text
ReviewerWorkflow
→ PromptDispatcher.EnsureContinuation(...)
```

直接删除 Reviewer-specific process HashSet。

Prompt 的物理幂等由通用 PromptAuthority/claim 机制承担。

---

## 4.5 Reviewer 双发永久测试

先 RED：

### REVIEW-P0-1：prose terminal only sends one guard

构造：

```text
Reviewer
→ 首轮只输出 prose
→ 两个 reconcile wake / 两个 plugin instance 竞争
```

断言：

```text
物理 ReviewerVerdictGuard request = 1
Prompt claim occasion = 1
```

### REVIEW-P0-2：first PERFECT only sends one challenge

```text
first PERFECT fact durable
→ 多次 reconcile
```

断言：

```text
skeptical challenge physical send = 1
```

### REVIEW-P0-3：Finality cannot send continuation

静态 gate：

```text
FinalityController.fs
FinalityTool.fs
FinalityReviewCohort.fs
```

不得引用：

```text
ReviewerVerdictGuard
ReviewConfirmation
nudgeReviewer
requestPerfectConfirmation
```

---

# 5. Student–Teacher：整个长期业务状态机必须拆除

这是本 Change 最重要的 clean break 之一。

## 5.1 删除

删除：

```fsharp
StudentTeacher.RunState
StudentRunCell.State
StudentRunCell.TeacherReturnHandoff
StudentRunCell.PendingTeacherReturn
StudentRunCell.PendingFinal
```

不得改名为：

```text
Mode
Phase
Status
Standing
Disposition
CurrentRequestState
```

然后继续用。

---

# 6. Student–Teacher 新程序结构

## 6.1 学习工具 `teacher(message)`

Application CE 应直接表达：

```fsharp
let askTeacher env student question =
    task {
        let! run = env.RequireStudentRun student

        do! env.QaAppend run question

        use! flight =
            env.TeacherCalls.Open run

        let! teacher =
            env.Satellites.EnsureTeacher run

        do!
            env.Prompts.SendTeacherQuestion
                teacher
                question

        let! answer =
            flight.AwaitAnswer()

        return answer
    }
```

这里：

```text
函数调用栈
```

就是：

```text
question written
→ Teacher available
→ prompt sent
→ wait return
→ answer delivered
```

不需要：

```text
State <- TeacherWaiting
```

---

## 6.2 Teacher `return`

Teacher return：

```fsharp
let submitTeacherAnswer env teacher toolRun answer =
    task {
        let! flight = env.TeacherCalls.RequireOwner teacher

        do! env.QaAppend flight.Student answer

        do!
            env.TeacherCompletions.Arm
                flight
                toolRun
                answer

        return TeacherReturnCompletionText
    }
```

这里 `TeacherCompletions` 是**物理单赋值 completion capability**。

允许它保存：

```text
tool provider run
answer
completion run
TaskCompletionSource
```

因为这是：

```text
正在等待的一个具体物理调用
```

不是：

```text
整个 Student 程序现在进行到哪一步
```

必须是独立 registry / scope。

不得再塞回一个大 `StudentRunCell`。

---

## 6.3 Teacher terminal

```fsharp
let observeTeacherTerminal env turn =
    task {
        match! env.TeacherCompletions.TryResolve turn with
        | Some flight ->
            do! flight.CompleteParent()
        | None ->
            return! handleTeacherIdleOrFailure env turn
    }
```

没有：

```text
TeacherReturnHandoff = true
State = CompileDispatching
```

---

## 6.4 Student learning terminal → Compile

正式 EXEC-027 本来就规定：

```text
Student learning idle
→ StudentCompile continuation
```

因此实现直接：

```fsharp
let observeStudentLearningTerminal env turn =
    task {
        let! profile = env.RequireProfile turn.SessionId

        match profile.RequestKind with
        | StudentLearn ->
            do! env.SendCompileContinuation turn
        | StudentCompile ->
            return! observeCompileTurn env turn
        | _ ->
            return! failInvalidStudentRequestKind ()
    }
```

这里 `RequestKind` 是当前 Authority/Prompt execution profile 的真实事实。

它不是另造的 program counter。

---

## 6.5 Compile dispatch acceptance unknown

禁止重新引入：

```text
CompileDispatching
```

发送后到底有没有落地，交给已经存在的：

```text
PromptAuthority
PendingClaim
PhysicalAccepted
recovery
```

解决。

也就是：

```text
send compile
→ unknown acceptance
→ Prompt recovery
→ 重入同一个 Student workflow
```

而不是：

```text
State = CompileDispatching
→ 等 event
→ State = CompileReady
```

---

## 6.6 Student final `return`

最终 return：

```fsharp
let finishStudent env student providerRun message =
    task {
        let! touched = env.SkillMutations.ForCurrentCompile student

        do! env.ValidateAllTouchedSkills touched

        do! env.QaDeleteAndVerifyGone student

        do!
            env.StudentFinalCompletion.Arm
                student
                providerRun
                message

        return FinalReturnToolResult
    }
```

`StudentFinalCompletion` 也是一次性物理 completion capability。

它不能表达：

```text
Student 正处于 Finalizing stage
```

只表达：

```text
这个 provider run 的下一次固定 Assistant completion
必须使用这段 exact text
```

terminal 被确认后释放即可。

---

# 7. EXEC-026 正式规范也需要收口

当前 EXEC-026 明确说 StudentRun 拥有：

```text
request kind
single-flight latch
QA writer
pending Teacher return
pending final message
```

这个定义本身给“大 StudentRun 状态容器”留下了过大空间。

修改为：

```text
Student logical run
    durable truth:
        PromptAuthority profile
        QA
        Student↔Teacher association

Physical owners are independent:
    TeacherCallScope
    TeacherCompletionScope
    StudentFinalCompletionScope
    SkillMutationEvidence

不存在一个同时容纳多个业务阶段轴的 StudentRun cell。
```

把“pending return / pending final”明确限定为：

> **call-scoped / provider-run-scoped physical single-assignment capability，不得合并为 Student lifecycle state。**

---

# 8. Join：从 Session latch 改成 JoinAttempt scope

## 8.1 删除

彻底删除：

```fsharp
pendingUserMessage: HashSet<string>
```

以及：

```text
Signal-before-Register latches
```

删除当前测试：

```text
EXEC_017_join_interrupt_registry_signal_before_register_latches
```

不是改名。

是反转语义。

---

## 8.2 正确 API

建议：

```fsharp
type IJoinAttemptRegistry =
    abstract Begin:
        SessionId * ToolCallId option
            -> JoinAttemptLease

type JoinAttemptLease =
    inherit IDisposable

    abstract Wait: Task<JoinInterruptReason>
    abstract SignalOperatorAbort: unit -> unit
    abstract SignalUserMessage: unit -> unit
    abstract SignalDeadline: unit -> unit
```

registry 内部只持有：

```text
当前活跃 JoinAttempt
```

没有：

```text
FutureJoinLatch
PendingUserMessage
```

---

## 8.3 JoinTool 第一件事就是 Begin attempt

顺序必须是：

```fsharp
use attempt =
    scope.JoinAttempts.Begin(
        sessionId,
        context.ToolCallId
    )

let detachAbort =
    context.AttachAbort attempt.SignalOperatorAbort

try
    let! permit =
        scope.RequireFamilyRecovery sessionId

    return!
        Join.joinAvailable
            runtime
            permit
            maxCount
            attempt.Wait
finally
    detachAbort()
```

为什么 `Begin` 必须在前？

因为真正需要解决的 signal-before-wait race 是：

```text
JoinTool 已经开始
但 mailbox race 尚未完全建立
```

这时候 attempt 已存在，user signal 可以记在这个 attempt 自己的 TCS。

完全不需要 session 级未来 latch。

---

## 8.4 用户消息 producer

真实外部用户消息：

```text
SignalUserMessage(session)
```

改为：

```text
for every ACTIVE join attempt in session:
    attempt.SignalUserMessage()
```

如果：

```text
active attempts = 0
```

则：

```text
DROP AS JOIN WAKE
```

用户消息本体仍然留在正常 Host 队列。

这里“drop”只表示：

```text
不生成 join interruption
```

不是丢用户消息。

---

## 8.5 新永久测试

### JOIN-P0-1

```text
join attempt active
→ user message
→ Interrupted(UserMessageArrived)
```

保留。

### JOIN-P0-2

```text
user message
→ no join active
→ later join begins
```

断言：

```text
later join remains blocked
```

直到真正：

```text
child completion
或新的 user message
或 Esc
```

### JOIN-P0-3

```text
Begin JoinAttempt
→ user signal arrives
→ mailbox wait setup completes later
```

仍应：

```text
current join wakes
```

这证明真正的 signal-before-register race 被 attempt scope 正确解决。

### JOIN-P0-4

```text
old user message
→ join A
→ join A ends
→ join B
```

B 不得继承 A 的 signal。

---

# 9. Esc：Abort 必须立即撤销 idle-derived continuation capability

## 9.1 不再丢弃 abort Host signal

当前：

```text
MessageAbortedError / AbortError
→ HostSignalAdapter 返回 None
```

改为 typed signal，例如：

```fsharp
HostSignal.AttemptAborted of SessionId
```

注意：

它**不是**：

```text
ProviderFailure
```

所以不能错误推进 fallback。

它只是物理事实：

```text
当前 attempt 不再有资格产生 idle-derived continuation。
```

---

## 9.2 Quiescence 增加 revoke

物理 gate 可以有状态。

因为它表达的是真实物理资源资格，不是业务 stage。

新增：

```fsharp
RevokeCurrentAttempt(session)
```

语义：

```text
当前 attempt 的所有 QuiescencePermit
立即永久失效
```

on abort：

```fsharp
| AttemptAborted session ->
    scope.Quiescence.RevokeCurrentAttempt session
    reconciler.SignalAbort session
```

---

## 9.3 Reconcile wake 增加真实 abort evidence

允许：

```fsharp
type ReconcileWake =
    | IdleWake of QuiescencePermit
    | RetryWake
    | FailureWake
    | AbortWake
```

这是外部事实类别。

不是程序位置。

`AbortWake` 下：

```text
Unknown
Provisional
```

绝不允许产生：

```text
RepairMissingFinalReport
InteractionRepair
"#"
```

可有界 reread 等 snapshot 出现 `TurnAborted`。

预算耗尽仍不得恢复 idle capability。

---

# 10. Esc 回归矩阵

### ESC-P0-1

```text
provider running
→ Esc
→ TurnAborted
```

断言：

```text
zero "#"
zero InteractionRepair
exactly one Aborted terminal
```

### ESC-P0-2：最危险 race

```text
BeginAttempt
→ ObserveIdle，mint permit
→ permit 尚未 consume
→ Esc
→ delayed Unknown reconcile
```

断言：

```text
TryConsume(old permit) = false
zero physical repair prompt
```

### ESC-P0-3

```text
Esc
→ delayed SessionIdle
```

不得重新 mint 可用 permit 给已经 aborted 的 attempt。

必须等：

```text
next real BeginProviderAttempt
```

才重新建立资格。

---

# 11. Canary：禁止“poll 一次 = 因果进展一次”

## 11.1 新硬规则

从本 Change 起：

> **E2E case 文件不得直接调用 `watchdog.advance`。**

只有：

```text
tests/e2e/support/*
```

里的因果观察 primitive 可以 feed watchdog。

新增 static gate：

```text
tests/e2e/cases/**
tests/e2e/*.test.mjs
```

出现：

```text
.watchdog.advance(
```

直接红。

---

## 11.2 Support API

统一提供：

```text
awaitExpectation(id)
awaitFact(name, predicate)
awaitEvent(predicate)
awaitTurnTerminal(...)
awaitMessageChange(...)
awaitProviderRequest(...)
```

这些 helper 只有在观察 token 真正变化时才：

```text
advance(blocking=true)
```

同一个：

```text
message list
fact count
event seq
expectation state
```

重复 poll 不 renew。

---

## 11.3 Student–Teacher 立即改

删除：

```js
while (...) {
    sleep(50)
    watchdog.advance(...)
}
```

当前两处已经明确存在。

换成：

```text
wait expectation student-learn
wait expectation teacher-answer
wait expectation student-compile
wait exact final assistant message transition
```

final message 如果 Host API 没有事件，就：

```text
poll snapshot
```

但只有：

```text
snapshot token / latest message id 发生变化
```

才记录 causal progress。

重复 GET 同一 snapshot：

```text
blocking = false
```

或者完全不 feed。

---

# 12. 全仓 Canary 审计

搜索所有：

```text
watchdog.advance({ ..., blocking: true })
```

尤其检查：

```text
while
setInterval
setTimeout retry
poll GET
awaitEvent(any event)
journal any append
provider HTTP traffic
```

本 Change 逐项分类：

```text
Causal:
    named script expectation consumed
    target fact count changed
    expected provider request appeared
    assistant terminal appeared
    required Host stage completed

Background:
    SSE traffic
    unrelated journal append
    polling iteration
    unchanged API snapshot
    logs
    provider bytes
    unrelated sidecar progress
```

任何无法明确说明：

> “这个 observation 为什么证明被测链向前走了一步？”

的 feed：

```text
blocking=false
```

---

# 13. DSL gate：堵死“mutable record 状态机”逃逸

## 13.1 修 field parser

至少从：

```js
/^(\w+)\s*:\s*.../
```

改成能识别：

```fsharp
mutable Foo: Type
```

并保留：

```text
isMutable=true
```

---

## 13.2 新 gate：mutable-record-field

新增：

```text
mutable-record-field
```

规则：

### Domain / Application

任何：

```fsharp
mutable Foo:
```

直接红。

没有 exemption。

### Session / Process

只有真正物理状态才允许，并必须显式：

```fsharp
/// DSL-state-combination: physical
```

且不得是：

```text
business Stage
business Phase
business RunState
business next-action
```

---

## 13.3 永久 fixture 必须复制 StudentRunCell 的逃逸形态

新增：

```text
tests/unit/verify/fixtures/mutable-record-program-counter.fs
```

至少包含：

```fsharp
type Cell =
    { mutable State: OtherNamespace.RunState
      mutable Return: ReturnInfo option
      mutable Handoff: bool
      mutable Final: FinalInfo option }
```

要求：

```text
RED
```

字段改名以后仍必须 RED。

例如：

```text
Availability
Receipt
Ownership
Closure
```

仍然 RED。

防止重新变成字段黑名单游戏。

---

# 14. `state-product` 必须识别 mutable field

当前 parser 因为不解析 `mutable Foo:`，所以压根没机会把 option/bool 算作 axis。

修复后：

```text
mutable Return: X option
mutable Handoff: bool
mutable Final: Y option
```

至少已经是三个独立状态轴。

未分类：

```text
RED
```

即使有：

```text
DSL-state-combination: physical
```

人工 proof 仍必须证明：

```text
每个轴都确实是物理资源
```

不能靠一行 annotation 给状态机办合法身份证。

---

# 15. TurnCompletionProgram 拆解

目标不是“整理一下”。

目标是把它降到通用 terminal plumbing。

---

## 15.1 禁止列表

完成以后 `TurnCompletionProgram.fs` 不得引用：

```text
Role.Manager
Role.Reviewer
StudentTeacherRuntime
ManagerLifecycleGate
ReviewerGuardState
HostReviewGuard
Finality
ManagerIdleEncouragement
ReviewConfirmation
ReviewerGuard
```

加 architecture gate。

---

## 15.2 新 Router

可以存在一个极薄 router：

```fsharp
let handleTurn env context =
    task {
        match context.Turn.Role with
        | Some Role.Student
        | Some Role.Teacher ->
            return!
                StudentTeacherWorkflow.observe env context

        | Some Role.Reviewer ->
            return!
                ReviewerWorkflow.observe env context

        | Some Role.Manager ->
            return!
                ManagerWorkflow.observe env context

        | _ ->
            return!
                OrdinaryTurnWorkflow.observe env context
    }
```

Router 只能：

```text
按 bounded context 委派
```

禁止在这里计算：

```text
pending
shouldContinue
already...
outstanding...
phase...
```

---

# 16. ManagerWorkflow 同样迁走

当前 Manager 已经比旧版有所改善，但仍由 TurnCompletionProgram 大判定表拥有。

迁到：

```text
Application/Manager/ManagerWorkflow.fs
```

用 CE 顺序表达：

```fsharp
let rec observe env turn =
    task {
        do! BackgroundWork.ensureCollected env turn

        let! life =
            ManagerLife.requireCurrent env turn.SessionId

        do!
            ManagerActivation.ensureAccepted
                env
                life

        do!
            ManagerFinality.awaitIfOutstanding
                env
                life

        return!
            ManagerLabor.continueOrComplete
                env
                life
                turn
    }
```

这些 helper：

```text
要么完成一个动作
要么等待一个事实
要么返回真实领域结果
```

不得返回：

```text
NextManagerAction
ManagerStage
ManagerDisposition
```

---

# 17. ReconcileProgram 不得继续成长成“大程序决策器”

`ReconcileDecision` 只允许解决：

```text
snapshot observation 是否稳定
是否需要 causal reread
是否可以 publish
是否存在 idle repair capability
```

它不拥有：

```text
Reviewer workflow
Student workflow
Manager lifecycle
Join lifecycle
```

也就是说 Reconcile 是：

```text
observation stabilization boundary
```

不是：

```text
business operating system
```

---

# 18. 实施顺序：不得跳步

本 Change 必须严格按以下顺序。

---

## Phase 0 — Freeze

在：

```text
changes/active/<this-change>.md
```

冻结本 Proposal 原文。

任何实现者不得在编码过程中把以下目标“重新解释”为：

```text
先修测试
以后再重构
```

---

## Phase 1 — RED：先证明当前五个严重错误

必须先新增并看到 RED：

```text
R1 Reviewer duplicate guard race
R2 Join old-user-message interrupts future join
R3 Esc invalidates pending idle repair
R4 StudentRunCell mutable state-product DSL gate
R5 Student canary polling cannot renew watchdog
```

没有五个 RED：

```text
禁止改 production。
```

---

## Phase 2 — DSL gate 先堵漏洞

先修：

```text
scripts/checks/dsl-ownership.mjs
tests/unit/verify/*
```

确保原 StudentRunCell 形态会 RED。

然后才允许迁移 Student。

理由：

> 否则程序员完全可能一边“重构”，一边又造一个名字不同的新 cell。

---

## Phase 3 — JoinAttempt clean break

修改：

```text
Session/JoinInterruptRegistry.fs
Infrastructure/OpenCode/Tools/JoinTool.fs
CompletionMailbox / Join drain call sites
Host ingress wake producer
join unit/integration/e2e
```

完成标准：

```text
no pendingUserMessage
no future latch
current attempt user wake works
future join immune
Esc remains operator_abort
completion still beats interrupt
```

---

## Phase 4 — Abort capability revocation

修改：

```text
HostSignal.*
SessionQuiescenceGate.fs
Reconciler.fs
ReconcileProgram.fs
Turn repair tests
```

完成标准：

```text
MessageAbortedError 不再无声丢弃
abort revokes idle permits
abort race produces zero "#"
```

---

## Phase 5 — ReviewerWorkflow single owner

新增 Application workflow。

从：

```text
TurnCompletionProgram
FinalityController
```

删除 reviewer continuation ownership。

完成后跑 duplicate race。

---

## Phase 6 — StudentTeacher clean break

先删除：

```text
RunState
TeacherReturnHandoff
StudentRunCell
```

编译应该 RED。

然后按独立 physical scope 重建。

**禁止先造 NewStudentRunCell 再逐字段搬过去。**

这是最容易阳奉阴违的路径。

---

## Phase 7 — TurnCompletionProgram 去业务化

迁 Manager / Reviewer / Student–Teacher owner 后：

```text
TurnCompletionProgram
```

只能保留通用：

```text
ordinary completion
generic abort/failure plumbing
XTrace generic terminal plumbing
```

跑静态 forbidden-reference gate。

---

## Phase 8 — Canary causal integrity

删除 case 内 direct watchdog feed。

迁所有“poll = progress”的路径。

Student–Teacher 必须成为第一条示范 canary。

---

## Phase 9 — One-stroke unhappy path

新增一个真正把所有坎坷串起来的一笔画 scenario。

---

# 19. 必须新增的一笔画 P0 scenario

建议：

```text
temporal-ownership-unhappy-path
```

轨迹：

```text
Student or Manager root starts
↓
normal provider work
↓
fork child
↓
join starts
↓
real user message arrives
↓
CURRENT join interrupted user_message
↓
message consumed by next provider turn
↓
start ANOTHER join
↓
prove previous user message does NOT wake it
↓
Esc
↓
current join returns operator_abort
↓
prove zero bare "#"
↓
child later completes
↓
join drains child normally
↓
Reviewer prose-only terminal
↓
exactly one verdict guard
↓
first PERFECT
↓
exactly one skeptical challenge
↓
second PERFECT
↓
confirmed exactly once
```

Student–Teacher 另有：

```text
Student root
→ teacher call
→ Teacher return
→ normal Teacher completion
→ Student resumes
→ compile
→ SKILL mutation
→ final return
→ exact final assistant
→ QA gone
```

全程：

```text
zero RunState dependency
zero timer-fed progress
zero duplicate continuation
```

---

# 20. 测试纪律

每一个时序测试必须明确写四件事：

```text
Given
Trigger
Expected observable effect
Forbidden observable effect
```

例如：

```text
Given:
  no join attempt exists

Trigger:
  external user message

Then:
  queue contains user message

Forbidden:
  any future JoinAttempt begins interrupted
```

禁止只写：

```text
works
passes
does not hang
```

---

# 21. 禁止以 wall-clock 运气证明 race

任何 race test：

```text
sleep 20ms
hope Register happened
send signal
```

如果能够用：

```text
explicit test barrier
registered-attempt probe
fact
callback
```

替代，就必须替代。

时间只能是：

```text
兜底失败预算
```

不能是 happens-before 证明。

---

# 22. 本 Change 一票否决项

Reviewer 看到以下任何一个，直接 `REVISE`：

1. 新增新的长期：

   ```text
   State
   Stage
   Phase
   Mode
   Next
   Should
   Already
   Handoff
   ```

   来替代被删字段；

2. 一个 record 同时保存：

   ```text
   lifecycle state
   pending return
   final slot
   repair state
   ```

3. `TurnCompletionProgram` 仍判断 Reviewer / Manager / Student 业务步骤；

4. Finality 仍发送 reviewer challenge / verdict guard；

5. Join registry 仍有 zero-waiter latch；

6. 用 session id 而非 attempt lifetime 表达“一条用户消息以后要打断哪个 join”；

7. Esc race 仍可能进入 missing-final-report；

8. e2e case 自己在 polling loop 调 `watchdog.advance(blocking=true)`；

9. DSL gate 仍看不见 `mutable Foo:` record field；

10. 因为：

    ```text
    baseline 本来就失败
    ```

    而豁免相关 hanging test；

11. 修改超时时间让 canary 变绿；

12. 增加 retry/dedupe 层代替唯一 writer；

13. 以：

    ```text
    “这是 process-local，不是 durable，所以不是状态机”
    ```

    为业务程序计数器辩护。

Process-local 只能证明：

```text
重启后会丢
```

不能证明：

```text
它不是程序计数器
```

---

# 23. Completion Gate

本 Change 只有同时满足以下条件才允许 Completed。

## Static

```text
npm run lint
dsl ownership threshold = 0
no mutable-record program counter
TurnCompletion forbidden-reference gate green
Finality reviewer-continuation ownership gate green
e2e direct-watchdog-feed gate green
```

## Unit

```text
全部 unit 通过
zero known hang
zero “baseline also hangs” exemption
```

## Integration

至少：

```text
real chat.message wakes active join
old chat.message does not wake future join
Esc revokes repair
Reviewer double-reconcile sends one continuation
Student Teacher tool-return terminal handshake
```

## E2E

至少：

```text
student-teacher
reviewer-verdict
manager-unhappy-path
new temporal-ownership-unhappy-path
restart variants affected by these workflows
```

全部 green。

## Full gate

必须真正执行并通过：

```text
npm run check
```

不能再出现：

```text
“full check 未作为 close 条件”
```

---

# 24. Reviewer 验收时不要问“代码看起来是不是更函数式”

只问以下问题。

### 问题一

删掉所有业务 mutable state 后：

> **仅从 facts、capabilities 和 CE 调用结构，能不能读出完整 happens-before？**

不能：

```text
REVISE
```

### 问题二

任意自动 continuation：

> **能不能指出唯一业务 owner？**

如果答案是：

```text
TurnCompletion 也会
Finality 有时也会
Guard 自己也可能会
但我们有 dedupe
```

直接：

```text
REVISE
```

### 问题三

任意 interrupt：

> **它绑定的是哪个具体 lifetime？**

如果答案是：

```text
session
```

而需求实际是：

```text
current join attempt
current provider attempt
```

直接：

```text
REVISE
```

### 问题四

任意 watchdog renewal：

> **哪一个新 observation 证明因果链前进？**

如果答案是：

```text
我们又 poll 了一次
```

直接：

```text
REVISE
```

---

# 25. 这次重构的最终形态

正确架构应该让代码自己读起来接近：

```text
ManagerWorkflow
    ensure children gathered
    ensure activation
    await finality
    continue labor

ReviewerWorkflow
    inspect witness
    request missing verdict
    challenge first perfect
    complete confirmed review

StudentTeacherWorkflow
    append question
    await Teacher call
    compile
    validate artifact
    final return

JoinWait
    open attempt scope
    race completion/user/abort/deadline
    drain again
    resolve
```

而不是：

```text
TurnEnd
    if A
    elif B
    elif C
    match State
    if Pending
    if Handoff
    if Already
    if Should
```

前者：

> **程序结构就是时序证明。**

后者：

> **程序员在脑中运行状态机，代码只存它的碎片。**

---

# 26. 最后的工程裁决

这次最需要纠正的不是某一行代码。

是一个错误的完成观：

> “加了 CE、加了 DU、加了 DSL gate、测试绿了，就等于完成了结构化程序迁移。”

不等于。

如果真正的业务 happens-before 仍然依赖：

```text
长期 State
多个 Pending
TurnEnd 优先级
session latch
多 writer dedupe
polling heartbeat
```

那么所谓 Direct CE 只是外观。

这也是为什么当前会出现看似彼此毫无关系的：

```text
Reviewer 双发
Student–Teacher 挂死
Esc 后 "#"
未来 join 被旧消息中断
Canary 卡着时序边缘通过
```

它们不是运气差。

它们是同一种架构自然产生的结果：

> **时序没有唯一 owner。**

本 Change 的完成标准因此只有一句话：

> **让“谁在什么时候可以做什么”由 F# CE 的调用结构、真实 durable evidence 和 attempt-scoped capability 共同决定，而不是由 TurnEnd 状态推理决定。**

做不到这一点，即使所有现有测试暂时变绿，也必须判定：

```text
REVISE
```

而不是再次宣布 Completed。
