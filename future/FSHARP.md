# 迁移 `tests/integration.mjs` → F#

## 目标

将 Node 端集成测试 (`tests/integration.mjs`, 853 行) 改写为 F# 集成测试模块,
挂入现有 F# 测试运行器 (`tests/Tests.fs`),由 Fable 编译到 `build/tests/Tests.js`,
由 `pnpm test` 驱动。

迁移后:

- 删除 `tests/integration.mjs`
- 新增 `tests/IntegrationTests.fs`,沿用 `tests/Assert.fs` 的 `check` / `equal` / `summary`
- `tests/Tests.fs` 里 `open` 并在 `main` 里调用 `IntegrationTests.run ()`
- `vibe-fs.fsproj` 里在 `tests/Tests.fs` 之前追加 `tests/IntegrationTests.fs` 的 `<Compile Include=...>`
- `package.json` 的 `test` 脚本保持 `node build/tests/Tests.js`(因为现在所有测试在 IIFE 内同步执行)
- 关键改动:在 `tests/Tests.fs` 末尾 `main` 调 `process.exit(summary())`,**让失败数成为 Node 退出码**
  (现状: `Assert.summary` 返回失败数但 IIFE 同步 return,Node 忽略;Fable 5.2 的 IIFE 形式不允许通过返回值传 exit code,只能 `process.exit`)
- IntegrationTests 内部 async 测试用 **busy-wait drain microtask** 同步等待 `Async.StartAsPromise` 解析

## 关键技术决策 (从代码中验证)

### 1. Fable 5.2 的 `[<EntryPoint>]` 编译产物
当前 `tests/Tests.fs` 编译到 `build/tests/Tests.js` 末尾是:
```js
(function (_arg) {
    transition$0027();
    ...
    run_1();
    return summary() | 0;
})(typeof process === 'object' ? process.argv.slice(2) : []);
```
IIFE 形式,**Node 直接跑 .js 拿不到返回值**。当前 `pnpm test` 之所以能传非 0 exit code,
靠的是 `&& node tests/integration.mjs` 中 `.mjs` 用 `process.exitCode = 1` 兜底。
F# 端失败时**不会**让 Node 退出非 0。

**必须改用 `process.exit(failed)` 显式退出**,把 F# 端的失败数传成 Node 的 exit code。

### 2. 异步测试同步等待
Fable 5 移除了 `Async.RunSynchronously`(已确认: `node_modules/@fable-org/.ignored_fable-library-js/CHANGELOG.md:119`)。
**但**所有 IntegrationTests 涉及的 async 链 (`runSubagent`、`plugin.config`、`plugin.event`、`eventHook`、`buildCapsFileReadData` 等)
都是纯 microtask 链(不涉及 `setTimeout(N>0)` / `setImmediate` / 网络 I/O),可以用
**busy-wait drain microtask** 同步等待。

实现:封装一个 `runSync` helper,`setTimeout(0)` 反复调用触发 microtask drain,直到 promise resolve。
`process.exit` 之前必须 `runSync` 完所有 IntegrationTests 的异步断言,否则失败数累加不到 `failed`。

### 3. 测试框架
`tests/Assert.fs` 提供 `check` / `equal` / `summary` 已有,直接复用。

### 4. 动态对象互操作
`VibeFs.Kernel.Dyn` 提供 `str` / `get` / `has` / `isNullish` / `call1` / `call2` / `opt`。
构造 JS 形状用 `box {| ... |}` 和 `createObj [ "k", box v; ... ]`。

### 5. mock 闭包
JS `{ execute: async (args) => 'File written' }` → F#:
```fsharp
createObj [ "execute", box (System.Func<obj, obj, JS.Promise<obj>>(
    fun _ _ -> async { return box "File written" :> obj } |> Async.StartAsPromise)) ]
```

### 6. 回归保护点 (必须严格保留!)
原 .mjs 606–650 行用 `part === readPartX` / `state === readStateX` 引用相等断言,
钉住"opencode host 契约: dedup 必须原地改写 `state.output`"。Fable `=` 对 `obj` 是 `===`。

## 文件改动清单

### 1. `tests/IntegrationTests.fs` (新建,约 700-900 行)

