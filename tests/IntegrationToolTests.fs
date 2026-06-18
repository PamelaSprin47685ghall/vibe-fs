module VibeFs.Tests.IntegrationToolTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Message
open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Opencode.Actors


[<Import("createRequire", "node:module")>]
let private createRequire' : string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta : obj = jsNative

let private requireFn : string -> obj = createRequire'(string importMeta?url)
let private fsAsync : obj = requireFn("fs")?promises
let private pathModule : obj = requireFn("path")

let private unlinkAsync (p: string) : JS.Promise<unit> =
    unbox (fsAsync?unlink(p))

let wrapperSpec (reg: obj) =
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let targets = wrappers |> Array.map (fun w -> str w "targetTool") |> Array.sort
    let expected = [| "agent_report"; "file_edit_insert"; "file_edit_replace_string"; "file_read"; "todo_write"; "web_fetch"; "web_search" |] |> Array.sort
    check "wrapper targets correct" (targets = expected)
    let ar = wrappers |> Array.find (fun w -> str w "targetTool" = "agent_report")
    check "agent_report wrapper exists" (not (isNullish ar))
    let ws = wrappers |> Array.find (fun w -> str w "targetTool" = "web_search")
    let wsWrapped = (get ws "wrapper") $ (null, createObj [ "cwd", box "/tmp"; "workspaceId", box "ws1" ])
    check "web_search wrapper has execute" (typeIs (get wsWrapped "execute") "function")

let computeCountSpec (reg: obj) =
    let tools = unbox<obj[]> (get reg "tools")
    let names = tools |> Array.map (fun t -> str t "name")
    check "has coder tool" (names |> Array.contains "coder")
    check "has webfetch tool" (names |> Array.contains "webfetch")
    check "has write tool" (names |> Array.contains "write")
    check "has read tool" (names |> Array.contains "read")
    check "has submit_review tool" (names |> Array.contains "submit_review")

let buildCapsFileReadDataSpec () = async {
    let! tmpDir = mkdtempAsync "caps-test-" |> Async.AwaitPromise
    do! writeFileAsync (unbox<string> (pathModule?join(tmpDir, "CAPS.md"))) "# Capabilities\nTest content" |> Async.AwaitPromise
    let! entries = buildCapsFileReadData tmpDir |> Async.AwaitPromise
    check "buildCapsFileReadData finds caps file" (entries.Length = 1)
    check "caps entry has path" (entries.[0].path = "CAPS.md")
    check "caps entry callId prefix" (entries.[0].callId.StartsWith "caps-fr-")
    check "caps entry output has content" (entries.[0].output.content.Contains "Test content")
    do! rmAsync tmpDir |> Async.AwaitPromise
}

