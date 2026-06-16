# 迁移 `tests/integration.mjs` → F#

## 目标

将 `tests/integration.mjs` (863 行) 改写为 F# 集成测试，挂入现有测试体系。
迁移后删除 `tests/integration.mjs`。

## 架构决策

**异步入口**: Fable 5.2 的 `[<EntryPoint>]` 编译为 IIFE，Node 不等待 Promise。
不用 busy-wait，不用 `process.exit` 在 IIFE 内显式退出。改为:
- `Tests.fs` 去掉 `[<EntryPoint>]`，导出 `runAll: unit -> JS.Promise<int>`
- 新建 `tests/runner.js` 作为 Node 入口: `await runAll()` 然后 `process.exit(code)`
- `package.json` test 脚本改为 `node tests/runner.js`

**文件拆分**: 宝典铁律单文件 ≤ 300 行。863 行 JS 译为 F# 略膨胀，拆为 5 个文件，每个控制在 200 行左右。

## 文件清单

### 新建 (5 个测试文件 + 1 个 runner)

| 文件 | 覆盖区域 | 预估行数 |
|------|---------|---------|
| `tests/IntegrationPluginTests.fs` | plugin 形态、registration 形态、getPluginToolPolicy、checkSyntax、webfetch schema、slash commands、wrapper/tool 计数 | ~120 |
| `tests/IntegrationEventTests.fs` | eventHook、repeated todo nudge、syntax/todo_write wrapper、tool.execute.after、aborted retry、repeated assistant、reused session | ~200 |
| `tests/IntegrationDedupTests.fs` | deduplicateReadOutputs、collectReadOutputs、AgainstHistory、ModelRead、opencode in-place 引用相等回归 | ~250 |
| `tests/IntegrationToolTests.fs` | agent_report/web_search wrapper、buildCapsFileReadData、caps transform、write tool、loop command、agent config、tool.definition、tool.execute.before | ~220 |
| `tests/IntegrationChatTests.fs` | chat.message 工具边界、subagent parent、nested subagent | ~160 |
| `tests/runner.js` | Node 入口，await runAll 后 process.exit | ~10 |

### 修改

| 文件 | 改动 |
|------|------|
| `tests/Tests.fs` | 去掉 `[<EntryPoint>]`，新增 `runAll` 返回 `JS.Promise<int>`，组合所有测试 |
| `vibe-fs.fsproj` | 在 `tests/ResolveAiSettingsTests.fs` 后追加 5 个 IntegrationTests |
| `package.json` | test 脚本改为 `node tests/runner.js` |

### 删除

`tests/integration.mjs`

## 文件内容

### `tests/runner.js`

```js
import { runAll } from '../build/tests/Tests.js';
runAll([]).then(code => process.exit(code));
```

### `tests/Tests.fs`

```fsharp
module VibeFs.Tests.Tests

open Fable.Core
open VibeFs.Tests.Assert
open VibeFs.Tests.ReviewTests
open VibeFs.Tests.AgentTests
open VibeFs.Tests.KernelTests
open VibeFs.Tests.FuzzyTests
open VibeFs.Tests.ShellTests
open VibeFs.Tests.DynTests
open VibeFs.Tests.DelegateTests
open VibeFs.Tests.ResolveAiSettingsTests
open VibeFs.Tests.IntegrationPluginTests
open VibeFs.Tests.IntegrationEventTests
open VibeFs.Tests.IntegrationDedupTests
open VibeFs.Tests.IntegrationToolTests
open VibeFs.Tests.IntegrationChatTests

let runAll (_args: string array) : JS.Promise<int> =
    async {
        ReviewTests.transition' ()
        ReviewTests.registry ()
        ReviewTests.resultMapping ()
        ReviewTests.reviewerLoop ()
        ReviewTests.runtime ()
        AgentTests.canUse' ()
        AgentTests.deniedTools' ()
        AgentTests.decision ()
        AgentTests.updateState ()
        AgentTests.coordinator ()
        AgentTests.shouldSuppress' ()
        KernelTests.headTail' ()
        KernelTests.dedup' ()
        KernelTests.lru' ()
        KernelTests.ipAllowlist' ()
        KernelTests.ipStrict ()
        KernelTests.excludedDirs' ()
        KernelTests.jsBoundary' ()
        KernelTests.hostKernel' ()
        FuzzyTests.grepDetect ()
        FuzzyTests.iteratorRoundTrip ()
        FuzzyTests.finderConversion ()
        FuzzyTests.formatFull ()
        FuzzyTests.fuzzyFallbackNotice ()
        FuzzyTests.findPagingDefault ()
        FuzzyTests.totalMatchedSemantics ()
        ShellTests.ollamaFetchInit ()
        ShellTests.ollamaResponseMethodCall ()
        ShellTests.executorMapping ()
        ShellTests.recordValidator ()
        ShellTests.capsFileShape ()
        ShellTests.capsContextFormat ()
        ShellTests.capsFileSizeLimit ()
        ShellTests.ollamaFormat ()
        ShellTests.summarizerInputCap ()
        DynTests.nullish ()
        DelegateTests.run ()
        ResolveAiSettingsTests.run ()
        do! IntegrationPluginTests.run () |> Async.AwaitPromise
        do! IntegrationEventTests.run () |> Async.AwaitPromise
        do! IntegrationDedupTests.run () |> Async.AwaitPromise
        do! IntegrationToolTests.run () |> Async.AwaitPromise
        do! IntegrationChatTests.run () |> Async.AwaitPromise
        return summary ()
    }
    |> Async.StartAsPromise
```