**结构**:
```fsharp
module VibeFs.Tests.IntegrationTests

open System.IO
open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dyn
open VibeFs.Index
open VibeFs.Opencode.Plugin
open VibeFs.Opencode.ChildAgent
open VibeFs.Opencode.Session
open VibeFs.Shell.TreeSitterSyntax

// ── sync helper: drain microtasks until promise resolves ──
let private setTimeout (ms: int) : obj =
    let f = JS.Constructors.Global?SetTimeout
    f.Invoke(ms) :> obj

let private runSync<'t> (p: JS.Promise<'t>) : 't =
    let mutable result : 't option = None
    let mutable err : System.Exception = null
    let pObj = p |> unbox<JS.Promise<obj>>
    pObj.then(
        Func<obj, obj>(fun v -> result <- Some (unbox<'t> v); null),
        Func<obj, obj>(fun e -> err <- System.Exception(string e); null)
    ) |> ignore
    while result.IsNone && isNull err do
        setTimeout 0
    if not (isNull err) then raise err
    result.Value

// ── factory helpers ──
let mkPart (toolName: string) (output: obj) (id: string) : obj = ...
let mkPartWithPath (toolName: string) (path: string) (output: obj) (id: string) : obj = ...
let fileReadOutput (content: string) : obj = ...
let mkModelRead (toolName: string) (output: obj) : obj = ...

// ── per-section test functions (all return unit) ──
let pluginShape () = ...
let registrationShape () = ...
let getPluginToolPolicySpec () = ...
let syntaxCheckSpec () = ...
let eventHookSpec () = ...
let repeatedTodoNudgesSpec () = ...
let syntaxWrapperSpec () = ...
let todoWriteWrapperSpec () = ...
let abortedRetrySpec () = ...
let repeatedAssistantSpec () = ...
let reusedSessionSpec () = ...
let webfetchSchemaSpec () = ...
let slashCommandsSpec () = ...
let wrapperCountSpec () = ...
let toolCountSpec () = ...
let agentReportWrapperSpec () = ...
let webSearchWrapperSpec () = ...
let capsFileReadDataSpec () = ...
let capsTransformSpec () = ...
let dedupReadOutputsSpec () = ...
let collectReadOutputsSpec () = ...
let dedupAgainstHistorySpec () = ...
let dedupModelSpec () = ...
let opencodeDedupInPlaceSpec () = ...   // 引用相等回归保护,重点
let writeToolSpec () = ...
let loopCommandSpec () = ...
let agentConfigSpec () = ...
let chatMessageSpec () = ...
let websearchBoundariesSpec () = ...
let toolDefinitionSpec () = ...
let toolExecuteBeforeSpec () = ...
let subagentParentSpec () = ...
let nestedSubagentSpec () = ...

let run () : unit =
    pluginShape ()
    registrationShape ()
    getPluginToolPolicySpec ()
    ...
```

**实现要点**:

#### a) sync helper 放在文件顶部
```fsharp
let private setTimeoutMs (ms: int) : unit =
    let f : obj = JS.Constructors.Global?SetTimeout
    f.Invoke(ms) |> ignore

let private runSync (p: JS.Promise<obj>) : obj =
    let mutable result : obj = null
    let mutable done = false
    p.then(fun v -> result <- v; done <- true; null) |> ignore
    while not done do
        setTimeoutMs 0
    result
```

注意:`result` 用 `obj` 而非 `'t option`,避免 generic 在 Fable 上的复杂性。
F# 端调用方拿到 `obj` 后用 `unbox<...>` 转型。

#### b) plugin 调用
```fsharp
let pluginShape () =
    let p = runSync (plugin (box {| directory = "/tmp/vibe" |}))
    check "plugin.name" (Dyn.str p "name" = "kunwei")
    check "plugin.tool" (Dyn.typeIs (Dyn.get p "tool") "object")
    ...
```

#### c) eventHook.length
```fsharp
let hook = Dyn.get reg "eventHook"
check "eventHook.length === 2" (unbox<int> (hook?length) = 2)
```

#### d) eventHook 调用
```fsharp
let ehResult = runSync (hook $ (createObj [ "type", box "stream-abort"; "workspaceId", box "test-ws" ], null))
```

#### e) getPluginToolPolicy
```fsharp
let pol1 = getPluginToolPolicy "some-agent" "manager"
let removes = unbox<string[]> (Dyn.get pol1 "remove")
check "manager removes write" (removes |> Array.contains "write")
```

