> 本文件是历史变更记录，不是当前产品规范。
> 当前产品语义仅以 `docs/` 正式层为准。

# Proposal：Causal CE — 可观察因果等待、Wait Graph 与 Canary 自解释诊断

**Status:** Proposed
**Priority:** P0 / architecture + debuggability + liveness
**Scope:** F# CE workflow / async capability / Manager / Orchestrator / Reviewer / Finality / Student–Teacher / Join / Recovery / E2E diagnostics
**Suggested file:** `changes/proposed/causal-ce-observability.md`

> **原则：程序结构不仅应该证明“谁有权做什么”，还必须在失败时显然展示“谁正在等什么、谁应该满足它、为什么还没满足”。**
>
> 君子不立危墙。一个时序系统如果只有在所有测试都绿时才显得正确，而 canary 一挂就必须人工横跨 Host event、Journal、projection、provider request、strict mock、session message 和 runtime cell 做考古，则该 DSL 仍未完成。

---

# 0. Executive Decision

当前 Direct CE 重构已经解决了上一阶段的大量根本问题：

* 业务时序逐步从长期 `State / Pending / bool` 搬回 CE 调用结构；
* Reviewer continuation 已有单 owner；
* Manager terminal sequencing 已独立；
* Student–Teacher Teacher 侧已经真正形成 `Returned → Completion` 的单一 CE await 链；
* watchdog 已从“poll 一次就续命”改为因果 observation 才续期；
* DSL 已规定 F# 调用栈就是流程栈。

这些方向正确。

但现在仍缺一层：

> **CE 调用栈拥有 control flow，却没有拥有可解释的 suspended-flow observability。**

典型表现是：3 个 orchestrator canary 在 clean master 上都能 watchdog timeout，只能看到诸如：

```text
orchestrator-publish
  blocked: orch.2
  blocked: manager.3
  blocked: manager.4

orchestrator-restart-publish
  blocked: barrier-reviewer.0
```

但这只能回答：

> “测试脚本还在等什么？”

无法回答：

> “生产 CE 当前到底在等什么？”
>
> “那个等待是谁创建的？”
>
> “谁有资格满足它？”
>
> “满足条件最后推进到哪里？”
>
> “为什么它没有发生？”
>
> “如果永远不发生，哪个 cancel/deadline/fail-closed 会终止它？”

这些 canary 已确认可在未含 Student–Teacher 新改动的 clean master 复现，因此不能归罪于刚完成的 collapse；反过来，它们正好证明当前稳定基线仍缺少足够强的 liveness/diagnostic visibility。

本 Change 因此**不以“把三个 canary 调绿”为首要目标**。

本 Change 的目标是：

> **让任何卡住的业务 CE，在一次诊断 dump 中自动给出最小未满足因果前沿。**

三个 orchestrator canary 是本 Change 的首批真实 RED 样本。

相关先行 Proposal：`changes/proposed/orchestrator-e2e-timeout.md`（现象与验收）；本 Change 负责可解释因果诊断与随后的根因修复。

---

# 1. 当前问题不是“日志太少”

现有 E2E diagnostics 已经收集很多东西：

```text
events
mock requests
session messages
session status
NDJSON
stderr/stdout
process tree
workspace files
```

watchdog timeout 也会 dump：

```text
event tail
Host stdout tail
Host stderr tail
Journal fact tail
blocked expectations
```

并且 strict mock fatal mismatch 也已经打印 request、候选 expectation 和 pending expectation。

所以问题绝不是：

```text
再多打印 100 行日志
```

现有 formatter 甚至可以展示：

```text
Req N:
  tools=[...]
  msgs=N
  last user=...

N unmatched expectations remaining
```

但这些信息仍属于：

> **Observation archaeology**

而不是：

> **Causal explanation**

例如看到：

```text
manager.3 没有消费
Reviewer session idle
Journal 最后出现 PromptAccepted
```

程序员仍然必须自己推理：

```text
Finality 是不是还在 await reviewer？
ReviewerWorkflow 有没有等待 verdict？
这个 reviewer 当前是哪一个 barrier？
Provider attempt 是否还 alive？
哪个 capability 没 resolve？
是不是已经 cancel 但 waiter 没撤？
是不是 producer 已经结束而 consumer 还活着？
```

这说明**业务时序结构与诊断结构仍然脱节**。

---

# 2. 新的“CE 完成标准”

以后不得仅凭：

```text
没有 CurrentStage
没有 NextAction
没有 RunState
DSL gate green
tests green
```

宣称 CE DSL 完成。

对任意 suspended business workflow，系统必须能结构化回答以下五问。

## CCE-001 — Owner

> **谁拥有当前 workflow？**

例如：

```text
ManagerWorkflow
ManagerLife=L8
Session=M9
```

或者：

```text
FinalityRequest=F3
ManagerLife=L8
```

---

## CCE-002 — Wait

> **它现在正在等待哪一个真实条件？**

例如：

```text
AwaitReviewerVerdict
Reviewer=R2
Barrier=B17
ProviderRun=P81
```

而不能只显示：

```text
Task pending
```