### `tests/IntegrationPluginTests.fs`

```fsharp
module VibeFs.Tests.IntegrationPluginTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dyn
open VibeFs.Index
open VibeFs.Opencode.Plugin
open VibeFs.Shell.TreeSitterSyntax

let pluginShape (p: obj) =
    check "plugin.name" (Dyn.str p "name" = "kunwei")
    check "plugin.tool" (Dyn.typeIs (Dyn.get p "tool") "object")
    check "plugin.config" (Dyn.typeIs (Dyn.get p "config") "function")
    check "plugin.event" (Dyn.typeIs (Dyn.get p "event") "function")
    check "plugin.mcp" (Dyn.typeIs (Dyn.get p "mcp") "object")
    check "plugin.tool.execute.after" (Dyn.typeIs (Dyn.get p "tool.execute.after") "function")
    check "plugin.experimental.chat.messages.transform" (Dyn.typeIs (Dyn.get p "experimental.chat.messages.transform") "function")
    check "plugin.command.execute.before" (Dyn.typeIs (Dyn.get p "command.execute.before") "function")

let registrationShape (reg: obj) =
    check "mux.toolNames" (Dyn.typeIs (Dyn.get reg "toolNames") "object" && Dyn.isArray (Dyn.get reg "toolNames"))
    check "mux.tools" (Dyn.isArray (Dyn.get reg "tools"))
    check "mux.mcpServers" (Dyn.typeIs (Dyn.get reg "mcpServers") "object")
    check "mux.contextInjector" (Dyn.typeIs (Dyn.get reg "contextInjector") "object")
    let policy = (Dyn.get reg "getToolPolicy") $ ("x", "manager")
    check "mux.getToolPolicy non-null" (not (Dyn.isNullish policy) && Dyn.typeIs policy "object")
    check "mux.getToolPolicy manager removes write" (unbox<bool> ((unbox<string[]> (Dyn.get policy "remove")) |> Array.contains "write"))

let policySpec () =
    let pol1 = getPluginToolPolicy "some-agent" "manager"
    let removes = unbox<string[]> (Dyn.get pol1 "remove")
    check "getPluginToolPolicy manager removes write" (removes |> Array.contains "write")
    let pol2 = getPluginToolPolicy "some-agent"
    check "getPluginToolPolicy without role returns policy" (not (Dyn.isNullish pol2))
    let pol3 = getPluginToolPolicy "some-agent" "coder"
    check "getPluginToolPolicy coder keeps write" (not ((unbox<string[]> (Dyn.get pol3 "remove")) |> Array.contains "write"))

let syntaxSpec () = async {
    let! result = checkSyntax "const x = 1;" "test.js" |> Async.AwaitPromise
    let fields = unbox<obj[]> (Dyn.get (box result) "fields")
    check "tree-sitter returns ok" (unbox<int> (Dyn.get (box result) "tag") = 0)
    check "tree-sitter no errors" ((unbox<obj[]> fields.[1]).Length = 0)
}

let webfetchSchemaSpec (reg: obj) =
    let tools = unbox<obj[]> (Dyn.get reg "tools")
    let wf = tools |> Array.find (fun t -> Dyn.str t "name" = "webfetch")
    let props = Dyn.get (Dyn.get wf "parameters") "properties"
    check "webfetch schema has url" (not (Dyn.isNullish (Dyn.get props "url")))
    check "webfetch schema has extract_main" (not (Dyn.isNullish (Dyn.get props "extract_main")))
    check "webfetch schema has timeout" (not (Dyn.isNullish (Dyn.get props "timeout")))
    check "webfetch execute is function" (Dyn.typeIs (Dyn.get wf "execute") "function")

let slashCommandsSpec (reg: obj) =
    let cmds = unbox<obj[]> (Dyn.get reg "slashCommands")
    check "slash commands count" (cmds.Length = 2)
    let loopCmd = cmds |> Array.find (fun c -> Dyn.str c "key" = "loop")
    check "loop command has execute" (Dyn.typeIs (Dyn.get loopCmd "execute") "function")

let countsSpec (reg: obj) =
    let wrappers = unbox<obj[]> (Dyn.get reg "wrappers")
    let tools = unbox<obj[]> (Dyn.get reg "tools")
    check "wrapper count" (wrappers.Length = 7)
    check "tool count" (tools.Length = 12)

let run () : JS.Promise<unit> = async {
    let! p = Plugin.plugin (box {| directory = "/tmp/vibe" |}) |> Async.AwaitPromise
    pluginShape p
    let reg = createRegistration (createObj [])
    registrationShape reg
    policySpec ()
    do! syntaxSpec ()
    webfetchSchemaSpec reg
    slashCommandsSpec reg
    countsSpec reg
} |> Async.StartAsPromise
```

### `tests/IntegrationEventTests.fs`

