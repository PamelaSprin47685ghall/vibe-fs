这是一份彻底消灭 `Async`、`JS.Promise` 和 `MailboxProcessor` 的**自底向上、保姆级重构执行手册**。

由于 F# 的强类型推导特性，如果在中间层乱改，会引发海量编译报错，导致重构心态崩溃。因此，我们必须**严格按照从底层（无依赖）到高层（被依赖）的顺序**推进。

请按照以下 6 个阶段（Phase）逐一执行。每完成一个阶段，确保编译（哪怕是局部）通过或错误范围如预期般上移。

---

### Phase 0：全局清理与基建准备

**目标：建立新的异步基础设施，干掉旧时代的别名。**

1. **新建文件 `src/Shell/TaskQueue.fs`**
   - 把它加到 `vibe-fs.fsproj` 中，紧跟在 `Dyn.fs` 等核心基建之后。
   - 把 `AGENTS.md` 中写好的 `SerialQueue` 类放进去，这就是我们新的“单线程无锁 Actor”。
   - 提供一个通用的 `withTimeout` 辅助函数（基于 `Task.WhenAny` 和 `Task.Delay`），用于后续替换 `Promise.race`。

2. **清理全局别名与辅助函数**
   - 全局搜索并**直接删除**所有 `let private asPromise<'T> (o: obj) = unbox<JS.Promise<'T>> o`。以后不需要这个桥接了。
   - 全局搜索并**直接删除** `let private promiseRace ... = ... Promise.race ...`。我们将用 `Task.WhenAny` 替代。

---

### Phase 1：血洗 FFI 底层与 IO 外壳 (Shell 层)

**目标：把所有跟 Node.js API 交互的底层文件，从 `JS.Promise` / `Async` 改成 `Task`。**
*策略：看到 `JS.Promise<'T>` 就改成 `Task<'T>`，看到 `async { }` 就改成 `task { }`，把中间的转换全删掉。*

1. **`tests/TempWorkspace.fs` & `src/Shell/FileSys.fs` & `src/Shell/WorkspaceFiles.fs`**
   - 签名返回值全改为 `Task<...>`。
   - `fsPromises?xxx` 的返回值，直接在 `task { }` 里 `let! res = unbox<Task<obj>> (fsPromises?xxx)`。
   - 替换 `Async.Parallel`：把列表转成 Array/Seq，然后 `let! results = Task.WhenAll(tasks)`。

2. **`src/Shell/TreeSitterShell.fs` & `src/Shell/ExecutorJavascript.fs` & `src/Shell/WikiFiles.fs`**
   - 删掉所有的 `|> Async.AwaitPromise`。
   - 删掉所有的 `|> Async.StartAsPromise`。
   - 直接用 `task { }` 包裹并返回。

3. **`src/Shell/OllamaClient.fs`**
   - Node 的 `fetch` 返回的是 Promise，签名直接改成 `Task<obj>`。
   - 删掉底部的 `|> Async.StartAsPromise`。

---

### Phase 2：消灭 Continuation 与 MailboxProcessor (并发控制层)

**目标：干掉代码库里最难搞的三座大山（锁、队列、TTL）。这是重构的核心战役。**

1. **`src/Shell/WikiPortLock.fs` (消灭 `Async.FromContinuations`)**
   - 当前这里用了原生 `createServer` 的事件监听和 `Async.FromContinuations`。
   - **重构方法**：使用 `TaskCompletionSource<obj>()`。在 `listening` 事件里调用 `SetResult`，在 `error` 事件里调用 `SetException`，最后返回 `tcs.Task`。
   - 把 `Async.Sleep` 换成 `Task.Delay`。

2. **`src/Shell/CallStore.fs` (消灭 `setTimeout` 竞速)**
   - 目前内部使用了 `JS.setTimeout` 和 `Async.FromContinuations`。
   - **重构方法**：返回值改为 `Task<obj>`。挂起调用的地方使用 `TaskCompletionSource<obj>()`，然后使用 Phase 0 中准备好的 `withTimeout` 包装它。超时直接走 C# 原生的 Task 超时机制。

