# Structured Flow 内核

承接 KISS-00：控制流去函数化 → 业务源码重新成为结构化程序。本卷定义可编译执行基座。

代码块：`[NORMATIVE]` 须进真实编译测试 · `[ILLUSTRATIVE]` 省略细节 · `[FORBIDDEN]` 反例。

---

## 一、核心类型 [NORMATIVE]

```
type Flow<'ctx, 'error, 'a> =
    private
    | Flow of ('ctx -> CancellationToken -> Task<Result<'a, 'error>>)
```

- 可执行闭包。非 AST、非自由 Monad、非工作流图、非 Registry。
- `'ctx`：领域上下文，内核对业务字段不透明。
- `'error`：终止当前 Flow 的错误。可预见分支用成功通道 DU（KISS-02）。
- `CancellationToken`：唯一取消信号族（可 linked）。
- `Task`：全库异步货币。Host 边界若给 Promise/Async，只在 Adapter 转一次。

`Flow` 是可执行闭包，不是可检查流程图。因此第一版不提供 AST、解释器、动态注册或 continuation 序列化；重启依赖事实、稳定键和主程序重入，而不是恢复某个 Stage 编号。

### 1.0 Bind 不变式

`Bind` 只执行左侧、遇到 Error 短路、把成功值传给右侧。它不自动捕获异常、不刷新投影、不写 Journal、不重试。

```
```

`Flow` 是函数闭包而不是可检查的流程图，因此第一版不提供 AST、解释器、动态注册或 continuation 序列化。重启恢复依赖领域事实、稳定键和主程序重入，而不是恢复某个“执行到第 17 个 Stage”的快照。

### 1.0 Bind 不变式

`Bind` 只做三件事：执行左侧、遇到 `Error` 短路、把成功值传给右侧。它不自动捕获异常、不刷新投影、不写 Journal、不重试。

```
action Error → next 不执行
action Ok x   → next x 执行
action throw  → 向领域 run / Host 边界传播
action cancel → OCE 传播，不伪装为业务 Error
```

因此“需要业务匹配的失败”不能塞进 Flow Error 后再期待 `match!` 看见它；它必须成为成功值 DU（KISS-02）。

### 1.1 进展策略（可选）

通用 `While` 不得写死 `ctx.Revision` 或硬编码 `NoProgress` 构造。

```
type ProgressGuard<'ctx, 'error> =
    { Stamp: 'ctx -> int64
      NoProgress: string -> 'error }

type FlowBuilder<'ctx, 'error>(progress: ProgressGuard<'ctx, 'error> option) =
    ...
```

```
let session =
    FlowBuilder(
        Some {
            Stamp = fun ctx -> ctx.ProgressStamp
            NoProgress = SessionError.NoProgress
        }
    )

let journal = FlowBuilder<JournalContext, JournalError>(None)
let process = FlowBuilder<ProcessContext, ProcessError>(None)
```

- Session/Review 等循环 DSL：`Some` guard。
- Journal/Process：`None`，无伪造 Stamp。

### 1.2 Builder 成员 [NORMATIVE]

```
type FlowBuilder<'ctx, 'error>(progress: ProgressGuard<'ctx, 'error> option) =

    member _.Return(value: 'a) : Flow<'ctx, 'error, 'a> =
        Flow(fun _ _ -> Task.FromResult(Ok value))

    member _.ReturnFrom(flow: Flow<'ctx, 'error, 'a>) = flow

    member _.Bind
        (Flow action: Flow<'ctx, 'error, 'a>,
         next: 'a -> Flow<'ctx, 'error, 'b>)
        : Flow<'ctx, 'error, 'b> =
        Flow(fun ctx ct ->
            task {
                let! result = action ctx ct
                match result with
                | Error e -> return Error e
                | Ok value ->
                    let (Flow cont) = next value
                    return! cont ctx ct
            })

    member _.Zero() : Flow<'ctx, 'error, unit> =
        Flow(fun _ _ -> Task.FromResult(Ok()))

    member _.Delay(create: unit -> Flow<'ctx, 'error, 'a>) : Flow<'ctx, 'error, 'a> =
        Flow(fun ctx ct ->
            let (Flow f) = create ()
            f ctx ct)

    member this.Combine(first: Flow<'ctx, 'error, unit>, second: Flow<'ctx, 'error, 'a>) =
        this.Bind(first, fun () -> second)

    /// 同步 cleanup；异步释放只走 use! / Using
    member _.TryFinally
        (body: Flow<'ctx, 'error, 'a>,
         compensation: unit -> unit)
        : Flow<'ctx, 'error, 'a>

    /// 不吞 OperationCanceledException
    member _.TryWith
        (body: Flow<'ctx, 'error, 'a>,
         handler: exn -> Flow<'ctx, 'error, 'a>)
        : Flow<'ctx, 'error, 'a>

    /// 异步资源：成功 / Error / throw / cancel 均 await DisposeAsync（有界）
    member _.Using
        (resource: 'resource,
         body: 'resource -> Flow<'ctx, 'error, 'a>)
        : Flow<'ctx, 'error, 'a>
        when 'resource :> IAsyncDisposable

    member _.While
        (condition: unit -> bool,
         body: unit -> Flow<'ctx, 'error, unit>)
        : Flow<'ctx, 'error, unit>

    /// 循环体 = unit 效应。不支持 for 体内非局部 return 跳出多层循环。
    /// 需「找到即停 / 有界重试」→ 尾递归函数（见 KISS-08/09），不扩建 CE。
    member _.For
        (items: seq<'t>,
         body: 't -> Flow<'ctx, 'error, unit>)
        : Flow<'ctx, 'error, unit>
```