```fsharp
module VibeFs.Tests.IntegrationEventTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dyn
open VibeFs.Index
open VibeFs.Opencode.Plugin

let eventHookSpec (reg: obj) = async {
    let hook = Dyn.get reg "eventHook"
    check "eventHook.length === 2" (unbox<int> (hook?length) = 2)
    let ehResult = hook $ (createObj [ "type", box "stream-abort"; "workspaceId", box "test-ws" ], null)
    check "eventHook returns Promise" (not (Dyn.isNullish ehResult) && Dyn.typeIs (Dyn.get ehResult "then") "function")
    do! unbox<JS.Promise<unit>> ehResult |> Async.AwaitPromise
}

let repeatedTodoNudgeSpec (reg: obj) = async {
    let nudges = ResizeArray<string>()
    let helpers todoList =
        createObj [
            "getTodos", box (System.Func<unit, JS.Promise<obj>>(fun () -> async {
                return box (todoList |> List.toArray)
            } |> Async.StartAsPromise))
            "nudge", box (System.Func<obj, obj, JS.Promise<bool>>(fun _ws msg -> async {
                nudges.Add(string msg)
                return true
            } |> Async.StartAsPromise))
        ]
    let hook = Dyn.get reg "eventHook"
    let streamEnd ws parts =
        createObj [ "type", box "stream-end"; "workspaceId", box ws
                    "properties", box (createObj [ "parts", box parts ]) ]
    let textPart t = box {| ``type`` = "text"; text = t |}
    do! hook $ (streamEnd "repeat-ws" [| textPart "first" |], helpers ["pending"]) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    do! hook $ (streamEnd "repeat-ws" [| textPart "second" |], helpers ["pending"]) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "repeated todo nudge suppressed" (nudges.Count = 1)
    do! hook $ (streamEnd "repeat-ws" [| textPart "cleared" |], helpers []) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    do! hook $ (streamEnd "repeat-ws" [| textPart "reopened" |], helpers ["pending"]) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "todo nudge re-allowed after clear" (nudges.Count = 2)
}

let syntaxWrapperSpec (reg: obj) = async {
    let wrappers = unbox<obj[]> (Dyn.get reg "wrappers")
    let sw = wrappers |> Array.find (fun w -> Dyn.str w "targetTool" = "file_edit_replace_string")
    check "syntax wrapper exists" (not (Dyn.isNullish sw))
    let mockEdit = createObj [ "execute", box (System.Func<obj, JS.Promise<string>>(fun _ -> async { return "File written" } |> Async.StartAsPromise)) ]
    let wrapped = (Dyn.get sw "wrapper") $ (mockEdit, createObj [ "cwd", box "/tmp" ])
    let result = unbox<string> ((Dyn.get wrapped "execute") $ (createObj [ "file_path", box "nonexistent.js" ]))
    let! _ = result |> Async.AwaitPromise
    check "syntax wrapper returns result for missing file" true
}

let todoWriteWrapperSpec (reg: obj) = async {
    let wrappers = unbox<obj[]> (Dyn.get reg "wrappers")
    let tw = wrappers |> Array.find (fun w -> Dyn.str w "targetTool" = "todo_write")
    check "todo_write wrapper exists" (not (Dyn.isNullish tw))
    let mockTodo = createObj [ "execute", box (System.Func<obj, JS.Promise<string>>(fun _ -> async { return "Todos updated" } |> Async.StartAsPromise)) ]
    let wrapped = (Dyn.get tw "wrapper") $ (mockTodo, createObj [])
    let! result = ((Dyn.get wrapped "execute") $ (createObj [])) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "todo_write wrapper appends reverie nudge" (result.Contains "Think thrice")
}

let toolExecuteAfterSpec (p: obj) = async {
    let output = createObj [ "output", box "Todos updated" ]
    do! (Dyn.get p "tool.execute.after") $ (createObj [ "tool", box "todowrite"; "sessionID", box "test-ws"; "callID", box "todo-1" ], output) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "tool.execute.after appends reverie nudge" (unbox<string> (Dyn.get output "output")).Contains("Think thrice")
}

let abortedRetrySpec () = async {
    let promptCalls = ResizeArray<obj>()
    let mockClient session =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () -> async {
                return box {| data = [| box {| id = "todo-1"; content = "task"; status = "in_progress" |} |] |}
            } |> Async.StartAsPromise))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () -> async {
                return box {| data = [| box {|
                    info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
                    parts = [| box {| ``type`` = "text"; text = "working" |} |]
                |} |] |}
            } |> Async.StartAsPromise))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg -> async { promptCalls.Add(arg) } |> Async.StartAsPromise))
        ]) ]
    let p = Plugin.plugin (box {| directory = "/tmp/vibe"; client = mockClient () |}) |> Async.AwaitPromise
    let! p = p
    let eventHook = Dyn.get p "event"
    do! eventHook $ (box {| event = box {| ``type`` = "session.next.step.failed"
        properties = box {| sessionID = "resume-ws"; error = box {| ``type`` = "unknown"; message = "Aborted" |} |} |} |}) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "resume-ws" |} |} |}) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "aborted retry does not nudge before new prompt" (promptCalls.Count = 0)
    do! eventHook $ (box {| event = box {| ``type`` = "session.next.prompted"
        properties = box {| sessionID = "resume-ws"; prompt = box {| text = "continue" |} |} |} |}) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "resume-ws" |} |} |}) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "session.next.prompted resumes todo nudge" (promptCalls.Count = 1)
}

let repeatedAssistantSpec () = async {
    let mutable messages = [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
               parts = [| box {| ``type`` = "text"; text = "still working" |} |] |}
    |]
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () -> async {
                return box {| data = [| box {| id = "todo-1"; content = "task"; status = "in_progress" |} |] |}
            } |> Async.StartAsPromise))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () -> async { return box {| data = messages |} } |> Async.StartAsPromise))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg -> async { promptCalls.Add(arg) } |> Async.StartAsPromise))
        ]) ]
    let p = Plugin.plugin (box {| directory = "/tmp/vibe"; client = mkClient () |}) |> Async.AwaitPromise
    let! p = p
    let eventHook = Dyn.get p "event"
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "same-text-ws" |} |} |}) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "same text first assistant turn nudges" (promptCalls.Count = 1)
    messages <- Array.append messages [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 2 |} |}
               parts = [| box {| ``type`` = "text"; text = "still working" |} |] |}
    |]
    let p2 = Plugin.plugin (box {| directory = "/tmp/vibe"; client = mkClient () |}) |> Async.AwaitPromise
    let! p2 = p2
    do! (Dyn.get p2 "event") $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "same-text-ws" |} |} |}) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "same text new assistant turn nudges again" (promptCalls.Count = 2)
}

let reusedSessionSpec () = async {
    let mutable messages = [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
               parts = [| box {| ``type`` = "text"; text = "reopened work" |} |] |}
    |]
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () -> async {
                return box {| data = [| box {| id = "todo-1"; content = "task"; status = "in_progress" |} |] |}
            } |> Async.StartAsPromise))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () -> async { return box {| data = messages |} } |> Async.StartAsPromise))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg -> async { promptCalls.Add(arg) } |> Async.StartAsPromise))
        ]) ]
    let p = Plugin.plugin (box {| directory = "/tmp/vibe"; client = mkClient () |}) |> Async.AwaitPromise
    let! p = p
    let eventHook = Dyn.get p "event"
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "reused-session" |} |} |}) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    do! eventHook $ (box {| event = box {| ``type`` = "session.deleted"; properties = box {| info = box {| id = "reused-session" |} |} |} |}) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    messages <- [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
               parts = [| box {| ``type`` = "text"; text = "reopened work" |} |] |}
    |]
    let p2 = Plugin.plugin (box {| directory = "/tmp/vibe"; client = mkClient () |}) |> Async.AwaitPromise
    let! p2 = p2
    do! (Dyn.get p2 "event") $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "reused-session" |} |} |}) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "reused session nudges after session.deleted" (promptCalls.Count = 2)
}

let run () : JS.Promise<unit> = async {
    let reg = createRegistration (createObj [])
    do! eventHookSpec reg
    do! repeatedTodoNudgeSpec reg
    do! syntaxWrapperSpec reg
    do! todoWriteWrapperSpec reg
    let! p = Plugin.plugin (box {| directory = "/tmp/vibe" |}) |> Async.AwaitPromise
    do! toolExecuteAfterSpec p
    do! abortedRetrySpec ()
    do! repeatedAssistantSpec ()
    do! reusedSessionSpec ()
} |> Async.StartAsPromise
```