---

## CCE-003 — Producer

> **谁有资格满足这个等待？**

例如：

```text
Producer =
  ReviewerWorkflow(
    Reviewer=R2,
    Barrier=B17
  )
```

或：

```text
Producer =
  ExternalProviderAttempt(P81)
```

---

## CCE-004 — Last causal progress

> **与这个等待直接相关的最后一个已发生事实是什么？**

例如：

```text
PromptAccepted(P81)
```

而不是：

```text
最近 Journal 有新行
```

无关 Blogger append、SSE bytes、heartbeat 等不得冒充相关进展。

---

## CCE-005 — Termination

> **如果 producer 永远不满足它，谁负责结束这个 wait？**

必须明确属于以下之一：

```text
Deadline
Attempt cancellation
Workflow cancellation
Session/process termination
Explicit fail-closed condition
Legitimate open-ended external wait
```

不得回答：

```text
应该以后还有一个 idle
一般会继续
watchdog 最后会杀
```

---

# 3. 正式 DSL 增补：DSL-012（已落入 `docs/what/dsl-structured-program.md`）

当前正式规则已经规定：

* F# 原生 CE 是业务控制流；
* 禁止长期 program counter；
* mutable 只允许算法 scratch 和真实物理资源，包括 Task / Dictionary / TCS 等。
* `Application = CE workflow`，`Session = physical resource ownership`，`Infrastructure = protocol adapter`。

正式条款见 `docs/what/dsl-structured-program.md`（DSL-012）。摘要：

任何跨业务 owner、跨 Host turn、跨 provider attempt 或跨 physical capability 的业务等待，都必须能够生成一个 process-local diagnostic wait observation。

该 observation：

```text
可以：
  描述当前 wait
  描述 owner
  描述 producer
  描述 causal identity
  描述 cancellation/deadline
  供 diagnostics 读取

绝对不可以：
  决定业务 branch
  写入 Journal 作为业务事实
  用于 recovery
  用于 dedupe
  mint permit
  推进 workflow
  影响 PromptAuthority
  影响 Finality / Reviewer / Manager 决策
```

一句话：

> **Wait observation 可以看程序，但程序绝不可以看 wait observation。**

---

# 4. 绝不能重新造一个状态机

本 Change 最危险的错误实现是：

```fsharp
type DiagnosticStage =
    | WaitingForManager
    | WaitingForReviewer
    | WaitingForVerdict
    | WaitingForCompletion
```

然后业务代码开始：

```fsharp
match diagnostics.CurrentStage with
| WaitingForReviewer -> ...
```

这是绝对禁止的。

另一个禁止方案是：

```text
WaitEntered
WaitExited
```

写进 AgentJournal，再让 workflow replay 它。

也禁止。

Wait registry 必须满足：

```text
process-local
ephemeral
diagnostic-only
non-authoritative
reconstructible
safe to lose
safe to be stale after crash
```

当前 `Diagnostic.fs` 自己已经明确规定：

> log/diagnostic 永远不能成为 recovery protocol 或 recovery decision。

Causal Wait 继承同一原则。

---

# 5. 类型设计：不要做全仓巨大 WaitKind DU

不要新增：

```fsharp
type GlobalWaitKind =
    | TeacherReturn
    | TeacherCompletion
    | ReviewerVerdict
    | ReviewerChallenge
    | FinalityWitness
    | ManagerFinality
    | OrchestratorManager
    | JoinCompletion
    | JournalChange
    | ProcessExit
    | ...
```

这最终会变成新的跨 bounded-context mega-DU。

正确做法：

> **每个 bounded context 定义自己的 typed wait reason，再降级为通用 diagnostic descriptor。**

---

# 6. 通用诊断核心类型

建议新增：

```text
src/Wanxiangshu/Kernel/CausalWait.fs
```

这里只定义**通用诊断词汇**，不包含 Manager / Reviewer / Student 等业务 case。

建议：

```fsharp
type CausalOwnerRef =
    { Kind: string
      Identity: (string * string) list }

type CausalProducerRef =
    | WorkflowProducer of CausalOwnerRef
    | ExternalProducer of
        kind: string *
        identity: (string * string) list

type WaitEscape =
    | DeadlineAt of DateTimeOffset
    | CancelledBy of CausalOwnerRef
    | ProcessLifetime
    | SessionLifetime
    | OpenEndedExternal

type DiagnosticWait =
    { WaitKind: string
      Owner: CausalOwnerRef
      Subject: (string * string) list
      Producer: CausalProducerRef
      Escapes: WaitEscape list
      Source: string }
```

注意：

```text
WaitKind / Subject 的字符串仅供 diagnostics render。
```

它们不是 Domain vocabulary，不进入 decision。

---

# 7. 每个 bounded context 自己提供 typed reason

例如 Student–Teacher：

```fsharp
type private TeacherWait =
    | ReturnFromTeacher of
        student: SessionId *
        teacher: SessionId

    | TeacherCompletionTerminal of
        student: SessionId *
        teacher: SessionId *
        toolRun: ProviderRunIdentity
```

映射：

