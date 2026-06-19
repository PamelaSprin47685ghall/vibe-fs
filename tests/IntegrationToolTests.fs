module VibeFs.Tests.IntegrationToolTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Message
open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Shell.ChildAgentRegistry


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
    do! writeFileAsync (unbox<string> (pathModule?join(tmpDir, "AGENTS.md"))) "---\nimport:\n  - CAPS.md\n---\n" |> Async.AwaitPromise
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
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "AGENTS.md"))) "---\nimport:\n  - CAPS.md\n---\n" |> Async.AwaitPromise
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
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "AGENTS.md"))) "---\nimport:\n  - CAPS.md\n---\n" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    do! (get p "experimental.chat.messages.transform") $ (createObj [], freshOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "caps transform mutates array in place" (obj.ReferenceEquals(get freshOut "messages", freshRef))
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let capsAndMagicOrderSpec () = async {
    let! workspaceDir = mkdtempAsync "caps-magic-order-" |> Async.AwaitPromise
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "CAPS.md"))) "# Capabilities\nTest content" |> Async.AwaitPromise
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "AGENTS.md"))) "---\nimport:\n  - CAPS.md\n---\n" |> Async.AwaitPromise
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
    check "tool.definition builds todo report description" (str reportSchema "description" = VibeFs.Kernel.MagicTodo.reportDesc)
    check "tool.definition requires todo report" (required |> Array.contains "completedWorkReport")
    check "tool.definition requires todos" (required |> Array.contains "todos")
    check "tool.definition builds todos description" (str (get todoProps "todos") "description" = VibeFs.Kernel.MagicTodo.todosDesc)
    let todoItemProps = get (get (get todoProps "todos") "items") "properties"
    check "tool.definition builds todo content description" (str (get todoItemProps "content") "description" = VibeFs.Kernel.MagicTodo.todoContentDesc)
    check "tool.definition builds todo status description" (str (get todoItemProps "status") "description" = VibeFs.Kernel.MagicTodo.todoStatusDesc)
    check "tool.definition builds todo priority description" (str (get todoItemProps "priority") "description" = VibeFs.Kernel.MagicTodo.todoPriorityDesc)

    let! mimoP = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let mimoTd = get mimoP "tool.definition"
    let taskParams =
        createObj [
            "type", box "object"
            "properties", box (createObj [ "operation", box (createObj [ "type", box "object" ]) ])
            "required", box [| box "operation" |]
        ]
    let taskDef = createObj [ "description", box "native"; "parameters", box taskParams ]
    do! mimoTd $ (createObj [ "toolID", box "task" ], taskDef) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "mimo task.definition keeps parameters not jsonSchema" (isNullish (get taskDef "jsonSchema"))
    check "mimo task.definition fuses report into parameters" (not (isNullish (get (get (get taskDef "parameters") "properties") "completedWorkReport")))
    check "mimo task.definition removes host task_id from schema" (isNullish (get (get (get taskDef "parameters") "properties") "task_id"))
    check "mimo task.definition preserves operation" (not (isNullish (get (get (get taskDef "parameters") "properties") "operation")))

    let taskJsonSchema = createObj [
        "type", box "object"
        "properties", box (createObj [ "operation", box (createObj [ "type", box "object" ]); "task_id", box (createObj [ "type", box "string" ]) ])
        "required", box [| box "operation"; box "task_id" |]
    ]
    let taskJsonDef = createObj [ "description", box "native"; "jsonSchema", box taskJsonSchema ]
    do! mimoTd $ (createObj [ "toolID", box "task" ], taskJsonDef) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "mimo task.definition rewrites jsonSchema when that is the exposed path" (not (isNullish (get (get (get taskJsonDef "jsonSchema") "properties") "completedWorkReport")))
    check "mimo task.definition strips task_id from jsonSchema" (isNullish (get (get (get taskJsonDef "jsonSchema") "properties") "task_id"))
    let jsonRequired = unbox<obj[]> (get (get taskJsonDef "jsonSchema") "required") |> Array.map string
    check "mimo task.definition keeps operation required in jsonSchema" (jsonRequired |> Array.contains "operation")
    check "mimo task.definition drops task_id required in jsonSchema" (not (jsonRequired |> Array.contains "task_id"))
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let private sampleCoderIntent (objective: string) (file: string) : obj =
    createObj
        [ "objective", box objective
          "background", box "test background"
          "targets", box [| createObj [ "file", box file; "guide", box "test guide" ] |] ]