3. **`src/Shell/ChildAgentRegistry.fs` & `src/Opencode/WikiRuntime.fs` (消灭 `MailboxProcessor`)**
   - 找到 `ExecutorActor` 和 `WikiActor`。
   - 把内部的 `MailboxProcessor` 实例直接替换为 `TaskQueue.SerialQueue()`。
   - 删掉 `PostAndAsyncReply`，直接调用 `queue.Enqueue(fun () -> task { ... })`。
   - 大量样板代码会瞬间消失。

---

### Phase 3：推平中间业务层 (Opencode / Mux)

**目标：底层 IO 全部变成 Task 后，中间的调用者会大面积报错。一路往上把 `async` 改成 `task`。**

1. **`src/Mux/Delegate.fs` & `src/Opencode/SessionIo.fs`**
   - 这两个文件是跑大模型的。返回值从 `JS.Promise<string>` 改为 `Task<string>`。
   - 原来用 `promiseRace` 和 `AbortSignal` 做取消和超时的，全部改成 `Task.WhenAny` 和 `TaskCompletionSource`。
   - `Async.Catch` 替换为 `try ... with ex -> ...`。

2. **`src/Opencode/HookExecute.fs` & `src/Opencode/HookTransform.fs` & `src/Opencode/ReviewerLoop.fs`**
   - 把所有的返回值改为 `Task<unit>` 或 `Task<ReviewResult>`。
   - 内部的 `async` 改为 `task`。
   - 删除结尾的 `|> Async.StartAsPromise`。

3. **`src/Mux/SubagentTools.fs` & `src/Opencode/Tools.fs`**
   - 工具定义的 `execute` 签名，之前是 `fun config args -> JS.Promise<string>`。
   - 签名改为 `fun config args -> Task<string>`。
   - 工具内部的实现全部去掉 `Async.StartAsPromise`。

---

### Phase 4：清理入口与顶层 (Plugin 边界)

**目标：完成对宿主平台暴露接口的改造。因为 Task 编译后就是 Promise，宿主无感。**

1. **`src/Mux/Plugin.fs` & `src/Opencode/PluginCore.fs` 等 Plugin 文件**
   - 暴露出去的 Hook 函数（比如 `event`, `chat.message`, `tool.execute.after`），它们的匿名函数或委托原来返回 `JS.Promise<unit>`。
   - 把它们全部改成返回 `Task<unit>`。
   - 去掉所有 `resolvedUnitPromise()` 这种补丁，直接在需要同步返回的地方写 `Task.FromResult()`，或者干脆用 `task { return () }`。

2. **`index.d.ts`**
   - 不需要改动！因为 TypeScript 眼里，F# 的 `Task` 就是 `Promise`，类型定义完美兼容。

---

### Phase 5：修复测试套件

**目标：测试代码也是异步重灾区，让测试套件全面适配 Task。**

1. **`tests/Integration*.fs` & `tests/WikiFileTests.fs` 等**
   - 把测试的定义从 `let spec () = async { ... }` 批量替换为 `let spec () = task { ... }`。
   - 遇到并发的地方，把 `Async.Parallel` 替换为 `Task.WhenAll`。
   - 遇到等待的地方，把 `Async.Sleep 200` 替换为 `Task.Delay 200`。
   - 删掉所有的 `|> Async.AwaitPromise`。

2. **`tests/Tests.fs` (总入口)**
   - `runAll` 方法改为返回 `Task<int>`。
   - 删掉最后一行 `|> Async.StartAsPromise`。

---

### Phase 6：最终扫尾与宪法审查

在终端执行以下检查（严禁有任何输出）：

```bash
# 检查是否还有旧日残留
grep -r "JS\.Promise" src/ tests/
grep -r "Async\." src/ tests/
grep -r "async {" src/ tests/
grep -r "MailboxProcessor" src/ tests/
grep -r "Promise\.race" src/ tests/
```

如果全空，执行 `pnpm build && pnpm test`。

**说明：**
这个重构虽然涉及文件多，但完全是**机械性、降维打击式**的。没有业务逻辑的改变，全是删减胶水代码。你可以选择一个最底层的模块（比如 `TempWorkspace.fs` 或 `FileSys.fs`），让我先提供那一个文件的改造代码，作为这套操作手册的“第一刀”。
