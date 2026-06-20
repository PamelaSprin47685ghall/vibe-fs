这是一份彻底消灭 `Async`、`Task` 和 `MailboxProcessor`，全面转向 `Fable.Promise` 的**自底向上重构执行手册**。

请严格按照从底层（无依赖）到高层（被依赖）的顺序推进。

---

### Phase 0：安装依赖与基建准备

**目标：引入正确的 Promise 库，建立新的异步基础设施。**

1. **安装并配置 `Fable.Promise`**
   - 运行 `dotnet add package Fable.Promise`
   - 确保 `Fable.Promise` 已经加入到 `vibe-fs.fsproj` 中。

2. **新建文件 `src/Shell/PromiseQueue.fs`**
   - 把它加到 `vibe-fs.fsproj` 中。
   - 把 `AGENTS.md` 中的 `SerialQueue` 和 `withTimeout` 放进去，替换掉旧的全局补丁。

3. **清理全局别名**
   - 搜索并**删除**所有 `asPromise<'T>` 辅助函数。
   - 搜索并**删除**遗留的 `promiseRace` 自定义封装。

---

### Phase 1：血洗 FFI 底层与 IO 外壳 (Shell 层)

**目标：把所有跟 Node.js API 交互的底层文件，统一为 `JS.Promise`。**

1. **`tests/TempWorkspace.fs` & `src/Shell/FileSys.fs` & `src/Shell/WorkspaceFiles.fs`**
   - 签名返回值全改为 `JS.Promise<...>`。
   - 删掉所有的 `|> Async.AwaitPromise`。
   - 内部逻辑用 `promise { }` 包裹。原生 JS 调用（如 `fsPromises?xxx`）直接 `let!`。

2. **`src/Shell/TreeSitterShell.fs` & `src/Shell/ExecutorJavascript.fs` & `src/Shell/WikiFiles.fs`**
   - 彻底干掉 `Async.StartAsPromise`。
   - 将 `Async.Parallel` 替换为 `Promise.all`。

3. **`src/Shell/OllamaClient.fs`**
   - Node 的 `fetch` 原生返回的就是 Promise。
   - 直接把返回类型改为 `JS.Promise<obj>`。

---

### Phase 2：消灭 Continuation 与 MailboxProcessor

**目标：干掉代码库里最难搞的三座大山（锁、队列、超时）。**

1. **`src/Shell/WikiPortLock.fs`**
   - 使用 `Promise.create (fun resolve reject -> ...)` 替代 `Async.FromContinuations`。
   - 在事件监听的回调中直接调用 `resolve()` 或 `reject()`。
   - `Async.Sleep` 换成 `Promise.sleep`。

2. **`src/Shell/CallStore.fs`**
   - 将返回值改为 `JS.Promise<obj>`。
   - 使用 `Promise.create` 挂起调用，并结合 Phase 0 的 `withTimeout` 进行超时控制。

3. **`src/Shell/ChildAgentRegistry.fs` & `src/Opencode/WikiRuntime.fs`**
   - 找到 `ExecutorActor` 和 `WikiActor`。
   - 把内部的 `MailboxProcessor` 实例直接替换为 `PromiseQueue.SerialQueue()`。
   - 将 `PostAndAsyncReply` 替换为 `queue.Enqueue(fun () -> promise { ... })`。

---

### Phase 3：推平中间业务层 (Opencode / Mux)

**目标：由底向上，把所有的 `async { }` 替换为 `promise { }`。**

1. **`src/Mux/Delegate.fs` & `src/Opencode/SessionIo.fs`**
   - 返回值彻底固定为 `JS.Promise<string>`。
   - `Async.Catch` 替换为在 `promise { }` 内部的 `try ... with ex -> ...`。

2. **`src/Opencode/HookExecute.fs` & `src/Opencode/HookTransform.fs` & `src/Opencode/ReviewerLoop.fs`**
   - 返回值改为 `JS.Promise<unit>` 或 `JS.Promise<ReviewResult>`。
   - 删掉结尾为了适配 JS 宿主而加的 `|> Async.StartAsPromise`。

3. **`src/Mux/SubagentTools.fs` & `src/Opencode/Tools.fs`**
   - 确保工具执行签名保持或统一为 `fun config args -> JS.Promise<string>`。
   - 内部实现直接使用 `promise { }`。

---

### Phase 4：清理入口与顶层 (Plugin 边界)

**目标：完成对宿主平台暴露接口的平滑对接。**

1. **`src/Mux/Plugin.fs` & `src/Opencode/PluginCore.fs` 等 Plugin 文件**
   - 这里的 Hook 签名本来就是期待 `Promise`。现在底层传上来的已经是纯正的 `JS.Promise`，直接 return 即可。
   - 同步返回的地方直接写 `Promise.resolve()`。

---

### Phase 5：修复测试套件

1. **`tests/Integration*.fs` & `tests/WikiFileTests.fs`**
   - 测试定义批量替换：`let spec () = promise { ... }`。
   - `Async.Sleep 200` 替换为 `Promise.sleep 200`。
   - 检查 Mocha/Jest 是否支持直接 return Promise（大部分测试框架完美支持 Promise）。

2. **`tests/Tests.fs` (总入口)**
   - 入口方法改为返回 `JS.Promise<int>`。

---

### Phase 6：最终扫尾与宪法审查

在终端执行以下检查，确保没有旧日残留：

```bash
# 以下命令应该没有任何输出
grep -r "Async\." src/ tests/
grep -r "async {" src/ tests/
grep -r "task {" src/ tests/
grep -r "MailboxProcessor" src/ tests/
grep -r "StartAsPromise" src/ tests/
```
完成后，执行 `pnpm build && pnpm test`。
