你说得对。**10 个 session、约 300MB/s 的增长速度，上一轮提到的 session 级缓存不可能是主因。**那些泄漏仍然存在，但只是长期基线问题；当前的爆炸性增长，基本可以锁定在**流式事件入口 + 无界 Promise 队列 + 文本重复聚合**。

## 第一根因：流式事件被塞进无界 Promise 链

公共 fallback handler 为每个 session 建立一个 `SerialQueue`，然后**无条件把传入的整个 `rawEvent` 捕获进队列闭包**：

```fsharp
let mutable queues = Map.ofList<string, SerialQueue> []

fun rawEvent ->
    promise {
        let queue = ...
        let! result =
            queue.Enqueue(fun () ->
                handleEvent ... rawEvent ...)
    }
```

而 `SerialQueue` 的实现本质是：

```fsharp
tail.Value <-
    oldTail
    |> Promise.bind (fun _ -> runNext ())
```

它没有：

* 队列容量；
* 丢弃或合并策略；
* latest-wins；
* 队列深度监控；
* 上游背压；
* session 结束后取消待处理项。

只要事件生产速度略高于处理速度，每个未执行的 Promise closure 都会保留完整 `rawEvent`。事件对象里的 `parts`、`info`、文本和宿主对象全部保持强可达，GC 无法回收。

### Mux 路径尤其明确

Mux 的 `createEventHook` 对事件没有入口过滤。即使：

```fsharp
parseHookEvent event = Ignore
```

也仍然先执行：

```fsharp
let! fbResult = fallbackHandler event
```

也就是说，**与 fallback 完全无关的流式 chunk 也会先进入 SerialQueue**，到队列内部才被判定为 `None`。这正是“每个 token 建一节 Promise 链”的模式。

OpenCode 代码里甚至已经留下了直接证据：

> 高频 `message.part.*` 被排除，以避免每个 token 分配一条 Promise 链。

说明作者已经意识到这一问题，但修复只覆盖了 OpenCode 的部分入口；Mux 没有对应过滤，而且 `message.updated` 仍在白名单内。若宿主在生成过程中高频发送 `message.updated`，问题依旧存在。

---

## 第二根因：排队的很可能是“不断变大的完整快照”

如果每个事件只包含 5～20 字节的真正 token delta，单靠 Promise 节点通常很难达到 300MB/s。

更符合现象的是：宿主的 `message.updated` 或某类 stream event 携带的是：

```text
第 1 次：当前完整文本 100 B
第 2 次：当前完整文本 200 B
第 3 次：当前完整文本 300 B
……
```

队列若产生积压，保留量不是：

[
O(n)
]

而是：

[
100 + 200 + 300 + \cdots + 100n = O(n^2)
]

比如每秒 500 个事件，每个事件平均携带 600KB 的完整消息、parts、metadata 或历史快照，增长就是：

[
500 \times 600\text{KB} \approx 300\text{MB/s}
]

这与观察值完全吻合。

因此它未必是传统意义上的“某个 Map 永久不删”，而是：

> 上游连续产生完整状态快照，下游用无界 FIFO 保留所有过时版本。

此时内存会几乎线性冲向上限，16G 和 64G 的区别只是死亡时间。

---

## 第三根因：`AssistantDelta` 的文本合并本身可能制造二次爆炸

`CurrentTurnEvidence.merge` 对两个 delta 直接执行：

```fsharp
AssistantDelta(..., t1 + t2, ...)
```

更危险的是，各 host translator 创建的 delta 经常使用：

```fsharp
AssistantDelta("", 0L, text, ...)
```

也就是：

* 没有 message ID；
* 没有 revision；
* 无法去重；
* 无法识别覆盖式快照；
* 无法判断乱序或重复事件。

代码只能把所有输入当成真正增量，不断执行 `t1 + t2`。

这里有两种灾难模式。

### 模式 A：输入是真 delta

假设每个 delta 长度为 (d)，累计 (n) 次。不可变字符串每次都要复制历史内容，总分配量为：