同步 `IDisposable` 可适配为 `IAsyncDisposable`。资源路径：Kill → 有界等退出 → Drain → `DisposeAsync` → **然后** Flow 返回。禁止同步 Dispose 内 fire-and-forget。

`Using` 必须让 body 的成功、Flow Error、同步异常和取消都进入同一释放路径。释放异常不能悄悄覆盖主失败；至少保留主失败与 cleanup 诊断。

`Using` 的实现必须在资源已经获得后包住整个 body，并保证 body 的返回值、Flow Error、同步异常和取消都经过同一释放路径。释放异常不能悄悄覆盖原始业务失败；实现至少要保留主失败与 cleanup 诊断。

### 1.3 While + 进展 [NORMATIVE]

```
member _.While(condition, body) =
    Flow(fun ctx ct ->
        task {
            let mutable result = Ok()
            while condition () && Result.isOk result do
                ct.ThrowIfCancellationRequested()
                match progress with
                | None ->
                    let (Flow runBody) = body ()
                    let! current = runBody ctx ct
                    result <- current
                | Some guard ->
                    let before = guard.Stamp ctx
                    let (Flow runBody) = body ()
                    let! current = runBody ctx ct
                    match current with
                    | Error e -> result <- Error e
                    | Ok() when guard.Stamp ctx = before ->
                        result <- Error(guard.NoProgress "Loop body completed without progress")
                    | Ok() -> ()
            return result
        })
```

**ProgressStamp** 仅权威提交前进（KISS-02）。禁止手增。

condition 每次迭代重新读取；body 通过 Delay 延迟构造，不能缓存旧 View。以下错误实现必须被测试杀死：

```
while view.Unfinished do return Ok()          // body 无变化
while view.Unfinished do do! noOp            // 永远读取同一快照
只刷新未变化投影                              // 不算 progress
Stamp <- Stamp + 1                            // 禁止伪造
```

`While` 的 condition 在每次迭代重新读取，不缓存进入循环时的布尔值。body 用 `Delay` 延迟构造，保证循环内拿到最新 Script/View。`ProgressGuard=None` 适用于不具有领域进展定义的技术流程；它不允许技术流程伪造业务 Stamp。

进展测试要杀死以下错误实现：

```
while view.Unfinished do return Ok()          // body 无变化
while view.Unfinished do do! noOp            // 永远读同一快照
body 完成后只刷新未变化投影                  // 不应算 progress
body 直接 Stamp <- Stamp + 1                 // 不应允许
```

### 1.4 运行与失败 [NORMATIVE]

```
module Flow =
    /// 通用：不捕获 / 不转换 OperationCanceledException。
    /// OCE 向上抛；由领域 run 或 Host 映射。
    val run:
        ctx: 'ctx ->
        ct: CancellationToken ->
        Flow<'ctx, 'error, 'a> ->
        Task<Result<'a, 'error>>

    val fail: 'error -> Flow<'ctx, 'error, 'a>

    /// 将终止错误转为成功通道 Result；外层 Flow 不主动 Error。
    /// 无 'never 类型——F# 无内置 never，也不为 KISS 造一个。
    val attempt:
        Flow<'ctx, 'error, 'a> ->
        Flow<'ctx, 'error, Result<'a, 'error>>
```

领域包装（示例）：

```
module SessionFlow =
    val run: SessionContext -> CancellationToken -> SessionFlow<'a> -> Task<Result<'a, SessionError>>
    // 捕获 OCE → Error SessionCancelled
    // 其它异常 → 不伪装成普通业务成功；Host 诊断 + Fail Closed

module ProcessFlow =
    val run: ... // OCE → ProcessCancelled
```