let capsTransformSpec () = async {
    let! workspaceDir = mkdtempAsync "caps-transform-" |> Async.AwaitPromise
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "CAPS.md"))) "# Capabilities\nTest content" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let tf = get p "experimental.chat.messages.transform"
    let originalMsg =
        box {| info = createObj [ "id", box "msg-1"; "agent", box "manager" ]
               parts = [||] |}
    let out = createObj [ "messages", box [| originalMsg |] ]
    do! tf $ (createObj [], out) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let msgs = unbox<obj[]> (get out "messages")
    check "caps transform injects two messages" (msgs.Length = 3)
    check "caps transform preserves original" (obj.ReferenceEquals(msgs.[2], originalMsg))
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let capsTransformInPlaceSpec () = async {
    let! workspaceDir = mkdtempAsync "caps-in-place-" |> Async.AwaitPromise
    let freshOut = createObj [ "messages", box [| box {| info = createObj [ "id", box "msg-1"; "agent", box "manager" ]; parts = [||] |} |] ]
    let freshRef = get freshOut "messages"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "CAPS.md"))) "# Capabilities\nTest content" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    do! (get p "experimental.chat.messages.transform") $ (createObj [], freshOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "caps transform mutates array in place" (obj.ReferenceEquals(get freshOut "messages", freshRef))
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let capsAndMagicOrderSpec () = async {
    let! workspaceDir = mkdtempAsync "caps-magic-order-" |> Async.AwaitPromise
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "CAPS.md"))) "# Capabilities\nTest content" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let tf = get p "experimental.chat.messages.transform"
    let messages = createObj [ "messages", box [|
        box {| info = createObj [ "id", box "u1"; "role", box "user"; "sessionID", box "test" ]
               parts = [| box {| ``type`` = "text"; text = "start" |} |] |}
        box {| info = createObj [ "id", box "m1"; "role", box "assistant"; "sessionID", box "test"; "time", box (createObj [ "created", box 123; "completed", box 456 ]) ]
               parts = [| createObj [
                   "type", box "tool"
                   "tool", box "todowrite"
                   "callID", box "c1"
                   "state", box (createObj [
                       "status", box "completed"
                       "input", box (createObj [ "completedWorkReport", box "R1"; "todos", box [||] ])
                       "output", box "Todos updated."
                   ])
               ] |] |}
        box {| info = createObj [ "id", box "u2"; "role", box "user"; "sessionID", box "test" ]
               parts = [| box {| ``type`` = "text"; text = "please fix this bug" |} |] |}
        box {| info = createObj [ "id", box "m2"; "role", box "assistant"; "sessionID", box "test"; "time", box (createObj [ "created", box 789; "completed", box 790 ]) ]
               parts = [| createObj [
                   "type", box "tool"
                   "tool", box "todowrite"
                   "callID", box "c2"
                   "state", box (createObj [
                       "status", box "completed"
                       "input", box (createObj [ "completedWorkReport", box "R2"; "todos", box [||] ])
                       "output", box "Todos updated."
                   ])
               ] |] |}
        box {| info = createObj [ "id", box "m3"; "role", box "assistant"; "sessionID", box "test"; "time", box (createObj [ "created", box 791; "completed", box 792 ]) ]
               parts = [| createObj [
                   "type", box "tool"
                   "tool", box "todowrite"
                   "callID", box "c3"
                   "state", box (createObj [
                       "status", box "completed"
                       "input", box (createObj [ "completedWorkReport", box "R3"; "todos", box [||] ])
                       "output", box "Todos updated."
                   ])
               ] |] |}
    |] ]
    do! tf $ (createObj [], messages) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let result = unbox<obj[]> (get messages "messages")
    let capsParts = unbox<obj[]> (get result.[0] "parts")
    let capsAssistantInfo = get result.[1] "info"
    let magicInfo = get result.[2] "info"
    let magicId : string = str magicInfo "id"
    check "caps/magic order: caps user first" (str capsParts.[0] "text" = "你好")
    check "caps/magic order: caps assistant second" ((str capsAssistantInfo "id").StartsWith(capsSynthAssistantPrefix : string))
    check "caps/magic order: magic prefix third" (magicId.StartsWith(magicTodoPrefixPrefix : string))
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let writeToolSpec (reg: obj) = async {
    let tools = unbox<obj[]> (get reg "tools")
    let writeDef = tools |> Array.find (fun t -> str t "name" = "write")
    let! missingPath = (get writeDef "execute") $ (createObj [ "cwd", box "/tmp" ], createObj [ "content", box "x" ]) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "write missing file_path error" (missingPath.Contains "file_path")
    let! tmpDir = mkdtempAsync "write-test-" |> Async.AwaitPromise
    let! writeResult = (get writeDef "execute") $ (createObj [ "cwd", box tmpDir ], createObj [ "file_path", box "empty.txt"; "content", box "" ]) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "write empty string succeeds" (writeResult.Contains "Successfully wrote")
    do! rmAsync tmpDir |> Async.AwaitPromise
}

let loopCommandSpec (reg: obj) = async {
    let cmds = unbox<obj[]> (get reg "slashCommands")
    let loopCmd = cmds |> Array.find (fun c -> str c "key" = "loop")
    let! result = (get loopCmd "execute") $ ("test-ws", "some task") |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "loop resolve includes task" (result.Contains "some task")
}