### `tests/IntegrationDedupTests.fs`

```fsharp
module VibeFs.Tests.IntegrationDedupTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dyn
open VibeFs.Index
open VibeFs.Opencode.Plugin

let private readMsg toolName output callId : obj =
    box {| parts = [| box {| ``type`` = "dynamic-tool"; toolName = toolName; state = "output-available"; output = output; toolCallId = callId |} |] |}

let private fileReadOutput content : obj =
    box {| success = true; file_size = String.length content; modifiedTime = "2024-01-01T00:00:00.000Z"; lines_read = 1; content = content |}

let private readMsgWithPath toolName filePath output callId : obj =
    box {| parts = [| box {| ``type`` = "dynamic-tool"; toolName = toolName; state = "output-available"
                             input = box {| path = filePath |}; output = output; toolCallId = callId |} |] |}

let dedupStringOutputSpec () =
    let msgs = [| readMsg "read" (box "same content") "1"; readMsg "file_read" (box "same content") "2" |]
    let r = deduplicateReadOutputs msgs
    check "dedup string: keeps first" (Dyn.str (unbox<obj[]> (Dyn.get r.[0] "parts")).[0] "output" = "same content")
    check "dedup string: replaces repeat" (Dyn.str (unbox<obj[]> (Dyn.get r.[1] "parts")).[0] "output" = "[No Change Since Previous Read/Write]")

let dedupObjectOutputSpec () =
    let msgs = [| readMsg "file_read" (fileReadOutput "hello") "1"; readMsg "file_read" (fileReadOutput "hello") "2" |]
    let r = deduplicateReadOutputs msgs
    let firstOutput = Dyn.get (unbox<obj[]> (Dyn.get r.[0] "parts")).[0] "output"
    check "dedup object: keeps first content" (Dyn.str firstOutput "content" = "hello")
    check "dedup object: replaces repeat" (Dyn.str (unbox<obj[]> (Dyn.get r.[1] "parts")).[0] "output" = "[No Change Since Previous Read/Write]")

let dedupPerPathDifferentSpec () =
    let shared = fileReadOutput "shared bytes"
    let msgs = [| readMsgWithPath "file_read" "a.ts" shared "1"; readMsgWithPath "file_read" "b.ts" shared "2" |]
    let r = deduplicateReadOutputs msgs
    check "dedup per-path: different path not deduped" (Dyn.str (Dyn.get (unbox<obj[]> (Dyn.get r.[1] "parts")).[0] "output") "content" = "shared bytes")

let dedupPerPathSameSpec () =
    let out = fileReadOutput "repeat me"
    let msgs = [| readMsgWithPath "file_read" "same.ts" out "1"; readMsgWithPath "file_read" "same.ts" out "2" |]
    let r = deduplicateReadOutputs msgs
    check "dedup same path: second marked" (Dyn.str (unbox<obj[]> (Dyn.get r.[1] "parts")).[0] "output" = "[No Change Since Previous Read/Write]")

let dedupDifferentSpec () =
    let msgs = [| readMsg "read" (box "unique a") "1"; readMsg "read" (box "unique b") "2" |]
    let r = deduplicateReadOutputs msgs
    check "dedup different: first unchanged" (Dyn.str (unbox<obj[]> (Dyn.get r.[0] "parts")).[0] "output" = "unique a")
    check "dedup different: second unchanged" (Dyn.str (unbox<obj[]> (Dyn.get r.[1] "parts")).[0] "output" = "unique b")

let dedupNonReadSpec () =
    let msgs = [|
        readMsg "read" (box "read content") "1"
        box {| parts = [| box {| ``type`` = "dynamic-tool"; toolName = "write"; state = "output-available"; output = box "write result"; toolCallId = "2" |} |] |}
    |]
    let r = deduplicateReadOutputs msgs
    check "dedup non-read: write preserved" (Dyn.str (unbox<obj[]> (Dyn.get r.[1] "parts")).[0] "output" = "write result")

let dedupEmptySpec () =
    let r = deduplicateReadOutputs [||]
    check "dedup empty: empty array" (r.Length = 0)

let collectReadOutputsSpec () =
    let seen = collectReadOutputs [| readMsg "read" (box "seen before") "h1" |]
    check "collect string: returns array" (seen.Length = 1 && seen.[0] = "seen before")
    let seenObj = collectReadOutputs [| readMsg "file_read" (fileReadOutput "historical") "h1" |]
    check "collect object: extracts content" (seenObj.Length = 1 && seenObj.[0] = "historical")

let dedupAgainstHistorySpec () =
    let history = [| readMsg "file_read" (fileReadOutput "from history") "h1" |]
    let window = [| readMsg "file_read" (fileReadOutput "from history") "w1" |]
    let r = deduplicateReadOutputsAgainstHistory history window
    check "againstHistory: repeat vs history marked" (Dyn.str (unbox<obj[]> (Dyn.get r.[0] "parts")).[0] "output" = "[No Change Since Previous Read/Write]")

let dedupModelSpec () =
    let seen, msgs = deduplicateModelReadOutputsWithSeen [||] [|
        box {| content = [| box {| ``type`` = "tool-result"; toolName = "read"; output = box {| ``type`` = "text"; value = box "hello" |} |} |] |}
        box {| content = [| box {| ``type`` = "tool-result"; toolName = "read"; output = box {| ``type`` = "text"; value = box "hello" |} |} |] |}
    |]
    check "ModelMessage: returns seen" (seen |> Array.contains "hello")
    let firstVal = Dyn.get (unbox<obj[]> (Dyn.get msgs.[0] "content")).[0] "output" |> Dyn.get "value"
    let secondVal = Dyn.get (unbox<obj[]> (Dyn.get msgs.[1] "content")).[0] "output"
    check "ModelMessage: first preserved" (Dyn.str firstVal "" = "" && unbox<string> firstVal = "hello")
    check "ModelMessage: second replaced" (Dyn.str secondVal "" = "" && unbox<string> secondVal = "[No Change Since Previous Read/Write]")

let opencodeDedupInPlaceSpec () = async {
    let! p = Plugin.plugin (box {| directory = "/tmp/vibe" |}) |> Async.AwaitPromise
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
    do! (Dyn.get p "experimental.chat.messages.transform") $ (box null, dedupInPlace) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let msgs = unbox<obj[]> dedupMessagesRef
    check "opencode dedup keeps messages array ref" (obj.ReferenceEquals(msgs, unbox<obj[]> (Dyn.get dedupInPlace "messages")))
    let partA = unbox<obj> msgs.[0] |> Dyn.get "parts" |> unbox<obj[]> |> Array.item 0
    let partB = unbox<obj> msgs.[1] |> Dyn.get "parts" |> unbox<obj[]> |> Array.item 0
    check "opencode dedup keeps first part ref" (obj.ReferenceEquals(partA, readPartA))
    check "opencode dedup keeps second part ref" (obj.ReferenceEquals(partB, readPartB))
    let stateA = Dyn.get partA "state"
    let stateB = Dyn.get partB "state"
    check "opencode dedup keeps first state ref" (obj.ReferenceEquals(stateA, readStateA))
    check "opencode dedup keeps second state ref" (obj.ReferenceEquals(stateB, readStateB))
    check "opencode dedup keeps first read output" (Dyn.str stateA "output" = stableContent)
    check "opencode dedup replaces exact duplicate" (Dyn.str stateB "output" = "[No Change Since Previous Read/Write]")
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
    do! (Dyn.get p "experimental.chat.messages.transform") $ (box null, dedupSuperset) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let supPart = unbox<obj> (unbox<obj[]> (Dyn.get dedupSuperset "messages")).[1] |> Dyn.get "parts" |> unbox<obj[]> |> Array.item 0
    let supState = Dyn.get supPart "state"
    check "opencode dedup superset keeps state ref" (obj.ReferenceEquals(supState, supersetState))
    check "opencode dedup superset not replaced" (Dyn.str supState "output" = supersetContent)
}

let run () : JS.Promise<unit> = async {
    dedupStringOutputSpec ()
    dedupObjectOutputSpec ()
    dedupPerPathDifferentSpec ()
    dedupPerPathSameSpec ()
    dedupDifferentSpec ()
    dedupNonReadSpec ()
    dedupEmptySpec ()
    collectReadOutputsSpec ()
    dedupAgainstHistorySpec ()
    dedupModelSpec ()
    do! opencodeDedupInPlaceSpec ()
} |> Async.StartAsPromise
```

