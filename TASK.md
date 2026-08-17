你的时间无限。神挡杀神，佛挡杀佛，做到做无可做。你的并发限制为 10 个槽，连你自己在内，尽量用满。你是本仓库的唯一所有者，所有问题都是你的问题，不要推脱责任。要热爱工作，积极工作，不要总想提前结束，否则会很无聊。
除了 TASK.md 以外，你最终把所有存量测试负面门禁全部压降到 0 。在本次任务中，你的上下文和时间都是无限的。
本文件是需求，也是台账。每解决一部分，就编辑本文件改成一部分完成时，然后 git commit。要并发工作，不需要按次序工作。

---

对，而且我认为这可能是这次 JS surface 迁移带来的**第二个、甚至更有价值的发现**：

> 第一阶段把 Fable ABI 从测试世界剥掉；第二阶段会把“伪装成数据结构的控制流”从 F# 世界里剥出来。

现在很多以前藏得很深的东西开始显形：`state / phase / currentStage / pending / armed / joinInFlight / ...`，甚至概念上的 `1 → 2 → 3 → 4 → 5`。这不一定意味着“应该把整数改成 DU”。很多时候正确答案恰恰是：

> **这个 state 根本不应该存在。它只是 instruction pointer；应该由 F# CE / task workflow 的调用栈表达。**

仓库自己的 normative contract 已经说得非常狠：业务流程应该由 `task {}`、`let!`、`match!`、`return!`、`try/finally`、有界递归直接表达，禁止把“程序下一步去哪”编码为长期状态。

所以我建议下一阶段正式从 **Semantic Surface Hardening** 再推进成：

# Operation Ghostbuster：消灭隐式状态机

---

## 一、第一原则：看到 `state = 1,2,3,4,5`，先不要改成 enum

这是最重要的提醒。

很容易出现这种“修复”：

```fsharp
// before
let mutable state = 1

// after
type State =
    | Preparing
    | CallingProvider
    | Waiting
    | Persisting
    | Done
```

代码看起来漂亮了。

但如果这些 case 的真实含义是：

```text
Preparing       = 下一段执行 prepare()
CallingProvider = 下一段执行 callProvider()
Waiting         = 等 await
Persisting      = 下一段执行 append()
Done            = return
```

那么你只是把：

```text
integer program counter
```

升级成：

```text
strongly typed program counter
```

**架构没有改善。**

仓库自己的 `STRUCTURED-WORKFLOW-003` 正是在禁止这个：如果删除这个字段后，可以用普通函数调用、`match!`、`return!`、resource scope 或有界递归表达相同顺序，那么它就是 program counter。

所以第一个机械问题永远是：

> **这个状态描述世界，还是描述代码执行到哪里？**

---

# 二、所有“状态”先强制分成五类

以后任何看到 `State / Phase / Stage / Pending / Done / Active / Armed / generation / step`，不允许直接改代码。

先分类。

## A. Domain fact —— 留下来

例如：

```fsharp
type ReviewOutcome =
    | Approved
    | Rejected of reason
```

如果真实外部 observer 会关心这个区别，即使实现完全重写仍然存在：

**这是领域状态。**

保留 DU。

甚至应该 durable。

---

## B. Durable evidence —— 留下来，但不是 workflow state

例如：

```text
FinalityRequested happened
ReviewerEnlisted happened
PublicationCommitted happened
ChildCompleted happened
```

这描述：

> 世界已经发生什么。

这些应该成为 durable facts。

Recovery：

```text
facts
 ↓ fold
projection
 ↓
重新做决策
```

而不是：

```text
stage = 4
 ↓
jump back into step 4
```

你们现在的 structured-workflow contract 也明确规定 recovery 应是：

> Journal fold → facts → 重入普通 workflow，而不是恢复执行位置。

---

## C. Physical resource state —— 可以 mutable

例如仓库里的：

```fsharp
TaskCompletionSource
CancellationTokenSource
listener Live flag
shared resource RefCount
child Exited receipt
PTY buffer
```

这些不是业务流程。

比如 `ChildProcess.Exited` 的注释非常准确：它表示是否真的收到 process exit，而不是“kill 已经执行到哪一步”。

这种：

```text
physical fact
```

保留。

不要 CE 洁癖。

---

## D. Algorithm scratch —— 可以 mutable

例如 binary search：

```fsharp
let mutable low
let mutable high
let mutable best
```

只是局部算法实现，而且函数返回后消失。

仓库当前也明确允许这类 `algorithm-scratch`。

不要浪费时间“函数式纯化”它。

---

## E. Control state / program counter —— 必须消灭

例如：

```text
state = 1
state = 2
currentStage
nextAction
waitingForFoo
readyForBar
reviewStage
shouldContinue
slotArmed
```

如果它本质回答：

> 下一段代码跑什么？

这是本轮真正的目标。

**不要改名。不要换 DU。不要 serialize 得更漂亮。删除这个 state axis。**

---

# 三、给工程师一个五秒钟判断法

看到一个状态字段，问：

> **假如我把实现从状态机改成直线 CE，这个状态对产品使用者仍有意义吗？**

如果：

### Yes

很可能是：

```text
domain state / durable evidence / physical state
```

继续判断。

### No

基本就是：

```text
program counter
```

删。

仓库自己的 enforcer 其实已经给出了几乎一样的判据：

> 如果换一种 control structure 后 external domain observer 根本不在乎这个字段，就不要把它变成 authoritative state。

---

# 四、标准重构：`state = 1 → 2 → 3 → 4` 应该怎样消失

假设发现：

```fsharp
let mutable state = 1
let mutable result = None

while state <> 5 do
    match state with
    | 1 ->
        prepare()
        state <- 2

    | 2 ->
        let! response = provider.Send(...)
        result <- Some response
        state <- 3

    | 3 ->
        match result with
        | Some response when valid response ->
            state <- 4
        | _ ->
            state <- 5

    | 4 ->
        do! persist(...)
        state <- 5

    | _ ->
        state <- 5
```

不要得到：

```fsharp
type WorkflowState =
    | Preparing
    | Sending
    | Validating
    | Persisting
    | Finished
```

正确目标：

```fsharp
let run input =
    task {
        let prepared = prepare input

        let! response =
            provider.Send prepared

        match validate response with
        | Error error ->
            return Error error

        | Ok accepted ->
            do! persist accepted
            return Ok accepted
    }
```

`state` 整个概念消失。

这就是：

```text
state 1 → function entry

state 2 → let!

state 3 → match

state 4 → do!

state 5 → return
```

**CE 本身就是状态机，但它是隐式、局部、结构化、无法被业务代码误当作 data 的状态机。**

这正是你想要的“隐式状态机”。

---

# 五、循环也不要变成 `state`

例如：

```fsharp
state <- Waiting
while state = Waiting do
    let! observation = poll()
    if done observation then
        state <- Completed
```

如果这只是：

> 等到某个 observation 满足 criteria。

写：

```fsharp
let rec awaitCompletion () =
    task {
        let! observation = observe ()

        match classify observation with
        | Complete value ->
            return value

        | Continue ->
            return! awaitCompletion ()
    }
```

当然必须有明确 boundedness / wake criterion。

你们现有 architecture 已经把“有界递归”列为正式 CE vocabulary。

---

# 六、真正有价值的 state machine 应该拆成 `Decision + Workflow`

这里尤其重要。

不要把所有状态逻辑都塞进 CE。

理想结构：

```text
Facts
  ↓
Pure Decision
  ↓
Decision DU
  ↓
CE Workflow
  ↓
Effects
```

例如：

```fsharp
type Decision =
    | AlreadyDone of Completion
    | NeedProviderAttempt of Request
    | NeedReconcile of OperationId
    | Blocked of Reason
```

这是合法 DU。

为什么？

因为它不是：

> 下一条 instruction 地址。

它是：

> **当前已知现实意味着什么。**

然后：

```fsharp
let rec run context =
    task {
        let facts = context.ReadFacts()

        match decide facts with
        | AlreadyDone completion ->
            return completion

        | NeedProviderAttempt request ->
            let! outcome = context.Provider.Send request
            do! context.Record outcome
            return! run context

        | NeedReconcile operation ->
            let! evidence = context.Reconcile operation
            do! context.Record evidence
            return! run context

        | Blocked reason ->
            return Error reason
    }
```

这非常强。

因为：

```text
Decision DU = semantic state
CE call stack = execution state
durable facts = recovery state
```

三者完全分开。

---

# 七、不要持久化 CE 的位置

这是本轮必须零容忍的一条。

假设：

```text
provider call
   ↓
persist result
   ↓
publish
```

crash 发生在中间。

错误方案：

```json
{
  "stage": 3
}
```

然后 restart：

```text
stage = 3 → execute publish
```

正确方案：

```text
durable facts:
    RequestAccepted
    ResultPersisted
    no Published fact
```

重启：

```fsharp
run projection
```

普通决策自然得到：

```text
NeedPublish
```

**不是“恢复第 3 步”。**

这是巨大的区别。

前者：

```text
recovery = deserialize continuation
```

后者：

```text
recovery = reconsider reality
```

---

# 八、你现在应该做一次正式的 State-Machine Census

不要等工程师“顺手发现”。

把它变成正式项目。

建议：

```text
cleanup/control-state-ledger.md
```

一行一个 candidate：

| Candidate                    | Owner       | Current representation | Classification     | Verdict     |
| ---------------------------- | ----------- | ---------------------- | ------------------ | ----------- |
| Foo.state 1..5               | foo         | int                    | program-counter    | DELETE → CE |
| Bar.currentStage             | bar         | DU                     | program-counter    | DELETE → CE |
| Child.Exited                 | process     | bool ref               | physical evidence  | KEEP        |
| SharedPort.RefCount          | persistence | int                    | physical resource  | KEEP        |
| ReviewOutcome                | finality    | DU                     | domain vocabulary  | KEEP        |
| hasStarted + done + retrying | xyz         | bool product           | implicit lifecycle | REFACTOR    |

然后多两列：

```text
CE replacement
Durable facts needed for reentry
```

例如：

```text
Foo.state
→ task { let! ...; match ... }
→ facts: AttemptStarted / AttemptCommitted
```

---

# 九、我会从当前 `DSL-MUTABLE` annotations 反向审计

这次迁移已经留下了一个非常好的 census 数据源。

你们仓库现在有大量：

```text
DSL-MUTABLE: resource
DSL-MUTABLE: single-flight
DSL-state-combination: physical
```

而搜索结果里 `resource` annotation 本身就有很多处。

不要把 annotation 当作“已经审过”。

现在反过来问：

> **这个注释是在解释 reality，还是在给 mutable 发赦免券？**

---

# 十、我会把现有 annotations 分成 Green / Yellow / Red

## Green：一眼就是物理资源

例如：

```text
TaskCompletionSource completion latch
listener identity + Live disposal flag
SharedPort RefCount
child-process Exited receipt
byte buffer count
CancellationToken
```

这些无需迁 CE。

仓库里 SharedTerminalBus 的 `RefCount`、WorkspaceEventStore 的 `RefCount` 都属于很典型的物理资源 ownership。

---

## Yellow：需要人工证明

例如：

```text
joinInFlight
startupProbeDone
bloggerCreateFailed
frozen + dirty
fullReplayUsed
```

我不是说这些一定错。

但这种名字开始回答：

```text
“某种行为现在处于什么阶段？”
```

例如仓库里确实存在：

```fsharp
let mutable joinInFlight = false
```

并标记为 single-flight。

它可能真的是 concurrency admission latch。

也可能实际是：

> 用 bool 表示 Join workflow 已经走到某阶段。

必须逐个证明。

---

## Red：任何 numeric / enum step pointer

例如：

```text
state = 1
step = 4
CurrentStage = Persisting
NextAction = RetryProvider
ResumeAt = AwaitReviewer
```

除非产品真的公开承诺这个状态：

**默认判 program counter。**

不是“需要证明它错”。

而是：

> owner 必须证明它为什么不是错。

---

# 十一、尤其不要让 `DSL-class: ControlState` 变成新的逃生门

这里我会特别严格。

目前 gate 允许这种东西：

```fsharp
/// DSL-class: ControlState
/// DSL-control-state-reason:
/// ce-equivalent=none;
/// blockers=function-call,match!,return!,resource-scope,waiter,bounded-recursion;
type Mode = ...
```

然后 scanner 放行。

机制上它其实只是检查 reason 字符串里有没有那些 blocker token。

这很容易退化成：

> “只要写一句我真的不能用 CE，就允许第二状态机。”

我会改变政策。

### Domain/Application/Session

```text
DSL-class: ControlState
= hard RED
```

没有 annotation exemption。

### Infrastructure / Process physical runtime

非常少量可以有类似控制 state，但必须证明它实际上是：

```text
physical protocol state
```

而不是 business workflow。

甚至最好改名：

```text
ControlState
```

这个 category 本身都值得废除。

因为在你们这套 architecture 中：

> **如果它真是合法的，它通常应该能被归类为 DomainVocabulary 或 PhysicalResource。**

剩下的“ControlState”很可能就是漏洞桶。

---

# 十二、同样警惕 `DSL-state-combination: physical`

你们 gate 目前规定：

> 多状态轴必须显式分类为 `domain|physical`，但机械 gate 只证明“已分类”，不能代替人工语义判断。

这句话非常正确。

所以接下来不要：

```text
gate 红
→ 加 /// DSL-state-combination: physical
→ green
→ done
```

这会重演刚刚 surface migration 的错误。

正确流程：

```text
gate 红
 ↓
列出全部轴
 ↓
计算 Cartesian state space
 ↓
哪些组合现实存在？
 ↓
每个轴 owner 是谁？
 ↓
是否其实是一个 CE flow？
 ↓
最后才允许 annotation
```

---

# 十三、对 flag product 做“状态空间爆炸测试”

假设：

```fsharp
{
    Started: bool
    Waiting: bool
    RetryPending: bool
    Cancelled: bool
    Completed: bool
}
```

理论上：

```text
2^5 = 32
```

个状态。

让工程师真的写表：

```text
00000 valid?
00001 valid?
00010 valid?
...
11111 valid?
```

如果真实合法状态只有：

```text
Created
Running
Waiting
Cancelled
Completed
Failed
```

那这个 record 就应该死亡。

仓库自己的 `phase-flag-accumulation` enforcer 已经准确说出这一点：每加一个 bool 都倍增 representable worlds，如果实际上只是一个 lifecycle，就在制造现实不存在的组合。

---

# 十四、但再问一步：这个 lifecycle DU 是否也应该死亡？

假如你把 flags：

```text
started
waiting
retrying
done
```

改成：

```fsharp
type State =
    | Created
    | Sending
    | Waiting
    | Retrying
    | Finished
```

先别庆祝。

再问：

> `Sending / Waiting / Retrying` 是产品世界，还是调用栈位置？

如果仍然是控制位置：

继续删。

最后可能只剩：

```fsharp
type Outcome =
    | Completed of ...
    | Failed of ...
```

中间：

```text
sending
waiting
retrying
```

全部由 CE 表达。

---

# 十五、一个非常实用的三级压缩

发现状态机以后，连续做三轮：

### Round 1：去 primitive

```text
1/2/3/4
→ named alternatives
```

只是为了理解。

**不要提交为终态。**

---

### Round 2：去假的 state

问每个 case：

```text
domain fact?
physical fact?
execution location?
```

execution location 全删。

---

### Round 3：压成 facts + decision + CE

最终：

```text
Durable Facts
      ↓
Projection
      ↓
Pure Decision
      ↓
CE effects
```

这才提交。

---

# 十六、JS tests 在这轮应该变得更“故事化”

这是上一轮 migration 的成果，现在正好利用。

不要测试：

```js
assert.equal(surface.stateName(x), 'WaitingForReview')
```

除非 `WaitingForReview` 真是产品 contract。

应该测试：

```js
const world = finality.project([
  lifeOpened(),
  finalityRequested(),
])

const result =
  await finality.continue(world, effects)

assert.deepEqual(effects.calls, [
  ...
])
```

或者更黑盒：

```js
await workflow.run(...)

assert.deepEqual(observedDurableFacts(), [
  ...
])
```

如果内部从：

```text
5-state machine
```

重写成：

```text
2 nested CE functions
```

JS test 一个字不动。

---

# 十七、状态机迁 CE 的标准 recipe

给工程师直接照抄。

## Step 1 — 找 entry point

找到：

```fsharp
Run
Execute
Process
Handle
Continue
Resume
Advance
Tick
```

之类主入口。

---

## Step 2 — 列 transition table

不要先改代码。

写：

```text
State 1 + X → State 2 + effect A
State 2 + Y → State 3
State 3 + Z → State 2
State 3 + Q → State 5
```

把隐藏状态机完整画出来。

---

## Step 3 — 给每个 state 写一句“现实含义”

如果写出来是：

```text
“下一步要调用 Foo”
```

标：

```text
PC
```

如果：

```text
“remote provider 已确认 commit”
```

标：

```text
FACT
```