#### f) caps transform IO
```fsharp
let capsFileReadDataSpec () =
    let tmpDir = Path.Combine("/tmp", "caps-test-" + System.Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory tmpDir |> ignore
    File.WriteAllText (Path.Combine(tmpDir, "CAPS.md"), "# Capabilities\nTest content")
    let entries = runSync (buildCapsFileReadData tmpDir |> unbox<JS.Promise<obj>>)
    let arr = unbox<obj[]> entries
    check "buildCapsFileReadData returns array" (arr.Length = 1)
    let first = arr.[0]
    check "caps entry has path" (Dyn.str first "path" = "CAPS.md")
    check "caps entry has callId prefix" (Dyn.str first "callId").StartsWith("caps-fr-")
    let output = Dyn.get first "output"
    check "caps entry has modifiedTime" (Dyn.str output "modifiedTime" <> "")
    check "caps entry output has content" (Dyn.str output "content" = "# Capabilities\nTest content")
    Directory.Delete(tmpDir, true)

let capsTransformSpec () =
    File.WriteAllText("/tmp/vibe/CAPS.md", "# Capabilities\nTest content")
    let p = runSync (plugin (box {| directory = "/tmp/vibe" |}))
    // ... 8-9 个 sub-cases
    File.Delete "/tmp/vibe/CAPS.md"
```

#### g) 引用相等回归保护 (重点, 严格按 .mjs 606-650 一一对应)
```fsharp
let opencodeDedupInPlaceSpec () =
    let p = runSync (plugin (box {| directory = "/tmp/vibe" |}))
    let stableContent = String.replicate 8 "line of stable content\n"
    let readStateA = createObj [ "output", box stableContent ]
    let readStateB = createObj [ "output", box stableContent ]
    let readPartA = createObj [ "type", box "tool"; "tool", box "read"; "state", box readStateA ]
    let readPartB = createObj [ "type", box "tool"; "tool", box "read"; "state", box readStateB ]
    let dedupInPlace =
        createObj [ "messages", box [|
            createObj [ "info", box (createObj [ "id", box "dedup-m1"; "agent", box "manager" ])
                        "parts", box [| readPartA |] ]
            createObj [ "info", box (createObj [ "id", box "dedup-m2"; "agent", box "manager" ])
                        "parts", box [| readPartB |] ]
        |] ]
    let dedupMessagesRef = Dyn.get dedupInPlace "messages"
    runSync ((Dyn.get p "experimental.chat.messages.transform") $ (box null, dedupInPlace))
        |> ignore
    let msgs = unbox<obj[]> dedupMessagesRef
    check "opencode dedup keeps messages array ref"
          (obj.ReferenceEquals(msgs, unbox<obj[]> (Dyn.get dedupInPlace "messages")))
    let partA = unbox<obj> msgs.[0] |> Dyn.get "parts" |> unbox<obj[]> |> Array.item 0
    let partB = unbox<obj> msgs.[1] |> Dyn.get "parts" |> unbox<obj[]> |> Array.item 0
    check "opencode dedup keeps first part ref" (obj.ReferenceEquals(partA, readPartA))
    check "opencode dedup keeps second part ref" (obj.ReferenceEquals(partB, readPartB))
    let stateA = Dyn.get partA "state"
    let stateB = Dyn.get partB "state"
    check "opencode dedup keeps first state ref" (obj.ReferenceEquals(stateA, readStateA))
    check "opencode dedup keeps second state ref" (obj.ReferenceEquals(stateB, readStateB))
    check "opencode dedup keeps first read output" (Dyn.str stateA "output" = stableContent)
    check "opencode dedup replaces exact duplicate with marker"
          (Dyn.str stateB "output" = "[No Change Since Previous Read/Write]")

    // superset case
    let supersetContent = stableContent + String.replicate 8 "new content\n"
    let supersetState = createObj [ "output", box supersetContent ]
    let supersetPart = createObj [ "type", box "tool"; "tool", box "read"; "state", box supersetState ]
    let dedupSuperset =
        createObj [ "messages", box [|
            createObj [ "info", box (createObj [ "id", box "dedup-s1"; "agent", box "manager" ])
                        "parts", box [| readPartA |] ]
            createObj [ "info", box (createObj [ "id", box "dedup-s2"; "agent", box "manager" ])
                        "parts", box [| supersetPart |] ]
        |] ]
    runSync ((Dyn.get p "experimental.chat.messages.transform") $ (box null, dedupSuperset))
        |> ignore
    let supPart = unbox<obj> (unbox<obj[]> (Dyn.get dedupSuperset "messages").[1])
                  |> Dyn.get "parts" |> unbox<obj[]> |> Array.item 0
    let supState = Dyn.get supPart "state"
    check "opencode dedup superset keeps state ref" (obj.ReferenceEquals(supState, supersetState))
    check "opencode dedup superset NOT replaced"
          (Dyn.str supState "output" = supersetContent)
```