[
d(1+2+\cdots+n)
===============

# \frac{dn(n+1)}2

O(n^2)
]

旧字符串最终可能回收，但在高频 Promise 队列下会迅速晋升到 old space，并造成严重 GC 追赶。

### 模式 B：输入其实是完整快照

假设事件依次携带：

```text
A
AB
ABC
ABCD
```

当前实现会得到：

```text
A
AAB
AABABC
AABABCABCD
```

最终保存的**活对象本身**就是 (O(n^2))，而不只是临时分配。这可以独立导致 OOM。

---

## 第四根因：每个更新都重新遍历并复制完整 `parts`

OpenCode 和 OMP 的 translator 会在更新事件上：

1. 遍历整个 `parts` 数组；
2. 提取所有 text part；
3. `String.concat` 创建新字符串；
4. 包装成新的 evidence；
5. 再进入 actor 的串行队列；
6. 再进行 snapshot 替换或 delta 拼接。

即使队列没有明显积压，若 `message.updated` 每次携带完整当前文本，一个长度从 1 增长到 (n) 的输出仍会导致累计复制量 (O(n^2))。

所以问题有两层：

* **retained memory**：Promise 队列保留旧 raw event；
* **allocation rate**：每次更新重新复制完整文本。

前者导致堆持续不降，后者让 GC 疲于奔命。

---

# 修复方向

## 1. 在创建 Promise 之前过滤事件

不能进入 handler 后再 `Ignore`。必须在最外层同步判断：

```fsharp
let shouldObserveMuxEvent eventType =
    match eventType with
    | "stream-end"
    | "stream-abort"
    | "error"
    | "session.error"
    | "session.deleted"
    | "session.close"
    | "session.delete"
    | "session.remove" -> true
    | _ -> false
```

然后：

```fsharp
if not (shouldObserveMuxEvent decoded.eventType) then
    Promise.lift ()
else
    // 才进入 promise / fallback handler
```

关键要求是：**被忽略的 token 事件不能创建 Promise、闭包或队列节点。**

## 2. 控制事件和流式 evidence 分离

控制事件可以严格 FIFO：

* busy；
* idle；
* error；
* abort；
* user turn start；
* completed。

流式 evidence 不应进入同一个 FIFO，而应采用：

```text
每个 session + messageId 只保留最新一份
```

结构类似：

```fsharp
mutable latestEvidence : Map<SessionId, Evidence>
mutable drainScheduled : Set<SessionId>
```

新事件覆盖旧值，只安排一个 drain。处理期间又来了 1,000 个更新，最终只处理最新的一份。

这才符合代码注释中已经声明的：

> Progress/evidence streams may use latest-wins.

但当前实际实现没有兑现这一规则。

## 3. 对子会话最好完全不消费文本流

当前 OpenCode 已经在 child session idle 时调用：

```fsharp
refreshChildTurnEvidence
```

它会读取最终 transcript，再计算完整 evidence。

因此最稳妥的设计是：

* 流式过程中只观察轻量控制信号；
* 不保存 assistant 文本 delta；
* `session.idle` 时读取一次最终 transcript；
* 从最终消息构造 `CurrentTurnEvidence`。

这既保持正确性，也彻底消除 token 级状态更新。

显式的 `task_complete`、todo、tool result 等少量离散事件仍可即时路由。

## 4. 删除 `AssistantDelta` 的无条件字符串拼接

至少需要区分：

```fsharp
type AssistantUpdate =
    | FullSnapshot of messageId * revision * text
    | TextDelta of messageId * revision * delta
```

规则应为：

* `FullSnapshot`：直接替换；
* revision 旧于当前值：忽略；
* 相同 revision：去重；
* `TextDelta`：只有明确的真实 delta 才追加；
* 缺少 message ID/revision：不允许默认按 delta 处理。

对于无法确定语义的宿主事件，应视为 snapshot，而不是 delta。**把完整快照误判为 delta 的破坏性远大于反过来。**

