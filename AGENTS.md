---
import:
  - src/
---

$ pnpm build && pnpm test

# 异步并发宪法：全面拥抱 Task

> **铁律：本代码库中绝对禁止出现 `Async` 和 `JS.Promise`。**
> Fable 5 已经完美支持 F# 的 `Task<'T>`，并在底层 1:1 编译为原生 JavaScript `Promise`。
> 为了彻底消除层层装箱/拆箱的样板代码，我们确立 `Task<'T>` 为全库唯一的异步货币。

## 1. 禁用词清单 (Kill List)

在任何新代码、重构代码中，**看到以下关键字，即视为 Bug，必须立刻重写**：

🚫 **禁止使用：**
- `async { ... }` （替换为 `task { ... }`）
- `JS.Promise<'T>` （替换为 `Task<'T>`）
- `Async<'T>` （替换为 `Task<'T>`）
- `Async.AwaitPromise` / `Async.StartAsPromise` / `Async.StartImmediate` （直接删除，不再需要）
- `Async.Parallel` （替换为 `Task.WhenAll`）
- `Async.Sleep` （替换为 `Task.Delay`）
- `Async.Catch` （替换为 `task { try ... with ex -> ... }`）
- `MailboxProcessor` / `Agent` （见第 3 节，替换为 Task 队列）

✅ **唯一合法用法：**
```fsharp
open System.Threading.Tasks

let doSomethingAsync () : Task<string> =
    task {
        do! Task.Delay(100)
        return "success"
    }
```

## 2. 边界交互法则 (FFI)

当调用外部 Node.js 库或宿主平台（Mux / Opencode）的异步 API 时，它们在 JS 中返回的是 `Promise`。
在 F# 中，**不需要**做任何转换，直接用 `task { }` 加上 `unbox<Task<'T>>` 即可“白嫖”原生 Promise：

```fsharp
// 错误（旧时代遗毒）：
let readFile path = fsPromises?readFile(path) |> asPromise<string> |> Async.AwaitPromise

// 正确：
let readFile (path: string) : Task<string> =
    task {
        // 直接 let! 解析原生 JS Promise
        let! content = unbox<Task<string>> (fsPromises?readFile(path, "utf-8"))
        return content
    }
```

任何需要向外暴露给宿主 JS 调用的 API（例如 Plugin 的 Hook），其签名直接写成返回 `Task<unit>` 或 `Task<obj>`，Fable 会自动将其编译为返回 `Promise` 的 JS 函数。

## 3. Actor 模式的 Node.js 适配 (替代 MailboxProcessor)

由于 `MailboxProcessor` 强制依赖 F# `Async`，我们将其彻底从代码库开除。
在 JS 单线程环境下，所谓的 Actor 串行化，本质上就是一个 **Promise Chain（Task 队列）**。

当你需要保护可变状态或实现端口锁（WikiPortLock）、执行器（ExecutorActor）的串行排队时，请使用以下极简模式：

```fsharp
open System.Threading.Tasks

/// 单线程下的无锁异步串行队列
type SerialQueue() =
    let mutable tail : Task<unit> = Task.FromResult()

    /// 将任务排入队列，并返回该任务的 Task 结果
    member _.Enqueue(work: unit -> Task<'T>) : Task<'T> =
        let completionSource = TaskCompletionSource<'T>()
        
        let runNext () = task {
            try
                let! result = work ()
                completionSource.SetResult(result)
            with ex ->
                completionSource.SetException(ex)
        }

        // 把 runNext 挂在队尾
        tail <- task {
            try do! tail with _ -> () // 吞掉前面任务的异常，防止队列卡死
            do! runNext ()
        }

        completionSource.Task
```

**用法示例（代替旧版的 ExecutorActor/WikiActor）：**

```fsharp
let private queue = SerialQueue()

let postWork (sessionID: string) : Task<string> =
    queue.Enqueue(fun () -> task {
        do! Task.Delay(50)
        return $"Done {sessionID}"
    })
```

## 4. 并发与异常处理模式

### 4.1 并发执行 (原 Async.Parallel)
```fsharp
let runParallel (items: string list) : Task<string array> =
    task {
        let tasks = items |> List.map (fun item -> task { return item.ToUpper() })
        let! results = Task.WhenAll(tasks)
        return results
    }
```

### 4.2 捕获异常 (原 Async.Catch)
永远使用带有领域语义的 `Result` (如 `DomainError`) 代替裸抛异常，在 `task` 内部就地捕获：

```fsharp
let safeCall () : Task<Result<string, DomainError>> =
    task {
        try
            let! res = riskyCall()
            return Ok res
        with ex ->
            return Error (UnknownJsError ex.Message)
    }
```

### 4.3 竞速超时 (原 Promise.race)
利用 `Task.WhenAny` 实现 `race`：

```fsharp
let withTimeout (timeoutMs: int) (work: Task<'T>) : Task<'T option> =
    task {
        let timeoutTask = task { do! Task.Delay(timeoutMs) }
        let! winner = Task.WhenAny(work, timeoutTask)
        
        if winner.Id = work.Id then
            let! res = work
            return Some res
        else
            return None // 超时
    }
```

遵守以上法则，我们将获得一个没有包装开销、堆栈跟踪清晰、与 JS 宿主生态 100% 融合的纯净内核。