```fsharp
let describeTeacherWait wait =
    match wait with
    | ReturnFromTeacher(student, teacher) ->
        { WaitKind = "teacher-return"
          Owner = ...
          Subject =
            [ "student", SessionId.value student
              "teacher", SessionId.value teacher ]
          Producer =
            WorkflowProducer(...)
          Escapes = [...]
          Source = "StudentTeacherRuntime.InvokeTeacher" }

    | TeacherCompletionTerminal(...) ->
        ...
```

Finality 自己定义：

```fsharp
type private FinalityWait =
    | ReviewerTerminal of ...
    | DurableRevisionOrWitness of ...
    | RecordReady of ...
```

Reviewer 自己定义：

```fsharp
type private ReviewerWait =
    | ProviderVerdictAttempt of ...
    | ContinuationAcceptance of ...
```

Manager / Orchestrator 同理。

---

# 8. Reader / Writer 权限必须从类型上隔离

建议新增：

```fsharp
type IWaitObserver =
    abstract Enter: DiagnosticWait -> IDisposable

type IWaitSnapshotReader =
    abstract Snapshot: unit -> DiagnosticWaitSnapshot
```

非常重要：

```text
Application workflow 只拿 IWaitObserver
diagnostics surface 才拿 IWaitSnapshotReader
```

绝不能把：

```fsharp
registry.Snapshot()
```

暴露给业务 workflow。

这样即使程序员想写：

```fsharp
if waitRegistry.Snapshot().Contains(...) then ...
```

也没有接口可以调用。

这比靠 code review 说“不要这么做”可靠。

---

# 9. Session 层物理实现

新增：

```text
src/Wanxiangshu/Session/CausalWaitRegistry.fs
```

实现可以包含：

```text
Dictionary<WaitLeaseId, DiagnosticWait>
bounded ring buffer<WaitTransition>
lock
monotonic local lease id
```

这是合法 physical mutable resource，性质与 TCS / Dictionary / subscription registry 相同。

建议注释：

```fsharp
// DSL-MUTABLE: resource — process-local diagnostic wait registry.
// It is not a business truth source, recovery input, dedupe key,
// workflow branch input, or Journal projection.
```

registry 只负责：

```text
Enter
Leave
Snapshot
last N transitions
```

不得负责：

```text
resolve Task
cancel Task
send prompt
append Journal
choose next action
```

---

# 10. 最核心 API：CausalAwait

新增：

```text
src/Wanxiangshu/Kernel/CausalAwait.fs
```

或者若 Kernel 不应持运行时接口，则放：

```text
src/Wanxiangshu/Session/CausalAwait.fs
```

核心 API：

```fsharp
let awaitTask
    (observer: IWaitObserver)
    (descriptor: DiagnosticWait)
    (pending: Task<'T>)
    : Task<'T> =
    task {
        use _lease = observer.Enter descriptor
        return! pending
    }
```

业务代码以后从：

```fsharp
let! returned = call.Returned.Task
```

变成：

```fsharp
let! returned =
    CausalAwait.awaitTask
        waits
        (TeacherWait.describe
            (ReturnFromTeacher(run.SessionId, call.Teacher)))
        call.Returned.Task
```

**业务顺序完全不变。**

也没有新增 decision。

只是：

```text
CE suspend
→ 注册一个 observation lease
→ Task resolve / fail / cancel
→ lease 自动 Dispose
```

---

# 11. TCS capability 推荐再进一步封装

对跨 owner 的重要单赋值 capability，推荐最终使用：

```fsharp
type CausalPromise<'T>
```

内部拥有：

```text
TaskCompletionSource<'T>
```

对 consumer 只开放：

```fsharp
Await(observer, descriptor)
```

对 producer 只开放：

```fsharp
TryResolve
TryFail
```

**不公开裸 `.Task`。**

例如当前 Student–Teacher 已经有：

```fsharp
TeacherCall =
    { Returned: TaskCompletionSource<Result<TeacherAnswer,string>>
      Completion: TaskCompletionSource<Result<unit,string>> }
```

第二阶段可以演进成：

```fsharp
TeacherCall =
    { Returned: CausalPromise<Result<TeacherAnswer,string>>
      Completion: CausalPromise<Result<unit,string>> }
```

这样以后新增跨 owner capability 时，忘记 observability 会变得困难。

第一版不要求一次改完全仓。

---

# 12. Race 必须作为一个 wait 展示

当前很多关键流程不是简单：

```text
await X
```

而是：

```text
X
vs timeout
vs cancellation
```

例如 Finality 的 Reviewer await 当前就是：

```text
reviewer terminal
vs timeout
vs cancel
```

真实代码使用 `Promise.race`。

不得打印成三个互不相关 wait。

建议：

```fsharp
CausalAwait.race
    observer
    { Primary = ReviewerTerminal(...)
      Escapes =
        [ DeadlineAt deadline
          CancelledBy reviewAttemptOwner ] }
    finished
```

诊断显示：

```text
WAIT reviewer-terminal
  reviewer=R2
  barrier=B17

escape:
  deadline in 83s
  cancellation=FinalityRequest/F3
```