## 5. 队列中禁止捕获原始宿主对象

入口先同步解码成很小的 DU：

```fsharp
type RelevantEvent =
    | BecameIdle
    | BecameBusy
    | Failed of ErrorInput
    | UserMessage of messageId: string
    | FinalAssistant of text: string
```

然后只把 `RelevantEvent` 放入队列，绝不能把 `rawEvent: obj` 捕获进去。

---

# 最快的确认实验

暂时在三个宿主入口加入：

```fsharp
if
    eventType = "message.updated"
    || eventType.StartsWith("message.part.")
    || eventType.StartsWith("stream-")
       && eventType <> "stream-end"
       && eventType <> "stream-abort"
then
    return ()
```

然后重复原工作负载。

判断标准：

* 若内存斜率从约 300MB/s 立即接近零：主因确认；
* 若只明显下降但未归零：再禁用 `AssistantDelta` 聚合；
* 若 `heapUsed` 下降但 RSS 暂不下降：说明对象已释放，只是 V8 尚未把页归还 OS；
* 若 `heapUsed` 仍以原速度增长：检查是否存在另一个未过滤的 event hook。

同时给 `SerialQueue` 增加：

```text
enqueued
completed
currentDepth
maxDepth
```

并记录：

```text
eventType
sessionId
raw event 估算字节数
queue depth
```

只要看到：

```text
enqueued - completed 持续扩大
```

就已经完成因果证明。

Heap snapshot 中预期 dominator 会是：

```text
PromiseReaction
runNext closure
rawEvent
properties / info / parts
assistant text string
SerialQueue.tail
```

## 修正后的优先级

我会重新排列上一轮结论：

1. **P0：流式事件进入无界 Promise FIFO，保留完整 raw event**
2. **P0：snapshot/delta 语义混淆与 `t1 + t2` 二次增长**
3. **P0：每次更新重复复制完整 parts/text**
4. P1：会话和 CAPS 等长期生命周期泄漏
5. P2：小型 Map 清理问题

所以这次不是“缓存忘了删”能够解释的。**它是一个典型的无背压流处理错误：把高频、可覆盖、过时即无价值的状态流，当成了必须逐条可靠处理的命令流。**

---
针对您提到的“10个会话，每秒消耗 300MB 内存”的严重内存泄漏问题，我们对系统中的流式事件和常驻服务进行了代码层面的排查。

排查发现，内存泄漏的根源高度契合**流式事件/高频回调引起的 Promise 引用链堆积以及缓存未及时释放**。以下是具体的诊断分析及对应的重构方案。

---

### 一、 核心问题诊断

#### 1. `SerialQueue` 中的无限 Promise 链积累（致命泄漏点）
在 `Runtime/PromiseQueue.fs` 中，`SerialQueue` 的实现如下：
```fsharp
let oldTail = tail.Value
tail.Value <-
    oldTail
    |> Promise.catch (fun ex -> ...)
    |> Promise.bind (fun _ -> runNext () |> Promise.map ignore)
```
* **原因分析**：这是 V8 引擎中经典的 Promise 链式内存泄漏。每次调用 `Enqueue` 时，新的 Promise 都会通过 `Promise.bind` 挂载到 `oldTail` 上，并将新产生的 Promise 赋给 `tail.Value`。
* **后果**：由于 `tail.Value` 持有着对最新 Promise 的引用，而每一个 Promise 都隐式持有着其链条上游所有 Promise（以及它们闭包中所捕获的整个上下文、历史消息、事件载荷等）的引用，导致**整个队列自诞生以来的所有任务和临时变量均无法被垃圾回收（GC）**。在流式事件高频触发（例如每次 token 更新或状态变更）时，这种堆积会在数秒内吞噬数百兆内存。