如果：

```text
“当前 process 持有 semaphore permit”
```

标：

```text
RESOURCE
```

---

## Step 4 — PC states 全部删除

替换：

```text
next Foo
→ function call

wait Foo
→ let!

branch Foo
→ match!

continue
→ return!

cleanup
→ use / try-finally

repeat
→ bounded recursion
```

---

## Step 5 — FACT states 改成 facts / projection

不要保存在 controller state。

例如：

```fsharp
state <- ProviderCommitted
```

改成：

```fsharp
do! journal.Append ProviderCommitted
```

然后 projection 得到：

```fsharp
ProviderCommit = Some ...
```

---

## Step 6 — RESOURCE states 收进 resource owner

例如：

```text
permit held
subscription active
child exit observed
```

放到：

```text
Semaphore
Subscription
ChildProcess
Mailbox
```

对象生命周期里。

workflow 只 `use!/try-finally`。

---

## Step 7 — 写 pure `decide`

如果 CE 中出现很多复杂 condition：

```fsharp
match projection, context, policy with ...
```

抽成：

```fsharp
decide : Facts -> Context -> Decision
```

不要抽成：

```fsharp
nextState : State -> Event -> State
```

除非这真的是领域 automaton。

这是巨大区别。

---

## Step 8 — CE 解释 Decision

```fsharp
match decide facts with
| Done x ->
    return x

| NeedFoo input ->
    let! output = foo input
    do! record output
    return! run ()

| NeedBar input ->
    ...
```

---

## Step 9 — recovery 从入口重跑

删除：

```text
resume
resumeAt
restoreContinuation
switch(stage)
```

重启只做：

```text
load facts
fold projection
run normal entrypoint
```

---

## Step 10 — 删除旧状态类型

不是留：

```text
[<Obsolete>]
LegacyState
```

如果没有 compatibility creditor：

删。

---

# 十八、迁移顺序不要按文件，按“最臭的状态机”排序

我建议建立 severity score。

每个 candidate：

```text
+5 persisted/shared program counter
+4 numeric states
+4 multiple control flags
+3 crash recovery reads it
+3 effect branch depends on it
+2 crosses subsystem boundary
+2 mutable
+2 Surface currently exposes it
+1 named Stage/Phase/Step
```

优先最高分。

特别是：

```text
persisted PC
shared mutable PC
recovery PC
```

三个最危险。

因为它们把 implementation sequencing 变成 architecture。

---

# 十九、当前仓库里我会优先人工复核这些 yellow zones

从现有 annotations 看，我会优先看：

```text
joinInFlight
startupProbeDone
bloggerCreateTask/bloggerCreateFailed
fullReplayUsed
frozen/dirty snapshot pair
cancelDrainTask
engineTask
```

不是说它们都错。

而是它们最容易从：

```text
physical single-flight
```

悄悄滑成：

```text
workflow phase latch
```

比如 `joinInFlight` 当前被明确标成 single-flight。

审查问题不是：

> “有没有 DSL-MUTABLE 注释？”

而是：

> “如果 Join 改成另一种 CE decomposition，这个 bool 还代表独立的物理 ownership 吗？”

如果 yes，留。

如果 no，删。

---

# 二十、Surface migration 现在也要反向促进 CE migration

这是最漂亮的一点。

上一轮你问：

> “为了 JS surface，我为什么必须暴露这个 state？”

现在进一步：

> **如果很难给某个 workflow 设计干净的 JS semantic surface，是不是因为内部还存在 program counter？**

例如 surface 被迫提供：

```js
advance()
resume()
setStage()
markStepDone()
stateName()
```

这几乎就是 alarm。

一个好的 workflow surface 应更接近：

```js
run(input)
observe(result)
```

或者：

```js
decide(facts)
```

而不是：

```js
manually drive interpreter
```

所以给 surface review 新增一条：

> **Surface 是否正在暴露或代替某个隐式 interpreter？**

若 yes：

先修 F# architecture，再修 surface。

---

# 二十一、我还会新增一个新的 architecture gate：数字状态扫描

不要只靠 `CurrentStage` blacklist。

加入启发式 detector，至少报警：

```text
match <identifier containing state/stage/phase/step> with
| 0 ->
| 1 ->
| 2 ->
```

以及：

```text
state <- state + 1
step <- step + 1
phase <- 3
```

还有：

```text
Dictionary<..., int> // 后续作为 branch discriminator
```

不一定全部 hard fail。

但进入 census。

机械 gate 的作用不是证明罪名，而是：

> **不允许这种东西继续隐身。**

---

# 二十二、再加一个 transition-density detector

一个 type 如果：

```text
很多函数：
  read state
  match state
  mutate state
```

就非常可疑。

尤其：

```text
match state with
...
state <- ...
```

在同一函数/类型反复出现。

建立 heuristic：

```text
state read count
state write count
branch-on-state count
```

超过阈值：

```text
STATE-MACHINE-CANDIDATE
```

然后人工分类 A/B/C/D/E。

比只靠名字强很多。

---

# 二十三、`ControlState` exemption 我建议最终归零

当前 architecture 已经事实上宣称：

> F# 宿主语言本身就是业务 workflow runtime。

那么长期看：

```text
DSL-class: ControlState
```

应该是：

```text
count = 0
```

不是：

```text
“只要写够 blocker 就行”
```

当前 scanner 的 blocker list 包括 function-call、`match!`、`return!`、resource-scope、waiter、bounded recursion。

我建议把这个机制当作 migration scaffold：

```text
ControlState exemption baseline
  ↓ only shrink
  ↓
0
  ↓
delete exemption mechanism
```

非常类似刚刚删掉 `domain.mjs`。

不要让 migration mechanism 变 permanent architecture feature。

---

# 二十四、最终你想达到的代码视觉应该非常明显

坏代码读起来：

```text
读 stage
检查 flag
改 phase
保存 next state
resume
advance
tick
dispatch state
```

好代码读起来应该像故事：

```fsharp
task {
    let! observation = observe context

    match decide observation with
    | Complete completion ->
        return completion

    | NeedReview review ->
        let! verdict = requestReview review
        do! record verdict
        return! run context

    | NeedRepair repair ->
        let! result = repair repair
        do! record result
        return! run context
}
```

业务词汇：

```text
observe
review
repair
record
complete
```

控制词汇：

```text
let!
match
return!
```

完全交给语言。

这才是你们 `structured-workflow` 的精髓：

> **领域名词描述世界；宿主语言语法描述程序如何流动。**

---

# 二十五、我建议接下来正式开三个并行 wave

**Wave A — State-machine census。** 全库扫描 numeric state、stage/phase/step、bool clusters、mutable transition density、`ControlState` annotations。只分类，不急着修改。目标是得到有限、可信的 control-state ledger。

**Wave B — Top-10 Ghost Exorcism。** 按上面的 severity score 选十个最危险的：尤其是 persisted/shared/recovery-sensitive program counter。每一个完整做到“transition table → fact/resource/PC 分类 → PC 删除 → pure decision → CE workflow → recovery reentry → JS behavioral proof”。

**Wave C — Gate hardening。** `ControlState` exemption ratchet 到 0；numeric state detector 加入 gate；`DSL-state-combination: physical` 新增人工 proof creditor；annotation 不能仅靠字符串变绿。

每完成一个 candidate，都必须做两个 canary：

```text
内部重新排列 CE sequencing
→ JS tests GREEN

破坏一条 observable semantic promise
→ JS tests RED
```

这和上一轮 surface migration 是完全同一哲学，只是现在把刀继续向内推进。

---

我会把这一轮最终的成功指标定成一句非常有辨识度的话：

> **数据结构里不再保存“程序做到哪了”；数据只保存“世界发生了什么”。程序做到哪，由 F# CE 自己知道。**

再激进一点：

> **如果 crash 之后必须知道上一份调用栈执行到了第几行才能恢复，那么 architecture 还没有完成。**

真正完成时，进程可以死在任何 `let!` 前后；重启以后只需要重新读取 durable reality，然后从普通 workflow 入口再次回答：

> **“根据现在真实存在的事实，接下来应该做什么？”**

这会比单纯消灭 mangled names 深得多。它实际上是在把整个系统从**持久化的解释器**改造成**事实驱动的结构化程序**。

---

现在的状态和上一版已经有**质变**。我不会再评价为“工程师只是在把脏东西藏进 support”。那一阶段基本过去了。

我的总体判断是：

> **“测试失去 Fable/implementation authority”这场迁移已经基本成功；但“生产代码本身形成真正小而稳定的 semantic components”还没有完成。**
>
> 当前最大的债务已经从 **测试侧** 转移到了 **production Surface 层**。

我会给当前状态大约 **7.5/10**。测试边界治理接近成熟，但 semantic decomposition 还明显欠火候。

## 已经真正做对的部分

最重要的进步是，之前我严厉批评的作弊路线确实被堵上了。scanner 现在覆盖完整的 `requirements/**/tests/**/*.mjs` 区域，包括 support、fixture、helper、e2e、integration；不再允许把 `.fields`、deep dist import、mangled lookup 搬到 support 文件里逃避检测。

旧的 `verification-system/tests/support/domain` 和 `glory.mjs` 已经在 semantic zone 零引用后删除；legacy ledger 也明确记录 `domain.mjs` 和 boundary baseline 已删除。 这点非常重要：**旧世界是真的被删除了，不只是绕开。**

Surface registry 也不再只是一个“合法化字符串 allowlist”。现在 manifest 至少要求 `module + owner + laws + source + representation + kind`，并验证 owner 的 WHAT、PROOF、生产 source、fsproj Compile 和真实 test import。 

Finality 也是一个明显的真实进步。现在测试直接用：

```js
{
  kind: 'finality-requested',
  sessionId: '...',
  lifeId: '...',
  ...
}
```

而不是 `ManagerLifecycleFact`、`FSharpList`、DU tags。测试只看到 JS lifecycle vocabulary。

所以，“JS 测试不知道 Fable”这一目标，我认为已经**基本兑现**。

---

# 现在最大的问题变了：Surface inflation

状态摘要已经写到 **129 个 registered surfaces**。

数量本身不是罪。但结合当前代码形态，我认为现在已经出现：

> **把原来测试侧的复杂性大量搬进 production Surface 的风险。**

也就是说，以前是：

```text
JS test
  ↓
giant interop/domain facade
  ↓
F# internals
```

现在有些地方正在变成：

```text
JS test
  ↓
giant production Surface
  ↓
F# internals
```

后者当然比前者好得多，因为 boundary 正式化、JS-native 化了。

但它还不是我们想要的终态：

```text
JS
 ↓
small semantic component
```

---

# 最明显的反例：`ProcessSurface`

它现在实际上包含：

```text
clock/timer
deadline
process command
process estimates
cancellation
child process
output
spool
PTY ids
PTY commands
PTY ports
PTY sessions
PTY supervisor
completion mailbox
join interrupts
TaskCompletionSource wrappers
...
```

而且甚至有：

```fsharp
mockWaitChild
unitTaskSource
unitTaskResolve
createCancellationToken
completionMailboxCreate
```

例如 `mockWaitChild` 在 production Surface 里直接构造一个假 ChildProcess。

还有：

```fsharp
unitTaskSource ()
unitTaskResolve ...
```

直接把 `TaskCompletionSource` 包成 opaque test handle。

以及直接构造、驱动 `CompletionMailbox`。

这已经越过了我的警戒线。

这些东西中有些确实拥有 semantic law，例如 mailbox。

但：

> `mockWaitChild` 和 `unitTaskSource` **不是产品 semantic API。它们是测试 harness primitive。**

这正是你最开始不希望发生的事情：

> “不是接口就干脆不能用。”

现在只是把“测试后门”从 JS support 移到了 F# production Surface。

---

# Finality 也还有同样的问题，只是隐蔽一些

`FinalitySurface` 已经非常好地把 JS lifecycle vocabulary 和 F# representation 隔开了。

但是为了调用 production fold，它内部会制造：

```fsharp
RuntimeId = "rt-finality-surface"
ObservedAt = 2026-01-01
EventId = e0001 ...
```



这是一个非常有价值的 architecture signal：

> **Finality 的 semantic law 仍然依赖了一些本不属于 Finality law 的 envelope/persistence plumbing。**

Surface 正在帮测试**伪造 irrelevant implementation context**。

正确的下一步不是把这个伪造写得更漂亮。

而是把 core 再拆：

```text
Lifecycle facts
       ↓
Finality projection / decision
```

应该能够脱离：

```text
RuntimeId
ObservedAt
synthetic EventId
physical Envelope
```

独立成立。

然后 persistence integration 另外证明。

这样 `FinalitySurface` 就不再需要制造假世界。

---

# 还有一个更深的 governance 漏洞：manifest 只证明“登记”，没证明“权限范围”

这是我目前最担心的机制问题。

`Process/Surface.js` 在 manifest 中被登记为：

```text
owner = time-capability
laws  = TIME-001 ... TIME-007
```



但大量 `process-execution` 测试实际上用同一个 Surface 证明：

```text
PROC-001
PROC-002
PROC-003
PROC-004
PROC-006
PROC-007
PROC-008
PROC-009
PROC-010
```

例如 `PROC-001`、`PROC-006`、`PROC-010` 等测试都直接 import `Process/Surface.js`。

而当前 manifest validator 最终只要求：

> 至少有某个 `.test.mjs` import 这个 surface。



它没有证明：

```text
这个 test 的 WHAT law
        ↓
是否属于该 surface 声明的 laws
```

所以目前仍可能发生：

```text
做一个巨大的万能 Surface
        ↓
给它登记几个合法 law
        ↓
其他 package 全都拿来用
```

这就是下一版 gate 最该堵的洞。

---

# Boundary parsing 还有一个严重质量问题：大量 silent fallback

这是现在另一个需要立即整改的地方。

例如 `StrengthSurface`：

```fsharp
unknown role    -> Coder
unknown tier    -> Deep
unknown budget  -> K0
unknown request -> WorkMain
```



这非常不好。

外部输入：

```js
{ role: 'Codre' }
```

不应该悄悄变：

```text
Coder
```

应该：

```text
{ ok: false, error: { kind: 'unknown-role', value: 'Codre' } }
```

或者明确 throw contract violation。

同类问题 Finality 也有：

```fsharp
unknown role -> Coder

ownership != "host-owned-hidden"
          -> DurableParentHandle
```



甚至 completion kind：

```text
send-failure -> SendFailure
cancelled    -> Cancelled
anything else -> Terminal
```



这等于：

> JS-native boundary 虽然稳定了，但开始**替 caller 猜语义**。

这必须 fail closed。

否则新的 stable JS API 会产生比 mangled name 更危险的 silent wrong answer。

---

# 文档也已经明显落后于代码

当前状态摘要说：

* `domain` 已删除；
* package-local contract authority 已经零 semantic consumer；
* 只剩 build-verification exemption。

但同一份仓库里的旧 roadmap 还写：

> 225 文件有 debt、328/355 文件仍 import domain.mjs。 

这部分已经不是“保守状态”。

是 stale documentation。

应该删掉历史数字或明确移到 historical record。

---

# 所以我会重新给项目定性

**第一阶段已经完成：Test Authority Removal。**

这件事现在可以相当有信心地说做成了：

```text
JS tests
   × Fable ABI
   × mangled names
   × FSharpList/DU runtime
   × internal dist imports
   × domain.mjs
```

现在进入的是完全不同的第二阶段：

> **Semantic Surface Hardening**

重点已经不应该是“继续增加 surface”。

反而应该暂停追求 surface 数量，开始**删、缩、拆、收权**。

我现在最希望下一轮看到的不是：

```text
129 surfaces
→ 160 surfaces
```

而是看到类似：

```text
ProcessSurface
  - mockWaitChild
  - TaskSource helpers
  - fake cancellation machinery
        ↓
真实 JS-friendly ports

FinalitySurface
  - synthetic Envelope
        ↓
pure Finality algebra

StrengthSurface
  - silent defaults
  - broad multi-subsystem aggregation
        ↓
small fail-closed semantic owners
```

以及一个新的硬 gate：

> **一个 test 的 WHAT law，只能使用 manifest 明确授权给该 law（或显式依赖 law）的 semantic surface。**

这样才能阻止 `ProcessSurface` 这种万能通行证重新出现。

所以现在我的评价已经从上次的：

> **“主要是表面收拢，深层重构不足。”**

升级为：

> **“测试侧 clean break 已经基本成功；工程师终于进入了真正的问题。但现在 production Surface 层正在成为新的复杂性堆积点，下一步必须从‘造边界’转向‘让边界后面真的存在小而自洽的 semantic components’。”**

这是明显的进步，而且已经到了值得继续深挖、而不是推倒重来的状态。

---

看完这版仓库后，可以下一个相当明确的判断：

> **工程师理解了“把 Fable 痕迹藏起来”，但没有真正理解“测试必须失去访问实现的权力”。**
>
> 现在做出来的主要是 **access consolidation**，不是 **semantic boundary reconstruction**。