主业务少用 `attempt`。可预见分支 → 成功值 DU；不可恢复 → `'error`。

`attempt` 是边界工具，不是把整个系统重新包成 Result 状态机。使用后应立即重新分类，不能让 `Result<Result<...>>` 沿主流程传播。

`attempt` 是边界工具，不是把整个 Flow 重新变成 `Result` 状态机。适用场景是：一个领域组合子需要把某个终止错误转成“可以选择下一步”的值；使用后必须立即重新分类，不能在外层到处传播 `Result<Result<…>>`。

### 1.5 Cancellation

- Token 取消以 OCE 展开处，在**领域 run** 映射为领域取消错误。
- 通用 `Flow.run` 不吞 OCE。
- `TryWith` 不捕获 OCE。

### 1.6 关于 for 与 early exit [NORMATIVE]

[FORBIDDEN] 依赖 monadic `for` 体内 `return x` 跳出外层循环或改变 `For` 的 `unit` 体类型：

```
for model in models do
    match! trySend model with
    | Completed o -> return o   // 不会停止后续 model；且类型常对不上
```

[NORMATIVE] 使用尾递归 / `let rec` 表达「尝试直至成功或耗尽」：

```
let rec tryModels s models = session { ... return! tryModels s remaining }
```

见 KISS-08、KISS-09。仍是结构化程序，不引入控制信号 AST。

`For` 适合逐项 unit 效应，例如按 wave 接受任务；不适合“找到值就退出”。搜索、Fallback 和有界 Invalid 必须用尾递归，把唯一停止路径写进模式匹配。

`For` 仍适合纯遍历和逐项 unit 效应：例如按 wave 接受任务、按输入顺序提交无返回值动作。它不适合“找到一个值就退出”的搜索。后者使用显式递归，把停止条件放在函数签名和模式匹配里，读者可以沿唯一返回路径证明不会继续执行。

---

## 二、领域别名 [NORMATIVE]

```
type SessionFlow<'a> = Flow<SessionContext, SessionError, 'a>
let session = FlowBuilder(Some sessionProgress)

type ChildFlow<'a> = Flow<ChildContext, ChildError, 'a>
let child = FlowBuilder(Some childProgress) // 或 None

type ProcessFlow<'a> = Flow<ProcessContext, ProcessError, 'a>
let process = FlowBuilder<ProcessContext, ProcessError>(None)

type JournalFlow<'a> = Flow<JournalContext, JournalError, 'a>
let journal = FlowBuilder<JournalContext, JournalError>(None)

type SquadFlow<'a> = Flow<SquadContext, SquadError, 'a>
let squad = FlowBuilder(Some squadProgress)
```

单内核。`Flow.fs` 约 200 行 = 健康观察，禁止为压行拆空壳。

---

## 三、组合子（非内核）

### 3.1 删除通用 callOnce [NORMATIVE]

[FORBIDDEN] 公共 `callOnce stepId action`。

各领域自建：`Prompt.sendOnce`、`Review.requestOnce`、`Child.runOnce`、`Merge.fastForwardOnce`。三处真实重复后再抽私有 helper。

### 3.2 retry

```
type RetryPolicy<'error> =
    { MaxAttempts: int
      Backoff: TimeSpan list
      RetryWhen: 'error -> bool }

val retry: RetryPolicy<'e> -> Flow<'c, 'e, 'a> -> Flow<'c, 'e, 'a>
```

幂等在领域动词；retry 只处理尝试中的瞬时失败。

### 3.3 mapBounded — Task 层，非共享 Flow Context [NORMATIVE]

[FORBIDDEN]

```
('t -> Flow<'c, 'e, 'u>) -> Flow<'c, 'e, 'u list>
// 并行任务共享同一可变 'c；CancelOnFirstError=false 时错误无处放
```

[NORMATIVE] 公共 helper 工作在 `Task` 层：

```
val mapBounded:
    maxConcurrency: int ->
    cancellation: CancellationToken ->
    action: ('t -> CancellationToken -> Task<'u>) ->
    items: 't list ->
    Task<'u list>
```

原则：

- 始终保持**输入顺序**；不提供 PreserveOrder 开关。
- 全部 await；无 fire-and-forget。
- Semaphore 在 finally 释放。
- 父 CT 传播；每 action 自行创建**独立** Child/Task 上下文。
- 任务成功/失败用领域值 DU 装进 `'u`，不靠共享 Flow Error 收集。
- fail-fast：外层 linked CTS 的独立薄包装（可选），不塞进默认 mapBounded 复杂开关。