---

# 13. Wait transition history

当前 active snapshot 只能回答：

```text
现在在等什么
```

还需要一个**有界、诊断-only**的 ring buffer，回答：

```text
刚才发生过什么
```

例如：

```text
#194 ENTER reviewer-terminal R2/B17
#195 RESOLVED reviewer-terminal R1/B16
#196 ENTER reviewer-verdict R2/P81
#197 CANCELLED manager-child M7
```

建议只保留：

```text
256 或 512 条
```

不能无界增长。

每项：

```fsharp
type WaitTransition =
    { Sequence: int64
      Kind: Entered | Left
      Wait: DiagnosticWait
      Exit: DiagnosticWaitExit option }
```

Exit 仅用于诊断：

```text
Resolved
Failed
Cancelled
TimedOut
Disposed
```

它不是业务 result。

---

# 14. 最小未满足因果前沿

这是本 Change 的核心输出。

定义：

> **Minimal Unsatisfied Causal Frontier = 从当前活着的根 workflow 出发，沿“consumer 正等待 producer”的关系向下追踪，直到无法继续解释的第一个 unresolved producer。**

算法必须是纯诊断算法。

---

## 14.1 建图

每个 active wait：

```text
Owner
  --waits-for-->
Producer
```

例如：

```text
OrchestratorJob O1
  → ManagerJob M4

ManagerWorkflow M4
  → Finality F3

Finality F3
  → ReviewerWorkflow R2

ReviewerWorkflow R2
  → ProviderAttempt P81
```

---

## 14.2 Frontier 情况 A：外部 producer

例如：

```text
ProviderAttempt P81
```

没有更深内部 owner。

输出：

```text
FRONTIER:
  waiting for external provider attempt P81
```

---

## 14.3 Frontier 情况 B：producer 消失

例如 consumer 仍然等待：

```text
ReviewerWorkflow R2
```

但当前 registry 中没有对应 owner，也没有 terminal durable evidence。

输出：

```text
BROKEN CAUSAL EDGE:
  Finality F3 waits for ReviewerWorkflow R2
  but no active R2 workflow exists
  and no terminal witness is durable
```

这是高度可疑的生产 bug。

---

## 14.4 Frontier 情况 C：producer 活着但未声明任何 wait

例如：

```text
ManagerWorkflow M4 active
no current wait
no terminal
```

超过很短诊断 sampling window 后：

```text
PRODUCER RUNNING WITHOUT DECLARED WAIT
```

这可能意味着：

```text
CPU loop
missing instrumentation
fire-and-forget lost task
workflow stuck before await
```

不能直接断言 bug，但必须显眼。

---

## 14.5 Frontier 情况 D：cycle

例如：

```text
A waits B
B waits C
C waits A
```

diagnostics 必须 SCC 检测并输出：

```text
CAUSAL WAIT CYCLE:
  A → B → C → A
```

不能递归爆栈。

---

# 15. diagnostics 输出顺序必须改变

当前 watchdog 先 dump：

```text
event tail
stdout
stderr
journal
blocked expectations
```

以后 timeout 第一屏必须是：

```text
════════════ CAUSAL FRONTIER ════════════
...
```

然后才是原始材料。

建议完整顺序：

```text
1. Minimal causal frontier
2. Active wait graph
3. Last wait transitions
4. Strict-mock blocked expectations
5. Correlation between expectation and producer
6. Journal relevant facts
7. Session messages
8. Event tail
9. Host stdout/stderr
10. process tree
```

原始信息不能删，只是降到第二层。

---

# 16. Canary 的目标输出示例

假设 `orchestrator-restart-publish` 再挂。

期望输出类似：

```text
════════════ CAUSAL FRONTIER ════════════

OrchestratorWorkflow
  session=O1
  job=OJ7

└── waits: manager-job-completion
    manager_session=M4
    manager_job=MJ2

    ManagerWorkflow
      session=M4
      life=L8

    └── waits: finality-request
        request=F3

        FinalityController
          request=F3
          tree=abc123

        └── waits: reviewer-terminal
            reviewer=R2
            barrier=B17

            ReviewerWorkflow
              reviewer=R2
              barrier=B17

            └── waits: provider-verdict
                provider_run=P81

FRONTIER:
  external provider attempt P81

last related causal progress:
  PromptAccepted(P81)

escape:
  review-attempt cancellation
  deadline in 81s
```

此时 strict mock 如果仍等：

```text
barrier-reviewer.0
```

diagnostics 再加：

```text
STRICT MOCK CORRELATION

blocked expectation:
  barrier-reviewer.0

production frontier:
  provider_run=P81
  reviewer=R2
  barrier=B17

correlation:
  MATCHED
```

---

# 17. 如果测试脚本本身错了，也必须显然

另一种情况：

```text
production active waits: none
Manager completed
Orchestrator completed
```

但 strict mock 仍等：

```text
manager.3
```

应输出：

```text
HARNESS/PRODUCTION DIVERGENCE

production workflow:
  completed / no active wait

strict mock:
  manager.3 still blocking

No production owner exists that could satisfy this expectation.

Likely classes:
  - stale scenario expectation
  - wrong lane binding
  - expectation attached to obsolete provider run
```