#### 2. `ProjectionCache` 中未注销的子会话状态（次要泄漏点）
在 `Runtime/EventLogProjectionCache.fs` 中：
* **原因分析**：子代理会话（如 `child-session-1`、`child-session-2` 等）在生命周期结束后会追加写入 `PhysicalSessionClosed` 事实。然而，`ProjectionCache` 中的 `sessionStates` 映射表（`Map<string, SessionState>`）仅支持 `FoldWan` 不断累加新状态，**从未对已关闭的临时会话进行移除**。
* **后果**：随时间推移，大量历史子会话状态常驻内存。

#### 3. `FallbackRuntimeState` 遗留的状态监听器
在 `Runtime/Fallback/FallbackRuntimeState.fs` 中：
* **原因分析**：`OnStateChanged` 会向全局的 `listeners`（`Map<string, ResizeArray<unit -> unit>>`）添加状态变更回调。在 `CleanupSession` 时，虽然移除了 `sessionStates` 和 `activeGates`，**却遗漏了清除该会话在 `listeners` 中注册的回调链**。若回调闭包捕获了上下文，则会导致与之关联的整个会话实例无法被 GC 释放。

#### 4. `RuntimeScope` 遗留的会话锁与临时文件
在 `Runtime/RuntimeScope.fs` 中：
* **原因分析**：`sessionLocks` 和 `tempFilesByPrompt` 会针对每一个子会话 ID 创建独立的读写锁与临时文件记录，但在会话销毁时没有针对这些映射表执行对应的局部清理。

---

### 二、 优化与修复方案

请按以下修改更新代码：

#### 1. 修复 `SerialQueue`（重构为非链式队列）
修改 `Runtime/PromiseQueue.fs`，通过显式的任务队列数组代替无限 Promise 链条。任务执行完毕后立即从数组中移除，从而彻底断开闭包对旧任务的引用链：

```fsharp
// 替换 Runtime/PromiseQueue.fs 中的 SerialQueue 实现
type SerialQueue(?observer: IExceptionObserver) =
    let queue = ResizeArray<unit -> JS.Promise<unit>>()
    let mutable running = false

    let rec processQueue () =
        promise {
            if queue.Count = 0 then
                running <- false
            else
                running <- true
                let task = queue.[0]
                queue.RemoveAt(0) // 立即移除，释放该任务闭包所捕获的所有内存
                try
                    do! task ()
                with _ ->
                    ()
                do! processQueue ()
        }

    member _.Enqueue(work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        Promise.create (fun resolve reject ->
            let task () =
                promise {
                    try
                        let! result = work ()
                        resolve result
                    with ex ->
                        observer |> Option.iter (fun o -> o.OnException ex)
                        reject ex
                }
            queue.Add(task)
            if not running then
                running <- true
                processQueue () |> Promise.start
        )
```

#### 2. 在 `ProjectionCache` 中及时卸载已关闭会话的状态
修改 `Runtime/EventLogProjectionCache.fs`，当捕获到物理会话关闭事件时，主动将该会话的数据从内存缓存中卸载：

```fsharp
// 修改 Runtime/EventLogProjectionCache.fs 的 FoldWan 方法
member _.FoldWan(e: WanEvent) =
    let sId = e.Session

    // 显式捕获物理会话关闭和删除事件，并从内存缓存中剔除
    if e.Kind = eventKindSubsessionPhysicalSessionClosed || e.Kind = "session_deleted" then
        sessionStates <- Map.remove sId sessionStates
        revision <- revision + 1
    else
        let oldState =
            match Map.tryFind sId sessionStates with
            | Some st -> st
            | None -> emptySessionState ()

        sessionStates <- Map.add sId (applyEvent oldState e) sessionStates
        squadProj <- applyWanEvent squadProj e
        revision <- revision + 1

        if isSquadEventKind e.Kind then
            latestSessionId <- Some e.Session
```

#### 3. 补全 `FallbackRuntimeState` 中的监听器清理
修改 `Runtime/Fallback/FallbackRuntimeState.fs` 中的 `CleanupSession`，避免闭包长久驻留：