**Fable 的 `obj.ReferenceEquals` 编译为 `===`**,正确做引用比较。**不要用 `=` 对两个 obj 值**——
F# `=` 对 `obj` 是值等,会递归比字段,把 readStateA 跟 readStateB 当作等(因为字段相同),断言失真。

#### h) write tool 调用
```fsharp
let writeDef = reg.tools |> unbox<obj[]> |> Array.find (fun t -> Dyn.str t "name" = "write")
let result = unbox<string> (runSync (Dyn.call2 (Dyn.get writeDef "execute")
                                          (box {| cwd = writeEmptyDir |})
                                          (box {| file_path = "empty.txt"; content = "" |})))
```

#### i) plugin.config
```fsharp
let cfg = runSync ((Dyn.get p "config")
                    $ (box {| agent = box {|
                        browser = box {| model = "kimi-for-coding/k2p7" |}
                        executor = box {| model = "opencode-go/deepseek-v4-flash" |}
                        custom = box {| model = "custom-model" |}
                    |} |}))
let browserAgent = Dyn.get (Dyn.get cfg "agent") "browser"
check "browser builtin system prompt empty" (Dyn.str browserAgent "prompt" = "")
...
```

#### j) chat.message
```fsharp
let orchChat = createObj [ "message", box (createObj [ "tools", box (createObj [
    "stealth-browser-mcp_*", box true
    "stealth-browser-mcp_foo", box true
    "write", box true
    "read", box true
]) ]) ]
runSync ((Dyn.get p "chat.message") $ (box {| sessionID = "root"; agent = "manager" |}, orchChat))
    |> ignore
let tools = Dyn.get (Dyn.get orchChat "message") "tools"
check "manager stealth disabled" (unbox<bool> (Dyn.get tools "stealth-browser-mcp_*") |> not)
```

#### k) subagent mock
```fsharp
let subagentParentSpec () =
    let createCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let messages = createObj [ "data", box [|
        createObj [ "info", box (createObj [ "role", box "user" ])
                    "parts", box [| createObj [ "type", box "text"; "text", box "navigate to example.com" |] |] ]
        createObj [ "info", box (createObj [ "role", box "assistant" ])
                    "parts", box [| createObj [ "type", box "text"; "text", box "Found the page title: Example Domain" |] |] ]
    |] ]
    let mockClient = createObj [ "session", box (createObj [
        "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
            async {
                createCalls.Add(arg)
                return (createObj [ "data", box (createObj [ "id", box "child-session-123" ]) ] :> obj)
            } |> Async.StartAsPromise))
        "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
            async { promptCalls.Add(arg) } |> Async.StartAsPromise))
        "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
            async { return (messages :> obj) } |> Async.StartAsPromise))
        "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> async { () } |> Async.StartAsPromise))
    ]) ]
    let result = unbox<string> (runSync (runSubagent mockClient "browser" "Browser"
                                            "navigate to example.com" "/tmp/vibe"
                                            "parent-session-456"
                                            (createObj [ "abort", box null ])
                                            null))
    check "runSubagent returns string" (result.Contains "Example Domain")
    let firstCreate = createCalls.[0]
    check "session.create received parentID"
          (Dyn.str (Dyn.get firstCreate "body") "parentID" = "parent-session-456")
    let firstPrompt = promptCalls.[0]
    check "session.prompt uses child id" (Dyn.str (Dyn.get firstPrompt "path") "id" = "child-session-123")
    check "session.prompt uses browser agent" (Dyn.str (Dyn.get firstPrompt "body") "agent" = "browser")
```

`runSubagent` 第 8 个参数 `tools: obj`,传 `null`(编译到 JS `null`)。

#### l) mock 闭包中的 registerChildAgent
```fsharp
let chatMessageSpec () =
    let p = runSync (plugin (box {| directory = "/tmp/vibe" |}))
    // ... orchChat, coderChat, browserChat
    registerChildAgent "child-browser-session" "browser" None
    let childChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "stealth-browser-mcp_*", box true
    ]) ]) ]
    runSync ((Dyn.get p "chat.message") $ (box {| sessionID = "child-browser-session" |}, childChat)) |> ignore
    let tools = Dyn.get (Dyn.get childChat "message") "tools"
    check "child session resolves to browser" (unbox<bool> (Dyn.get tools "stealth-browser-mcp_*"))
    unregisterChildAgent "child-browser-session"
```