注意：

> 只写 `Likely classes`，不要 diagnostics 自动断言根因。

这样生产 bug 与 canary script bug 可以在第一屏区分。

---

# 18. E2E diagnostics 改造

修改：

```text
tests/e2e/support/diagnostics-collect.js
tests/e2e/support/diagnostics-format.js
tests/e2e/support/diagnostics.js
tests/e2e/support/watchdog.js
tests/e2e/support/scenario-runtime.js
```

当前 `diagnostics-collect` 已经形成结构化 record，再由 formatter 渲染。

在 record 新增：

```js
causalWaitSnapshot
causalWaitHistory
causalFrontier
causalExpectationCorrelation
```

不要让 formatter 自己猜业务状态。

`gatherDiagnostics()` 收的是已经结构化的数据。

---

# 19. F# → E2E 的诊断读取通道

优先级按以下顺序实现。

## 方案 A — 已有内部诊断 read surface 能扩展

如果当前 Plugin/Host 已存在可安全扩展的测试诊断读取面：

```text
直接返回 WaitSnapshot
```

优先用它。

---

## 方案 B — 没有 read surface 时

建立一个**明确非权威**的 process-local diagnostic snapshot bridge。

允许使用：

```text
in-memory snapshot
+
test/debug-only read adapter
```

如果由于 Host/E2E 进程边界确实没有读取能力，才允许写：

```text
<workDir>/.wanxiangshu/diagnostics/causal-waits.json
```

但必须满足：

```text
不是 Journal
启动时覆盖
dispose 时删除 best-effort
包含 process identity / snapshot sequence
业务代码绝不读
recovery 绝不读
prompt 绝不读
```

禁止把这个文件变成新的 durable protocol。

---

# 20. 第一批必须 instrument 的真实流程

不要一上来扫全仓。

按因果价值迁移。

## Phase A — 当前失败的 Orchestrator 链

第一批：

```text
Application/Orchestration/*
ManagerWorkflow.fs
FinalityController.fs
ReviewerWorkflow.fs
```

因为 3 个现存 failing canary 就在这里。

目标是：

> **先让失败变得一眼可解释，再修失败。**

---

## Phase B — Student–Teacher

当前 Teacher 已经是真 CE：

```text
sendTeacherPrompt
→ await Returned
→ await Completion
```

它是最适合证明 CausalAwait 不改变语义的 pilot。

instrument：

```text
Returned
Completion
```

要求：

```text
原测试结果完全不变
只是 diagnostics 多出 wait graph
```

---

## Phase C — Join / Agent completion

instrument：

```text
JoinAttempt completion
user-message wake
operator abort
deadline
family recovery/journal completion
```

一个 Join race 必须显示为一个 composite wait，而不是多个 disconnected Task。

---

## Phase D — Recovery / Journal waits

例如：

```text
AwaitJournalChangeFrom
record ready
completion pulse
```

必须明确：

```text
Journal wake = transport observation
durable completion = truth
```

不能把“Journal changed”本身描述成业务满足条件。

---

## Phase E — Process / PTY

已有 `NodeProcessWait` 本来就被 DSL-009 指定拥有 exit/deadline/cancel/kill acknowledgement 的完整等待作用域。

把这些物理等待暴露到同一 diagnostics graph。

---

# 21. 不得偷偷把 Application wait 变成大 Decision DU

禁止：

```fsharp
type WaitDecision =
    | WaitForReviewer
    | WaitForManager
    | WaitForJournal
    | WaitForProvider
```

然后：

```fsharp
match waitDecision with ...
```

这是把 program counter 重新包装。

正确：

```fsharp
task {
    ...
    let! witness =
        CausalAwait.awaitTask
            waits
            descriptor
            witnessTask

    ...
}
```

业务程序仍然由 CE 源码顺序表达。

---

# 22. RED 测试必须先写

在 production 实现前，先看到以下 RED。

## RED-1：active wait 可见

建立：

```text
Flow A
→ await unresolved capability X
```

断言：

```text
snapshot 有 1 active wait
owner=A
producer=X
```

当前应 RED。

---

## RED-2：resolve 后自动消失

```text
Enter wait
resolve Task
```

断言：

```text
active=0
history 最后一条=Resolved
```

---

## RED-3：异常也不泄漏

```text
await Task throws
```

断言：

```text
active=0
history=Failed
```

---

## RED-4：cancel 不泄漏

```text
await cancellation
```

断言：

```text
active=0
history=Cancelled
```

---

## RED-5：nested causal graph

构造：

```text
A waits B
B waits External C
```

frontier 必须：

```text
A → B → C
```

---

## RED-6：missing producer

```text
A waits B
B 不存在
```

必须报告：

```text
broken causal edge
```

---

## RED-7：cycle

```text
A → B → C → A
```

必须输出 cycle，不能 hang。

---

## RED-8：业务代码不能读取 snapshot

编译/architecture contract 必须保证：

```text
Application workflow
```