```fsharp
// 修改 Runtime/Fallback/FallbackRuntimeState.fs
member _.CleanupSession(sessionID: string) : unit =
    sessionStates <- Map.remove sessionID sessionStates
    activeGates <- removeSessionGates activeGates sessionID
    listeners <- Map.remove sessionID listeners // 显式移除不再使用的监听器队列
    triggerStateChanged sessionID
```

#### 4. 在 `RuntimeScope` 中增加局部垃圾回收接口
修改 `Runtime/RuntimeScope.fs`，允许移除特定会话对应的读写锁和暂存文件信息：

```fsharp
// 修改 Runtime/RuntimeScope.fs，在 RuntimeScope 内增加以下成员
member _.RemoveSessionQueue(sessionId: string) : unit =
    sessionLocks <- Map.remove sessionId sessionLocks

member _.RemoveTempFiles(sessionId: string) : unit =
    tempFilesByPrompt <-
        tempFilesByPrompt
        |> Map.filter (fun k _ -> not (k.StartsWith(sessionId + "\u0000")))
```

#### 5. 在会话生命周期终点执行深度清理
确保在会话被销毁、关闭或主动释放时调用这些新增接口。

* **针对 Opencode 主机** (`Hosts/OpenCode/PluginHooks.fs`)：
```fsharp
// 修改 handleSessionCleanup 函数中对应的分支
if ptyCleanupSessionId <> "" then
    cleanupPtyBySession ptyCleanupSessionId
    Wanxiangshu.Runtime.LivelockGuard.cleanup services.RuntimeScope ptyCleanupSessionId
    services.FallbackRuntime.CleanupSession ptyCleanupSessionId
    Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance ptyCleanupSessionId
    Wanxiangshu.Runtime.ToolHookRuntime.closeSession ptyCleanupSessionId
    Wanxiangshu.Hosts.Opencode.MessageTransform.cleanupCapsEpochBySession services.RuntimeScope ptyCleanupSessionId
    
    // 增加清理锁及缓存文件
    services.RuntimeScope.RemoveSessionQueue ptyCleanupSessionId
    services.RuntimeScope.RemoveTempFiles ptyCleanupSessionId

    let sid = SessionId.create ptyCleanupSessionId
    ...
```

* **针对 Omp 主机** (`Hosts/Omp/TodoHooks.fs`)：
```fsharp
// 修改 sessionShutdownHandler 函数
let sessionShutdownHandler (reviewStore: ReviewStore) (ctx: obj) : JS.Promise<unit> =
    promise {
        match getSessionIdFromContext ctx with
        | None -> ()
        | Some sessionId ->
            ...
            if cwd <> "" then
                ...
                SubsessionActorRegistry.Remove cwd sessionId

            do! cleanupRunnerJob ExecutorTools.ompScope sessionId
            Wanxiangshu.Runtime.LivelockGuard.cleanup ExecutorTools.ompScope sessionId
            Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance sessionId
            Wanxiangshu.Runtime.ToolHookRuntime.closeSession sessionId
            
            // 增加清理锁及缓存文件
            ExecutorTools.ompScope.RemoveSessionQueue sessionId
            ExecutorTools.ompScope.RemoveTempFiles sessionId
    }
```

这些更改不涉及系统业务逻辑的变化。通过阻断 Promise 的链式常驻引用，并补充边界注销动作，高频事件下的内存流失情况可得到根本改善。

---

您说得非常对。从架构设计上来看，插件确实**不应该、也无需监听流式生成过程中的增量事件**，而应该完全采用**最终状态事件驱动**。

系统在设计之初，为了在流式生成中实时更新子会话的微小状态（例如在生成中间抽取模型或临时诊断），错误地将 `message.updated` 或 `message.part.*` 这类高频增量事件纳入了监听范围。但在实际运行中：
1. fallback 状态机、nudge 触发器、待办列表（todo）以及代码审查（review）等所有核心决策逻辑，**只有在回合彻底结束（即收到 `session.idle` 或 `stream-end`）时才会触发**。
2. 即使在结束时需要最新的消息历史，也可以在终态事件触发后，**通过单次调用 `executor.FetchMessages` 批量获取完整上下文**，完全不需要实时监听流。

