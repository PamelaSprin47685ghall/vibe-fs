> `IAsyncEnumerable`/Flow 完全可以把复杂时序表述得非常凝练、明显正确；问题不在“流”，而在于是否把**业务一致性、外部副作用和流的取消语义**混成一件事。

你以前使用 Kotlin Flow 重构复杂状态机的经验，很可能确实可以迁移到这里。

# 一、我之前混淆了三件事

## 1. Flow 作为函数式时序代数

Kotlin Flow 不只是一个异步迭代器。它提供了完整的组合语言：

```text
map
filter
scan / runningFold
combine
merge
flatMapLatest
flatMapMerge
buffer
conflate
catch
retry
onCompletion
StateFlow
SharedFlow
```

Flow 默认顺序执行，并明确强调 context preservation 和 exception transparency；`StateFlow`、`SharedFlow` 又补充了热流、共享状态和广播语义。正因为这些约束和算子，Flow 确实很适合把“事件随时间如何演化”写成高阶函数。([Kotlin][1])

例如：

```kotlin
events
    .runningFold(initialState, ::reduce)
    .map(::deriveAction)
    .distinctUntilChanged()
```

这本身就比手工写几十个 setter、listener 和等待 flag 更容易证明。

## 2. 裸 `IAsyncEnumerable<T>` 协议

裸 `IAsyncEnumerable<T>` 的核心只是异步的 pull-based sequence：消费者通过 `MoveNextAsync`/`await foreach` 请求下一个元素。它本身没有 Kotlin Flow 那么完整的热流、广播、状态保持和并发算子语义。微软文档也明确把它描述为异步流/异步迭代机制；在 .NET 10 中才正式提供完整的 `AsyncEnumerable` LINQ 算子，较早版本通常依赖 `System.Linq.Async`。([微软学习][2])

所以：

```text
Kotlin Flow
≠ 裸 IAsyncEnumerable
```

但可以通过：

```text
IAsyncEnumerable
+ Channel
+ AsyncEnumerable operators
+ 少量自定义 scan/merge/latest 算子
```

获得非常接近 Flow 的表达能力。

## 3. 用流替代领域语义

这是我真正反对的部分：

```text
把“枚举器被取消”
直接等价为
“业务 continuation 已失效”
```

或者：

```text
把 flatMapLatest 取消了本地 Task
直接等价为
Host 没收到 prompt
```

这不成立。

但这不妨碍我们用 Flow 表达：

```text
Abort 事件
→ cancelGeneration + 1
→ 旧 effect result 被 filter 掉
```

换句话说：

> **Flow 可以表达状态机，但不能替代 continuation ID、generation、provenance 等领域身份。**

这两者应该结合，而不是二选一。

# 二、真正合适的方案可能是“Flow 化 Reactor”

我现在认为，对你的项目最优雅的架构很可能不是前面说的“普通 Reactor”，而是：

> **单一反馈流 + 纯 `scan` 状态积分 + 声明式 effect 流**

整体可以写成：

```text
Host events ──────┐
Tool results ─────┤
Timers ───────────┤
Effect results ───┘
        │
        ▼
    Input Flow
        │
        ▼
 normalize / dedup
        │
        ▼
 scan(step, initialState)
        │
        ├── State snapshots
        ├── Domain events
        └── Effects
                   │
                   ▼
           Effect Flow
                   │
                   ▼
          结果回灌 Input Flow
```

这本质上仍然是单写者，但代码表达形式是 Flow，而不是 Actor handler。

# 三、核心不是 `State -> State`，而是 `Step`

建议定义：

```fsharp
type Step =
    { State: SessionState
      Events: DomainEvent list
      Effects: Effect list }

val step:
    SessionState ->
    SessionInput ->
    Step
```

然后整个 Session Kernel 是：

```fsharp
inputs
|> AsyncEnumerable.scan
    (fun step input ->
        SessionKernel.step step.State input)
    { State = initialState
      Events = []
      Effects = [] }
```

或者 Kotlin 风格：

```kotlin
val steps: Flow<Step> =
    inputs.runningFold(Step.initial) { previous, input ->
        kernel.step(previous.state, input)
    }
```

这就是你说的：

> 把复杂状态机用高阶函数表述得凝练。

而且完全可行。

# 四、策略模块也可以天然 Flow/函数化

此前我说 fallback、budget、review、nudge 应是纯 candidate 函数。它们完全可以组合成一个高阶决策流：

```fsharp
let policies =
    [ FallbackPolicy.tryPlan
      CompactionPolicy.tryPlan
      ContextBudgetPolicy.tryPlan
      ReviewPolicy.tryPlan
      TodoPolicy.tryPlan ]

let plan snapshot =
    policies
    |> List.choose (fun policy -> policy snapshot)
    |> List.sortBy Candidate.priority
    |> List.tryHead
```

甚至表达为：

```text
stateFlow
 ├─ map fallbackCandidate
 ├─ map compactionCandidate
 ├─ map budgetCandidate
 ├─ map reviewCandidate
 └─ map todoCandidate
          │
          ▼
       combine
          │
          ▼
      chooseHighest
```