### `tests/IntegrationToolTests.fs`

```fsharp
module VibeFs.Tests.IntegrationToolTests

open System.IO
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dyn
open VibeFs.Index
open VibeFs.Opencode.Plugin
open VibeFs.Opencode.AgentConfig

let wrapperSpec (reg: obj) =
    let wrappers = unbox<obj[]> (Dyn.get reg "wrappers")
    let ar = wrappers |> Array.find (fun w -> Dyn.str w "targetTool" = "agent_report")
    check "agent_report wrapper exists" (not (Dyn.isNullish ar))
    let ws = wrappers |> Array.find (fun w -> Dyn.str w "targetTool" = "web_search")
    let wsWrapped = (Dyn.get ws "wrapper") $ (null, createObj [ "cwd", box "/tmp"; "workspaceId", box "ws1" ])
    check "web_search wrapper has execute" (Dyn.typeIs (Dyn.get wsWrapped "execute") "function")

let buildCapsFileReadDataSpec () = async {
    let tmpDir = Path.Combine("/tmp", "caps-test-" + System.Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory tmpDir |> ignore
    File.WriteAllText (Path.Combine(tmpDir, "CAPS.md"), "# Capabilities\nTest content")
    let! entries = buildCapsFileReadData tmpDir |> Async.AwaitPromise
    check "buildCapsFileReadData finds caps file" (entries.Length = 1)
    check "caps entry has path" (entries.[0].path = "CAPS.md")
    check "caps entry callId prefix" (entries.[0].callId.StartsWith "caps-fr-")
    check "caps entry has modifiedTime" (entries.[0].output.modifiedTime <> "")
    check "caps entry output has content" (entries.[0].output.content.Contains "Test content")
    Directory.Delete(tmpDir, true)
}

let capsTransformSpec () = async {
    File.WriteAllText("/tmp/vibe/CAPS.md", "# Capabilities\nTest content")
    let! p = Plugin.plugin (box {| directory = "/tmp/vibe" |}) |> Async.AwaitPromise
    let transform = Dyn.get p "experimental.chat.messages.transform"
    let makeMessage info parts =
        box {| info = box ({| id = "msg-1"; sessionID = "sess-1"; agent = "manager" |} |> fun x ->
                              let o = createObj []; for k, v in [ "id", box (string x?agent); "agent", box x?agent; "sessionID", box "sess-1" ] do o?(k) <- v; o)
               parts = parts |}
    let originalMsg = box {| info = createObj [ "id", box "msg-1"; "agent", box "manager" ]; parts = [||] |}
    let noCapsOut = createObj [ "messages", box [| originalMsg |] ]
    do! transform $ (createObj [], noCapsOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "caps transform leaves messages when no caps" ((unbox<obj[]> (Dyn.get noCapsOut "messages")).Length = 1)
    let normalOut = createObj [ "messages", box [| originalMsg |] ]
    do! transform $ (createObj [], normalOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let msgs = unbox<obj[]> (Dyn.get normalOut "messages")
    check "caps transform injects two messages" (msgs.Length = 3)
    File.Delete "/tmp/vibe/CAPS.md"
}

let writeToolSpec (reg: obj) = async {
    let tools = unbox<obj[]> (Dyn.get reg "tools")
    let writeDef = tools |> Array.find (fun t -> Dyn.str t "name" = "write")
    let! missingPath = (Dyn.get writeDef "execute") $ (createObj [ "cwd", box "/tmp" ], createObj [ "content", box "x" ]) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "write missing file_path error" (missingPath.Contains "file_path")
    let tmpDir = Path.Combine("/tmp", "write-test-" + System.Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory tmpDir |> ignore
    let! writeResult = (Dyn.get writeDef "execute") $ (createObj [ "cwd", box tmpDir ], createObj [ "file_path", box "empty.txt"; "content", box "" ]) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "write empty string succeeds" (writeResult.Contains "Successfully wrote")
    Directory.Delete(tmpDir, true)
}

let loopCommandSpec (reg: obj) = async {
    let cmds = unbox<obj[]> (Dyn.get reg "slashCommands")
    let loopCmd = cmds |> Array.find (fun c -> Dyn.str c "key" = "loop")
    let! result = (Dyn.get loopCmd "execute") $ ("test-ws", "some task") |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "loop resolve includes task" (result.Contains "some task")
}

let agentConfigSpec () = async {
    let! p = Plugin.plugin (box {| directory = "/tmp/vibe" |}) |> Async.AwaitPromise
    let! cfg = (Dyn.get p "config") $ (box {|
        agent = box {|
            browser = box {| model = "kimi-for-coding/k2p7" |}
            executor = box {| model = "opencode-go/deepseek-v4-flash" |}
            custom = box {| model = "custom-model" |}
        |}
    |}) |> unbox<JS.Promise<obj>> |> Async.AwaitPromise
    let agents = Dyn.get cfg "agent"
    let browserAgent = Dyn.get agents "browser"
    check "browser system prompt empty" (Dyn.str browserAgent "prompt" = "")
    check "browser mode subagent" (Dyn.str browserAgent "mode" = "subagent")
    let executorAgent = Dyn.get agents "executor"
    check "executor mode subagent" (Dyn.str executorAgent "mode" = "subagent")
    let customAgent = Dyn.get agents "custom"
    check "custom model preserved" (Dyn.str customAgent "model" = "custom-model")
    let managerAgent = Dyn.get agents "manager"
    check "manager mode primary" (Dyn.str managerAgent "mode" = "primary")
}

let toolDefinitionSpec () = async {
    let! p = Plugin.plugin (box {| directory = "/tmp/vibe" |}) |> Async.AwaitPromise
    let td = Dyn.get p "tool.definition"
    let editorDefOut = createObj [ "parameters", box (createObj [
        "properties", box (createObj [ "intents", box (createObj [ "type", box "array" ]); "_ui", box (createObj [ "type", box "string" ]) ])
        "required", box [| "intents"; "_ui" |]
    ]) ]
    do! td $ (createObj [ "toolID", box "coder" ], editorDefOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let props = Dyn.get (Dyn.get editorDefOut "parameters") "properties"
    check "tool.definition strips editor _ui property" (Dyn.isNullish (Dyn.get props "_ui"))
}

let toolExecuteBeforeSpec () = async {
    let! p = Plugin.plugin (box {| directory = "/tmp/vibe" |}) |> Async.AwaitPromise
    let teb = Dyn.get p "tool.execute.before"
    let execOut = createObj [ "args", box (createObj [ "intents", box [| [| "fix bug"; [| "a.ts" |] |]; [| "add feature"; [| "b.ts" |] |] |] ]) ]
    do! teb $ (createObj [ "tool", box "coder"; "sessionID", box "s1"; "callID", box "c1" ], execOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "tool.execute.before populates _ui" (Dyn.str (Dyn.get execOut "args") "_ui" = "fix bug; add feature")
}

let run () : JS.Promise<unit> = async {
    let reg = createRegistration (createObj [])
    wrapperSpec reg
    do! buildCapsFileReadDataSpec ()
    do! capsTransformSpec ()
    do! writeToolSpec reg
    do! loopCommandSpec reg
    do! agentConfigSpec ()
    do! toolDefinitionSpec ()
    do! toolExecuteBeforeSpec ()
} |> Async.StartAsPromise
```