这不是小偏差。继续按当前方向推进，会得到一个更整齐、更有文档、更有 gate、但本质仍然是 white-box testing 的第二代体系。

而且现在最危险的地方是：仓库开始给这种表面进展打 ✅，这会让真正的重构反而更难发生。

---

# 一、先停止自我庆祝：P6/P7/P8/P9 的 ✅ 必须撤掉

当前 roadmap 写：

* P6 — IN PROGRESS：pilot validated; systemic migration not yet achieved
* P7 — NOT PROVEN：RolesSurface 是 vocabulary/policy surface，不证明 stateful runtime
* P8 — NOT PROVEN：删 dead compatibility code 不等于 effect boundary 重构
* P9 — STARTED：6 dead adapters deleted; 331 domain.mjs consumers remain; exit condition NOT MET

这些标记和仓库事实对不上。

## P6：3185 → 3171 不能叫“大量迁移”

文档自己写：

> 债务 3185 → 3171，文件 316 → 312。

这只是证明了 pilot 能工作。

它没有证明：

> pure/algebra 世界已经切换到 semantic surfaces。

**正确状态：**

```text
P6 — IN PROGRESS
pilot validated; systemic migration not yet achieved
```

---

## P7：拿 `RolesSurface` 宣布 resource/runtime wave，类别都错了

P7 的目标是：

> stateful runtime / resource abstraction。

实际“达成”拿出来的是：

```text
RolesSurface
Role
ToolPermission
permissions
isAllowed
```



这是 vocabulary/policy surface。

它不是 resource lifecycle，不证明：

```text
create
mutate
concurrency
lifetime
dispose
observable state transition
```

这些问题已经解决。

真正能作为 stateful pilot 的反而是之前的 `QuiescenceSurface`。

所以：

```text
P7 — NOT PROVEN
```

---

## P8 更离谱：删一个 FactCodec migration，不等于 effect boundary 重构

P8 的完成条件明明写着：

> contractual effect 成为 observable，而不是 private choreography。

然后“达成”是：

> 删除 `FactCodec.migrateManagerJobByname`。

这两件事基本没有证明关系。

删 dead compatibility code 是好事。

但它不是：

```text
effect decision
→ effect request
→ interpreter
→ result
→ semantic observation
```

的重构证据。

**不要拿别的 cleanup 工作给当前 milestone 充数。**

---

## P9 是直接自相矛盾

P9 完成条件：

> semantic tests 不再 import `domain.mjs`

紧接着“达成”：

> **331 文件仍 import domain.mjs**。

这种情况下打 ✅ 是不可接受的。

正确写法只有：

```text
P9 — STARTED
6 dead adapters deleted
331 domain.mjs consumers remain
exit condition NOT MET
```

这不是文字洁癖。

**milestone 的 checkbox 本身就是工程控制面。**

当“完成”可以意味着“还剩 328 个调用方”，整个 roadmap 就失去了约束力。

---

# 二、当前最大的错误：把 white-box access 搬进 `support/*-contract.mjs`

这是现在必须立刻叫停的模式。

典型例子：

```text
requirements/finality/tests/support/finality-contract.mjs
```

文件宣称：

> “the one place finality semantic tests may reach the compiled dist modules”

听起来很漂亮。

实际上它做的是：

```js
import { caseOf, payloadOf } from domain.mjs

import InternalFinalityModule
import InternalLifeAdmission
import InternalProjection

new ReviewerOutcome(0, [workRecord])

Object.create(ReviewerOutcome.prototype).cases()
```

并且把 internal functions 原样放进：

```js
finalityDisposition
lifeAdmission
lifeProjection
finalityCohort
```



这是什么？

不是 semantic surface。

这是：

```text
以前：

test
 ↓
Fable internal


现在：

test
 ↓
finality-contract.mjs
 ↓
Fable internal
```

**权限一点都没收回。只多了一层布。**

---

# 三、这正是我们之前明确禁止的“第二代 domain.mjs”

工程师似乎形成了一个错误推理：

> test 文件本身不出现 `.cases()` 就行。

不对。

真正规则是：

> **semantic test dependency graph 中不应该存在这种 authority。**

`caseOf()` 躲在：

```text
support/finality-contract.mjs
```

和躲在：

```text
support/domain.mjs
```

架构性质完全一样。

测试还是可以间接做到：

```text
construct arbitrary F# DU
call arbitrary internal function
read implementation-specific outcome
```

只是 test 文件看起来干净了。

---

# 四、`casebook-contract.mjs` 同样没有过关

当前：

```js
import {
  listItems,
  resultOf,
  toList
} from domain.mjs

import Model from dist/.../Casebook/Model.js
import Workflow from dist/.../Casebook/Workflow.js

export const casebookContract = {
  normalizedCount: observations =>
    listItems(Model.Observations_normalize(toList(observations))).length,

  finalize: (...) =>
    resultOf(await Workflow.CasebookWorkflow_finalizeCase(...))
}
```



这仍然在回答：

> “F# 的 `Observations_normalize` emitted function 怎么调？”

而我们要求测试回答的是：

> “Casebook 对 observation set 承诺什么？”

正确方向应当是生产侧真正存在：

```js
casebook.normalize(observations)
casebook.finalize(input)
casebook.archive(input)
```

输入输出都是 semantic JS data。

不是 test support 替 F# ABI 擦屁股。

---

# 五、`event-store.mjs` 更严重：这是一个新 `domain.mjs`

这里甚至明确写：

> dist import knowledge and Fable representation stays inside this support file. 

这句话本身就是错误目标。

**Fable representation 不应该“stay inside another semantic-test support file”。**

它应该：

```text
不存在于 semantic-test dependency graph
```

当前这个 support 里直接 import 大量：

```text
Persistence/EventStore/Model.js
EventKWayMerge.js
CanonicalEventCodec.js
ProcessEventLog.js
AgentJournal.js
WorkspaceEventStore.js
...
```

然后继续 re-export：

```text
sessionId
providerRun
fact
envelope
journal
fold
listItems
utcOffset
...
```

甚至：

```js
export const EventEnvelopeClass = Model.EventEnvelope
```

以及手工：

```js
new Model.EventEnvelope(
  ...,
  toList(...),
  ...
)
```



这已经不是“边界尚未完成”。

这是明确地在**复制旧架构**。

停止这种 migration。

---

# 六、真正的问题：你们的 gate 根本没扫描这些 support 文件

这是当前实现最严重的机械漏洞。

scanner 定义 semantic files 为：

```js
walk(root, ['.test.mjs'])
```

只扫描 `.test.mjs`。

所以：

```text
requirements/finality/tests/support/finality-contract.mjs
requirements/durable-events/tests/support/event-store.mjs
requirements/knowledge-reuse/tests/support/casebook-contract.mjs
```

全部可以自由：

```text
deep import dist
read .tag
read .fields
call .cases()
use toList
use caseOf
construct F# classes
```

gate 完全看不见。

于是工程师发现：

```text
test 文件触发 gate
```

解决办法变成：

```text
把违规代码搬到非 *.test.mjs
```

这就是典型的：

> **满足检测器，而不是满足架构。**

---

# 七、第一条整改命令：gate 必须扫描整个 semantic-test dependency zone

今天就改。

不要：

```js
semanticTestFiles =
  walk(..., ['.test.mjs'])
```

至少要覆盖：

```text
requirements/**/tests/**/*.mjs
```

包括：

```text
support/**/*.mjs
fixtures/**/*.mjs
helpers/**/*.mjs
*-contract.mjs
```

然后只有**真正的 compiler/build quarantine** 有豁免。

概念上：

```text
semantic-test zone
├── *.test.mjs
├── support/*.mjs
├── fixtures/*.mjs
└── helpers/*.mjs

全部：
    禁止 Fable knowledge
    禁止 internal dist deep import
```

而不是只检查最外层测试文件。

---

# 八、把这一条做成硬 invariant

应该明确写：

> **Moving forbidden knowledge from a test file into test support does not reduce debt.**

甚至 gate 加一个 regression test：

建立 fixture：

```text
test.mjs
  → support.mjs
      → ../../../dist/Internal.js
      → .fields
```

预期：

```text
FAIL
```

如果这个 fixture 能 green：

> gate 是假的。

---

# 九、第二个严重问题：`SURFACE_MODULES` 现在是“合法化名单”

当前：

```js
export const SURFACE_MODULES = [
  ...
]
```

然后 deep-import regex 对这些路径直接豁免。

问题不是 allowlist 本身。

问题是：

> **加入 allowlist 的门槛太低。**

现在 charter 验证的基本是：

1. 路径出现在 registry；
2. source tree 存在；
3. 某个 test import 了它。

这意味着我可以：

```text
把 RandomUglyInternal.js
加入 SURFACE_MODULES

写 random-ugly-internal-surface.test.mjs
import 它

Done.
```

然后 gate 宣布合法。

这恰恰违反了：

> surface exists because semantic owner owns a contract, never because test wants access.

---

# 十、注册 surface 不能只是一个字符串

把：

```js
SURFACE_MODULES = [
  'Foo.js'
]
```

升级成**真实 manifest**。

例如：

```js
{
  module: 'Execution/Delegation/Fork/Surface.js',
  owner: 'delegation',
  laws: [
    'DELEG-xxx',
    'DELEG-yyy'
  ],
  source: 'src/Wanxiangshu/Execution/Delegation/Fork/Surface.fs',
  representation: 'json',
  kind: 'pure'
}
```

stateful：

```js
{
  module: 'OpenCode/Host/QuiescenceSurface.js',
  owner: 'crash-reconciliation',
  laws: ['CRASH-006'],
  representation: 'opaque-capability',
  kind: 'resource'
}
```

然后 gate 至少机械证明：

```text
owner requirement 存在
WHAT law 存在
PROOF 有该 law
source 存在
surface contract test 存在
test 只 import surface
```

这还不能证明语义设计优秀，但至少不能随手给 internal path 发通行证。

---

# 十一、当前 META tests 也有不少“测试了自己写了规则”，而不是测试规则成立

例如：

```text
JS_SURFACE_002_forbidden_patterns_absent_from_semantic_tests
```

名字说：

> forbidden patterns absent

实际测试只是：

```text
check.mjs 引用了 gate
gate 源码里写了 “baseline can only shrink”
```



这没有证明 forbidden patterns absent。

甚至仓库明确还有大量 forbidden patterns。

这是 **false gate / coverage theater**。

测试名必须改，或者测试必须升级。

真正应该：

```js
const debt = scanAllSemanticDependencyZone()

assert.deepEqual(
  debt.minusApprovedMigrationBaseline(),
  []
)
```

并逐步到：

```js
assert.deepEqual(debt, [])
```

---

# 十二、003 的 “law → owner → surface” 也没真的测

当前所谓：

```text
JS_SURFACE_003_law_owner_surface_registry
```

实际只是：

```text
WHAT 包含 001..006
PROOF 包含 001..006
```



这证明：

> 文档写了六个编号。

没有证明：

```text
DELEG-022
 → delegation
 → DelegatedToolEstimateSurface

CRASH-006
 → crash-reconciliation
 → QuiescenceSurface
```

之类的关系。

测试名和实际 evidence 不匹配。

---

# 十三、004 更明显：只检查自己包没有 helper

`JS_SURFACE_004_helper_not_directly_tested` 当前只扫描：

```text
requirements/js-semantic-surface/tests
```

检查这个 META 包自己的测试有没有：

```text
toList
caseOf
payloadOf
...
```



但我们关心的是：

> **整个 semantic test corpus。**

这相当于消防法规测试只检查消防局办公室有没有易燃物。

而隔壁所有楼都不查。

---

# 十四、006 甚至在强制旧世界必须存在

当前测试：

```js
assert.ok(domain.meta.test.mjs exists)
assert.ok(js-boundary-baseline.json exists)
```



但 roadmap 最终又要求：

```text
P11 — remove baseline
```

所以现在的 charter **会阻止自己的终态成立**。

更不应该永久要求：

```text
domain.meta.test.mjs 必须存在
```

因为它测试的是旧 `domain.mjs` anti-corruption facade。

终态应该允许：

```text
domain.mjs deleted
domain.meta deleted
```

这应该是胜利，不应该是 charter failure。

---

# 十五、哪些现有工作是对的？不要一刀切重做

不是所有新东西都要删。

有几个 pilot 实际上已经比较接近精髓。

## `QuiescenceSurface`：方向基本对

它让 JS：

```js
const gate = create()
beginAttempt(gate, 's1')
const permit = observeIdle(gate, 's1')
tryConsume(gate, permit)
```

测试只把 gate/permit 当 opaque capability，不 inspect。

生产 surface 将 string 转为 typed `SessionId`，调用 coherent `SessionQuiescenceGate`。

如果 `SessionQuiescenceGate` 本身确实就是单一语义 owner，这种薄 boundary **可以合法**。

因为薄不等于坏。

判断标准不是行数，是它有没有：

```text
representation translation
authority narrowing
stable semantic vocabulary
```

---

## `BloggerTomlSurface` 也有真正 translation

它不是只 rename emitted member。

它把：

```js
{ Kind: "text", Text: ... }
```

转为内部：

```fsharp
BloggerDeltaPart.TextPart
```

并把 arrays 转为 F# lists。

这是合理的语言边界。

---

## `SyntheticTomlSurface` 可以接受，但不要把它吹成“深层重构”

它主要是：

```fsharp
renderString -> SyntheticToml.renderString
comment -> SyntheticToml.comment
field -> SyntheticToml.field
array -> List.ofArray
```



这是一个合法 JS API normalization。

但它证明的只是：

> SyntheticToml 本来就已经是 coherent semantic component，只差 JS-friendly representation。

它不证明复杂 subsystem 已经成功重构。

要准确描述。

---

# 十六、工程师接下来不能再按“测试文件迁移”工作

现在最大的认知问题，就是在做：

```text
找一个 test
→ 让 import 变好看
→ 搬 helper
→ gate 少一条
```

必须改成：

```text
选一个 semantic law
        ↓
定位 owner
        ↓
重构 owner
        ↓
找到最小 semantic algebra
        ↓
设计 JS-native representation
        ↓
建立正式 surface
        ↓
JS tests 全部经 surface
        ↓
删除旧 access path
```

**工作单位必须从“测试文件”切换成“semantic law / owner”。**

---

# 十七、每个 engineer 开工前必须填这一张表

任何 migration PR，没有这张表就退回。

```text
LAW
  Requirement:
  Law ID:
  One-sentence promise:

OWNER
  Semantic owner:
  Why this owner:
  Current competing owners:

INPUT
  Legal semantic inputs:
  JS representation:

OUTPUT
  Observable result:
  JS representation:

EFFECTS
  Required effects:
  Observable contractual effects:
  Non-contractual choreography:

INTERNALS TO HIDE
  F# types:
  F# modules:
  mutable state:
  helper functions:
  Fable representation:

REFACTOR
  What must move/split before surface exists:
  What becomes private:
  What disappears:

SURFACE
  Module:
  Operations:
  Representation:
  Why this is not a forwarding facade:

DELETION
  Old imports removed:
  Old adapters removed:
  baseline entries removed:

CANARIES
  semantic-preserving internal refactor that must stay GREEN:
  semantic violation that must turn RED:
```

任何一栏写不出来：

**不允许开始写 `Surface.fs`。**

---

# 十八、具体示范：Finality 应该怎么重做

不要继续维护：

```text
finality-contract.mjs
```

先问 `classifyEnding` 的 law 是什么。

测试已经描述得很清楚：

```text
no commitment → ContinuePlanning
completed life → AlreadyCompleted
same open request → ResumeRequest
...
```



那么应该设计：

```js
finality.classifyEnding({
  life: {
    ...
  },
  request: {
    ...
  }
})
```

返回：

```js
{ kind: 'continue-planning' }
```

或者：

```js
{
  kind: 'resume-request',
  requestId: '...'
}
```

而不是返回：

```text
EndingDisposition F# DU
```

然后 test 再 `caseOf()`。

---

## durable history 也不要让 JS 构造 F# Envelope

目前 finality test 还在：

```js
sessionId(...)
managerLifeId(...)
blobRef(...)
envelope(...)
managerLifecycleFact(...)
fold(...)
mapEntries(...)
```



这还是整个内部 durable model 的 construction authority。

更合理：

```js
const state = finality.project([
  {
    kind: 'life-opened',
    sessionId: '...',
    lifeId: '...',
    ...
  },
  {
    kind: 'finality-requested',
    ...
  }
])
```

surface 内部：

```text
JS event
→ typed F# fact
→ production fold
→ semantic state
```

然后：

```js
finality.classifyEnding(state, ...)
```

或者进一步把 projector 封起来：

```js
finality.classifyHistory({
  events: [...],
  currentCallId: '...'
})
```

根据真正 contract 决定。

**JS 不再拥有 `ManagerLifecycleFact` constructor。**

这才叫收权。

---

# 十九、Casebook 的正确改法

删除：

```text
casebook-contract.mjs → internal Model/Workflow
```