只见 `IWaitObserver`，不能见 `IWaitSnapshotReader`。

---

## RED-9：Wait observation 不得写 Journal

静态 gate 搜：

```text
DiagnosticWait
WaitTransition
CausalWait
```

不得进入：

```text
Kernel/Fact.fs
FactCodec
Journal fold
AgentFact
ManagerLifecycleFact
```

---

## RED-10：Canary timeout 首屏包含 frontier

人为制造最小 stalled scenario：

```text
Manager → unresolved fake capability
```

watchdog timeout 后 stderr 必须包含：

```text
CAUSAL FRONTIER
owner=
wait=
producer=
```

---

# 23. 当前 3 个 orchestrator canary 是强制 RED

不要先修：

```text
orchestrator-publish
orchestrator-unhappy-path
orchestrator-restart-publish
```

当前 Change 的前半段要求它们**保持原失败形态**。

先执行并保存：

```text
旧 diagnostics
```

然后完成 wait graph instrumentation。

再重跑。

要求此时即使仍 RED，也必须把失败从：

```text
watchdog timeout
blocked manager.3
```

升级为：

```text
root workflow
→ causal chain
→ exact frontier
→ producer
→ last progress
→ escape
```

只有达到这一点后，才允许开始修 canary root cause。

---

# 24. 修 Canary 时的新铁律

有了 frontier 后，禁止再使用：

```text
多 sleep 100ms
多等 20 秒
再 feed 一次 watchdog
放松 expectation
删掉 blocking expectation
改成 nonblocking
加 retry 直到碰巧过
```

必须从 frontier 得到一个具体断边：

例如：

```text
Finality waits Reviewer R2
但 ReviewerWorkflow 没启动
```

那么修 owner/start。

或者：

```text
ReviewerWorkflow waits ProviderRun=P81
但 strict mock expectation 绑定的是 P80
```

那么修 scenario/provider-run binding。

或者：

```text
production 已完成
strict mock 还等 manager.3
```

那么修测试声明。

必须留下：

```text
before frontier
root cause
production/harness fix
after frontier
```

作为 Change proof。

---

# 25. Static gate 只做它真正擅长的事情

不要再造：

```text
causal-wait-regex-v3
```

然后声称“机器证明所有 wait 已观测”。

静态 gate 只负责可以可靠证明的边界：

```text
1. Domain 不得引用 CausalWaitRegistry implementation
2. Application 不得访问 IWaitSnapshotReader
3. CausalWait 不得进入 Fact/Journal codec
4. diagnostics snapshot 不得进入 PromptDispatcher / decision
5. critical migrated sites 不得重新直接 await 裸 TCS.Task
6. CausalWaitRegistry 的 mutable 必须标 physical diagnostic resource
```

第 5 项只对**已迁移的明确关键文件**做 ratchet：

```text
StudentTeacherRuntime.fs
FinalityController.fs
ManagerWorkflow.fs
ReviewerWorkflow.fs
Orchestration Runtime/Program
Join wait owner
```

不要假装 regex 可以理解全仓所有异步语义。

---

# 26. 推荐文件改动清单

## 新增

```text
src/Wanxiangshu/Kernel/CausalWait.fs
src/Wanxiangshu/Session/CausalWaitRegistry.fs
src/Wanxiangshu/Session/CausalAwait.fs

tests/unit/.../causal-wait.test.mjs
tests/unit/.../causal-frontier.test.mjs
tests/integration/.../causal-wait-diagnostics.test.mjs
```

如果需要：

```text
scripts/checks/causal-wait-boundary.mjs
```

---

## 修改生产

```text
src/Wanxiangshu/Infrastructure/OpenCode/Tools/ToolRuntimeScope.fs

src/Wanxiangshu/Application/Manager/ManagerWorkflow.fs

src/Wanxiangshu/Application/Review/ReviewerWorkflow.fs

src/Wanxiangshu/Infrastructure/OpenCode/Tools/FinalityController.fs

src/Wanxiangshu/Session/StudentTeacherRuntime.fs

Join / HostForkRuntime 真实 wait owner

Process/NodeProcessWait.fs
```

---

## 修改 E2E

```text
tests/e2e/support/diagnostics-collect.js
tests/e2e/support/diagnostics-format.js
tests/e2e/support/diagnostics.js
tests/e2e/support/watchdog.js
tests/e2e/support/scenario-runtime.js
```

---

## 修改正式文档

```text
docs/what/dsl-structured-program.md
docs/shape/dsl-structured-program.md
docs/how/dsl-structured-program.md
docs/proof/dsl-structured-program.md

docs/proof/verify.md
```

必要时同步：

```text
docs/shape/execution.md
docs/proof/execution.md
```

---

# 27. 文档层应该分别写什么

## what

定义：

```text
observable causal waits (see DSL-012 in docs/what)
observation != authority
```

---

## shape

定义：

```text
IWaitObserver
IWaitSnapshotReader
CausalWaitRegistry
业务层只能写 observation
diagnostics 层才能读 snapshot
```

---

## how

给程序员三个复制模板：

```text
simple await
capability await
race await
```

并明确：