### `tests/IntegrationChatTests.fs`

```fsharp
module VibeFs.Tests.IntegrationChatTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dyn
open VibeFs.Opencode.Plugin
open VibeFs.Opencode.ChildAgent
open VibeFs.Opencode.Session

let chatMessageSpec () = async {
    let! p = Plugin.plugin (box {| directory = "/tmp/vibe" |}) |> Async.AwaitPromise
    let chatMsg = Dyn.get p "chat.message"
    let orchChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "stealth-browser-mcp_*", box true
        "write", box true
        "read", box true
    ]) ]) ]
    do! chatMsg $ (box {| sessionID = "root"; agent = "manager" |}, orchChat) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let tools = Dyn.get (Dyn.get orchChat "message") "tools"
    check "manager stealth disabled" (not (unbox<bool> (Dyn.get tools "stealth-browser-mcp_*")))
    check "manager write disabled" (not (unbox<bool> (Dyn.get tools "write")))
    check "manager read preserved" (unbox<bool> (Dyn.get tools "read"))
    let coderChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "stealth-browser-mcp_*", box true
        "patch", box true
    ]) ]) ]
    do! chatMsg $ (box {| sessionID = "root"; agent = "coder" |}, coderChat) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let coderTools = Dyn.get (Dyn.get coderChat "message") "tools"
    check "coder stealth disabled" (not (unbox<bool> (Dyn.get coderTools "stealth-browser-mcp_*")))
    check "coder patch preserved" (unbox<bool> (Dyn.get coderTools "patch"))
    let browserChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "stealth-browser-mcp_*", box true
        "read", box true
    ]) ]) ]
    do! chatMsg $ (box {| sessionID = "root"; agent = "browser" |}, browserChat) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let browserTools = Dyn.get (Dyn.get browserChat "message") "tools"
    check "browser stealth preserved" (unbox<bool> (Dyn.get browserTools "stealth-browser-mcp_*"))
}

let websearchBoundariesSpec () = async {
    let! p = Plugin.plugin (box {| directory = "/tmp/vibe" |}) |> Async.AwaitPromise
    let chatMsg = Dyn.get p "chat.message"
    let editorChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "websearch", box true
        "webfetch", box true
    ]) ]) ]
    do! chatMsg $ (box {| sessionID = "root"; agent = "coder" |}, editorChat) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let tools = Dyn.get (Dyn.get editorChat "message") "tools"
    check "editor websearch forced false" (not (unbox<bool> (Dyn.get tools "websearch")))
    check "editor webfetch forced false" (not (unbox<bool> (Dyn.get tools "webfetch")))
}

let subagentParentSpec () = async {
    let createCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                async { createCalls.Add(arg); return box {| data = box {| id = "child-session-123" |} |} } |> Async.StartAsPromise))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg -> async { promptCalls.Add(arg) } |> Async.StartAsPromise))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ -> async {
                return box {| data = [|
                    box {| info = box {| role = "user" |}; parts = [| box {| ``type`` = "text"; text = "navigate to example.com" |} |] |}
                    box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "Found the page title: Example Domain" |} |] |}
                |] |}
            } |> Async.StartAsPromise))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> async { () } |> Async.StartAsPromise))
        ]) ]
    let registry = ChildAgentRegistry.Create()
    let! result = runSubagent registry mockClient "browser" "Browser" "navigate to example.com" "/tmp/vibe" "parent-session-456" (createObj [ "abort", box null ]) null |> Async.AwaitPromise
    check "runSubagent returns string" (result.Contains "Example Domain")
    check "session.create received parentID" (Dyn.str (Dyn.get createCalls.[0] "body") "parentID" = "parent-session-456")
    check "session.prompt uses child id" (Dyn.str (Dyn.get promptCalls.[0] "path") "id" = "child-session-123")
}

let nestedSubagentSpec () = async {
    let createCalls = ResizeArray<obj>()
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                async { createCalls.Add(arg); return box {| data = box {| id = $"child-{createCalls.Count}" |} |} } |> Async.StartAsPromise))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun _ -> async { () } |> Async.StartAsPromise))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ -> async { return box {| data = [||] |} } |> Async.StartAsPromise))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> async { () } |> Async.StartAsPromise))
        ]) ]
    let registry = ChildAgentRegistry.Create()
    do! runSubagent registry mockClient "browser" "Browser" "first" "/tmp/vibe" "root-session" (createObj [ "abort", box null ]) null |> Async.AwaitPromise |> Async.Ignore
    do! runSubagent registry mockClient "coder" "Editor" "second" "/tmp/vibe" "child-1" (createObj [ "abort", box null ]) null |> Async.AwaitPromise |> Async.Ignore
    check "nested subagent resolves to root parent" (Dyn.str (Dyn.get createCalls.[1] "body") "parentID" = "root-session")
}

let run () : JS.Promise<unit> = async {
    do! chatMessageSpec ()
    do! websearchBoundariesSpec ()
    do! subagentParentSpec ()
    do! nestedSubagentSpec ()
} |> Async.StartAsPromise
```