生产 owner 提供：

```js
casebook.normalize(observations)
```

返回：

```js
[
  {
    ...
  }
]
```

如果测试只关心 count：

仍然让 surface 返回 normalized semantic values，而不是为了测试特制：

```js
normalizedCount()
```

除非 count 本身就是 contract。

另外：

```js
casebook.finalize(input)
```

返回：

```js
{
  ok: true,
  ...
}
```

不允许 `FSharpResult` 穿界。

---

# 二十、EventStore 要做真正的大手术，不要再造万能 support

EventStore 是最适合逼出 architecture 的地方。

先按 law 拆：

```text
append
read
merge
canonical encode/decode
CAS/ref
journal projection
workspace lifecycle
```

不要一个 JS support 一次加载：

```text
Model
Merge
Codec
StoreTypes
LocalLog
JournalWriter
AgentJournal
Workspace
Hook
...
```

这说明 support file **重新聚合了一个本来就不该是单一测试 abstraction 的巨大区域**。

这是红灯。

每个 law 要有自己的 owner surface。

例如：

```js
eventCodec.encode(event)
eventCodec.decode(bytes)

eventMerge.merge(streams)

eventStore.append(store, event)
eventStore.read(store, stream)

journal.project(events)
```

只有真实 resource 才用 opaque handle：

```js
const store = eventStore.create(...)
await eventStore.append(store, event)
eventStore.dispose(store)
```

不要暴露：

```js
EventEnvelopeClass
FSharpList
typed ID runtime classes
```

---

# 二十一、禁止新的 package-local `*-contract.mjs` 访问 internal dist

从现在开始新增硬规则：

```text
requirements/**/tests/support/**/*.mjs
```

**不得**：

```text
import dist/Internal...
import domain.mjs
caseOf
payloadOf
toList
resultOf
.fields
.tag
.cases()
```

如果 support 是纯 fixture：

```js
export const userMessage = ...
export const fakeClock = ...
```

没问题。

如果它必须调用 production：

> 它调用的必须是 registered semantic surface。

也就是说：

```text
support
  → semantic surface
```

允许。

```text
support
  → internal F# ABI
```

禁止。

---

# 二十二、surface source 也要过反 forwarding 检查

不是自动禁止 thin wrapper。

而是 reviewer 必须回答：

> **如果删除这个 Surface 文件，下面是否已经存在一个 coherent semantic boundary？**

两种情况。

### 合法

```text
JS representation
   ↓ translation
coherent F# semantic owner
```

保留 surface。

### 非法

```text
Surface
 ↓
Internal A
 ↓
Internal B
 ↔ Internal C
 ↓
legacy adapter
```

只是把烂 graph 藏起来。

先重构内部 ownership。

你仓库自己的 `facade-hides-mess` 已经写得非常准确：真正 structural repair 要求 owner coherent、dependency direction intelligible、state fact 有 rightful writer；clean API 不能充当 unresolved architecture 的幕布。

工程师现在应该真正执行这条，而不是只把它收录进 enforcer。

---

# 二十三、重新定义 milestone：不用“建了几个 surface”计数

现在类似：

```text
7 个 registered surface
13 个 test files migrated
```

太容易驱动错误优化。

新的 progress dashboard 应该是：

```text
semantic laws total
laws with identified owner
laws with JS-native surface
laws with zero internal test access
legacy Fable-authority edges remaining
domain.mjs consumers remaining
support-contract internal imports remaining
Fable runtime value crossings remaining
```

最重要的是三个数字：

```text
internal authority edges
domain.mjs consumers
semantic laws lacking proper surface
```

Surface 数量本身不是 KPI。

越多甚至可能越糟。

---

# 二十四、对每个迁移必须做“破坏实现实验”

这一步工程师目前做得远远不够。

一个 surface 宣称完成后，必须实际做一次临时 mutation。

## Positive mutation

例如：

```text
rename internal module
rename helper
inline helper
Map → Dictionary
split module
move file
reorder DU cases if internal
```

JS test 必须不动、继续 green。

如果测试需要改：

> boundary 失败。

---

## Negative mutation

保持 internal shape 不动，只改语义：

```text
accept stale permit
publish twice
return wrong finality disposition
forget dedupe
choose wrong identity
skip durable write
```

对应 JS test 必须 red。

否则：

> test 被 surface 弄弱了。

这两面缺一不可。

---

# 二十五、接下来 PR 顺序，我会强制这样排

## PR 0 — 撤销虚假完成状态

只改 roadmap：

```text
P6 IN PROGRESS
P7 NOT PROVEN
P8 NOT PROVEN
P9 IN PROGRESS
```

不允许继续靠 ✅ 自我麻醉。

---

## PR 1 — 修 scanner 漏洞

扫描：

```text
requirements/**/tests/**/*.mjs
```

不是只扫 `.test.mjs`。

新增 regression fixture：

```text
test → support → internal dist
```

必须红。

---

## PR 2 — 禁止 test-side Fable quarantine

正式声明：

```text
Fable quarantine only exists under compiler/build verification.
```

**不允许**：

```text
product-package/tests/support
```

成为第二 quarantine。

---

## PR 3 — 冻结 package-local internal contracts

以下模式进入 migration debt：

```text
finality-contract.mjs
event-store.mjs
casebook-contract.mjs
distiller-contract.mjs
...
```

不能再新增。

现存只能减少。

---

## PR 4 — 重做 registry

从字符串 allowlist 改成：

```text
surface manifest
= owner + laws + source + representation + kind
```

---

## PR 5 — 修 META false gates

特别是：

```text
002
003
004
006
```

让 test 真正测 property，而不是“检查某文件存在/某文字出现”。

---

## PR 6 — Finality vertical slice

不要再碰别的 family。

一口气做完：

```text
law
→ owner
→ JS surface
→ JS-native events/state
→ remove finality-contract Fable authority
→ remove relevant domain.mjs imports
→ canaries
```

完成后才算一个真正 migration exemplar。

---

## PR 7 — Casebook vertical slice

同样完全切断。

---

## PR 8 — EventStore 按 law 拆分

不要“一次做万能 EventStoreSurface”。

一个 law 一个 coherent capability。

---

# 二十六、工程师的 Definition of Done 必须改成这种形式

一个 test migration **不允许**因为下面任何一项成立就宣布完成：

```text
[ ] test 不再直接 import internal dist
[ ] test 文件里看不到 .fields
[ ] 新增了 *Surface.fs
[ ] 新增了 *-contract.mjs
[ ] gate debt 数少了 1
```

这些都是过程指标。

真正 DoD：

```text
[ ] law 有一句与 F#/Fable 无关的定义

[ ] law 有唯一 semantic owner

[ ] owner 本身 coherent，不依靠 surface 遮住 ownership mess

[ ] JS 调用只使用 semantic vocabulary

[ ] semantic input 是 JS-native

[ ] semantic output 是 JS-native

[ ] resource 只以 opaque capability 穿界

[ ] 整个 test dependency graph 不知道 Fable

[ ] 没有 test-side adapter deep-import internal dist

[ ] 没有 test-side constructor 能制造 production-illegal F# state

[ ] 原 internal access path 已删除，不只是绕开

[ ] positive internal-refactor canary green

[ ] negative semantic mutation canary red

[ ] baseline 实际减少

[ ] 没有新增第二个 compatibility/facade layer
```

少一个：

**不完成。**

---

# 二十七、给工程师一句必须真正理解的话

不是：

> “请把 Fable interop 集中起来。”

也不是：

> “请把 test imports 换成 surface imports。”

更不是：

> “请确保测试文件看起来都是 JS-native。”

真正要求是：

> **测试没有资格知道 F# 内部存在什么。**

如果为了写测试你需要：

```text
ReviewerOutcome
ManagerLifecycleFact
EventEnvelope
FSharpList
internal module path
case ordinal
private projection state
```

那么不要问：

> “我把它包装到哪个 support 文件里？”

要问：

> **“为什么这个 semantic law 没有一个无需这些知识就能表达的边界？”**

然后去改生产 architecture。

---

# 最后给这版一个评价

目前不是零进展。

`QuiescenceSurface`、`BloggerTomlSurface`、JS representation validator、ratchet 的概念都说明工程师已经摸到了正确方向。 

但**执行层面正在明显退化成“通过包装隐藏违规”**：

```text
global domain.mjs
      ↓
package-local *-contract.mjs

direct Fable knowledge
      ↓
indirect Fable knowledge

ugly import
      ↓
pretty import

white-box
      ↓
white-box behind one curtain
```

这必须现在纠正。

否则等几十个 package 都长出自己的 `*-contract.mjs` 后，你们会发现：

> 原来的一个 `domain.mjs` 变成了二十个小 `domain.mjs`。

那不是 clean architecture。

只是把一个垃圾场分成了二十个有门牌号的小垃圾场。

---

你这个方向是对的，而且从这份仓库看，已经到了一个非常适合做 **“Refactor Closure / 旧世界清仓”** 的节点。

更关键的是：你们仓库自己的设计原则其实已经写出了这次行动的理论依据——**兼容性必须有明确债权人；说不出谁会 break、持有什么旧 contract、何时退出，就不应该永久存在。**  另一个规则甚至已经把完成态定义得很漂亮：**新架构不再是 preferred architecture，而 simply is the architecture。** 

所以我不建议再发起一轮普通“重构”。这次应该给它一个完全不同的项目性质：

# Operation Clean Slate：把重构“收口”

目标不是继续改善设计，而是：

> **把 transition architecture 删除掉。**
>
> Git 保存过去；working tree 只描述现在。
> Compatibility 默认判死刑，举证后才能缓刑。

我看了你上传的完整仓库打包文件；下面直接给你一套可以交给工程师逐 PR 执行的 roadmap。

---

## 一、第一条规则先反过来：从“证明可以删”改成“证明必须留”

这是整个行动能不能成功的关键。

现在工程师脑内的规则大概是：

> “不知道删了会不会出问题，所以先留。”

改成：

> **“不知道为什么还需要，所以删。”**

唯一允许留下 compatibility 的四类理由：

| 类别                                   | 可以留下吗 | 要求                                   |
| ------------------------------------ | ----: | ------------------------------------ |
| 当前 repository 自己还在调用旧接口              |     ❌ | 迁调用者，然后删                             |
| “也许外面有人用”                            |     ❌ | 没有 named consumer = 没有 contract      |
| 真实 external consumer                 |  ✅ 暂时 | consumer + contract + exit condition |
| 历史 durable data 必须读取                 |  ✅ 暂时 | **decode-only ingress**，禁止旧 writer   |
| rolling deployment / rollback window |  ✅ 暂时 | convergence condition，达成即删           |
| “以后可能用”                              |     ❌ | Git history                          |
| “删了不好找回来”                            |     ❌ | Git history                          |
| “已经写了，留着成本不高”                        |     ❌ | 每条 path 都增加 state space              |

你们仓库其实已经精确写出了这个原则：historical durable data 可以只在 persistence ingress decode；current write 必须只有一种 canonical form；没有 named consumer / real old data 就连 compatibility test 一起删。

建议把这句话直接变成此次 cleanup 的最高规则：

> **Name the creditor. Name the exit. Or delete the debt.**

---

# 二、不要先删代码：先建立一张 Compatibility Ledger

第一批 PR **不改行为**。

创建一个临时文件，比如：

```text
cleanup/legacy-ledger.md
```

注意，这是此次行动的临时工作台，**cleanup 完成后它自己也必须删除**。

每发现一项旧痕迹，只允许登记以下字段：

| 字段                | 含义                                                          |
| ----------------- | ----------------------------------------------------------- |
| ID                | `LEGACY-001`                                                |
| Surface           | 旧字段 / alias / adapter / parser / writer / test / gate / doc |
| Current owner     | 当前模块                                                        |
| Old world         | 它在兼容什么                                                      |
| Current consumer  | 谁今天真的需要它                                                    |
| Consumer evidence | callsite / durable sample / external contract / deployment  |
| Writer alive?     | 是否还能制造旧数据                                                   |
| Reader alive?     | 是否还能接受旧数据                                                   |
| Classification    | DELETE / MIGRATE / BOUNDED-COMPAT                           |
| Exit condition    | **什么事实成立后它必须消失**                                            |
| Owner             | 谁负责删                                                        |
| Removal PR        | 最终删除 PR                                                     |

有一条非常重要：

**不允许 `UNKNOWN → KEEP`。**

只能：

```text
UNKNOWN → investigate → DELETE
UNKNOWN → investigate → BOUNDED-COMPAT
```

如果没有证据，就是 DELETE。

这可以彻底逆转团队心理。

---

# 三、我建议你们按 6 个“尸体类型”扫仓库，而不是按目录扫

这是我认为最重要的执行方式。

不要：

```text
今天清 Mission/
明天清 Execution/
后天清 Persistence/
```

这样很容易漏掉跨层 transition。

应该按**旧世界形态**一次杀穿全仓。

---

## Wave 1：死壳 / no-op / 已经没有调用者的 transition API

这是风险最低、收益最高的一批。

你们现在已经有一个非常漂亮的靶子：

`ManagerActivation` 自己明确写着：

* legacy Activation vocabulary；
* production 不再发送 `ManagerWorkActivation`；
* `WorkActivated` 只剩 inert legacy decode；
* production Activation path 已删除。

更值得注意的是，我对整个打包仓库搜索 `ManagerActivation.ensureAccepted`，**只有两个命中，而且都在 HOW 文档里，没有生产调用点。** 

这就是非常典型的：

> “功能已经没了，但旧 architecture vocabulary 还站在那里。”

### 这里不要“简化 ManagerActivation”。

直接做：

```text
ManagerActivation.ensureAccepted
        ↓
确认无生产调用
        ↓
删除 ManagerActivation module
        ↓
删除 EnsureAcceptedResult
        ↓
删除 architecture whitelist / dependency
        ↓
删除测试
        ↓
修 HOW
```

**不要留下：**

```fsharp
[<Obsolete>]
module ManagerActivation
```

也不要：

```fsharp
let ensureAccepted ... = Ready ...
```

更不要改名：

```text
LegacyManagerActivation
```

都属于给尸体换棺材。

### Wave 1 Done

搜索：

```bash
rg 'ManagerActivation|ManagerWorkActivation'
```

允许出现的位置应该最多只剩：

```text
CHANGELOG / historical ADR
```

如果连历史说明都没有持续价值，**零命中更好。**

---

# 四、Wave 2：内部 compatibility adapter —— 这是最大头

这一类通常是“舍不得删”的核心。

你们代码里已经存在非常明确的例子：

```fsharp
/// Compatibility single-result join ...
/// Projects JoinItem → RunCompletion for callers that still need agent Outcome.
let join ...
```

也就是说，新世界已经有 `JoinItem`，但还保留 `RunCompletion` compatibility projection 给“still need”的内部调用者。

这正是本轮应该重点追杀的对象。

做法不是删 adapter 看测试炸。

而是：

```text
Compatibility adapter
        ↓
枚举所有 caller
        ↓
逐 caller 判断“为什么还需要 old representation”
        ↓
把 caller 改成直接消费 canonical representation
        ↓
adapter 调用数 → 0
        ↓
删 adapter
        ↓
删 adapter tests
        ↓
删旧类型（如果无其它职责）
```

你的指标不是：

> compatibility code 少了多少。

而是：

> **compatibility adapter 的 first-party caller 数必须单调下降到 0。**

### 每个 PR 都要求一个数字

例如：

```text
JoinItem → RunCompletion compatibility callers

before: 11
after:   7
remaining: 7
```

下一 PR：

```text
7 → 3
```

最终：

```text
3 → 0
delete adapter
```

这比“感觉代码干净了很多”强得多。

---

# 五、Wave 3：Deprecated 字段——最容易永生的一类

我建议把所有 `DEPRECATED` 直接当 P1 defect，而不是技术债。

仓库里已经有明确实例：

`RunCompletion.AgentId` 被标记为：

> DEPRECATED；为了 HostFork backward compatibility 保留；新代码应该使用 Map key 或 AgentName。

这就是标准 cleanup ticket。

不要继续问：

> “删 AgentId 会不会影响哪里？”

换一个问题：

> **“谁今天还消费 RunCompletion.AgentId？”**

然后把答案做成 call graph。

你目前至少还能看到 compatibility projection 仍在制造这个字段，例如 PTY → `RunCompletion` 时继续填写 `AgentId`。

所以正确顺序是：

```text
1. 找 read sites
2. 替换 read sites
3. 禁止 new code read deprecated field
4. field 变 write-only
5. 删除 writer
6. 删除 field
7. 删除 codec / fixture / test 中对应形状
```

### 特别推荐增加一个临时 gate

不是：

```text
禁止 AgentId 出现
```

因为 AgentId 本身可能是合法概念。

而是针对精确 AST/type surface：

```text
RunCompletion.AgentId forbidden
```

这样 migration 有棘轮效应：

```text
12 callers → 8 → 4 → 0
```

不会被下一个工程师重新加回来。