这里 Flow 的组合性确实比一个巨大的：

```fsharp
match input, lifecycle, owner, phase, ...
```

更有优势。

不过，我更推荐在**一个 snapshot 上同步调用所有纯 policy**，而不是创建五条真正独立调度的异步流。因为这些计算都很快，不值得引入流之间的采样时刻问题。

也就是：

```fsharp
stateFlow
|> map plan
```

而不是：

```fsharp
combine
    fallbackFlow
    nudgeFlow
    reviewFlow
```

前者每次使用同一个原子 snapshot；后者可能需要考虑不同流是否已经看到同一个版本。

# 五、Flow 最有价值的地方，是把“异步查询生命周期”写得漂亮

例如收到新 human turn 后，需要异步获取：

* transcript；
* 当前 model；
* context limit；
* token usage。

这种查询非常适合 `flatMapLatest`：

```kotlin
humanTurns
    .flatMapLatest { turn ->
        flow {
            val transcript = host.fetchTranscript(turn.sessionId)
            emit(TranscriptFetched(turn.epoch, transcript))
        }
    }
```

新人类轮次到达，旧 transcript 查询自动取消。

但结果仍然携带：

```text
humanTurnId
sessionGeneration
cancelGeneration
requestId
```

即使底层请求不能真正取消，返回结果进入 reducer 时也会因为 epoch 过期而被忽略。

因此是双保险：

```text
flatMapLatest：减少无用工作
epoch 校验：保证领域正确性
```

这其实比纯 Actor 手写 request ID、cancel pending、callback routing 更凝练。

# 六、哪些 Flow 算子在这个项目中会非常合适

## `scan` / `runningFold`

用于唯一状态积分：

```text
Input Flow
→ SessionState
```

这是核心。

## `flatMapLatest`

适用于：

* 当前 human turn 的 transcript 获取；
* 当前 model/context-limit 查询；
* 新 generation 出现后旧查询无意义的操作；
* UI 派生信息。

不适用于已经开始的、必须记录物理结果的 Host prompt dispatch。

## `flatMapMerge`

适用于相互独立的外部读取：

```text
fetch transcript
fetch provider info
fetch model info
```

并发完成后将结果作为独立 input 回流。

## `debounce`

适用于：

* 高频 token usage 更新；
* progress display；
* 非关键 nudge 重新评估。

不能用于 Abort、lease、dispatch、settlement 等领域事实。

## `conflate`

只适用于：

* telemetry；
* UI progress；
* “只关心最新值”的观察量。

绝不能用于：

* human message；
* Abort；
* continuation dispatch；
* tool result；
* event log append。

## `distinctUntilChanged`

特别适合候选动作去重：

```text
连续多个状态都得出同一个 nudge candidate
→ 只产生一次 proposal
```

但最好基于强类型 fingerprint：

```fsharp
CandidateId =
    humanTurnId
    + generation
    + purpose
    + anchor
```

不能只按 prompt 文本去重。

# 七、Flow 版本应该如何处理副作用

这是最容易写错的地方。

错误版本：

```fsharp
inputs
|> scan reduce
|> mapAsync host.SendPrompt
```

因为：

* `mapAsync` 如果顺序等待，可能阻塞 Abort；
* 如果并发执行，可能乱序；
* 如果取消枚举，物理副作用状态不明；
* 可能先执行副作用，后写事件日志。

更好的结构是两个区：

## 串行提交区

```text
Input
→ step
→ append DomainEvent transaction
→ commit state
→ emit Effect
```

这一区必须顺序执行。

## 并发 Effect 区

```text
Effect Flow
→ flatMapMerge(maxConcurrency)
→ Host I/O
→ EffectResult
→ 写回 Input Channel
```

这一区可以并发。

伪代码：

```fsharp
let committedSteps =
    inputs
    |> scan SessionKernel.step initialStep
    |> mapAsyncSequential (fun step ->
        promise {
            do! journal.Append step.Events
            return step
        })

let effects =
    committedSteps
    |> collect (fun step -> step.Effects |> AsyncEnumerable.ofSeq)

let effectResults =
    effects
    |> flatMapMerge maxConcurrency executeEffect

effectResults
|> iterAsync inbox.WriteAsync
```

最关键的是：

> `EffectRunner` 不修改 SessionState，只把结果写回输入流。

这和当前 `SubsessionActor` 中“queue 只 commit decide/state，Host effects detached outside queue”的正确设计原则完全一致，只是改用流算子表达。

# 八、Flow 可以让 Abort 更漂亮，而不是更危险

可以把 Abort 分成两个层次。

## 计算层取消

```kotlin
currentEpoch
    .flatMapLatest { epoch ->
        runCurrentQueries(epoch)
    }
```

Abort 或新 turn 更新 epoch，自动取消旧计算。

## 领域层取消

```fsharp
UserAbortObserved
→ reducer:
    CancelGeneration + 1
    Lifecycle = Cancelled
    ActiveLease = invalidated
```

所有 effect result：