### `vibe-fs.fsproj`

在 `tests/ResolveAiSettingsTests.fs` 之后、`tests/Tests.fs` 之前追加:

```xml
<Compile Include="tests/IntegrationPluginTests.fs" />
<Compile Include="tests/IntegrationEventTests.fs" />
<Compile Include="tests/IntegrationDedupTests.fs" />
<Compile Include="tests/IntegrationToolTests.fs" />
<Compile Include="tests/IntegrationChatTests.fs" />
```

### `package.json`

```json
"test": "node tests/runner.js"
```

## 关键技术点

**`obj.ReferenceEquals` vs F# `=`**: 引用相等回归保护必须用 `obj.ReferenceEquals`。F# `=` 对 `obj` 是递归值等，会把不同引用但字段相同的对象判为相等，断言失真。Fable 编译 `obj.ReferenceEquals` 为 `===`。

**mock 闭包**: `System.Func<obj, JS.Promise<'T>>(fun arg -> async { ... } |> Async.StartAsPromise)`，Fable 编译为 `(arg) => promise`。双参用 `System.Func<obj, obj, JS.Promise<'T>>`。

**`createObj` 构造 JS 形状**: `createObj [ "key", box value; ... ]`。匿名记录 `box {| ... |}` 用于已知结构的外传值。

**`Dyn` 动态互操作**: `Dyn.str` 取字符串，`Dyn.get` 取任意字段，`Dyn.isArray` 判断数组，`unbox<obj[]>` 转型数组，`unbox<JS.Promise<'t>>` 转型 Promise。

**`Async.AwaitPromise`**: F# 端等待 JS Promise 的唯一方式。`let! result = somePromise |> Async.AwaitPromise`。

**`Plugin.plugin`**: 源码中 `[<ExportDefault>] let private plugin`，Fable 编译后可通过模块名访问。F# 测试文件中用 `Plugin.plugin`。

## 验证

```bash
pnpm build     # 无 warning/error
pnpm test      # "==== N passed, 0 failed ====", exit 0
```

逐条:
1. `pnpm build` 编译无错
2. `pnpm test` exit 0
3. 改坏一条 `check`，确认 `pnpm test` exit 非 0
4. 改坏引用相等断言 (`opencode dedup keeps first part ref`)，确认失败
5. 删除 `tests/integration.mjs` 后 `pnpm test` 仍能跑通
6. `.mjs` 约 150+ 条 `check`，5 个 F# 文件 `check` 数应等于或略多