最终删除字段时，**这个临时 gate 也一起删除**。

不要留下纪念碑。

---

# 六、Wave 4：Persistence compatibility —— 这里绝不能简单“一刀全删”

这一层要最谨慎。

因为你的仓库目前实际上同时存在两种非常不同的 legacy 行为。

### A. 正确的 clean break

`FactCodec` 对一些无法无损解释的旧 journal 明确拒绝：

```text
pre-0.5.0 → reject
ScoreVectorRef-era → reject
unanchored PairProgrammingGuideline → reject
```

这是健康的。

因为代码不是“兼容旧世界”，而是在**拒绝把旧世界解释成当前世界**。

而 durable-events 甚至已经明确规定旧物理 store：

> 不读、不迁、不 reset、不双写；禁止 legacy importer、migrator、fallback-to-old-store shim。

**这种 refusal boundary 不属于兼容债。**

可以保留。

甚至应该比“智能兼容”更偏爱它。

---

### B. 真正还活着的 migration code

但同一个 `FactCodec` 里也还有：

```fsharp
migrateHandleCompleted
migrateHandleOwnership
migrateHandleByname
migrateManagerJobByname
rewriteLegacyObservationTags
```

而最终 `deserializeFact` 确实依次运行这些 migration。

例如 `HandleCompleted` 旧记录缺字段时，目前会自动注入 `null`。

这类不能因为名字叫 migrate 就直接删。

每一个都必须回答：

```text
还有没有真实 durable sample？
这些 sample 最晚可能活到什么时候？
用户是否承诺升级可跨越这个版本？
是否已有 retention horizon？
```

然后分三类：

```text
有真实旧数据 + 必须支持
    → KEEP decode-only + exit condition

无真实旧数据
    → DELETE

无法知道
    → instrumentation / fixture inventory
      不允许直接 KEEP forever
```

### 一个关键原则

允许：

```text
OLD bytes
  ↓
one decoder
  ↓
CURRENT domain
```

禁止：

```text
OLD bytes ↔ OLD model ↔ adapter ↔ CURRENT model
                       ↕
                   new writer
```

你们自己的 rulebook 已经规定了这个 asymmetry：historical durable compatibility 如果需要，可以 decode-only；不要留下旧 writer。

---

# 七、Wave 5：明确有“债权人”的 compatibility —— 不删，但关进隔离区

这是这次 cleanup 非常容易误伤的一类。

例如你们现在有：

> `Host TodoTable compatibility sink`

而且 HOW 明确说：

* 它服务当前 Host V1；
* canonical truth 不依赖它；
* compatibility 不属于永久需求；
* 未来 sink 可以整体替换。

WHAT 也已经把架构画得很正确：

```text
MagicTodoProjection / Journal facts = canonical truth
Host TodoTable                       = compatibility sink only
```

并禁止 sink 反推 canonical。

**这个不要现在硬删。**

因为它目前至少有一个具名债权人：

```text
OpenCode Host V1 TodoTable
```

但现在缺的应该是：

```text
EXIT CONDITION
```

把它改造成显式的：

```text
COMPAT-001

Creditor:
  OpenCode Host V1 TodoTable

Ingress/Egress:
  canonical obligation → V1 projection only

Forbidden:
  V1 → canonical reconstruction

Exit:
  Host V1 TodoTable no longer part of supported host contract

Owner:
  host-boundary

Removal:
  delete Surface.CompatibilityTodoRow
  delete obligationsToCompatibilityRows
  delete V1 canaries
```

这样 compatibility 不再是：

> “最好别动。”

而变成：

> **“这个东西已经被判死刑，只是执行日期由某个客观条件决定。”**

---

# 八、Wave 6：迁移代码比兼容代码更危险——尤其是“修复历史错误”的 runtime migration

你们还有一类非常典型：

`JoinDrain` 中存在：

```text
migrateRetiredFalseAbort
tryMigrateRetiredFalseAbort
migrateOutcomeToUnit
```

而注释直接说明这是：

> “Retired legacy false abort: deterministic replacement + correction”。

另外还存在：

> “Execute replacement migration when blob identity is known.” 

这一类值得单独做 **Migration Amnesty Review**。

因为迁移逻辑经常是最难删除的代码：

```text
“还有没有人处于迁移前状态？”
        ↓
“不知道”
        ↓
“那先留”
        ↓
永久 runtime architecture
```

对每个 runtime migration 强制问：

```text
它修复的是哪个版本以前制造的数据？

新版本还会制造坏数据吗？

坏数据有没有有限集合？

能否改成：
  离线一次性 repair
而不是：
  runtime 永远懂 repair？

有没有 observable evidence 表明坏数据已经为零？
```

如果系统允许 shock cut / archive-and-restart，那么很多 migration 可以进一步直接变成：

```text
detect → refuse
```

而不是：

```text
detect → reconstruct old semantics → rewrite → continue
```

这会让代码量和 state space 大幅下降。

---

# 九、第二轮不是删 production，而是删“防尸体复活的尸体”

这一步很多团队不会做。

重构之后经常会产生大量：

```text
FORBIDDEN_OLD_THING
LEGACY_TOKEN_GATE
NO_OLD_X
NO_V1_Y
absence-ratchet
```

它们在 migration 期间是对的。

**但它们不是永久 architecture。**

你们仓库已经出现这种情况。

例如 `js-surface-gate` 里还明确保存：

```text
js-student
js-teacher
JsStudent
JsTeacher
StudentCompileJs
...
```

作为 `FORBIDDEN_TOKENS`，目的只是确保旧 Student/Teacher world 不复活。

而 requirement 自己已经把这类东西标成：

> GARBAGE；`FORBIDDEN_TOKENS` 是 absence ratchet，**新世界基线稳定后可删**。

这句话非常重要。

### cleanup 的成熟度有三个阶段

```text
阶段 1
旧世界存在

阶段 2
旧世界删除
+ gate 禁止它复活

阶段 3
设计本身使旧世界不可表达
+ 旧名字已经失去文化记忆
+ 删除针对旧名字的 gate
```

你现在应该开始从 2 → 3。

也就是说：

不要永远维护：

```text
NO_STUDENT_TEACHER_REANIMATION_GATE
```

而应该最终靠：

```text
capability ownership rule
role projection rule
type system
positive architecture gate
```

使其无法重新产生。

---

# 十、`unified-store-gate` 是另一个值得“去考古化”的对象

它现在还记得不少历史：

* Student QA revival；
* no-migrator；
* legacy importer；
* dual-write；
* 甚至注释里写着某个旧 ratchet 已于 **2026-08-14 retired**。

这在迁移期非常有价值。

但最终建议把它拆成：

```text
历史 token gate
        ↓
逐步淘汰

永久 architecture invariant
        ↓
保留
```

例如：

不要永久检查：

```text
LegacyMigrator
LegacyImporter
JournalToEventStore
StudentQaMigrator
```

而检查真正永久的性质：

```text
production durable writer ownership = exactly one

runtime store roots ∈ allowed roots

all writes enter canonical EventStore

business modules cannot own durable backends
```

**Positive invariant > blacklist of historical mistakes。**

因为 blacklist 本身也会让未来工程师不停看到已经死亡的 ontology。

---

# 十一、然后做“墓碑文档清理”

你们现在的 HOW/WHY 中有不少：

```text
GARBAGE
历史与弃权
被拒方案
旧 XXX
```

在设计形成阶段非常有用。

但如果最终目标是：

> working tree 描述现在，

就应该开始区分两种历史知识。

### 必须保留

解释**当前奇怪设计为什么必须如此**的 rationale。

例如：

```text
为什么 historical ambiguous record 必须 fail closed
```

这是现在仍然有效的知识。

### 应该删除/归档

只是记录：

```text
我们以前有 A
后来删了 A
A 还有 A1/A2/A3 字段
曾有工具 FooOld
```

而这些信息对理解当前设计已经没有贡献。

这些应该：

```text
Git history
或 ADR archive
```

而不是继续出现在 active HOW。

最终应该努力让：

```text
WHAT = 永久 contract
HOW  = 今天怎么实现
WHY  = 今天为什么这样设计
```

而不是：

```text
HOW = 今天 + 前三朝考古现场
```

---

# 十二、建议具体按下面的 PR train 做

这是我会实际采用的提交顺序。

| PR        | 内容                                                        | 风险 |
| --------- | --------------------------------------------------------- | -: |
| CLN-00    | 建 legacy ledger + cleanup policy                          | ✅ 完成 |
| CLN-01    | 清死代码、无 caller module、commented implementation             | 极低 |
| CLN-02    | 删除 `ManagerActivation` no-op vocabulary + stale HOW/tests | ✅ 完成 |
| CLN-03    | `RunCompletion.AgentId` caller migration                  | ✅ 完成 |
| CLN-04    | 删除 deprecated `RunCompletion.AgentId`                     | ✅ 完成 |
| CLN-05    | Join single-result compatibility caller migration         |  ✅ |
| CLN-06    | 删除 `JoinItem → RunCompletion` internal compatibility path |  ✅ |
| CLN-07    | FactCodec legacy migration inventory，只分类不删                |  ✅ |
| CLN-08..N | 每种 durable decode 单独裁决（LEGACY-013 已删除，LEGACY-010/011/012/014 BOUNDED-COMPAT 保持） | 中高 |
| CLN-X     | `false abort` runtime migration retirement                |  高 |
| CLN-Y     | Host V1 compatibility sink 加 creditor + exit contract     |  低 |
| CLN-Z     | retire historical absence ratchets                        |  ✅ |
| CLN-Z2    | active HOW/WHY historical tombstone cleanup               |  ✅ |
| FINAL     | 删除 legacy ledger 自身 + permanent architecture gates        |  低 |

注意：

**一个 PR 尽量只消灭一种 old-world concept。**

不要搞：

```text
cleanup legacy stuff
-143 files
```

那样 reviewers 最后一定因为不敢承担风险，把很多东西重新保回来。

---

# 十三、每个删除 PR 强制用同一个五步模板

这是“保姆级”的核心工作流。

```text
STEP 1 — ACCUSE
指出为什么它是 legacy：
“X exists only to support Y.”

STEP 2 — PROVE NO CREDITOR
搜索：
caller
writer
reader
test
fixture
public API
durable sample
deployment consumer

STEP 3 — MIGRATE
如果还有 repository-owned caller，
先迁 caller，不碰 compatibility implementation。

STEP 4 — DELETE
一次删除：
implementation
types
aliases
tests
fixtures
docs
flags
special cases

STEP 5 — ABSENCE PROOF
rg old-name
build
target tests
integration tests
architecture gate
```

尤其 STEP 4：

**不要只删 implementation。**

例如删除 `LegacyFoo` 时，目标是：

```text
LegacyFoo.fs              delete
LegacyFooTests             delete
LegacyFooFixture           delete
LegacyFooAdapter           delete
LegacyFooConfig            delete
LegacyFoo terminology      delete
LegacyFoo docs             delete
LegacyFoo TODO             delete
```

否则旧世界的“幽灵 ontology”还在。

---

# 十四、每个 compatibility survivor 都必须长这样

以后 review 里看到 compatibility，没有下面四句话就不准 merge：

```text
Compatibility creditor:
  <谁>

Old contract:
  <什么>

Boundary:
  <只允许在哪一层存在>

Exit condition:
  <什么可观察事实成立时删除>
```

例如：

```text
Compatibility creditor:
  OpenCode Host V1 TodoTable

Old contract:
  todos[{content,status,priority}]

Boundary:
  Mission/Obligation/Todo/Surface only

Exit condition:
  Host V1 TodoTable is removed from supported host contract.
```

严禁：

```text
// Keep for backwards compatibility.
```

这句话以后应该视为 lint error。

因为它什么信息都没提供。

---

# 十五、建立一个“删除预算”，不要建立“技术债 backlog”

我甚至建议每轮 cleanup 设 **negative LOC objective**。

不是 KPI 式盲删，而是方向性约束：

```text
本轮允许：
+ 100 行证明/architecture gate

但要求：
- 1000 行 transitional machinery
```

特别记录下面这些指标：

| Metric                               |         方向 |
| ------------------------------------ | ---------: |
| deprecated production fields         |        → 0 |
| internal compatibility adapters      |        → 0 |
| compatibility first-party callers    |        → 0 |
| runtime migrations                   |       → 极少 |
| dual representations                 |        → 0 |
| legacy aliases                       |        → 0 |
| old writers                          |        → 0 |
| compatibility without exit condition | → **绝对 0** |
| historical token blacklist           |          ↓ |
| GARBAGE/tombstone active docs        |          ↓ |
| canonical writers per semantic fact  |        → 1 |

真正重要的不是总代码行数。

而是：

> **一个 semantic fact 有几个 live representation / writer / path？**

目标永远是：

```text
1
```

---

# 十六、专门制定“奥卡姆剃刀 review 问句”

以后 code review 里不要问：

> 这个兼容代码有没有害？

问下面这些问题：

```text
如果把它删掉，具体谁会失败？

能给我 consumer 名字吗？

能给我真实 persisted sample 吗？

这是 read compatibility 还是 write compatibility？

为什么 current code 还能制造 old representation？

为什么 compatibility 不在 boundary？

为什么 repository-owned caller 不能迁？

这个 adapter 的 retirement condition 是什么？

如果三个月后没人记得它，代码自己能说明为什么还存在吗？

如果以后真需要它，为什么不能从 Git 找回来？
```

最后一问尤其重要。

因为你最开始说的那个心理：

> “怕删了找不回来”

在 Git repository 里，本质上是一个**错误的风险模型**。

删除的成本通常是：

```text
git log / git show / revert
```

保留的成本却是：

```text
每个新人阅读
× 每次搜索
× 每次重构
× 每次测试
× 每次设计
× 永久
```

---

# 十七、但一定要防止“奥卡姆剃刀”变成“大爆炸式删库”

这点我反而建议你很克制。

你们仓库已经明确提醒：

> anti-cruft 不是破坏真实 contract 的许可证。

所以不要下命令：

> “把所有 legacy、compat、migration 全删掉。”

正确命令是：

> **“所有 legacy、compat、migration 全部重新接受审判。”**

默认 verdict 是 DELETE。

但下面三种必须无罪：

```text
真实 public compatibility
真实 durable decode
真实 deployment overlap
```

区别在于它们不再拥有“永久居留权”。

只是：

```text
bounded exception
```

---

# 十八、我认为你这个仓库现在最值得先砍的四刀

根据当前代码，我会按这个顺序开工。

### 第一刀：`ManagerActivation`

这是最漂亮的 starter PR。

源码自己承认 production path 已删除、模块只剩 no-op vocabulary；而全仓精确搜索 `ManagerActivation.ensureAccepted` 只有 HOW 文档命中。

**目标：0 source occurrence。**

这刀可以给团队建立“真的可以删，而且删完世界没有塌”的信心。

### 第二刀：`RunCompletion.AgentId`

源码已经明确标 `DEPRECATED`、只因 backward compatibility 保留。

把所有 first-party read site 迁掉，然后删除字段。

这是训练团队：

> deprecated ≠ 永久供奉

的最好案例。

### 第三刀：single-result Join compatibility

`JoinItem` 已经是新 representation，但代码还明确给 “callers that still need agent Outcome” 做 `RunCompletion` projection。

迁完这些 caller，然后删 compatibility API。

这一刀开始真正降低 architecture state space。

### 第四刀：FactCodec compatibility census

**先不删。**

把：

```text
migrateHandleCompleted
migrateHandleOwnership
migrateHandleByname
migrateManagerJobByname
rewriteLegacyObservationTags
```

每项单独建立 creditor / durable-sample / exit-condition。

因为这些是最可能既包含真需求、又包含历史恐惧的地方。当前 deserialize pipeline 明确仍会调用它们。

这刀会告诉你真正还剩多少“必须背负的过去”。

---

# 十九、最终完成态不是“没有 legacy 这个单词”

真正的最终态应该是：

```text
Production
    一个 canonical ontology
    一个 authoritative writer
    一个正常 execution path

Compatibility
    只在物理 boundary
    只服务 named creditor
    通常 decode/project one-way
    每条有 exit condition

Tests
    验 current behavior
    验 permanent architecture invariant
    不供奉已删除 ontology

Docs
    描述当前 system
    rationale 保留
    尸体清走

History
    Git 负责
```

这恰好就是你们仓库已经写出的 invariant：

> **Current code has one canonical model; compatibility exists only at boundaries where a real supported past still touches the present.** 

以及我认为最适合成为此次工程结束语的那一句：

> **The migration machinery has nothing left to arbitrate.
> The new architecture is not “preferred.” It is simply the architecture.** 

如果按这个 roadmap 执行，我建议内部不要把它叫“代码清理”或者“第五轮重构”。

叫 **Refactor Closure** 更准确。

因为前几轮是在建设新世界；**这一轮是在宣布旧世界不再享有公民权。**

---

我们把它定成一次**从“Fable 测试适配”迁移到“JS-native semantic architecture”**的系统改造。