```text
不要手动 Enter/Leave
优先 CausalAwait bracket
```

---

## proof

必须有表：

| 路径                   | Owner                   | Wait                  | Producer             | Escape           | Proof           |
| -------------------- | ----------------------- | --------------------- | -------------------- | ---------------- | --------------- |
| Teacher Returned     | Student teacher tool CE | Teacher return        | Teacher session/tool | cancel           | unit/e2e        |
| Teacher Completion   | same                    | terminal completion   | Teacher workflow     | budget/cancel    | unit            |
| Finality reviewer    | Finality request        | reviewer terminal     | ReviewerWorkflow     | deadline/cancel  | unit/e2e        |
| Manager finality     | ManagerWorkflow         | finality result       | Finality             | life abort       | e2e             |
| Orchestrator manager | Orch workflow           | manager job           | ManagerWorkflow      | job cancellation | e2e             |
| Join                 | JoinAttempt             | completion/user/abort | child/user/operator  | attempt/deadline | integration/e2e |

任何一格填不出来：

```text
REVISE
```

---

# 28. 代码审查时新增五问

以后 Reviewer 在检查 async CE 时，不再只问：

```text
有没有 State？
有没有 mutable？
```

必须同时问：

### 一

> 从源码能否读出业务 happens-before？

否 → REVISE。

### 二

> 每一个自动动作是否有唯一 owner？

否 → REVISE。

### 三

> 每一个 suspend 点是否能说明在等什么真实 capability？

否 → REVISE。

### 四

> diagnostics 能否指出 producer 与最后相关 causal progress？

否 → REVISE。

### 五

> wait 永远不满足时，是否存在明确 termination contract？

否 → REVISE。

---

# 29. 明确禁止的“偷工减料完成方式”

以下任何一种出现，本 Change 不得进入 completed：

```text
只给 watchdog 多打印日志
只把 timeout 调大
只给 orchestrator canary 加 verbose
只把 blocked expectation 打得更漂亮
只记录 function name，不记录 owner identity
只记录 Task pending，不记录 producer
只记录 producer，不记录 cancellation/deadline
把 wait observation 写 Journal
业务逻辑读取 wait registry
用 wait registry 做 dedupe
用 wait registry 做 recovery
新增 DiagnosticStage
新增 WaitingForX 业务状态机
靠 regex 宣称全部 await 已覆盖
只让 targeted canary green，不跑完整 gate
```

---

# 30. 实施顺序——不得跳步

## Phase 0 — Freeze

把本 proposal 原样放：

```text
changes/active/causal-ce-observability.md
```

冻结 scope。

---

## Phase 1 — RED

先写：

```text
RED-1 .. RED-10
```

并重新运行三个 orchestrator canary，保存原始失败输出。

不得先改生产业务逻辑。

---

## Phase 2 — Core types

实现：

```text
CausalWait
IWaitObserver
IWaitSnapshotReader
CausalWaitRegistry
CausalAwait
frontier pure algorithm
```

只跑 unit。

---

## Phase 3 — E2E diagnostic bridge

让 E2E diagnostics 可以读 snapshot。

此时还不迁大业务。

构造 fake wait，证明 watchdog 能显示 frontier。

---

## Phase 4 — Student–Teacher pilot

只 instrument：

```text
Returned
Completion
```

要求：

```text
所有 Student–Teacher 原行为测试不变
CE collapse contract 不变
active lease 无泄漏
```

---

## Phase 5 — Orchestrator failure chain

依次 instrument：

```text
Orchestrator → Manager
Manager → Finality
Finality → Reviewer
Reviewer → Provider/continuation
```

然后重跑 3 个 failing canary。

**仍然允许 RED。**

但必须已经得到明确 frontier。

---

## Phase 6 — Root cause repair

一次只修 frontier 暴露的真实断边。

每修一个：

```text
RED
→ root cause
→ smallest fix
→ GREEN
```

禁止顺手大改。

---

## Phase 7 — Join / Recovery / Process

迁移其它高价值业务 wait。

---

## Phase 8 — Boundary gate

加静态边界和 ratchet。

---

## Phase 9 — Full verification

至少真实运行：

```text
npm run check
npm run test:e2e
```

若环境允许：

```text
npm run check:release
```

当前仓库自己已经把 `check:release` 定义为包含完整 e2e repeat 的更强 gate，因此它是最终封板的优选。

---

# 31. Completion Criteria

本 Change 只有同时满足以下条件才允许移入 `completed/`。

## Architecture

```text
[ ] Wait observation 完全非权威
[ ] Application 无 Snapshot read API
[ ] Journal 无 Wait fact
[ ] 无 DiagnosticStage / WaitDecision program counter
[ ] critical cross-owner waits 均有 owner/producer/escape
```

## Unit

```text
[ ] enter/resolve/cancel/fail/dispose 全无 lease leak
[ ] nested graph 正确
[ ] missing producer 正确
[ ] cycle 正确
[ ] bounded history 正确
[ ] reader/writer capability separation 正确
```

## Integration