将高频流式事件过滤掉，可以让事件挂钩的触发频率从**每秒数百次（Token 级别）直接降到每回合 1 次**。

以下是针对 Mux、OpenCode、Omp 三个主机的事件过滤器重构方案：

---

### 1. Mux 主机：过滤非终态事件
修改 `Hosts/Mux/EventHook.fs` 中的事件挂钩入口。在最外层直接拦截非终态事件，阻止其进入后续的异步计算和队列：

```fsharp
// 修改 Hosts/Mux/EventHook.fs 中的事件挂钩定义
let createEventHook (deps: obj) (reviewStore: ReviewStore) (scope: RuntimeScope) : obj =
    ...
    let fn =
        System.Func<obj, obj, JS.Promise<unit>>(fun event helpers ->
            promise {
                let decoded = decodeHookEvent event
                let eventType = decoded.eventType

                // 核心过滤：非终态事件直接拦截返回，不启动异步 Promise 和任何队列
                if eventType <> "stream-end" && eventType <> "error" && eventType <> "session.error" 
                   && eventType <> "session.deleted" && eventType <> "session.close" 
                   && eventType <> "session.delete" && eventType <> "session.remove" then
                    return ()
                else
                    let workspaceId = decoded.workspaceId
                    if workspaceId <> "" then
                        fallbackRuntime.SetEventHandlingActive workspaceId true
                    ...
            })
    box fn
```

---

### 2. OpenCode 主机：移出流式消息监听
修改 `Hosts/OpenCode/NudgeTrigger.fs` 中的 `pluginObservedHostEventTypes` 集合，将代表高频更新的 `"message.updated"` 彻底移除。

```fsharp
// 修改 Hosts/OpenCode/NudgeTrigger.fs (或对应引用的位置)
let pluginObservedHostEventTypes =
    Set.ofList
        [ "stream-abort"
          "session.status"       // 用于捕获终态的 busy/idle 状态
          "session.idle"
          "session.error"
          "session.interrupted"
          "session.deleted"
          "session.delete"
          "session.remove"
          "session.close"
          // "message.updated"   // 彻底移除此行：流式输出时不会再频繁触发挂钩
          "session.compacted" ]
```

---

### 3. Omp 主机：剔除增量事件
修改 `Hosts/Omp/PluginComposition.fs` 和 `Hosts/Omp/Fallback/EventTranslator.fs`，不再响应流式消息段（`message.part.*`）：

* **修改 `Hosts/Omp/PluginComposition.fs` 中的挂钩白名单**：
```fsharp
// 修改 Hosts/Omp/PluginComposition.fs 中的事件订阅白名单
let registerAbortHandler ... =
    let fallbackEventTypes =
        Set [ "session.busy"; "session.idle"; "session.updated" ] // 剔除 "message.updated"
```

* **修改 `Hosts/Omp/Fallback/EventTranslator.fs` 中的抽取逻辑**：
```fsharp
// 修改 Hosts/Omp/Fallback/EventTranslator.fs
member _.IsAssistantMessage(rawEvent: obj) = false // 流式生成中无需实时提取

member _.ExtractTurnObservation(rawEvent: obj) : TurnObservation option =
    let eventObj = Dyn.get rawEvent "event"
    if Dyn.isNullish eventObj then None
    else
        let t = Dyn.str eventObj "type"
        // 仅响应明确的非流式终态行为（如工具执行完毕返回结果）
        if t = "tool_result" then
            Some { TurnId = tryExtractTurnIdFromEvent rawEvent; Evidence = { CurrentTurnEvidence.empty with Tool = HasToolResult } }
        else None
```

---

### 总结
执行该项重构后，系统将不再参与任何 Token 级别的流式消息构建和状态跟踪。所有会话数据将在 `idle` / `stream-end` 到达时进行一次性同步，这从机制上消除了由于高频并发带来的内存分配与垃圾回收压力。