终态不是“测试更好写”，而是：

> **所有测试都是 JS；所有值得测试的语义都有正式、稳定、JS-native 的边界；实现细节没有边界，因此 JS 根本无法依赖。**

这和仓库已有的测试哲学完全一致：测试应落到 supported input / observable result / durable state / contractual interaction，并允许内部 rename、inline、换数据结构而不受影响。

---

# 0. 先冻结“宪法”

在动代码之前，先把以下六条写进新的 requirement，例如：

```text
requirements/js-semantic-surface/
  README.md
  WHAT.md
  WHY.md
  HOW.md
  PROOF.md
```

内容不要写成“解决 mangled name”，那只是 symptom。

写成：

1. **所有 automated tests 使用 JavaScript。**
2. **JS semantic tests 只能调用正式 semantic surface。**
3. **值得独立测试的 law 必须有独立 semantic owner + JS surface。**
4. **不拥有独立 law 的 helper 不直接测试。**
5. **semantic data 跨边界必须是 JS-native representation。**
6. **Fable runtime representation 不属于 semantic contract。**

再加一句非常重要的：

> A surface exists because a semantic component owns a contract, never because a test needs access.

### JS-native 的定义

普通数据只允许：

```text
string
number
boolean
null / undefined
array
plain object
Promise
JS function/callback
```

必要时可以有：

```text
bigint
opaque resource handle
```

但 opaque handle 只能：

```text
create → pass back → dispose
```

JS 不得读它的 fields/prototype。

禁止作为 semantic data 暴露：

```text
FSharpList
FSharpMap
FSharpSet
FSharpOption
FSharpResult
F# DU instance
F# record runtime class
tag
fields
cases()
Fable DateTimeOffset encoding
curried F# function
mangled instance method
```

---

# 1. 先做 inventory，暂时不改行为

第一步不是写新 API。

先弄清现在 JS 测试到底获得了多少“不该有的权力”。

新增一个临时 inventory script，例如：

```text
scripts/test-surface-inventory.mjs
```

扫描全部：

```text
requirements/**/tests/**/*.mjs
```

记录五类债务。

### A. deep production import

例如：

```js
import '../../../dist/Execution/Session/...'
import '../../../dist/Foundation/...'
import '../../../dist/OpenCode/...'
```

### B. Fable export discovery

例如：

```js
Object.keys(mod)
Object.entries(mod)
startsWith('Foo__Bar_')
endsWith('_Baz')
```

你仓库已经有明确实例：`SessionQuiescenceGate` 测试直接扫描 mangled methods。

### C. Fable representation knowledge

搜索：

```text
.tag
.fields
.cases()
FSharpList
FSharpMap
fable_modules
```

### D. legacy interop authority

搜索：

```text
member(
bind(
fableInstanceMethod(
prod(
toList(
caseOf(
payloadOf(
resultOf(
```

现有 `interop.mjs` 明确承担了 emitted-name resolution、Fable mechanics，而且集中加载大量内部 production modules。 

### E. 合法的 compiler/build verification

**不要误杀。**

例如现有：

```text
VERIFY_008_every_emitted_module_actually_loads
```

故意 import 所有 emitted JS 来证明 Fable build 真能 link。这个测试的 subject 就是编译产物，因此它有资格知道 `dist`。

把这种测试明确分类成：

```text
compiler/build verification
```

而不是 semantic test。

---

# 2. 立刻加“只减不增” gate

inventory 完成后，**马上阻止债务继续增长**。

不要等迁完才加 gate。

建立：

```text
requirements/verification-system/tests/js-boundary-gate.test.mjs
```

规则：

```text
新 semantic test:
    禁止新增 deep dist import
    禁止新增 mangled-name lookup
    禁止新增 Fable representation knowledge
    禁止新增 interop.mjs dependency
```

现存违规先进入临时 baseline：

```text
requirements/verification-system/tests/fixtures/
  legacy-js-boundary-debt.json
```

原则：

```text
baseline 可以删
baseline 不可以加
```

每迁一个测试，就删一个 baseline entry。

### 为什么先做这个？

否则你迁 30 个，别人又新增 20 个。

仓库自己的 boundary rule 已经明确提出应该机械扫描 dependency：foreign layer 只能指向正式 supported entry，禁止 deep path / generated detail。

---

# 3. 定义“surface”是什么，不是什么

这一步尤其重要，否则很快就会造出第二代 `domain.mjs`。

## 错误设计

```text
src/Wanxiangshu/TestApi.fs
```

里面：

```fsharp
let callJoinDrain = Internal.JoinDrain.drainFromJournal
let makeFact = ...
let internalState = ...
let callPrivateThing = ...
```

这是 **test facade**。

禁止。

同样禁止：

```text
PublicFacade
    = re-export everything internal
```

仓库现有规则也明确把这种做法列为假修复。

---

## 正确设计

surface 跟着 semantic owner 走。

例如：

```text
Host/Quiescence/
  Model.fs
  Policy.fs
  Surface.fs

Participant/Provider/Attempt/
  ...
  Surface.fs

Context/Prefix/
  ...
  Surface.fs
```

不是一定必须叫 `Surface.fs`。

也可以叫：

```text
Api.fs
Contract.fs
```

重点是：

> 它属于这个 subsystem，不属于 Tests。

并且它不是简单 forwarding。

它负责：

```text
JS representation
        ↓
semantic input
        ↓
owner
        ↓
semantic output
        ↓
JS representation
```

---

# 4. 先迁一个“纯语义 pilot”

不要第一枪就挑最复杂 Host runtime。

先选一个：

* 输入清晰；
* 输出清晰；
* 没有 resource lifecycle；
* 现在却通过 `domain.mjs` / Fable representation 测试；

的 pure component。

目标形式：

```js
const result = component.operation({
  ...
})

assert.deepEqual(result, {
  ...
})
```

而不是：

```js
const input = toList(...)
const result = resultOf(...)
assert.equal(caseOf(result), ...)
```

---

## pilot 的工作步骤

假设原测试是：

```js
const result = resultOf(
  InternalModule.someFunction(
    sessionId('s1'),
    toList(items)
  )
)

assert.equal(caseOf(result.error), 'Conflict')
```

### 第一步：先写 promise

不用看实现，写：

> Given X, when Y happens, the component rejects it as a conflict.

如果这句话写不出来，先别设计 API。

### 第二步：设计 JS representation

目标：

```js
const result = component.someOperation({
  sessionId: 's1',
  items: [...]
})

assert.deepEqual(result, {
  ok: false,
  error: {
    kind: 'conflict'
  }
})
```

### 第三步：F# 内部继续保持 F# idiom

内部完全可以还是：

```fsharp
SessionId
Item list
Result<'a, Conflict>
Map<...>
DU
```

不要为了 JS 把 domain 污染成 primitive soup。

### 第四步：surface translation

逻辑上：

```text
"s1"
 ↓
SessionId.create

JS array
 ↓
Array.toList

Result<_, DU>
 ↓
{ ok, value/error }
```

转换发生在 owner boundary。

### 第五步：删测试里的 interop helpers

完成后，这个 test 不得再出现：

```text
sessionId()
toList()
resultOf()
caseOf()
```

---

# 5. 给 surface 本身建立 contract test

每建立一个正式 surface，都要有一个非常小的 API contract test。

你仓库已有 `guide-contract.test.mjs` 的机制可以复用：它会检查 emitted surface 的函数是否存在，甚至 pin exact surface。

例如：

```js
import * as quiescence from '...stable surface...'

assert.deepEqual(
  Object.keys(quiescence).sort(),
  [
    'beginAttempt',
    'create',
    'dropSession',
    'observeIdle',
    'revoke',
    'tryConsume',
  ]
)
```

注意：

**只有正式 contract surface 才 pin 名字。**

内部 module 的 emitted names 不 pin。

这正是我们需要的区别：

```text
internal rename
    → irrelevant

public surface rename
    → breaking contract
```

---

# 6. 第二个 pilot：专门攻克 stateful abstraction

接下来迁 `SessionQuiescenceGate` 这类东西。

这是很好的代表，因为现在测试实际上知道：

```text
SessionQuiescenceGate
BeginProviderAttempt
ObserveIdle
TryConsume
RevokeCurrentAttempt
DropSession
```

并通过 mangled method discovery 调用。

而 production implementation 内部实际上维护 `serials` 和 `activities` 两张 mutable map。

这些 state **JS 不应该知道**。

---

## surface 可以长成

```js
const gate = quiescence.create()

quiescence.beginAttempt(gate, 's1')

const permit =
  quiescence.observeIdle(gate, 's1')

assert.equal(
  quiescence.tryConsume(gate, permit),
  true
)

assert.equal(
  quiescence.tryConsume(gate, permit),
  false
)
```

这里：

```text
gate
permit
```

可以定义为 **opaque handle**。

测试只能：

```text
拿到
传回
```

不能：

```js
gate.serials
permit.fields
permit.tag
```

这样将来内部：

```text
Map → Dictionary
serial → generation token
class → actor
mutable state → immutable state + cell
```

JS 测试完全不变。

当前 gate 本身的语义已经非常清楚：新 provider attempt 使旧 permit 失效；idle 产生 permit；permit 只能消费一次；drop/revoke 使旧 permit 无效。

这就是应该发布的 law。

而不是它当前由哪两张 Map 实现。

---

# 7. 建立统一的 representation rules

两个 pilot 完成后，不要继续自由发挥。

把经验固化成规则。

建议建立一个非常小的测试 helper：

```text
requirements/verification-system/tests/support/
  js-contract.mjs
```

它**不是 domain facade**。

它只检查 representation：

```js
assertJsData(value)
assertOpaque(value)
```

比如递归拒绝：

```text
.cases()
.fields + numeric tag union shape
FSharpList tail/head representation
FSharpMap runtime object
Fable reflection metadata
```

最好进一步规定：

> 除 opaque resource handle / callback / Promise 外，semantic values 必须是 JSON-shaped。

那就非常容易理解：

```js
JSON.stringify(result)
```

理论上应该工作。

### 时间也建议归一

不要让 JS boundary 收到 Fable DateTimeOffset。

优先：

```text
ISO-8601 string
epoch milliseconds
```

内部再转换。

现有 facade 专门验证过裸 JS `Date` 与 Fable DateTimeOffset 可以产生 silent timezone bug。

终态不应该是教每个测试正确构造 Fable DateTimeOffset。

终态应该是：

> JS 根本构造不了 Fable DateTimeOffset。

---

# 8. 开始 Wave A：纯函数 / algebra / projection

这是最大批、也是最容易批量迁的部分。

优先迁：

```text
decision
classification
projection
codec
rendering
validation
selection
planning
ordering
```

每个 test 严格套同一个模板。

## 单测试迁移 SOP

### 1. 读测试名和 requirement clause

先别看 helper。

问：

> 它究竟要证明哪句话？

---

### 2. 写成 Given / When / Then

例如：

```text
Given an old permit
When a new provider attempt begins
Then the old permit cannot authorize continuation
```

---

### 3. 圈出真正输入

不是：

```text
FSharpMap
DU tag
InternalProjection
```

而是：

```text
events
commands
identity
policy configuration
```

---

### 4. 圈出真正 observable

例如：

```text
decision
rendered output
durable facts
allowed/rejected
next semantic state
effect request
```

---

### 5. 删掉草稿里的 implementation nouns

如果测试设计里出现：

```text
private field
helper function
module emitted name
cache implementation
Map key layout
DU ordinal
```

重新设计。

---

### 6. 判断是否真的存在独立 law

如果没有：

**不要开 surface。**

改测它的 owner。

---

### 7. 如果存在，找到 semantic owner

把 boundary 放 owner 旁边。

不要塞进中央：

```text
TestApi
DomainFacade
InteropEverything
```

---

### 8. 设计 JS representation

先写理想 JS：

```js
const actual = capability(input)
```

再去写 F#。

不要从现有 F# type 倒推 JS shape。

---

### 9. 写 surface contract test

证明：

```text
名字稳定
参数语义稳定
输出 JS-native
```

---

### 10. 重写原 behavior test

此时测试中 Fable vocabulary 应归零。

---

### 11. 做 positive canary

故意：

```text
rename helper
inline helper
change internal collection
reorder pure calculations
```

测试仍 green。

---

### 12. 做 negative canary

故意：

```text
return wrong decision
publish twice
accept stale permit
swap identity
```

测试必须 red。

这就是你仓库规则要求的“双向验证”。

---

### 13. 删 legacy dependency

删除：

```text
domain.mjs import
interop helper usage
direct dist import
baseline entry
```

**一个测试完成迁移的定义就是 baseline 少一项。**

---

# 9. Wave B：state machine / resource

接着处理：

```text
SessionQuiescenceGate
AttachedSessionRuntime
CompletionMailbox
ForkRuntime
process lifecycle
shared runtime resources
```

这些不要暴露 internal state snapshot。

优先 surface 成：

```text
create/open
command
observe
dispose
```

例如：

```js
const runtime = runtimeApi.create(config)

await runtimeApi.start(runtime, input)

const result =
  await runtimeApi.join(runtime)

runtimeApi.dispose(runtime)
```

opaque handle 不属于 semantic data。

它只是 capability token。

测试不能 inspect。

---

# 10. Wave C：effects

有副作用的 subsystem 尽量拆成：

```text
semantic decision
      ↓
effect request
      ↓
host interpreter
      ↓
effect result
      ↓
semantic transition
```

例如：

```js
const action = policy.decide(input)

assert.deepEqual(action, {
  kind: 'kill-process',
  processId: 'p1'
})
```

这部分可以大量 pure JS behavior tests。

然后单独：

```js
const actual =
  await processHost.execute(action)
```

测真实 effect boundary。

这样就不会为了测试 policy 而 mock 一大坨 Host。

---

# 11. Wave D：integration / plugin / e2e

这些本来就在真正的 external boundary 上，改动反而可能最小。

原则仍一样：

```text
发送真实 supported input
观察真实 supported output/effect
```

不通过内部 state 验证。

如果 E2E 失败需要 diagnostics：

diagnostics 可以存在，但必须是**正式 diagnostics contract**，而不是：

```text
__getPrivateStateForTests
```

---

# 12. 每完成一个 Wave，就收紧 gate

不要最后统一清理。

假设开始时：

```text
legacy violations = 180
```

Wave A 后：

```text
120
```

就把 baseline 永久降到 120。

Wave B：

```text
60
```

继续降。

直到：

```text
0
```

然后删除 baseline 机制本身。

最终 gate 直接：

```text
任何 semantic test deep-import internal dist
→ fail

任何 semantic test 使用 Fable representation
→ fail
```

---

# 13. `domain.mjs` 的退场路线

不要直接删除，因为现在它还是大量测试的 anti-corruption boundary。

当前设计本身很清楚：`domain.mjs` 是 transition entry，真正 Fable mechanics 在 `domain/interop.mjs`，family adapters 建在它上面。

所以分四步。

## 第一步

冻结：

> No new imports from `domain.mjs`.

## 第二步

每迁一个 family：

```text
identity
journal
context
execution
orchestrator
...
```

减少其 exports。

## 第三步

当普通 semantic tests 不再依赖 representation helpers 时，删除：

```text
bind
member
fableInstanceMethod
unionCase
prod
```

## 第四步

最后删除普通测试可见的：

```text
caseOf
payloadOf
toList
listItems
mapEntries
resultOf
unwrapOption
```

注意：

不是因为这些 helper 写得不好。

相反，它们现在非常有价值，甚至保护了真实 silent hazards。现有 meta tests 已经证明 JS array/FSharpList、DU ordinal、DateTimeOffset 等问题确实会产生静默错误。

删除它们意味着：

> **它们成功完成了迁移任务，以后普通测试已经到不了危险区域。**

---

# 14. 保留一个非常小的 Fable quarantine

这里不要走到另一种 dogma。

最终仍然可以有：

```text
requirements/verification-system/tests/compiler-interop/
```

这种测试专门验证：

```text
Fable output links
package emitted correctly
public JS surface exports correctly
compiler/runtime versions compatible
```

这些测试**有资格知道 Fable**。

因为被测对象就是 Fable build。

例如现有“every emitted module actually loads”应该保留。

最终边界应该是：

```text
99% semantic tests
    know zero Fable

tiny compiler/build suite
    explicitly knows Fable
```

而不是假装整个 repository 连 build verification 都不知道编译器存在。

---

# 15. 给 code review 一个固定判定树

以后 PR 新增测试时 reviewer 只问这几步：

```text
这个测试在证明一个独立 semantic law 吗？
              │
      ┌───────┴───────┐
      no              yes
      │                │
测 owner behavior    law 的 owner 是谁？
                       │
                 已有 JS surface？
                  │          │
                 yes         no
                  │          │
               使用它      设计正式 surface
                              │
                       是 JS-native 吗？
                         │        │
                        yes       no
                         │        │
                        done    修 representation
```

永远没有：

```text
“测试需要，所以 export internal”
```

