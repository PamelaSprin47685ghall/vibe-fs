这是一份彻底消灭 `Async`、`Task` 和 `MailboxProcessor`，全面转向 `Fable.Promise` 的**自底向上重构执行手册**。

请严格按照从底层（无依赖）到高层（被依赖）的顺序推进。

---

### Phase 0：安装依赖与基建准备 ✅

**目标：引入正确的 Promise 库，建立新的异步基础设施。**

1. **安装并配置 `Fable.Promise`** ✅
   - 已运行 `dotnet add package Fable.Promise`，安装 **3.2.0**，兼容 net10.0。
   - 已加入 `vibe-fs.fsproj` `<PackageReference>`。

2. **新建文件 `src/Shell/PromiseQueue.fs`** ✅
   - 已加入 `vibe-fs.fsproj`（位于 `ReviewRuntime.fs` 之后、`CallStore.fs` 之前）。
   - 已实现 `SerialQueue`（基于 Promise Chain）与 `withTimeout`，编译通过。

3. **清理全局别名**（随各文件重构逐个删除，不单独成步）
   - 搜索并**删除**所有 `asPromise<'T>` 辅助函数（见 Phase 1/3）。
   - 搜索并**删除**遗留的 `promiseRace` 自定义封装及 `src/Shell/PromiseRace.fs`（见 Phase 2/3，依赖方迁移完后删除）。

> **实测确认的 Fable.Promise 3.2.0 API 约定（写代码必读）：**
> - `Promise` 模块是**顶层 RequireQualifiedAccess**，**不要** `open Fable.Promise`；`open Fable.Core` 后直接用 `Promise.lift/create/sleep/all/Parallel/race/bind/map/catch/reject/result` 等限定名。
> - `promise { }` CE 由 `PromiseImpl`（AutoOpen）自动可用，无需 open。
> - resolve 对应 **`Promise.lift`**，库里**没有** `Promise.resolve`。
> - 全库类型签名统一用 `JS.Promise<'T>`（`open Fable.Core` 暴露 `JS`），保持 `Promise` 名字无歧义地指向模块。
> - `promise { }` 内 `let!` 可直接解析原生 JS Promise（`fsPromises?xxx` 等），无需 unbox。

---

### Phase 1：血洗 FFI 底层与 IO 外壳 (Shell 层) ✅

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

### Phase 2：消灭 Continuation 与 MailboxProcessor ✅

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

### Phase 3：推平中间业务层 (Opencode / Mux) ✅

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

### Phase 4：清理入口与顶层 (Plugin 边界) ✅

**目标：完成对宿主平台暴露接口的平滑对接。**

1. **`src/Mux/Plugin.fs` & `src/Opencode/PluginCore.fs` 等 Plugin 文件**
   - 这里的 Hook 签名本来就是期待 `Promise`。现在底层传上来的已经是纯正的 `JS.Promise`，直接 return 即可。
   - 同步返回的地方直接写 `Promise.resolve()`。

---

### Phase 5：修复测试套件 ✅

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

---

## 完成状态与关键决策 ✅

`pnpm build && pnpm test` → **869 passed, 0 failed**（与重构前基线一致）。最终 `grep -rnE "async \{|task \{|MailboxProcessor|StartAsPromise|AwaitPromise|StartImmediate|Async\." src/ tests/` **零命中**。

**重构中做出的关键正确性决策（值得 reviewer 关注）：**

1. **`withWikiPortLock` 改为接收 thunk `unit -> JS.Promise<'a>`**（不再是 `JS.Promise<'a>`）。
   原因：JS Promise 是**热**的——若直接传入已构造的 Promise，work 会在拿到端口锁**之前**就开始执行，彻底破坏串行化（`wikiPortSerialSpec` 不加此改会失败，`WikiRuntime.submitForKind` 在生产环境也有同样的并发 bug）。旧的 `Async<'a>` 是冷的（只在拿到锁后才跑），thunk 还原了"work 必须在锁之后才启动"的保证。调用方已同步更新：`WikiRuntime.submitForKind`、`ShellTests.wikiPortSerialSpec`。

2. **Opencode NudgeHook 的 4 个事件测试增加 `do! Promise.sleep 0` 让有意为之的 fire-and-forget nudge（`startNudgeFlow |> Promise.start`）排空。**
   生产环境的"脱离"语义**保持不变**（NudgeHook 注释说明了它防止的死锁）。原生 `Promise.then` 永远 defer 到微任务，所以同步断言这个脱离副作用本质是竞态；`Promise.sleep 0`（一个宏任务）会先排空全部微任务队列，是测试 fire-and-forget 的正确写法。`reusedSessionSpec` 额外在 `session.idle` 与 `session.deleted` 之间加一次排空，确保 nudge#1 的 `decideNudge` 在状态被清除前跑完。

3. **`MailboxProcessor` → 按 key 的 `SerialQueue`（`Dictionary<string, SerialQueue>`）**：`ChildAgentRegistry.ExecutorActor`、`WikiRuntime.WikiActor`。`Post = Enqueue |> Promise.start`（fire-and-forget），`Run = Enqueue`（await）。

4. **机械映射表**：`Async.FromContinuations`→`Promise.create`；`Async.Catch`→`Promise.result`（NudgeHook）或 `try/with`；`Async.Sleep`→`Promise.sleep`；`Async.Parallel`→`Promise.all`；`Async.StartImmediate`→`Promise.start`；`Async.AwaitPromise`/`Async.StartAsPromise` 删除；原生 JS Promise 在 `promise { }` 内直接 `let!`/`do!` 解析。

5. **修复 `ExecutorJavascript.parseImports` 中一个潜伏的预存在 bug**（reviewer 触发新增测试后暴露）。查阅 `node_modules/es-module-lexer/types/lexer.d.ts` 确认真实 API：`init` 是 **`Promise<void>` 值**（直接 `do! init`），**不是函数**——旧代码 `Dyn.call1 init ()` 一直在调用一个非函数对象；`parse` 是**同步函数**，返回 `[imports, exports, facade, hasModuleSyntax]` 数组，不是 Promise。修正后：`do! Dyn.get module' "init" |> unbox<JS.Promise<unit>>`（await），再同步 `Dyn.call1 parse ...`。新增测试 `ShellTests.rewriteJavascriptRelativeImports` 覆盖 `rewriteJavascriptModuleSpecifiers`（此前测试套件完全不覆盖 JS executor 路径，故该 bug 一直潜伏）。

**注意**：`pnpm test` 本身**不会**重新编译，必须先 `pnpm build`（`AGENTS.md` 的 `pnpm build && pnpm test` 已正确处理）。