领域包装 [ILLUSTRATIVE]：

```
member c.RunParallel(requests) =
    child {
        let! results =
            mapBounded
                c.Config.MaxConcurrency
                ct
                (fun request linkedCt ->
                    c.RunIndependent(request, linkedCt))
                requests
            |> child.ofTask
        return results
    }
```

`RunParallel` = 领域名 + mapBounded，不进 While/For 内核。

---

## 四、黄金流程 [NORMATIVE 签名]

真实测试项目编译，禁止字符串「编译测试」。

### 4.1 Session

```
let finishTodo (s: SessionScript) : SessionFlow<unit> =
    session {
        while s.Todo.Unfinished do
            do! s.ContinueWork()
    }

let passReview (s: SessionScript) : SessionFlow<unit> =
    session {
        do! finishTodo s
        while s.Review.Required do
            do! s.RequestReview()
            do! finishTodo s
    }

let run (s: SessionScript) : SessionFlow<SessionOutcome> =
    session {
        do! passReview s
        return! s.Finish()
    }
```

Driver 启动时机见 KISS-Driver（初始 Host turn 终态后激活，非空 Session 创建即跑）。

### 4.2 Child

```
let runChild (c: ChildScript) (request: ChildRequest) : ChildFlow<ChildResult> =
    child {
        let! session = c.GetOrCreateSession(request)
        return! session.Run(request.Prompt)
    }
```

### 4.3 Process

```
let execute (p: ProcessScript) (command: Command) : ProcessFlow<ProcessResult> =
    process {
        use! child = p.Spawn(command)  // Spawn 返回前已装 pump
        return! child.RunToCompletion()
    }
```

### 4.4 Journal

```
// 启动：枚举他日志固定前缀 → 归并 BootSnapshot；CreateNew 本 Runtime 文件（KISS-04）
// 运行：本文件唯一 Writer 队列 → Serialize/Write/Flush；先盘后内存
let append (j: JournalScript) (fact: Fact) : JournalFlow<Envelope> =
    journal {
        return! j.AppendAndFlush(fact)
    }
```

### 4.5 Squad

```
let prepareTask (z: SquadScript) (task: SquadTask) : SquadFlow<VerifiedResult> =
    squad {
        use! worktree = z.CreateWorktree(task)
        use! slave = z.StartSlave(worktree, task)
        let! result = slave.Work()
        do! z.Verify(result)
        return! z.PublishVerified(worktree, result)
    }

let runSquad (z: SquadScript) (plan: SquadPlan) : SquadFlow<SquadOutcome> =
    squad {
        for wave in plan.Waves do
            let! verified = z.RunParallel(wave.Tasks, prepareTask)
            for item in z.MergeOrder(verified) do
                do! z.FastForward(item)
            do! z.AcceptWave(verified)
        return! z.Complete()
    }
```

`for wave` / `for item` 体为 `unit` 效应，无 early return 问题。

---

## 五、实现顺序

|PR|内容|
|---|---|
|1|Flow + ProgressGuard + While/For/Using/TryFinally(sync) + run/fail/attempt|
|2|领域别名 + SessionFlow.run 等取消映射|
|3|retry；Task 层 mapBounded|
|4|GoldenFlows + 进展/取消/释放测试|

每个 PR 都要有反例测试：Bind 短路、Delay 最新值、TryWith 不吞 OCE、Using 四路径释放、NoProgress、mapBounded 全 await、semaphore finally 释放，以及黄金源码与文档签名一致。

每个 PR 都要有可编译的最小证明：

|PR|必须杀死的错误|
|---|---|
|1|Bind 不短路、While 不延迟、TryWith 吞 OCE、Using 不释放|
|2|领域取消被伪装成成功、ProgressGuard 类型泄漏|
|3|mapBounded 共享可变 Context、任务未 await、信号量未释放|
|4|黄金源码与文档漂移、NoProgress 未触发、嵌套 Flow 丢上下文|

### 出口

- 黄金源码真编译
- 进展 / NoProgress / 嵌套 while / cancel / Using 四路径 dispose
- 无 AST / 解释器 / Registry / 序列化 continuation / 公共 callOnce
- 无依赖 for 非局部 return 的「规范」示例

### 明确不做

|不做|理由|
|---|---|
|Flow AST/解释器|闭包直接跑|
|Flow 序列化|重启重放幂等动词|
|CE 非局部 break/return|尾递归即可|
|异步 finally 语法糖|仅 use!/Using|
|共享 Context 的 Flow 并行|mapBounded 在 Task 层|
|通用 callOnce|领域 durable effect|

---

*KISS-01 终。*