let private sampleCoderIntentWithDoNotTouch (objective: string) (file: string) (doNotTouch: string array) : obj =
    createObj
        [ "objective", box objective
          "background", box "test background"
          "do_not_touch", box doNotTouch
          "targets", box [| createObj [ "file", box file; "guide", box "test guide" ] |] ]

let private sampleInvestigatorIntent (objective: string) : obj =
    createObj
        [ "objective", box objective
          "background", box "test background"
          "questions", box [| box "What did you find?" |] ]

let toolExecuteBeforeSpec () = async {
    let! workspaceDir = mkdtempAsync "tool-execute-before-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let teb = get p "tool.execute.before"
    let intents : obj array = [|
        sampleCoderIntent "fix bug" "a.ts"
        sampleCoderIntent "add feature" "b.ts"
    |]
    let execOut = createObj [ "args", box (createObj [ "intents", box intents ]) ]
    do! teb $ (createObj [ "tool", box "coder"; "sessionID", box "s1"; "callID", box "c1" ], execOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "tool.execute.before populates _ui" (str (get execOut "args") "_ui" = "fix bug; add feature")
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let mimoApplyPatchExecuteBeforeSpec () = async {
    let! workspaceDir = mkdtempAsync "mimo-apply-patch-before-" |> Async.AwaitPromise
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let teb = get p "tool.execute.before"

    let stringArgsOut = createObj [ "args", box "*** Begin Patch\n*** End Patch" ]
    do! teb $ (createObj [ "tool", box "apply_patch"; "sessionID", box "s1"; "callID", box "c1" ], stringArgsOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "mimo apply_patch execute.before wraps string args" (str (get stringArgsOut "args") "patchText" = "*** Begin Patch\n*** End Patch")

    let patchArgsOut = createObj [ "args", box (createObj [ "patch", box "*** Begin Patch\n*** End Patch" ]) ]
    do! teb $ (createObj [ "tool", box "apply_patch"; "sessionID", box "s1"; "callID", box "c2" ], patchArgsOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "mimo apply_patch execute.before rewrites patch field" (str (get patchArgsOut "args") "patchText" = "*** Begin Patch\n*** End Patch")

    let correctArgsOut = createObj [ "args", box (createObj [ "patchText", box "already-correct" ]) ]
    do! teb $ (createObj [ "tool", box "apply_patch"; "sessionID", box "s1"; "callID", box "c3" ], correctArgsOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "mimo apply_patch execute.before preserves patchText" (str (get correctArgsOut "args") "patchText" = "already-correct")
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let mimoTaskExecuteRoundTripSpec () = async {
    let! workspaceDir = mkdtempAsync "mimo-task-before-after-" |> Async.AwaitPromise
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let teb = get p "tool.execute.before"
    let tea = get p "tool.execute.after"

    let operation = createObj [ "action", box "done"; "id", box "T1"; "event_summary", box "Finished parser fix" ]
    let originalArgs = createObj [ "operation", operation; "completedWorkReport", box "Detailed backlog report" ]
    let beforeOut = createObj [ "args", box originalArgs ]
    let hookInput = createObj [ "tool", box "task"; "sessionID", box "s1"; "callID", box "c1" ]

    do! teb $ (hookInput, beforeOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let sanitizedArgs = get beforeOut "args"
    check "mimo task execute.before keeps operation" (not (isNullish (get sanitizedArgs "operation")))
    check "mimo task execute.before strips report before host call" (isNullish (get sanitizedArgs "completedWorkReport"))

    let afterInput = createObj [ "tool", box "task"; "sessionID", box "s1"; "callID", box "c1"; "args", box sanitizedArgs ]
    let afterOut = createObj [ "output", box "ok" ]
    do! tea $ (afterInput, afterOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "mimo task execute.after restores report for backlog" (str (get afterInput "args") "completedWorkReport" = "Detailed backlog report")
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let mimoTaskExecuteNestedReportSpec () = async {
    let! workspaceDir = mkdtempAsync "mimo-task-nested-report-" |> Async.AwaitPromise
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let teb = get p "tool.execute.before"
    let tea = get p "tool.execute.after"

    let operation = createObj [ "action", box "create"; "summary", box "Build feature"; "completedWorkReport", box "Misplaced backlog report" ]
    let originalArgs = createObj [ "operation", operation ]
    let beforeOut = createObj [ "args", box originalArgs ]
    let hookInput = createObj [ "tool", box "task"; "sessionID", box "s1"; "callID", box "cn1" ]

    do! teb $ (hookInput, beforeOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let sanitizedArgs = get beforeOut "args"
    let sanitizedOperation = get sanitizedArgs "operation"
    check "mimo task execute.before keeps operation when report nested inside" (not (isNullish sanitizedOperation))
    check "mimo task execute.before keeps real operation fields" (str sanitizedOperation "summary" = "Build feature")
    check "mimo task execute.before strips report nested inside operation" (isNullish (get sanitizedOperation "completedWorkReport"))
    check "mimo task execute.before leaves no top-level report" (isNullish (get sanitizedArgs "completedWorkReport"))

    let afterInput = createObj [ "tool", box "task"; "sessionID", box "s1"; "callID", box "cn1"; "args", box sanitizedArgs ]
    let afterOut = createObj [ "output", box "ok" ]
    do! tea $ (afterInput, afterOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "mimo task execute.after restores nested report to top level for backlog" (str (get afterInput "args") "completedWorkReport" = "Misplaced backlog report")
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let mimoTaskExecuteInPlaceStripSpec () = async {
    let! workspaceDir = mkdtempAsync "mimo-task-inplace-" |> Async.AwaitPromise
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let teb = get p "tool.execute.before"

    let operation = createObj [ "action", box "create"; "summary", box "test task tool" ]
    let originalArgs = createObj [ "operation", operation; "completedWorkReport", box "top-level report text"; "task_id", box "T99" ]
    let beforeOut = createObj [ "args", box originalArgs ]
    let hookInput = createObj [ "tool", box "task"; "sessionID", box "s1"; "callID", box "ci1" ]

    do! teb $ (hookInput, beforeOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "mimo task strip mutates the original args reference in place" (isNullish (get originalArgs "completedWorkReport"))
    check "mimo task strip removes stray task_id on original args reference" (isNullish (get originalArgs "task_id"))
    check "mimo task strip preserves operation on original args reference" (not (isNullish (get originalArgs "operation")))

    let nestedOperation = createObj [ "action", box "create"; "summary", box "nested case"; "completedWorkReport", box "nested report text" ]
    let nestedArgs = createObj [ "operation", nestedOperation ]
    let nestedBeforeOut = createObj [ "args", box nestedArgs ]
    do! teb $ (createObj [ "tool", box "task"; "sessionID", box "s1"; "callID", box "ci2" ], nestedBeforeOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "mimo task strip mutates the original operation reference in place" (isNullish (get nestedOperation "completedWorkReport"))
    check "mimo task strip keeps real fields on original operation reference" (str nestedOperation "summary" = "nested case")
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let mimoTaskExecuteStripsTaskIdSpec () = async {
    let! workspaceDir = mkdtempAsync "mimo-task-strip-task-id-" |> Async.AwaitPromise
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let teb = get p "tool.execute.before"

    let operation = createObj [ "action", box "list" ]
    let originalArgs = createObj [ "operation", operation; "task_id", box "T4"; "completedWorkReport", box "noop report" ]
    let beforeOut = createObj [ "args", box originalArgs ]
    do! teb $ (createObj [ "tool", box "task"; "sessionID", box "s1"; "callID", box "ctid" ], beforeOut) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "mimo task execute.before strips task_id in place" (isNullish (get originalArgs "task_id"))
    check "mimo task execute.before keeps operation after task_id strip" (not (isNullish (get originalArgs "operation")))
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let mimoTaskDefinitionHandlesZodLikeParametersSpec () = async {
    let! workspaceDir = mkdtempAsync "mimo-task-zod-params-" |> Async.AwaitPromise
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let td = get p "tool.definition"

    let extendCalls = ResizeArray<obj>()
    let describeCalls = ResizeArray<string>()
    let optionalCalls = ResizeArray<string>()
    let summaryField = createObj [
        "describe", box (System.Func<obj, obj>(fun desc ->
            describeCalls.Add(string desc)
            createObj [
                "optional", box (System.Func<obj>(fun () ->
                    optionalCalls.Add("optional")
                    createObj [ "kind", box "optional-string"; "description", desc ]))
            ]))
    ]
    let zodLikeParams = createObj [
        "safeExtend", box (System.Func<obj, obj>(fun arg ->
            extendCalls.Add(arg)
            createObj [ "kind", box "extended" ]))
        "shape", box (createObj [
            "operation", box (createObj [
                "options", box [| createObj [ "shape", box (createObj [ "summary", box summaryField ]) ] |]
            ])
        ])
    ]
    let taskDef = createObj [ "description", box "native"; "parameters", box zodLikeParams ]

    do! td $ (createObj [ "toolID", box "task" ], taskDef) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "mimo task.definition rewrites zod-like parameters" (string (get (get taskDef "parameters") "kind") = "extended")
    check "mimo task.definition adds report field through safeExtend" (
        string (get (get extendCalls.[0] "completedWorkReport") "kind") = "optional-string"
        && string (get (get extendCalls.[0] "completedWorkReport") "description") = VibeFs.Kernel.MagicTodo.mimoReportFieldDesc)
    check "mimo task.definition derives report field from host zod schema" (
        describeCalls.Count = 1
        && describeCalls.[0] = VibeFs.Kernel.MagicTodo.mimoReportFieldDesc
        && optionalCalls.Count = 1)
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let investigatorToolMissingClientSpec () = async {
    let! workspaceDir = mkdtempAsync "investigator-missing-client-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let investigator = get (get p "tool") "investigator"
    let! result = (get investigator "execute") $ (createObj [ "intents", box [| sampleInvestigatorIntent "find investigator registration" |] ], createObj [ "directory", box workspaceDir; "sessionID", box "investigator-test" ]) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "investigator without client returns readable error" (result.Contains("ctx.client.session"))
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let investigatorToolSpec () = async {
    let createCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                (async { createCalls.Add(arg); return box {| data = box {| id = "child-investigator-session" |} |} } |> Async.StartAsPromise)))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (async { promptCalls.Add(arg) } |> Async.StartAsPromise)))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (async { return box {| data = [|
                    box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "Found src/Opencode/Tools.fs" |} |] |}
                |] |} } |> Async.StartAsPromise)))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ ->
                (async { () } |> Async.StartAsPromise)))
        ]) ]
    let! workspaceDir = mkdtempAsync "investigator-tool-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir; client = mockClient |}) |> Async.AwaitPromise
    let investigator = get (get p "tool") "investigator"
    let! result = (get investigator "execute") $ (createObj [ "intents", box [| sampleInvestigatorIntent "find investigator registration" |] ], createObj [ "directory", box workspaceDir; "sessionID", box "investigator-parent"; "abort", box null ]) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "investigator tool returns subagent output" (result.Contains("src/Opencode/Tools.fs"))
    check "investigator tool creates child session under parent" (str (get createCalls.[0] "body") "parentID" = "investigator-parent")
    check "investigator tool prompts child investigator agent" (str (get promptCalls.[0] "body") "agent" = "investigator")
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let coderToolSpec () = async {
    let createCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                (async { createCalls.Add(arg); return box {| data = box {| id = "child-coder-session" |} |} } |> Async.StartAsPromise)))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (async { promptCalls.Add(arg) } |> Async.StartAsPromise)))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (async { return box {| data = [|
                    box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "Coder finished" |} |] |}
                |] |} } |> Async.StartAsPromise)))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ ->
                (async { () } |> Async.StartAsPromise)))
        ]) ]
    let! workspaceDir = mkdtempAsync "coder-tool-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir; client = mockClient |}) |> Async.AwaitPromise
    let coder = get (get p "tool") "coder"
    let intents : obj array = [|
        sampleCoderIntentWithDoNotTouch "fix bug" "a.ts" [| "src/shared.fs"; "Do not rename public API" |]
        sampleCoderIntent "add feature" "b.ts"
    |]
    let! result = (get coder "execute") $ (createObj [ "intents", box intents ], createObj [ "directory", box workspaceDir; "sessionID", box "coder-parent"; "abort", box null ]) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "coder tool returns subagent output" (result.Contains("Coder finished"))
    check "coder tool creates one child per intent" (createCalls.Count = 2)
    check "coder tool prompts child coder agent" (str (get promptCalls.[0] "body") "agent" = "coder")
    let firstPrompt = str (unbox<obj[]> (get (get promptCalls.[0] "body") "parts")).[0] "text"
    let secondPrompt = str (unbox<obj[]> (get (get promptCalls.[1] "body") "parts")).[0] "text"
    check "coder prompt includes first intent do_not_touch" (firstPrompt.Contains("Do not touch:") && firstPrompt.Contains("src/shared.fs") && firstPrompt.Contains("Do not rename public API"))
    check "coder prompt omits do_not_touch section when absent" (not (secondPrompt.Contains("Do not touch:")))
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let investigatorToolLateClientInjectionSpec () = async {
    let createCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                (async { createCalls.Add(arg); return box {| data = box {| id = "child-investigator-session-late" |} |} } |> Async.StartAsPromise)))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (async { promptCalls.Add(arg) } |> Async.StartAsPromise)))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (async { return box {| data = [|
                    box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "Late client injection worked" |} |] |}
                |] |} } |> Async.StartAsPromise)))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ ->
                (async { () } |> Async.StartAsPromise)))
        ]) ]
    let! workspaceDir = mkdtempAsync "investigator-tool-late-client-" |> Async.AwaitPromise
    let ctx = createObj [ "directory", box workspaceDir ]
    let! p = plugin ctx |> Async.AwaitPromise
    ctx?("client") <- mockClient
    let investigator = get (get p "tool") "investigator"
    let! result = (get investigator "execute") $ (createObj [ "intents", box [| sampleInvestigatorIntent "find investigator registration" |] ], createObj [ "directory", box workspaceDir; "sessionID", box "investigator-parent-late"; "abort", box null ]) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "investigator tool sees client injected after plugin init" (result.Contains("Late client injection worked"))
    check "investigator tool late injection creates child session under parent" (str (get createCalls.[0] "body") "parentID" = "investigator-parent-late")
    check "investigator tool late injection prompts child investigator agent" (str (get promptCalls.[0] "body") "agent" = "investigator")
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
        do! mimoApplyPatchExecuteBeforeSpec ()
        do! mimoTaskExecuteRoundTripSpec ()
        do! mimoTaskExecuteNestedReportSpec ()
        do! mimoTaskExecuteInPlaceStripSpec ()
        do! mimoTaskExecuteStripsTaskIdSpec ()
        do! mimoTaskDefinitionHandlesZodLikeParametersSpec ()
        do! coderToolSpec ()
        do! investigatorToolSpec ()
        do! investigatorToolLateClientInjectionSpec ()
        do! executorActorSpec ()
    }
    |> Async.StartAsPromise