```text
[ ] Teacher Returned→Completion graph 正确
[ ] Finality→Reviewer graph 正确
[ ] Manager→Finality graph 正确
[ ] Join race graph 正确
[ ] cancellation 后 active wait 清零
```

## E2E diagnostics

故意 stall 一个 canary，必须一屏显示：

```text
root owner
wait chain
frontier producer
last causal progress
termination/escape
blocked expectation correlation
```

## Existing problematic canaries

```text
[ ] orchestrator-publish green
[ ] orchestrator-unhappy-path green
[ ] orchestrator-restart-publish green
```

并且每个 root cause 都有：

```text
before frontier
root cause
fix
after proof
```

## Full gate

```text
[ ] npm run check green
[ ] npm run test:e2e green
[ ] no timeout increase used to obtain green
[ ] no expectation weakening
[ ] no watchdog fake renewal
[ ] no baseline exemption
```

---

# 32. 最终目标形态

成功后的 CE 不应该只是：

```fsharp
task {
    let! x = capability.Task
    ...
}
```

而应该达到：

```text
源码：
  显然表达 happens-before

类型：
  显然限制 owner / capability

运行时：
  显然知道当前 suspend 在哪里

诊断：
  显然知道 producer 是谁

失败：
  显然知道因果链断在哪里
```

最终我们希望看到的不是：

```text
Watchdog timeout after 30s
blocked: manager.3
```

而是：

```text
Orchestrator O1
→ waits ManagerJob M4
→ Manager M4 waits Finality F3
→ Finality F3 waits Reviewer R2/B17
→ Reviewer R2 waits ProviderRun P81

FRONTIER:
ProviderRun P81 has no observed terminal.

Last causal progress:
PromptAccepted(P81)

Escape:
review-attempt cancel or deadline

Strict mock:
barrier-reviewer.0 is waiting for the same R2/B17 edge.
```

到这个程度，调试不再是“运行一个隐形状态机然后靠经验猜”。

它变成：

> **读一棵因果树。**

---

# 33. 最终工程裁决

上一阶段的核心命题是：

> **F# 调用栈就是流程栈。**

这个命题仍然正确。

但它还不完整。

本 Change 要补上的后半句是：

> **F# 调用栈是流程栈；每一个挂起点必须拥有一个非权威、结构化、可追溯的因果等待描述。**

这样才能同时得到五件事：

```text
Safety
Ownership
Liveness
Observability
Replayable causal evidence
```

其中：

```text
Journal / durable projection
负责“已经发生了什么”

CE
负责“程序按什么顺序执行”

Capability
负责“谁能唤醒谁”

Causal Wait observation
负责“程序此刻为什么还没有继续”
```

四者严格分工。

任何一个模块试图兼任另外一个：

```text
REVISE
```

**本 Proposal 的真正完成标准不是“新的 diagnostics 看起来很漂亮”。**

而是：

> **下一次 canary 挂住时，程序员不需要首先读 3000 行日志，不需要脑内模拟状态机，不需要猜 race；第一屏就能指出最小未满足因果前沿。**

做到这一点，才有资格把当前 Direct CE 从“已经能工作”提升为“结构上可解释、可审计、可调试的时序 DSL”。

---

# Active work

| Phase | Item | Status |
|---|---|---|
| 0 | Freeze proposals → active/ | DONE |
| 1 | RED + baseline dumps | DONE |
| 2 | CausalWait / Registry / Await / frontier | DONE |
| 3 | E2E diagnostic bridge + watchdog frontier | DONE |
| 4 | Student–Teacher Returned/Completion | DONE |
| 5 | Orch→Manager→Finality→Reviewer instrumentation | DONE |
| 6 | Root-cause repair of 3 orchestrator canaries | DONE — SharedState.BloggerFlights (worktree/root HasFlight miss); evidence `evidence/orchestrator-frontier/ROOT-CAUSE.md` |
| 7 | Join / Recovery / Process high-value waits | PARTIAL — Join + Finality blessing/journal wrapped; Process left physical (Deadline escapes) |
| 8 | Boundary gate | DONE — `scripts/checks/causal-wait-boundary.mjs` wired into `scripts/check.mjs` |
| 9 | Full verification (`check` + `test:e2e`) | DONE |

---

## Final outcome

Causal CE observability shipped as a non-authoritative wait graph + frontier diagnostics.

- Core: `CausalWait` / `CausalWaitRegistry` / `CausalAwait` / E2E bridge + watchdog `CAUSAL FRONTIER`.
- Instrumented: Student–Teacher, Manager job, Finality (reviewer/blessing), Join, Orchestrator host/review.
- DSL-012 formalized in `docs/what|shape|how|proof/dsl-structured-program.md`.
- Boundary gate: `scripts/checks/causal-wait-boundary.mjs` (wired into `scripts/check.mjs`).
- Orchestrator canary root cause: worktree/root `HasFlight` miss → `SharedState.BloggerFlights` (see `evidence/orchestrator-frontier/ROOT-CAUSE.md`).
- Verification: `npm run check` PASS; `npm run test:e2e` PASS (all staggered scenarios, 1 iteration). Three orchestrator canaries green without timeout bumps.