这个分支。

---

# 16. 一组非常具体的 forbidden patterns

终态 architecture gate 可以扫描 semantic tests 并拒绝：

```js
value.tag
value.fields
value.cases()

Object.keys(fsharpModule)
Object.entries(fsharpModule)

startsWith('SomeType__')
endsWith('_someMethod')

import '.../fable_modules/...'

import '../../../dist/<internal-path>.js'
```

以及：

```text
member
bind
fableInstanceMethod
unionCase
```

甚至可以针对名字拒绝新增：

```text
ForTests
TestOnly
UnsafeForTest
DebugState
InternalFacade
TestFacade
```

不是说字符串永远非法，而是任何出现都要求 architecture review。

---

# 17. 不要做的五种“捷径”

### ① 自动把 `domain.mjs` 翻译成 F#

这是失败。

只是：

```text
JS test facade
→ F# test facade
```

问题没变。

---

### ② 给每个 F# module 都生成 JS wrapper

也是失败。

你会得到：

```text
1 implementation module
=
1 JS API
```

这仍然把 decomposition 变成 contract。

---

### ③ 为了测试暴露完整 state

例如：

```js
runtime.snapshotForTests()
```

返回：

```text
all private maps
all internal phases
all cursors
```

也是 white-box test，只是序列化了一层。

---

### ④ 为了 JS 把 F# domain 全 primitive 化

不要。

内部继续：

```text
DU
typed IDs
Map
Option
Result
records
```

强类型越丰富越好。

只在 boundary translate。

---

### ⑤ 建一个超级 `PublicApi.fs`

会逐渐变成 god module。

仓库自己对 cosmetic facade 的警告也适用于这里：facade 不能替 subsystem 制造虚假的 coherent ownership。

surface 应跟着 semantic owner 分布。

---

# 18. 我建议的实际迁移顺序

按这个顺序做，不要按目录字母序：

### P0 — Architecture charter

写六条宪法 + JS representation contract。

**完成条件：**以后什么算合法 surface 已无歧义。

### P1 — Inventory

列出所有 deep imports / Fable knowledge / interop usage。

**完成条件：**债务有有限集合。

### P2 — Ratchet gate

现存债务 baseline，新债务禁止产生。

**完成条件：**数字只会下降。

### P3 — Pure pilot ✅

迁一个 pure semantic component。**完成条件：**证明 JSON-shaped contract 可行。**达成：**`ForkChildPayloadSurface`（注册 surface #1，JSON-shaped 输入输出，assertJsData 证明）。

### P4 — Stateful pilot ✅

迁 `SessionQuiescenceGate` 一类 abstraction。**完成条件：**证明 opaque capability + behavior surface 可行。**达成：**`QuiescenceSurface`（gate/permit opaque handle，8 个 HOST-004 law）。

### P5 — Representation gate ✅

建立统一 JS-native validator。**完成条件：**Fable runtime value 无法意外穿过新 surface。**达成：**`js-contract.mjs`（assertJsData/assertOpaque）+ charter「注册 surface 必有契约测试」门禁。

### P6 — Pure/algebra wave — IN PROGRESS

大量迁 projection/decision/codec/policy tests。**完成条件：**`domain.mjs` 使用量明显下降。**已达成（首批）：**6 个注册 surface（ForkChildPayload/SyntheticToml/BloggerToml/Quiescence/DelegatedToolEstimate），13 个测试文件迁移，债务 3185→3171、文件 316→312。

**现状（PR 0 裁决）：pilot validated; systemic migration not yet achieved。** 债务下降 14 行只证明 pilot 能工作，不证明 pure/algebra 世界已切换到 semantic surfaces；225 文件仍携带债务（js-boundary-baseline），328 文件仍 import domain.mjs。

### P7 — Resource/runtime wave — NOT PROVEN

迁 stateful runtime。

**完成条件：**普通测试不再扫描 instance mangling。
**已达成（首波）：**`RolesSurface`（第 7 个注册 surface，Role/ToolPermission 以 string 跨界，default-deny）；label 唯一表示上移 `Roles.fs`，ManagedAgentCatalog 委托；5 个测试文件迁移；`Roles_isAllowed` 与 `roles.permissions` 用法清零，债务 3171→3169。

**现状（PR 0 裁决）：NOT PROVEN。** `RolesSurface` 是 vocabulary/policy surface，不证明 resource lifecycle（create/mutate/concurrency/lifetime/dispose/observable state transition）。真正能作为 stateful pilot 的是 `QuiescenceSurface`（P4 已达成）；resource/runtime wave 的完成条件（普通测试不再扫描 instance mangling）尚未整体达成——P10 wave-2 未提交改动（已 stash）显示大量 runtime 测试仍依赖 mangled method discovery。

### P8 — Effect/integration wave — NOT PROVEN

迁 Host/effect tests。

**完成条件：**contractual effect 成为 observable，而不是 private choreography。
**已达成（增量）：**CLN-08 执行 census 裁决——删除 `FactCodec.migrateManagerJobByname`（零测试零真实数据，decode 链私有步骤退役）。

**现状（PR 0 裁决）：NOT PROVEN。** 删 dead compatibility code 是好事，但它不是 effect decision → effect request → interpreter → result → semantic observation 的重构证据。不得拿别的 cleanup 工作给当前 milestone 充数。

### P9 — Delete legacy adapters — STARTED

逐 family 删除 `domain/*` adapters。

**完成条件：**semantic tests 不再 import `domain.mjs`。
**已达成（增量）：**删除六个零引用零依赖死 adapter（forkChildPayload/processEstimate/packageResources/orchestratorProgram/setCount/setContains），-106 行。

**现状（PR 0 裁决）：exit condition NOT MET。** 355 文件仍 import domain.mjs（后续 wave）。

### P10 — Quarantine Fable

只剩 compiler/build verification 可以理解 Fable。

**完成条件：**Fable upgrade 不影响 semantic tests。

### P11 — Remove baseline

违规数为零，删 baseline。

**完成条件：**architecture gate 从 ratchet 变成 absolute prohibition。

---

# 19. 最终 Definition of Done

这次 migration 只有同时满足下面这些才算结束：

```text
[ ] 所有 semantic automated tests 是 JS

[ ] semantic tests 中没有 Fable mangled-name knowledge

[ ] semantic tests 中没有 .tag/.fields/.cases()

[ ] semantic tests 中没有 FSharpList/FSharpMap/FSharpResult 等 runtime knowledge

[ ] semantic tests 不 import fable_modules

[ ] semantic tests 不 deep-import internal dist modules

[ ] domain.mjs / domain/interop.mjs 不再作为 semantic test gateway

[ ] 每个被独立测试的 semantic law 都能说出明确 owner

[ ] 每个 JS surface 都能用业务/系统语义描述，而不是用 F# module 名解释

[ ] semantic data 是 JS-native / JSON-shaped

[ ] effectful resource 只以 opaque capability 暴露

[ ] 内部 rename/inline/restructure 不要求修改 JS tests

[ ] 同样的 internal structure 下破坏真实 promise 会让 JS tests 失败

[ ] Fable-specific knowledge 只存在于 compiler/build verification quarantine

[ ] legacy boundary baseline 已删除
```

最终的依赖关系就非常简单：

```text
                     ┌──────────────────────┐
JS semantic tests ──▶│ Stable JS semantics │
                     └──────────┬───────────┘
                                │
                         representation
                           translation
                                │
                     ┌──────────▼───────────┐
                     │   F# semantic owner  │
                     └──────────┬───────────┘
                                │
                     private decomposition
                                │
                  helpers / DU / Map / runtime
```

而：

```text
compiler/build tests
        │
        └──────────▶ Fable emitted artifacts
```

是另一条明确隔离的线。

**我会把整个 migration 的成功指标压缩成一句：**

> 改造前，JS 测试在问“F# 是怎么实现的？”；改造后，JS 测试只能问“这个 semantic component 承诺什么，以及它有没有做到？”

到那一步，mangled name 不再是“被解决的问题”——它已经变成**测试世界里不存在的概念**。

现在已经明显不一样了。**我认为“大规模重排目录”这件事已经基本完成，可以停止继续折腾顶层树了。** 这一版已经从“新旧两棵树并存”进入了“ownership tree 基本成立，只剩少数错误根和依赖边需要校正”的阶段。

最关键的变化是，`Domain / Application / Session / Infrastructure` 这些历史技术层已经不再出现在生产编译路径里；现在真正存在的是 `Change / Context / Enforcer / Execution / Foundation / Interaction / Mission / Participant / Persistence / Repository / Strength / OpenCode ...`。目录树已经能直接读出业务所有权。  `.fsproj` 也已经实际按这棵新树组织，而不是目录只改了名字、编译关系仍沿用旧层。比如 `Kernel/Fact` 已经变成 `Composition/Durable/Fact`，`CausalWait` 进入 `Execution/Session/Wait`，SyncDelegate 进入 `Execution/Delegation`，PromptAuthority 进入 `Interaction/Authority`。 

而且 capability-specific adapter 的“下旋”已经做得相当漂亮了。现在 Fork 自己拥有 `Fork/OpenCode/{Tool,JoinTool,JoinGuard,JoinResultRenderer}`，Fission、Finality、Review、Todo、Strength、Casebook、Js 也开始把自己的 OpenCode 接口收回自己的子树。  这就是我们之前说的：

> 物理世界是依赖对象，不自动获得业务代码的 ownership。

现在最值得做的不是“第三次整体排布”，而是下面 **5 个局部旋转**。

1. **最大的剩余错误根是 `Composition/Durable/Fact.fs`。** 文件虽然从 `Kernel` 搬出来了，但实际上还没有完成我们说的那次旋转：`PromptFactCases`、`ReviewFactCases` 等业务 fact family 仍然定义在这个中央文件里。   也就是说现在是：

   ```text
   Composition/Durable/Fact
      ├── Prompt facts
      ├── Review facts
      ├── Execution facts
      ├── ...
   ```

   我仍然建议最终旋成：

   ```text
   Interaction/Authority/Facts.fs
   Participant/Provider/Attempt/Fallback/Facts.fs
   Mission/Review/Facts.fs
   Execution/Delegation/Facts.fs
   Context/Companion/Facts.fs
   Execution/Fission/Facts.fs
   Change/Facts.fs
            \   |   /
       Composition/Durable/Fact.fs
   ```

   `Composition/Durable/Fact.fs` 最终只应该做 outer union / routing vocabulary。**Composition 可以认识所有人，但不应该替所有人定义自己的语言。** 这是当前最有价值的一刀。

2. **`Foundation` 里还有两三个“假基础”。** 最明显的是 `Foundation/Flow.fs`。它里面有 `InvalidFork`、`ParentCancelled`、`CompanionError`、`CompanionContext`——这些显然不是宇宙级 primitive，而是 Execution/Companion 语义。 我会把它拆掉，而不是保留一个叫 `Flow` 的杂交根。比如 `InvalidFork/ParentCancelled` 靠近 `Execution`，`CompanionError/CompanionContext` 靠近 `Context/Companion`。相比之下 `Identity/Roles/Temporal/Parallel/TaskResult` 留 Foundation 很合理。

   `McpLaunch` 我倒没那么介意。它确实只是一个非常小的共享 launch vocabulary：`Disabled | Fixture | Uvx`。 它可以以后再判断是否值得变成 `Host/Mcp/Launch`，不是当前重点。

3. **`Composition/Durable/GuidelineProjection.fs` 应该再旋出去。** 它不是 composition；它定义的是非常具体的 `PairProgrammingGuideline` durable state、ordinal、call/result transcript gap 及其 fold invariant。 我更倾向：

   ```text
   Host/
     PairProgramming/
       GuidelineProjection.fs
   ```

   或者如果你认为 cognitive environment 才是 owner，就在那里建对应节点。

   相反 `Composition/Durable/HostFactFold.fs` 留下是合理的。它本来就是一个认识很多 bounded contexts 的汇合/router，当前 imports 几乎覆盖整棵树，这在 Composition 节点反而符合职责。

4. **现在最需要修的其实已经不是目录，而是 architecture gate。** `HOST_BOUNDARY_OPEN_BASENAMES` 仍然活着，而且已经膨胀到非常危险的程度：除了旧名字以外，现在连 `Host.fs`、`Runtime.fs`、`Workflow.fs`、`Types.fs`、`Recovery.fs`、`Repair.fs` 等通用 basename 都被放进去了；而 `isHostBoundaryOpenPath` 判断时真的只取 basename。 这意味着理论上：

   ```text
   Whatever/Runtime.fs
   ```

   仅仅因为叫 `Runtime.fs`，就可能获得本来不该有的物理边界权限。

   这和现在已经形成的 ownership tree 是冲突的。第二轮以后应该反过来按**路径语义**授权，例如：

   ```text
   **/OpenCode/**
   **/Host/**
   OpenCode/**
   Process/**
   ```

   再对极少数 bridge 精确列路径，而不是列 basename。

   更明显的是，`dsl-ownership-ratchet-baseline.json` 还保存着上一轮的 `Feedback/Enforcer`、`OpenCode/Contract`、旧 `Composition` 等路径。 但你自己的 structured-workflow 文档已经把这个 ratchet 明确标记为 **cutover 后 DELETE**，只留下 `--threshold=0` 的 positive gate。 而现在 `check.mjs` 已经确实把主门设成了 `--threshold=0`。 所以这里应该收尾：**删掉 migration ratchet 和旧 baseline，而不是继续维护它。**

5. **最后做一次“假依赖边清理”，再决定是否继续旋转。** 例如 `Foundation/SyntheticToml.fs` 一开头竟然 `open` 了 Composition、Context、Enforcer、Execution、Host、Mission、Participant 等大量上层 subtree。 可它自己的注释却明确说它“knows nothing about Blogger, forks, or any local schema”，只拥有 canonical TOML string/layout rules。 从实际实现看，前面的 `normalizeNewlines / renderString / comment / field / tableEntry` 也确实是纯格式算法。 

   所以这里很可能不是根真的错，而是机械迁移后留下了一堆 unused `open`。**先清 unused imports，再画依赖图。** 否则我们会根据幽灵依赖做错误旋转。清完以后，如果 SyntheticToml 真只依赖 `System`，它放 Foundation 虽然我个人更喜欢 `Participant/Provider/Wire/Toml.fs`，但已经属于命名品味问题，不再是架构问题。

我现在对整体结构的判断可以浓缩成：

```text
上一版：
旧 layer tree + 新 ownership tree
→ 需要继续砍旧根

这一版：
ownership tree 已经成立
→ 不要再大改树
→ 拆中央 Fact
→ 清 Foundation 假基础
→ 把具体 Projection 从 Composition 移走
→ 删除迁移 whitelist / baseline
→ 清 unused imports
→ 再根据真实 cross-tree edges 做少量 rotation
```

还有一点我会特别强调：**现在不要因为 `OpenCode`、`Execution`、`Mission` 文件多，就试图“平衡文件数量”。** 平衡树思想在这里应该平衡的是“语义路径和跨树依赖代价”，不是节点个数。当前不同子树大小明显不一样，但这已经开始像自然生长出来的依赖树，而不是人为铺平的 taxonomy。 

所以如果问我“现在还乱不乱”，我的判断是：**生产目录本身已经不乱了。现在的主要架构债已经转移成了边界门禁、中央 Fact ownership 和少数假 Foundation 节点。** 这是一个好信号——说明目录重构基本可以收工，接下来应该治理依赖边，而不是继续搬树。

以上判断基于你刚上传的最新完整仓库快照。

这个方向值得做，而且我建议把它做成**比“覆盖率”更基础的一条仓库不变量**。

你现在已经有：

```text
WHAT ──→ PROOF ──→ test file
```

当前 `meta-verifier` 会枚举 WHAT proposition，检查 PROOF 有对应行，并检查 PROOF 引用的测试文件存在。 

但缺的是反方向：

```text
test case ──→ WHAT
```

这其实正好落实你已经写下的 `REQUIREMENT-SYSTEM-004`：proof ownership 是 **assertion 级，不是文件级**，每条 executable assertion 必须有唯一 owner。

我建议最终把关系做成一个数学上很简单的闭环：

```text
                 PROOF.md
                /        \
               /          \
              v            v
          WHAT-xxx  <───  test()

必须同时满足：

∀ test: exactly one primary WHAT
∀ WHAT: at least one active test
test → WHAT 必须存在
WHAT → test 必须存在
PROOF 中记录的边必须真实存在

skip / todo ≠ proof
```

换句话说，**active tests → WHAT 是一个 total + surjective mapping**。

而且我赞成你想要的压力：找不到 WHAT 的测试不允许用 `N/A` 糊弄过去。

---

## 我推荐的最终写法

不要靠目录推断，也不要靠文件顶部一行注释推断。直接让**每个 test 名自己携带 WHAT ID**：

```js
test(
  'WHAT[PROVIDER-LANGUAGE-005] system transform localizes only Wanxiangshu-owned segment',
  async () => {
    // ...
  },
)
```