**注意**:`registerChildAgent` 第 3 个参数是 `string option`,`None` 编译为 JS `undefined`——
与 .mjs 的 `undefined` 一致。

### 2. `tests/Tests.fs` 改动

**`main` 末尾**改为调 `process.exit(summary())`,把失败数显式传成 Node exit code:

```fsharp
[<EntryPoint>]
let main _ =
    ReviewTests.transition' ()
    ...
    DelegateTests.run ()
    ResolveAiSettingsTests.run ()
    IntegrationTests.run ()   // 新增
    let code = summary ()
    // F# 端 process 全局(在 IIFE 内同步执行)
    Fable.Core.JS.Constructors.Global?process?exit(code) |> ignore
    code
```

(或者更直接地调 `node:process` 的 exit 绑定;若 Fable 编译后 `process.exit` 路径不顺,
退回 `process.exitCode := code` + `process.exit()`,然后让 IIFE 正常 return。)

**优先用 `process.exit(summary())` 显式退出**,确保失败数 → exit code。

在文件顶部加 `open VibeFs.Tests.IntegrationTests`。

### 3. `vibe-fs.fsproj` 改动

在 `tests/ResolveAiSettingsTests.fs` 之后、`tests/Tests.fs` 之前加:
```xml
<Compile Include="tests/IntegrationTests.fs" />
```

### 4. `package.json` 改动

`test` 脚本保持:
```json
"test": "node build/tests/Tests.js"
```
(去掉 `&& node tests/integration.mjs`)。

### 5. 删除 `tests/integration.mjs`

迁移完成后删除源文件。

## 验证步骤

```bash
cd /home/kunweiz/Desktop/vibe/vibe-fs
pnpm build     # 必须无 warning/error
pnpm test      # 期望: "==== N passed, 0 failed ====", exit 0
```

**逐条覆盖**:
1. `pnpm build` 编译无错
2. `pnpm test` exit 0
3. 改坏一条 `check`,确认 `pnpm test` exit 非 0
4. 改坏引用相等回归保护 (`opencode dedup keeps first part ref`),确认失败
5. 删除 `tests/integration.mjs` 后 `pnpm test` 仍能跑通(无 `ENOENT`)
6. `.mjs` 里有约 150+ 条 `check`;新文件 `check` 数应等于或略多

**必须保留的引用相等回归保护** (原 .mjs 606–650):
- messages array ref 不变 (`obj.ReferenceEquals`)
- 第一个 / 第二个 part ref 不变
- 第一个 / 第二个 state ref 不变
- 第一个 output 内容保持原值
- 第二个 output 替换为 marker
- superset 不被替换

## 风险与权衡

1. **busy-wait drain microtask** — 当前 `IntegrationTests` 涉及的 async 链都是
   microtask-only(`fs` 异步 API,`async` 函数).无 `setTimeout(N>0)` / `setImmediate`。
   若后续添加涉及定时器或网络的断言,会死锁——届时需改用 `node -e` shim + `JS.Promise<int>` EntryPoint 方案。

2. **`process.exit` in IIFE** — Fable 5.2 把 `main` 编译成 IIFE 同步执行,
   `process.exit(N)` 在 IIFE 内调会立即终止 Node,exit code = N。当前 `Assert.summary`
   是纯函数不抛,失败时 IIFE return N,Node 默认 exit 0——这**无法**反映失败。
   必须显式 `process.exit(summary())`。

3. **`obj.ReferenceEquals` vs `=`** — F# `=` 对 `obj` 是递归值等(对 dynamic object 容易误判相等);
   **引用相等必须用 `obj.ReferenceEquals`**。Fable 编译为 `===`。

4. **`Func<obj, _, _>` 调用** — Fable 5 编译为 `f(a, b)` 形式,
   对应 JS `execute(args, opts)` 双参调用。单参数用 `Func<obj, _>`。

5. **mock 闭包捕获 `ResizeArray`** — `ResizeArray<obj>.Add` 等价 JS `Array.push`,
   索引访问 `.[0]` 顺序与 .mjs `arr[0]` 一致。**不要用 `let mutable x : obj list`**,
   列表追加顺序会反。

6. **匿名记录字段大小写** — Fable 4+ 默认 lowercase 首字母输出,
   `box {| toolCallId = "1" |}` 编译为 `{ toolCallId: "1" }`。
   若不工作,用 `[<Fable.Core.JsonField("toolCallId")>] name` 显式标注。