```fsharp
effectResults
|> filter (fun result ->
    result.Context.Generation = current.Generation
    && result.Context.CancelGeneration = current.CancelGeneration)
```

不过这个 filter 最好仍在 `step` 内进行，以便把迟到结果记录成：

```text
StaleEffectIgnored
```

而不是静默丢弃。

当前 PRD 也已经指出，状态机返回 `DoNothing` 并不足以阻止已经离开状态机的旧 `SendContinue`；最终必须比较 session generation、human turn ID、cancel generation、continuation ID 和 owner。

Flow 不会改变这个事实，但能把结果路由和取消组合写得更简洁。

# 九、`IAsyncEnumerable` 相比 Kotlin Flow 的真实差距

不应该说它“不堪”。更准确是：

## Kotlin Flow 是完整语言

它已经定义好了：

* 冷流；
* hot `StateFlow`/`SharedFlow`；
* buffer；
* merge；
* latest；
* context preservation；
* exception transparency；
* structured concurrency；
* cancellation 传播。

## `IAsyncEnumerable` 是较小的基础协议

它天然擅长：

* 单消费者；
* pull/backpressure；
* 顺序异步迭代；
* 延迟生成；
* 组合查询。

在 .NET 10 中，`AsyncEnumerable` 已有完整 LINQ 查询算子，包括异步 selector；但 hot sharing、当前值、广播和复杂并发调度通常仍需要 Channel 或额外抽象。([微软学习][3])

所以对于你的目的，我甚至建议不要过度执着接口名字。

可以定义自己的：

```fsharp
type Flow<'T> = IAsyncEnumerable<'T>
```

再补少量真正需要的算子：

```fsharp
scan
mapAsyncSequential
flatMapLatest
flatMapMerge
merge
share
stateIn
takeUntil
materialize
```

或者在 Fable/JS 中直接基于：

```text
AsyncIterable<'T>
+ Channel/Subject
+ Promise
```

实现同一套代数。

重要的是 Flow 语义，而不是是否使用 BCL 的那个具体接口。

# 十、流式架构有几条不可破坏的约束

只要遵守这些约束，Flow 可以比 Actor 更“明显正确”。

1. **状态 fold 只能有一个 collector。**

   不能对冷流 collect 两次，否则可能重复执行整个流程或重复副作用。

2. **领域输入在 `scan` 前不能 unordered merge。**

   多 Host 来源必须先进入有序 inbox，分配 sequence 后再 fold。

3. **`scan` 中不能执行慢 Host I/O。**

   否则整个流被背压，Abort 也被堵住。

4. **副作用前先持久化 intent。**

   `ContinuationPlanned` append 成功后才允许 dispatch。

5. **取消必须既是控制信号，也是领域事件。**

   `CancellationToken` 负责停止本地工作；`UserAbortObserved` 和 generation 负责业务正确性。

6. **不得对领域事实使用 `conflate`、`debounce` 或 `drop`。**

7. **`flatMapLatest` 只用于可安全废弃的查询。**

   不能用它表示“Host 一定没收到 prompt”。

8. **所有并发 effect 返回结果都必须回到唯一 fold。**

做到这些，Flow 并不隐晦；反而会把串行、并发、取消和反馈边界在 pipeline 上直接画出来。

# 十一、我现在的修正结论

我不再建议：

> 不要用 `IAsyncEnumerable` 取代状态机。

更准确的建议是：

> **可以用 Flow/`IAsyncEnumerable` 高阶算子取代目前手写的控制流状态机、回调和 mutable flags；但应保留一个强类型的纯 `step`/`scan` 作为领域语义核心。**

也就是：

```text
Flow 是组合语言
State + Event 是语义
scan 是状态积分
Channel 是反馈入口
Effect Flow 是并发边界
generation/lease 是正确性证明
Event Log 是持久事实
```

最可能的最佳形态是：

```text
Channel<SessionInput>
        ↓ ReadAllAsync
normalize
        ↓
scan SessionKernel.step
        ↓
append transaction sequentially
        ↓
publish state + effects
        ↓
effects.flatMapMerge execute
        ↓
results → Channel<SessionInput>
```

它可以叫：

> **Flow-based Event-Sourced Session Loop**

这比我前面描述的传统 Actor/Reactor 外壳更接近你的经验，也很可能更适合你的思维方式。

当前仓库已经有大量纯 fold、纯 decision 和事件状态结构，但运行时仍散落着独立 mutable 状态；Flow 化最有价值的地方，正是把这些已有纯函数串成一条唯一、可组合、可观测的时间管线，而不是重新写一套新的大状态机。

**所以这一次，我倾向于支持你的方向：值得认真做一个 Flow-first 原型，而不是先否定它。**

[1]: https://kotlinlang.org/docs/coroutines-flow.html?utm_source=chatgpt.com "Flows | Kotlin Documentation"
[2]: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/yield?utm_source=chatgpt.com "yield statement - provide the next element in an iterator"
[3]: https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/10.0/asyncenumerable?utm_source=chatgpt.com "Breaking change - System.Linq.AsyncEnumerable in .NET 10"