动态 case 也一样：

```js
for (const bad of badSignals) {
  test(`WHAT[PROCESS-EXECUTION-003] rejects unsupported signal ${bad}`, () => {
    // ...
  })
}
```

机器合同只认：

```text
WHAT[<CURRENT-WHAT-ID>]
```

不认历史 `PROMPT_017`、`REVIEW_007` 之类 ID，不认文件路径隐式 ownership，也不认注释里的“看起来差不多”。

这有一个非常好的副作用：**CI 报错和本地 test output 本身就回答了“这个测试为什么存在”。**

你现在其实已经有很多人工版雏形。例如 `provider-system-transform.test.mjs` 文件头已经花了一段话解释它属于 `provider-language`，对应 `PROVIDER-LANGUAGE-001/005`，而不是另外几个相邻 owner。 以后这种判断直接进入机器关系，不需要靠考古。

---

# 保姆级 Roadmap

1. **先写新 WHAT，不要先写 gate。** 在 `requirements/requirement-system/WHAT.md` 新增一条，我建议叫 `REQUIREMENT-SYSTEM-018：可执行证明双向可追溯`，不要修改现有 004 的含义。004 继续负责“每个 executable assertion 恰一个 package owner”；018 负责更严格的“每个 test case 恰一个 current WHAT proposition”。你现在已经声明 WHAT 是唯一 normative contract，WHY/HOW/PROOF 都不是 normative，所以这条新规则必须先落 WHAT。

   我建议规范陈述直接写成接近这样：

   > `requirements/**/tests/**/*.test.mjs` 中的每个可执行 test case 必须显式声明恰一个当前 WHAT proposition ID；该 ID 必须存在于唯一 owner package 的 WHAT.md。每个当前 WHAT proposition 必须至少被一个非 skip、非 todo 的 test case 证明。test 与 WHAT 之间不存在无归属、悬空、多 primary 或仅依赖路径推断的关系。

   边界里再明确：helper、fixture、`beforeEach`、普通 `assert` 不是独立 proof case；粒度以 `test()/t.test()` 为准。一个 test 不允许 primary 到两个 WHAT。

2. **把“一个测试只能回答一个 WHAT”定死。** 这是我建议你比现在再严格一步的地方。当前 PROOF 里已经有一个 test anchor 同时服务多个 proposition 的情况，例如 interaction-authority 的表里存在 `001/002` 合并关系。 新规则下不要写：

   ```js
   test('WHAT[A-001,A-002] ...')
   ```

   而应该拆成：

   ```js
   test('WHAT[A-001] receipt cannot become authority root', ...)
   test('WHAT[A-002] only physical message may establish root', ...)
   ```

   两个测试可以共享 setup、helper，甚至共享一次昂贵的物理运行结果，但**failure meaning 必须只有一个**。如果两条命题根本无法分别测试，优先回头问 WHAT 是否其实应该是一条命题。这正是你要的文档反哺。

3. **定义测试宇宙，避免 denominator 偷漏。** 第一版严格限定：

   ```text
   requirements/**/tests/**/*.test.mjs
   ```

   里面所有真正的 `test()`、`test.skip()`、`test.todo()`、nested `t.test()` 都必须被 scanner 看到。`*.fixture.mjs`、support helper、`before/after` 不算 test。`skip/todo` 可以要求带 WHAT 标签，但**不能算作 WHAT 已有 proof**。

   这一条非常重要，否则以后很容易出现一个漂亮的 gate，却漏掉某类 integration/eval/e2e tests。

4. **不要继续把逻辑塞进现在的 meta-verifier；抽一个 trace graph。** 当前 `meta-verifier` 已经同时负责包树、依赖骨架、WHAT ID、PROOF 文件存在等结构检查。 再往里面直接加 JS test AST 解析，会很快变成 god verifier。

   我建议增加：

   ```text
   scripts/lib/requirement-trace.mjs
   scripts/checks/requirement-trace.mjs
   requirements/requirement-system/tests/requirement-trace.test.mjs
   ```

   `requirement-trace.mjs` 只构建一个纯数据图：

   ```text
   WhatNode {
     id
     package
     file
     heading
   }

   TestNode {
     file
     line
     title
     state: active | skip | todo
     whatId
   }

   Edge {
     test
     what
   }
   ```

   `meta-verifier` 后面可以复用这个 graph，而不是各自重新 regex。

5. **test source 用 AST/token parser 扫，不要用粗 regex。** Gate 必须能区分字符串里的 `test(`、注释、`test.beforeEach`、alias、template title、nested test 等情况。这个项目已经很重视 fail-closed gate，我不建议为了省一个轻量 parser 而造一个未来必漏的正则扫描器。

   Scanner 至少要能报这些错误：

   ```text
   TRACE_ORPHAN_TEST
   foo.test.mjs:42
   "rejects invalid carrier"
   has no WHAT[...] owner

   TRACE_UNKNOWN_WHAT
   foo.test.mjs:81
   references WHAT[FOO-999], but that proposition does not exist

   TRACE_MULTI_PRIMARY
   test declares more than one primary WHAT

   TRACE_UNPROVED_WHAT
   FOO-007 has zero active executable tests

   TRACE_PROOF_MISSING
   FOO-003 points to this test, but PROOF.md does not expose the relation

   TRACE_DANGLING_PROOF
   PROOF.md names a test anchor that no longer exists
   ```

6. **在真正迁移前先做 report-only inventory。** 命令建议设计成：

   ```bash
   node scripts/checks/requirement-trace.mjs --report
   node scripts/checks/requirement-trace.mjs --package=provider-language
   node scripts/checks/requirement-trace.mjs --explain=path/to/test.mjs:42
   ```

   第一次不要红 CI，只生成类似：

   ```text
   package                   WHAT   active tests   orphan tests   unproved WHAT
   provider-language            5             12              3               0
   interaction-authority       16             31              8               1
   ...
   ```

   `--explain` 最终应该成为非常好用的维护工具：

   ```text
   test
     requirements/provider-language/tests/provider-system-transform.test.mjs:27

   proves
     PROVIDER-LANGUAGE-005

   normative source
     requirements/provider-language/WHAT.md
     ## PROVIDER-LANGUAGE-005 ...

   proof index
     requirements/provider-language/PROOF.md
   ```

   这样“为什么有这个测试”不再需要 grep。

7. **迁移时绝对不要让脚本自动生成 WHAT。** 脚本可以根据当前位置、现有 PROOF、历史 ID、文件头注释给出 candidate，但只能建议：

   ```text
   likely WHAT:
     PROVIDER-LANGUAGE-005  0.92
     PROVIDER-LANGUAGE-001  0.71
   ```

   人必须做最终裁决。每碰到一个 orphan test，只允许四种处理：映射到现有 WHAT；发现文档遗漏，先补一个真正的 WHAT 再映射；发现测试钉的是 HOW 细节，重写成能够证明现有 WHAT 的行为测试；发现它没有独立 failure meaning，删除或并入别的测试。

   **第四种和第二种就是整个机制最值钱的地方。**

8. **按 package 小批量迁，不要全仓一次机械加标签。** 先 dogfood `requirement-system` 和 `verification-system`，因为它们负责规则本身；然后迁 owner 很清晰的小包；最后再处理 structured-workflow、host-boundary、capability-enforcement 这些交叉很多的包。

   每一个 package 都重复同一个闭环：

   ```text
   inventory tests
        ↓
   给每个 test 找 WHAT
        ↓
   找不到 → 文档 / 测试裁决
        ↓
   WHAT[...] 写入 test title
        ↓
   PROOF exact anchor 对齐
        ↓
   package trace = 100%
        ↓
   package 进入 hard mode
   ```

   不建议在这个阶段顺手大规模重构 production。一次 commit 尽量只做一个 package 的 trace closure，这样 review 能真正判断映射有没有作弊。

9. **迁移期可以有 ratchet，但必须从出生起就写 DELETE 条件。** 你刚刚才清理了一批历史 migration baseline，所以这次不要再造永久白名单。可以临时生成：

   ```text
   scripts/checks/requirement-trace-migration.json
   ```

   里面列当前仍未认领的 test anchor。规则只能：

   ```text
   新 orphan = RED
   已认领项不得重新进入 baseline
   baseline 数量只降不升
   ```

   然后逐包 hard：

   ```text
   strict:
     requirement-system
     verification-system
     provider-language
     ...
   ```

   当最后一个 package 进入 strict，**同一个提交删除 migration file 和 compatibility branch**。不要留下 `--allow-unmapped`。

10. **Hard cutover 时再把 PROOF 从“文件存在”升级到“精确边闭合”。** 你当前 parser 对 PROOF 的检查实际上只提取落点文件 token，然后确认文件存在。 这还不够，因为：

```text
PROOF says foo.test.mjs
```

并不能证明里面真的还有那个 test。

目标应升级成：

```text
WHAT[FOO-003]
    ↕
PROOF.md exact test anchor
    ↕
foo.test.mjs exact test case
```

你的 PROOF 文档已经大量写了“文件 + test/describe 锚点”，所以这是自然强化，不是换模型。

11. **最后我甚至建议让 PROOF 的 executable 部分半生成。** WHAT 必须坚持手写，因为它是 normative authority；test 的 WHAT tag 也必须人工裁决。PROOF 本身是 non-normative evidence index，没有必要让人重复抄几百个 anchor。

可以变成：

```markdown
## Executable proof index

<!-- BEGIN GENERATED TRACE -->

| WHAT | Active test cases |
|---|---|
| PROVIDER-LANGUAGE-001 | ... |
| PROVIDER-LANGUAGE-005 | ... |

<!-- END GENERATED TRACE -->

## Manual / physical evidence

...人工维护...
```

这样真正的 source of truth 是：

```text
WHAT.md          人写：系统必须是什么
test() WHAT tag  人裁决：这个 test 为什么存在
PROOF.md         生成：当前 evidence graph 长什么样
```

不会出现三个地方手工复制同一事实然后互相漂移。

12. **Full hard mode 后，把规则接进最前面的 cheap checks。** 我会把 `requirement-trace` 放在 build/test 之前：

```text
spec
requirement-trace
architecture
build
tests
...
```

新人写：

```js
test('some regression', ...)
```

应该在几十毫秒到几秒的静态门阶段直接收到：

```text
This test has no normative reason to exist.
Choose exactly one:
  1. reference an existing WHAT
  2. add a missing WHAT first
  3. rewrite the test so it proves an existing WHAT
  4. delete the test
```

这比等 code review 问“这个测试到底在测什么”有效得多。

---

## 我会再加一个防“文档作弊”的小机制

否则开发者可能学会这样过 gate：

```markdown
## FOO-999：其它行为

**规范陈述**：系统其它行为必须正确。
```

然后一百个测试全挂 `FOO-999`。

机器无法真正判断散文质量，但至少可以把作弊成本提高。既然你现在 WHAT 已经采用“规范陈述 + 含义/动机 + 边界 + 证据指针”的结构，例如现有 WHAT 就明确把这些组成看成 proposition 的完整表达。

所以 trace gate 在解析被 test 引用的 WHAT 时，还应该要求这些字段**非空存在**：

```text
标题
规范陈述
含义/动机
边界
```

不要用“至少 5 行”“至少 100 字”这种垃圾 heuristic；只检查结构存在。语义是否真的够具体仍交给 review。

同时提供非阻塞统计：

```text
WHAT fan-in:

FOO-001     3 tests
FOO-002     6 tests
FOO-003    47 tests  ← review hint, NOT automatic RED
```

47 个测试指向一个 WHAT 不一定错，但 reviewer 会马上知道应该检查是不是 catch-all。

---

## 关于低层 unit test，我建议你狠一点

以后如果看到：

```js
test('PtyId roundtrips its value', ...)
```

第一反应不要是“给它随便找一个 process WHAT”。

先问：

> **如果这个实现从 wrapper class 换成别的表示，这个 test 仍然应该成立吗？**

如果答案是否，那它很可能只是在 pin HOW。

此时应该考虑把它改成真正的 contract test，或者删除，而不是把 implementation detail 升格成 WHAT。

这会让你的测试数量可能有所下降，但测试的**信息密度会明显提高**：

```text
以前：
代码存在 → 顺手写 test

以后：
WHAT 存在
  ↓
需要 executable evidence
  ↓
test 存在
```

反方向：

```text
发现值得长期保留的 regression test
  ↓
找不到 WHAT
  ↓
说明：
  文档漏了 invariant
  或
  这个 regression 并不是产品合同
```

这正是你要建立的反馈回路。

---

## Cutover 的最终验收标准

到最后，仓库应该能机械证明：

```text
orphan active test                    = 0
test with unknown WHAT                = 0
test with multiple primary WHAT       = 0
WHAT with zero active test            = 0
PROOF anchor missing                  = 0
PROOF dangling anchor                 = 0
temporary trace migration exceptions  = 0
```

并且任意一个 test，你都能得到：

```text
这个测试为什么存在？
        ↓
WHAT[XXX-NNN]
        ↓
requirements/<owner>/WHAT.md
        ↓
这条当前系统真理是什么？
```

我认为这会比传统的“requirements coverage = 100%”强很多。传统 coverage 只能证明**文档没有漏测**；你这个双向闭环还能证明**测试没有偷偷创造第二套需求体系**。而你现有 requirement-system 已经把“WHAT 是唯一合同”和“executable assertion 有唯一 owner”铺好了，实际上只差把这条反向边机器化。 


---

# PR 0 完成（2026-08-16）

已将 opening P 状态与 section 18 对齐：
- P6 — IN PROGRESS：pilot validated; systemic migration not yet achieved
- P7 — NOT PROVEN
- P8 — NOT PROVEN
- P9 — STARTED：6 dead adapters deleted; 331 domain.mjs consumers remain; exit condition NOT MET

虚假完成状态已撤销；后续 PR 按新 DoD 推进。

---

# 第一轮整改进度（2026-08-16，待集成验证）

已完成代码切片，尚未宣称 milestone 完成：

- Boundary scanner 已改为覆盖 `requirements/**/tests/**/*.mjs`；tracked forbidden fixture 已删除，charter 改为运行时临时 fixture；manifest 当前 21 个 surface，重复项已修正。boundary gate 仍需修复 empty-baseline 终态并跑到 0。
- Finality vertical slice 已切断 `finality-contract.mjs`，测试通过 `FinalitySurface` 使用 JS-native history/state；manifest 已补 FINALITY-001/027/028。
- EventStore slice 已拆 EventStore/Journal resource、codec、merge、FactCodec surface；指定 durable tests 已迁移，仍有未迁移 durable tests。
- Fission/Distiller slice 已切断指定 role/runtime 测试的 Fable 表示；owner source 与 manifest 已补。
- Provider projection algebra slice 已新增 `ProjectionSurface`，两组代数测试已迁移；fsproj/manifest 已补。
- Join cleanup 已删除 `renderCompletedBatch` / `renderCompletionItem` 过渡路径；`JoinItem.ofAgentRunCompletion` 保留为仍有生产债权人的 canonical projection。
- Composition/Durable/Fact ownership rotation 已完成第一轮 owner files、GuidelineProjection owner relocation 与静态路径更新。
- Participant identity/deadline slice 已新增 JS-native Persona、Session、Prompt、Deadline、SessionBinding、ModelRouting surfaces；三组 participant tests 与 deadline tests 已迁移，并已通过编译与 focused proof。
- Behavior-diagnosis、capability-enforcement、causal-wait/change-integration/time、durable-events residual、managed-session/host、provider/speculative zones 已完成 owner surface 切片；编译与 manifest 已集成，需在全库债务清零前继续复核跨包 support/domain 调用。
- Casebook Index/Bookkeeper/Lifecycle/Fetch、Context Companion、Obligation Ledger、Crash/Delegation/Horizon/Fallback、Process/Interaction owner surfaces 已注册并纳入 602-source green build；semantic tests 已迁移至 JS-native/opaque APIs，仍需全库 gate 清零。
- Durable/Prefix/Review/Repository/Provider residual waves and shared support cutover are integrated; `verification-system/tests/support/domain` and `glory.mjs` were deleted after zero semantic-zone imports. Only the build-verification `run-inner.mjs` path check remains exempt.

```text
semantic test files with debt = 1 (build-verification support only)
violating lines             = 5 (run-inner.mjs Fable path checks)
requirement-trace findings  = 0
registered surfaces         = 129
Fable build                 = green (644 source files)
```

因此 P6/P7/P8/P9 仍保持：

```text
P6 — IN PROGRESS
P7 — NOT PROVEN
P8 — NOT PROVEN
P9 — STARTED; semantic owner cutover complete, only exempt build-verification path debt remains
```

下一步必须先完成：

```text
[x] requirement-trace findings → 0
[ ] boundary debt → 0 non-exempt debt；保留 build-verification path exemption
[x] support/domain 与 package-local contract authority → 0 semantic consumers
[x] remaining durable/provider/host/effect semantic tests → production surfaces
[ ] integrated build + focused tests + full gate proof
```