let agentConfigSpec () = async {
    let! workspaceDir = mkdtempAsync "agent-config-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let cfgInput =
        box {|
            agent = box {|
                browser = box {| model = "kimi-for-coding/k2p7" |}
                executor = box {| model = "opencode-go/deepseek-v4-flash" |}
                custom = box {| model = "custom-model" |}
            |}
        |}
    let! cfg = (get p "config") $ cfgInput |> unbox<JS.Promise<obj>> |> Async.AwaitPromise
    let agents = get cfg "agent"
    let browser = get agents "browser"
    check "browser prompt empty" (str browser "prompt" = "")
    check "browser mode subagent" (str browser "mode" = "subagent")
    let executor = get agents "executor"
    check "executor mode subagent" (str executor "mode" = "subagent")
    let custom = get agents "custom"
    check "custom model preserved" (str custom "model" = "custom-model")
    let manager = get agents "manager"
    check "manager mode primary" (str manager "mode" = "primary")
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let toolDefinitionSpec () = async {
    let! workspaceDir = mkdtempAsync "tool-definition-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let td = get p "tool.definition"
    let coderDef = createObj [ "jsonSchema", box (createObj [
        "properties", box (createObj [ "intents", box (createObj [ "type", box "array" ]); "_ui", box (createObj [ "type", box "string" ]) ])
        "required", box [| "intents"; "_ui" |]
    ]) ]
    do! td $ (createObj [ "toolID", box "coder" ], coderDef) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let props = get (get coderDef "jsonSchema") "properties"
    check "tool.definition strips coder _ui property" (isNullish (get props "_ui"))
    check "tool.definition keeps coder intents" (not (isNullish (get props "intents")))

    let todoParams = createObj [ "__effectSchema", box true ]
    let todoDef = createObj [ "description", box "old desc"; "parameters", box todoParams ]
    do! td $ (createObj [ "toolID", box "todowrite" ], todoDef) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "tool.definition rewrites todo description" (str todoDef "description" |> fun text -> text.Contains "append-only work backlog")
    check "tool.definition leaves todo parameters untouched" (obj.ReferenceEquals(get todoDef "parameters", todoParams))
    let todoSchema = get todoDef "jsonSchema"
    let todoProps = get todoSchema "properties"
    let reportSchema = get todoProps "completedWorkReport"
    let required = unbox<obj[]> (get todoSchema "required") |> Array.map string
    check "tool.definition builds todo report field" (str reportSchema "type" = "string")
    check "tool.definition builds todo report description" (str reportSchema "description" = VibeFs.Opencode.MagicTodo.reportDesc)
    check "tool.definition requires todo report" (required |> Array.contains "completedWorkReport")
    check "tool.definition requires todos" (required |> Array.contains "todos")
    check "tool.definition builds todos description" (str (get todoProps "todos") "description" = VibeFs.Opencode.MagicTodo.todosDesc)
    let todoItemProps = get (get (get todoProps "todos") "items") "properties"
    check "tool.definition builds todo content description" (str (get todoItemProps "content") "description" = VibeFs.Opencode.MagicTodo.todoContentDesc)
    check "tool.definition builds todo status description" (str (get todoItemProps "status") "description" = VibeFs.Opencode.MagicTodo.todoStatusDesc)
    check "tool.definition builds todo priority description" (str (get todoItemProps "priority") "description" = VibeFs.Opencode.MagicTodo.todoPriorityDesc)
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let toolExecuteBeforeSpec () = async {
    let! workspaceDir = mkdtempAsync "tool-execute-before-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let teb = get p "tool.execute.before"
    let intents : obj array = [|
        box [| box "fix bug"; box [| "a.ts" |] |]
        box [| box "add feature"; box [| "b.ts" |] |]
    |]
    let execOut = createObj [ "args", box (createObj [ "intents", box intents ]) ]
    do! teb $ (createObj [ "tool", box "coder"; "sessionID", box "s1"; "callID", box "c1" ], execOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "tool.execute.before populates _ui" (str (get execOut "args") "_ui" = "fix bug; add feature")
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let executorActorSpec () = async {
    let seen = System.Collections.Generic.List<string>()
    let first = post "session-1" (fun () ->
        async {
            seen.Add "first-start"
            do! Async.Sleep 50
            seen.Add "first-end"
            return "one"
        } |> Async.StartAsPromise)
    let second = post "session-1" (fun () ->
        async {
            seen.Add "second-start"
            seen.Add "second-end"
            return "two"
        } |> Async.StartAsPromise)
    let! _ = first |> Async.AwaitPromise
    let! _ = second |> Async.AwaitPromise
    check "executor actor preserves order" (seen |> Seq.toArray = [| "first-start"; "first-end"; "second-start"; "second-end" |])
}

let run () : JS.Promise<unit> =
    async {
        let reg = createRegistration (createObj [])
        wrapperSpec reg
        computeCountSpec reg
        do! buildCapsFileReadDataSpec ()
        do! capsTransformSpec ()
        do! capsTransformInPlaceSpec ()
        do! capsAndMagicOrderSpec ()
        do! writeToolSpec reg
        do! loopCommandSpec reg
        do! agentConfigSpec ()
        do! toolDefinitionSpec ()
        do! toolExecuteBeforeSpec ()
        do! executorActorSpec ()
    }
    |> Async.StartAsPromise
