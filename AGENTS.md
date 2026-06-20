---
import:
  - src/
---

$ pnpm build && pnpm test

# 异步并发宪法：全面拥抱 Fable.Promise

> **铁律：本代码库中绝对禁止出现 `Async` 和 `Task`。**
> 在 Fable 编译到 JS 的环境下，与 Node.js 交互的最优解是 `Fable.Promise`。它在底层 100% 对应原生的 JS `Promise`，没有任何状态机装箱/拆箱的运行时开销和方法缺失问题。
> 我们确立 `JS.Promise<'T>` 为全库唯一的异步货币。

## 1. 禁用词清单 (Kill List)

在任何新代码、重构代码中，**看到以下关键字，即视为 Bug，必须立刻重写**：

🚫 **禁止使用：**
- `async { ... }` （替换为 `promise { ... }`）
- `task { ... }` / `Task<'T>` （彻底禁用，严禁将 JS Promise 强转为 Task）
- `Async<'T>` （替换为 `JS.Promise<'T>`）
- `Async.AwaitPromise` / `Async.StartAsPromise` / `Async.StartImmediate` （直接删除，不再需要）
- `Async.Parallel` （替换为 `Promise.all`）
- `Async.Sleep` （替换为 `Promise.sleep`）
- `MailboxProcessor` / `Agent` （见第 3 节，替换为 Promise 队列）

✅ **唯一合法用法：**
```fsharp
open Fable.Core.JS
// 必须安装并引用 Fable.Promise 库

let doSomethingAsync () : JS.Promise<string> =
    promise {
        do! Promise.sleep 100
        return "success"
    }
```

## 2. 边界交互法则 (FFI)

当调用外部 Node.js 库或宿主平台（Mux / Opencode）的异步 API 时，它们在 JS 中返回的是原生 `Promise`。
在使用 `Fable.Promise` 时，**不需要任何 unbox 或转换**，直接用 `promise { }` 原生接收：

```fsharp
// 正确：原生 JS Promise 完美融入 promise { }
let readFile (path: string) : JS.Promise<string> =
    promise {
        // 直接 let! 解析原生 JS Promise，无需任何强转
        let! content = fsPromises?readFile(path, "utf-8")
        return content
    }
```

向外暴露给宿主 JS 调用的 API（例如 Plugin 的 Hook），签名直接写为 `JS.Promise<unit>` 或 `JS.Promise<obj>`。

## 3. Actor 模式的 Node.js 适配 (替代 MailboxProcessor)

由于 `MailboxProcessor` 强制依赖 F# `Async`，我们将其彻底从代码库开除。
在 JS 单线程环境下，所谓的 Actor 串行化，本质上就是一个 **Promise Chain**。

当你需要保护可变状态或实现执行器串行排队时，请使用以下极简模式：

```fsharp
open Fable.Core.JS

/// 单线程下的无锁异步串行队列
type SerialQueue() =
    let mutable tail : JS.Promise<unit> = Promise.resolve()

    /// 将任务排入队列，并返回该任务的 Promise 结果
    member _.Enqueue(work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        Promise.create (fun resolve reject ->
            let runNext () = 
                promise {
                    try
                        let! result = work ()
                        resolve result
                    with ex ->
                        reject ex
                }

            // 把 runNext 挂在队尾，吞掉前面的异常防止队列断裂
            tail <- tail 
                    |> Promise.catch (fun _ -> ())
                    |> Promise.bind (fun _ -> runNext () |> Promise.map ignore)
        )
```

**用法示例：**
```fsharp
let private queue = SerialQueue()

let postWork (sessionID: string) : JS.Promise<string> =
    queue.Enqueue(fun () -> promise {
        do! Promise.sleep 50
        return $"Done {sessionID}"
    })
```

## 4. 并发与异常处理模式

### 4.1 并发执行 (原 Async.Parallel)
```fsharp
let runParallel (items: string list) : JS.Promise<string array> =
    promise {
        let promises = items |> List.map (fun item -> promise { return item.ToUpper() })
        let! results = Promise.all promises
        return results
    }
```

### 4.2 捕获异常 (原 Async.Catch)
永远使用带有领域语义的 `Result` (如 `DomainError`) 代替裸抛异常，在 `promise` 内部就地捕获：

```fsharp
let safeCall () : JS.Promise<Result<string, DomainError>> =
    promise {
        try
            let! res = riskyCall()
            return Ok res
        with ex ->
            return Error (UnknownJsError ex.Message)
    }
```

### 4.3 竞速超时 (原 JS 补丁的 promiseRace)
直接使用 `Fable.Promise` 提供的机制或原生 `Promise.race` 包装：

```fsharp
let withTimeout (timeoutMs: int) (work: JS.Promise<'T>) : JS.Promise<'T option> =
    let timeoutPromise = promise {
        do! Promise.sleep timeoutMs
        return None
    }
    let workPromise = promise {
        let! res = work
        return Some res
    }
    Promise.race [ timeoutPromise; workPromise ]
```
