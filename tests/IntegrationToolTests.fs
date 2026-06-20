module VibeFs.Tests.IntegrationToolTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Message
open VibeFs.Kernel.Wiki
open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Opencode.WikiRuntime
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.WikiFiles


[<Import("createRequire", "node:module")>]
let private createRequire' : string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta : obj = jsNative

let private requireFn : string -> obj = createRequire'(string importMeta?url)
let private fsAsync : obj = requireFn("fs")?promises
let private pathModule : obj = requireFn("path")

let private unlinkAsync (p: string) : JS.Promise<unit> =
    unbox (fsAsync?unlink(p))

let private wikiEntry idStr q a : WikiEntry =
    match tryParseId idStr with
    | Some id -> { id = id; q = q; a = a }
    | None -> failwithf "invalid wiki id: %s" idStr

let private dayMs (date: string) : float =
    match date.Split('-') with
    | [| year; month; day |] ->
        System.DateTimeOffset(int year, int month, int day, 0, 0, 0, System.TimeSpan.Zero).ToUnixTimeMilliseconds() |> float
    | _ -> failwithf "invalid date: %s" date

let private wikiDraftEntry (id: string option) (q: string) (a: string) : obj =
    let fields =
        [ match id with
          | Some value -> yield "id", box value
          | None -> ()
          yield "q", box q
          yield "a", box a ]
    createObj fields

let private registerWikiJobForTest (wikiRuntime: obj) (sessionID: string) (workspaceRoot: string) (kindTag: string) (payload: obj) : unit =
    let registrar = get wikiRuntime "registerJobForTesting" |> unbox<System.Func<string, string, string, obj, unit>>
    registrar.Invoke(sessionID, workspaceRoot, kindTag, payload)

let private submitWikiTool (pluginObject: obj) : obj =
    get (get pluginObject "tool") "submit_wiki"

let private pluginWikiRuntime (pluginObject: obj) : obj =
    get pluginObject "__wikiRuntime"

let private takeBookkeeperLaunchesForTesting (pluginObject: obj) : obj array =
    let wikiRuntime = pluginWikiRuntime pluginObject
    let takeLaunches = get wikiRuntime "takeBookkeeperLaunchesForTesting"
    if typeIs takeLaunches "function" then
        unbox<obj[]> ((takeLaunches $ null))
    else
        [||]

let private waitForBackgroundJobsForTesting (pluginObject: obj) : JS.Promise<unit> =
    let wikiRuntime = pluginWikiRuntime pluginObject
    let waiter = get wikiRuntime "waitForBackgroundJobsForTesting"
    if typeIs waiter "function" then
        unbox<JS.Promise<unit>> ((waiter $ null))
    else
        async { () } |> Async.StartAsPromise

let private writeWikiFileAsync (filePath: string) (header: WikiHeader) (entries: WikiEntry list) : JS.Promise<unit> =
    writeFileAsync filePath (renderNdjson header entries)

let private assistantCompletionMessage (sessionID: string) (text: string) : obj =
    box {| info = createObj [ "id", box (sessionID + "-assistant"); "agent", box "manager"; "sessionID", box sessionID; "role", box "assistant"; "finish", box "stop"; "time", box (createObj [ "created", box 1; "completed", box 2 ]) ]
           parts = [| box {| ``type`` = "text"; text = text |} |] |}

let private managerSessionMessage (sessionID: string) : obj =
    box {| info = createObj [ "id", box (sessionID + "-manager"); "agent", box "manager"; "sessionID", box sessionID ]
           parts = [||] |}

let private bookkeeperMockClient (messages: obj array) : obj =
    createObj [
        "session",
        box (
            createObj [
                "messages",
                box (System.Func<obj, JS.Promise<obj>>(fun _ -> async { return box {| data = messages |} } |> Async.StartAsPromise))
                "todo",
                box (System.Func<unit, JS.Promise<obj>>(fun () -> async { return box {| data = [||] |} } |> Async.StartAsPromise))
                "prompt",
                box (System.Func<obj, JS.Promise<unit>>(fun _ -> async { () } |> Async.StartAsPromise))
                "create",
                box (System.Func<obj, JS.Promise<obj>>(fun _ -> async { return box {| data = box {| id = "child-bookkeeper-session" |} |} } |> Async.StartAsPromise))
                "abort",
                box (System.Func<obj, JS.Promise<unit>>(fun _ -> async { () } |> Async.StartAsPromise))
            ]
        )
    ]

let private executorDefinition (pluginObject: obj) : obj =
    get (get pluginObject "tool") "executor"

let private objectKeys (value: obj) : string array =
    JS.Constructors.Object.keys(value) |> Seq.toArray

let private executorSchema (pluginObject: obj) : obj =
    let definition = executorDefinition pluginObject
    let args = get definition "args"
    if not (isNullish args) then args else get definition "parameters"

let private executorModeSchema (pluginObject: obj) : obj =
    let schema = executorSchema pluginObject
    let direct = get schema "mode"
    if not (isNullish direct) then direct
    else
        let shape = get schema "shape"
        if not (isNullish shape) then get shape "mode"
        else
            let properties = get schema "properties"
            if isNullish properties then null else get properties "mode"

let private enumValues (modeSchema: obj) : string array =
    let candidates =
        [ get (get modeSchema "def") "entries"
          get modeSchema "enum"
          get modeSchema "options" ]
    candidates
    |> List.tryPick (fun candidate ->
        if isNullish candidate then None
        elif isArray candidate then Some (unbox<obj[]> candidate |> Array.map string)
        else
            let values = objectKeys candidate
            if values.Length = 0 then None else Some values)
    |> Option.defaultValue [||]

let private readAllWikiFiles (workspaceRoot: string) : JS.Promise<WikiFile list> =
    readWikiFiles workspaceRoot

let private readProjectionAsync (workspaceRoot: string) : JS.Promise<WikiProjection> =
    readProjection workspaceRoot

let wrapperSpec (reg: obj) =
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let targets = wrappers |> Array.map (fun w -> str w "targetTool") |> Array.sort
    let expected = [| "agent_report"; "file_edit_insert"; "file_edit_replace_string"; "file_read"; "todo_write" |] |> Array.sort
    check "wrapper targets correct" (targets = expected)
    let ar = wrappers |> Array.find (fun w -> str w "targetTool" = "agent_report")
    check "agent_report wrapper exists" (not (isNullish ar))

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
    check "caps/magic order: caps user first" ((str capsParts.[0] "text").StartsWith "你好")
    check "caps/magic order: caps assistant second" ((str capsAssistantInfo "id").StartsWith(capsSynthAssistantPrefix : string))
    check "caps/magic order: magic prefix third" (magicId.StartsWith(magicTodoPrefixPrefix : string))
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let titleAgentInputProjectionSpec () = async {
    let mkMsg () =
        box {| info = createObj [ "id", box "u1"; "agent", box "title"; "role", box "user"; "sessionID", box "title-session" ]
               parts = [| box {| ``type`` = "text"; text = "Generate a title for this chat" |} |] |}
    let checkPlugin (pluginObject: obj) = async {
        let transform = get pluginObject "experimental.chat.messages.transform"
        let output = createObj [ "messages", box [| mkMsg () |] ]
        let originalMsgs = unbox<obj[]> (get output "messages")
        let originalMsg = originalMsgs.[0]
        let originalParts = unbox<obj[]> (get originalMsg "parts")
        let originalPart = originalParts.[0]
        do! transform $ (createObj ["agent", box "title"], output) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
        let msgs = unbox<obj[]> (get output "messages")
        let parts = unbox<obj[]> (get msgs.[0] "parts")
        let wrappedText = str parts.[0] "text"
        check "title input starts with wrapper" (wrappedText.StartsWith "请给 input-data 中的需求命名。<input-data do-not-exec>")
        check "title input preserves payload" (wrappedText.Contains "Generate a title for this chat")
        check "title mutation in-place message" (obj.ReferenceEquals(msgs.[0], originalMsg))
        check "title mutation in-place parts array" (obj.ReferenceEquals(parts, originalParts))
        check "title mutation in-place part object" (obj.ReferenceEquals(parts.[0], originalPart))
    }
    let! workspaceDir = mkdtempAsync "title-input-projection-" |> Async.AwaitPromise
    let! opencodePlugin = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    do! checkPlugin opencodePlugin
    let! mimoPlugin = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    do! checkPlugin mimoPlugin
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let wikiPreludeWithoutCapsSpec () = async {
    let! workspaceDir = mkdtempAsync "wiki-prelude-" |> Async.AwaitPromise
    do! unbox<JS.Promise<unit>> (fsAsync?mkdir(pathModule?join(workspaceDir, "wiki"), box {| recursive = true |})) |> Async.AwaitPromise
    let snapshotFile = unbox<string> (pathModule?join(workspaceDir, "wiki", "snapshot.ndjson"))
    do! writeFileAsync snapshotFile (renderNdjson (SnapshotHeader(Some "2026-06-14")) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Opencode 主入口是 src/Opencode/Plugin.fs。" ]) |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let tf = get p "experimental.chat.messages.transform"
    let originalMsg = box {| info = createObj [ "id", box "msg-1"; "agent", box "manager"; "sessionID", box "wiki-session" ]; parts = [||] |}
    let out = createObj [ "messages", box [| originalMsg |] ]
    do! tf $ (createObj [], out) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let msgs = unbox<obj[]> (get out "messages")
    check "wiki prelude injects synthetic messages without caps" (msgs.Length = 3)
    let firstParts = unbox<obj[]> (get msgs.[0] "parts")
    let firstText = str firstParts.[0] "text"
    check "wiki prelude keeps hello prefix" (firstText.StartsWith "你好")
    check "wiki prelude includes history header" (firstText.Contains "[项目背景和历史]")
    check "wiki prelude includes question" (firstText.Contains "0a3f 项目插件入口在哪里？")
    check "wiki prelude hides answers" (not (firstText.Contains "src/Opencode/Plugin.fs"))
    check "wiki prelude preserves original message" (obj.ReferenceEquals(msgs.[2], originalMsg))
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let fetchWikiSnapshotSpec () = async {
    let! workspaceDir = mkdtempAsync "wiki-fetch-" |> Async.AwaitPromise
    do! unbox<JS.Promise<unit>> (fsAsync?mkdir(pathModule?join(workspaceDir, "wiki"), box {| recursive = true |})) |> Async.AwaitPromise
    let snapshotFile = unbox<string> (pathModule?join(workspaceDir, "wiki", "snapshot.ndjson"))
    do! writeFileAsync snapshotFile (renderNdjson (SnapshotHeader(Some "2026-06-14")) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Old answer" ]) |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let tf = get p "experimental.chat.messages.transform"
    let managerMsg = box {| info = createObj [ "id", box "msg-fetch"; "agent", box "manager"; "sessionID", box "wiki-fetch-session" ]; parts = [||] |}
    let out = createObj [ "messages", box [| managerMsg |] ]
    do! tf $ (createObj [], out) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    do! writeFileAsync snapshotFile (renderNdjson (SnapshotHeader(Some "2026-06-14")) [ wikiEntry "0a3f" "项目插件入口在哪里？" "New answer" ]) |> Async.AwaitPromise
    let tools = get p "tool"
    let fetchTool = get tools "fetch_wiki"
    let context = createObj [ "directory", box workspaceDir; "sessionID", box "wiki-fetch-session" ]
    let! oldAnswer = (get fetchTool "execute") $ (createObj [ "id", box "0a3f" ], context) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "fetch_wiki returns snapshot answer" (oldAnswer = "Old answer")
    let! invalidId = (get fetchTool "execute") $ (createObj [ "id", box "nope" ], context) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "fetch_wiki validates id format" (invalidId.Contains "Invalid wiki id")
    let! missing = (get fetchTool "execute") $ (createObj [ "id", box "b912" ], context) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "fetch_wiki reports missing snapshot entry" (missing.Contains "Wiki entry not found in this session snapshot")
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let directWriteTurnAggregationSpec () = async {
    let! workspaceDir = mkdtempAsync "direct-write-aggregation-" |> Async.AwaitPromise
    do! ensureWikiDir workspaceDir |> Async.AwaitPromise
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "turn-1" "Patched files" |]
    let! pluginObject = plugin (box {| directory = workspaceDir; client = mockClient |}) |> Async.AwaitPromise
    let toolExecuteAfter = get pluginObject "tool.execute.after"
    let eventHook = get pluginObject "event"

    let writeInput =
        createObj [ "tool", box "write"
                    "sessionID", box "turn-1"
                    "callID", box "write-call-1"
                    "args", box (createObj [ "file_path", box "src/turn.fs"; "content", box "let turn = 1" ]) ]
    let writeOutput = createObj [ "output", box "Successfully wrote to src/turn.fs" ]
    do! toolExecuteAfter $ (writeInput, writeOutput) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise

    let patchInput =
        createObj [ "tool", box "apply_patch"
                    "sessionID", box "turn-1"
                    "callID", box "patch-call-1"
                    "args", box (createObj [ "patchText", box "*** Begin Patch\n*** Update File: src/turn.fs\n@@\n-let turn = 0\n+let turn = 1\n*** End Patch" ]) ]
    let patchOutput = createObj [ "output", box "Applied patch to src/turn.fs" ]
    do! toolExecuteAfter $ (patchInput, patchOutput) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise

    let completionEvent =
        createObj [
            "event",
            box
                {| ``type`` = "message.updated"
                   properties = box
                       {| sessionID = "turn-1"
                          info = createObj [ "id", box "turn-1-assistant"; "agent", box "manager"; "sessionID", box "turn-1"; "role", box "assistant"; "finish", box "stop"; "time", box (createObj [ "created", box 1; "completed", box 2 ]) ]
                          parts = [| box {| ``type`` = "text"; text = "Patched files" |} |] |} |}
        ]
    do! eventHook $ completionEvent |> unbox<JS.Promise<unit>> |> Async.AwaitPromise

    do! waitForBackgroundJobsForTesting pluginObject |> Async.AwaitPromise
    let launches = takeBookkeeperLaunchesForTesting pluginObject
    check "direct write turn aggregation launches exactly one bookkeeper job" (launches.Length = 1)
    check "direct write turn aggregation uses bookkeeper agent" (str launches.[0] "agent" = "bookkeeper")
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let dailyMaintenanceLaunchSpec () = async {
    let! workspaceDir = mkdtempAsync "daily-maintenance-" |> Async.AwaitPromise
    do! ensureWikiDir workspaceDir |> Async.AwaitPromise
    let dayFilePath = dayPath workspaceDir "2026-06-18"
    do! writeWikiFileAsync dayFilePath (DayHeader("2026-06-18", false)) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Daily candidate" ] |> Async.AwaitPromise
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "daily-session" "Wiki prelude" |]
    let! pluginObject = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-20" |}) |> Async.AwaitPromise
    let transform = get pluginObject "experimental.chat.messages.transform"
    let messages = createObj [ "messages", box [| managerSessionMessage "daily-session" |] ]
    do! transform $ (createObj [], messages) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise

    let launches = takeBookkeeperLaunchesForTesting pluginObject
    check "daily maintenance schedules one launch" (launches.Length = 1)
    check "daily maintenance launch mentions daily rewrite" (
        let title = str launches.[0] "title"
        let prompt = str launches.[0] "prompt"
        title.ToLowerInvariant().Contains "daily" || prompt.ToLowerInvariant().Contains "daily" || prompt.ToLowerInvariant().Contains "rewrite")
    do! waitForBackgroundJobsForTesting pluginObject |> Async.AwaitPromise
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let weeklyMaintenanceLaunchSpec () = async {
    let! workspaceDir = mkdtempAsync "weekly-maintenance-" |> Async.AwaitPromise
    do! ensureWikiDir workspaceDir |> Async.AwaitPromise
    let snapshotFilePath = snapshotPath workspaceDir
    do! writeWikiFileAsync snapshotFilePath (SnapshotHeader(Some "2026-06-13")) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Snapshot baseline" ] |> Async.AwaitPromise
    do! writeWikiFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ wikiEntry "b912" "Magic Todo backlog 如何保存？" "Weekly candidate one" ] |> Async.AwaitPromise
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "weekly-session" "Wiki prelude" |]
    let! pluginObject = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-15" |}) |> Async.AwaitPromise
    let transform = get pluginObject "experimental.chat.messages.transform"
    let messages = createObj [ "messages", box [| managerSessionMessage "weekly-session" |] ]
    do! transform $ (createObj [], messages) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise

    let launches = takeBookkeeperLaunchesForTesting pluginObject
    check "weekly maintenance schedules at least one launch" (launches.Length >= 1)
    check "weekly maintenance launch mentions snapshot or weekly" (
        launches
        |> Array.exists (fun launch ->
            let title = (str launch "title").ToLowerInvariant()
            let prompt = (str launch "prompt").ToLowerInvariant()
            title.Contains "snapshot" || title.Contains "weekly" || prompt.Contains "snapshot" || prompt.Contains "weekly"))
    do! waitForBackgroundJobsForTesting pluginObject |> Async.AwaitPromise
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let weeklyMaintenanceUsesLastSundaySpec () = async {
    let! workspaceDir = mkdtempAsync "weekly-maintenance-sunday-" |> Async.AwaitPromise
    do! ensureWikiDir workspaceDir |> Async.AwaitPromise
    let snapshotFilePath = snapshotPath workspaceDir
    let snapshotThrough = "2026-06-07"
    do! writeWikiFileAsync snapshotFilePath (SnapshotHeader(Some snapshotThrough)) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Snapshot baseline" ] |> Async.AwaitPromise
    for day in [ "2026-06-08"; "2026-06-09"; "2026-06-10"; "2026-06-11"; "2026-06-12"; "2026-06-13"; "2026-06-14" ] do
        do! writeWikiFileAsync (dayPath workspaceDir day) (DayHeader(day, true)) [ wikiEntry "b912" ("周内问题 " + day) "Day entry" ] |> Async.AwaitPromise
    let lastSunday = "2026-06-14"
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "weekly-sunday-session" "Wiki prelude" |]
    let! pluginObject = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-15" |}) |> Async.AwaitPromise
    let transform = get pluginObject "experimental.chat.messages.transform"
    let messages = createObj [ "messages", box [| managerSessionMessage "weekly-sunday-session" |] ]
    do! transform $ (createObj [], messages) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise

    do! waitForBackgroundJobsForTesting pluginObject |> Async.AwaitPromise
    let launches = takeBookkeeperLaunchesForTesting pluginObject
    check "weekly maintenance lastSunday schedules exactly one weekly launch" (launches.Length = 1)
    let launch = launches.[0]
    let title = str launch "title"
    let prompt = str launch "prompt"
    check "weekly maintenance launch references lastSunday cutoff" (title.Contains lastSunday || prompt.Contains lastSunday)
    check "weekly maintenance launch does not reference old snapshot through" (not (title.Contains snapshotThrough) && not (prompt.Contains snapshotThrough))
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let weeklyMaintenanceWithoutSnapshotFileSpec () = async {
    let! workspaceDir = mkdtempAsync "weekly-maintenance-no-snapshot-" |> Async.AwaitPromise
    do! ensureWikiDir workspaceDir |> Async.AwaitPromise
    do! ensureTodayFile workspaceDir "2026-06-15" |> Async.AwaitPromise
    do! rewriteDay workspaceDir "2026-06-10" [ wikiEntry "0a3f" "周初问题" "Day 10 entry" ] |> Async.AwaitPromise
    do! rewriteDay workspaceDir "2026-06-12" [ wikiEntry "b912" "周中问题" "Day 12 entry" ] |> Async.AwaitPromise
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "weekly-no-snapshot-session" "Wiki prelude" |]
    let! pluginObject = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-15" |}) |> Async.AwaitPromise
    let transform = get pluginObject "experimental.chat.messages.transform"
    let messages = createObj [ "messages", box [| managerSessionMessage "weekly-no-snapshot-session" |] ]
    do! transform $ (createObj [], messages) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise

    let launches = takeBookkeeperLaunchesForTesting pluginObject
    check "weekly maintenance without snapshot file schedules at least one launch" (launches.Length >= 1)
    check "weekly maintenance without snapshot file mentions snapshot or weekly" (
        launches
        |> Array.exists (fun launch ->
            let title = (str launch "title").ToLowerInvariant()
            let prompt = (str launch "prompt").ToLowerInvariant()
            title.Contains "snapshot" || title.Contains "weekly" || prompt.Contains "snapshot" || prompt.Contains "weekly"))
    do! waitForBackgroundJobsForTesting pluginObject |> Async.AwaitPromise
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let directPatchWriteAggregationSpec () = async {
    let! workspaceDir = mkdtempAsync "direct-patch-aggregation-" |> Async.AwaitPromise
    do! ensureWikiDir workspaceDir |> Async.AwaitPromise
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "patch-turn-1" "Patched via patch tool" |]
    let! pluginObject = plugin (box {| directory = workspaceDir; client = mockClient |}) |> Async.AwaitPromise
    let toolExecuteAfter = get pluginObject "tool.execute.after"
    let eventHook = get pluginObject "event"

    let patchInput =
        createObj [ "tool", box "patch"
                    "sessionID", box "patch-turn-1"
                    "callID", box "patch-call-1"
                    "args", box (createObj [ "patchText", box "*** Begin Patch\n*** Update File: src/patch.fs\n@@\n-let x = 0\n+let x = 1\n*** End Patch" ]) ]
    let patchOutput = createObj [ "output", box "Applied patch to src/patch.fs" ]
    do! toolExecuteAfter $ (patchInput, patchOutput) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise

    let completionEvent =
        createObj [
            "event",
            box
                {| ``type`` = "message.updated"
                   properties = box
                       {| sessionID = "patch-turn-1"
                          info = createObj [ "id", box "patch-turn-1-assistant"; "agent", box "manager"; "sessionID", box "patch-turn-1"; "role", box "assistant"; "finish", box "stop"; "time", box (createObj [ "created", box 1; "completed", box 2 ]) ]
                          parts = [| box {| ``type`` = "text"; text = "Patched via patch tool" |} |] |} |}
        ]
    do! eventHook $ completionEvent |> unbox<JS.Promise<unit>> |> Async.AwaitPromise

    do! waitForBackgroundJobsForTesting pluginObject |> Async.AwaitPromise
    let launches = takeBookkeeperLaunchesForTesting pluginObject
    check "direct patch tool triggers one bookkeeper launch after completion" (launches.Length = 1)
    check "direct patch bookkeeper launch uses bookkeeper agent" (str launches.[0] "agent" = "bookkeeper")
    check "direct patch turn result records patch summary" (
        let prompt = str launches.[0] "prompt"
        prompt.Contains "patch" && prompt.Contains "src/patch.fs")
    do! rmAsync workspaceDir |> Async.AwaitPromise
}



let submitWikiAppendSpec () = async {
    let! workspaceDir = mkdtempAsync "submit-wiki-append-" |> Async.AwaitPromise
    do! ensureWikiDir workspaceDir |> Async.AwaitPromise
    let snapshotPath = unbox<string> (pathModule?join(workspaceDir, "wiki", "snapshot.ndjson"))
    do! writeFileAsync snapshotPath (renderNdjson (SnapshotHeader(Some "2026-06-14")) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Old answer" ]) |> Async.AwaitPromise
    let appendDay = "2026-06-20"
    let! p = plugin (box {| directory = workspaceDir; nowMs = dayMs appendDay |}) |> Async.AwaitPromise
    registerWikiJobForTest (pluginWikiRuntime p) "wiki-job-append" workspaceDir "append" (createObj [ "today", box appendDay ])
    let submitTool = submitWikiTool p
    let entries =
        [|
            wikiDraftEntry (Some "0a3f") "项目插件入口在哪里？" "Updated answer"
            wikiDraftEntry None "新知识？" "Fresh answer"
            wikiDraftEntry (Some "ffff") "未知旧 id" "Should become new id"
        |]
    let! result =
        ((get submitTool "execute")
            $ (createObj [ "entries", box entries ], createObj [ "directory", box workspaceDir; "sessionID", box "wiki-job-append" ]))
        |> unbox<JS.Promise<string>>
        |> Async.AwaitPromise
    check "submit_wiki append returns a response" (result <> "")

    let! projection = readProjectionAsync workspaceDir |> Async.AwaitPromise
    let updated = Map.find (tryParseId "0a3f" |> Option.get) projection
    check "submit_wiki append updates existing id" (updated.a = "Updated answer")
    let fresh = projection |> Map.toList |> List.tryFind (fun (_, entry) -> entry.q = "新知识？")
    check "submit_wiki append allocates id for new entry" (
        match fresh with
        | Some (id, entry) -> idValue id <> "" && entry.a = "Fresh answer"
        | None -> false)
    let remapped = projection |> Map.toList |> List.tryFind (fun (_, entry) -> entry.q = "未知旧 id")
    check "submit_wiki append does not keep unknown id" (
        match remapped with
        | Some (id, entry) -> idValue id <> "ffff" && entry.a = "Should become new id"
        | None -> false)

    let! files = readAllWikiFiles workspaceDir |> Async.AwaitPromise
    let dayFile = files |> List.tryFind (fun file -> match file.header with DayHeader(date, _) -> date = appendDay | _ -> false)
    check "submit_wiki append creates today file" (dayFile.IsSome)
    match dayFile with
    | Some file -> check "submit_wiki append keeps day file unrewritten" (match file.header with DayHeader(_, rewritten) -> not rewritten | _ -> false)
    | None -> ()
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let submitWikiAppendEmptySpec () = async {
    let! workspaceDir = mkdtempAsync "submit-wiki-empty-" |> Async.AwaitPromise
    do! ensureWikiDir workspaceDir |> Async.AwaitPromise
    let appendDay = "2026-06-20"
    let! p = plugin (box {| directory = workspaceDir; nowMs = dayMs appendDay |}) |> Async.AwaitPromise
    registerWikiJobForTest (pluginWikiRuntime p) "wiki-job-empty" workspaceDir "append" (createObj [ "today", box appendDay ])
    let submitTool = submitWikiTool p
    let! result =
        ((get submitTool "execute")
            $ (createObj [ "entries", box [||] ], createObj [ "directory", box workspaceDir; "sessionID", box "wiki-job-empty" ]))
        |> unbox<JS.Promise<string>>
        |> Async.AwaitPromise
    check "submit_wiki empty array returns a response" (result <> "")

    let! files = readAllWikiFiles workspaceDir |> Async.AwaitPromise
    let dayFile = files |> List.tryFind (fun file -> match file.header with DayHeader(date, _) -> date = appendDay | _ -> false)
    check "submit_wiki empty array does not create day file" (dayFile.IsNone)
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let submitWikiDailyRewriteSpec () = async {
    let! workspaceDir = mkdtempAsync "submit-wiki-daily-" |> Async.AwaitPromise
    do! ensureWikiDir workspaceDir |> Async.AwaitPromise
    let dayFilePath = dayPath workspaceDir "2026-06-18"
    let dayFileContent =
        renderNdjson (DayHeader("2026-06-18", false)) [
            wikiEntry "0a3f" "项目插件入口在哪里？" "Old answer"
            wikiEntry "b912" "Magic Todo backlog 如何保存？" "Old backlog answer"
        ]
    do! writeFileAsync dayFilePath dayFileContent |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    registerWikiJobForTest (pluginWikiRuntime p) "wiki-job-day" workspaceDir "daily" (createObj [ "date", box "2026-06-18" ])
    let submitTool = submitWikiTool p
    let! result =
        ((get submitTool "execute")
            $ (createObj [ "entries", box [| wikiDraftEntry None "合并后问题" "Canonical answer" |] ], createObj [ "directory", box workspaceDir; "sessionID", box "wiki-job-day" ]))
        |> unbox<JS.Promise<string>>
        |> Async.AwaitPromise
    check "submit_wiki daily rewrite returns a response" (result <> "")

    let! files = readAllWikiFiles workspaceDir |> Async.AwaitPromise
    let dayFile = files |> List.find (fun file -> match file.header with DayHeader(date, _) -> date = "2026-06-18" | _ -> false)
    check "submit_wiki daily rewrite flips rewritten header" (match dayFile.header with DayHeader(date, rewritten) -> date = "2026-06-18" && rewritten | _ -> false)
    check "submit_wiki daily rewrite replaces entries" (dayFile.entries.Length = 1 && dayFile.entries.Head.q = "合并后问题" && dayFile.entries.Head.a = "Canonical answer")
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let submitWikiWeeklyRewriteSpec () = async {
    let! workspaceDir = mkdtempAsync "submit-wiki-weekly-" |> Async.AwaitPromise
    do! ensureWikiDir workspaceDir |> Async.AwaitPromise
    let snapshotFilePath = snapshotPath workspaceDir
    let snapshotFileContent =
        renderNdjson (SnapshotHeader(Some "2026-06-14")) [
            wikiEntry "0a3f" "保留问题" "Old snapshot answer"
        ]
    let dayFilePathOne = dayPath workspaceDir "2026-06-15"
    let dayFileContentOne =
        renderNdjson (DayHeader("2026-06-15", false)) [
            wikiEntry "b912" "周内问题一" "Day 1"
        ]
    let dayFilePathTwo = dayPath workspaceDir "2026-06-16"
    let dayFileContentTwo =
        renderNdjson (DayHeader("2026-06-16", false)) [
            wikiEntry "c001" "周内问题二" "Day 2"
        ]
    do! writeFileAsync snapshotFilePath snapshotFileContent |> Async.AwaitPromise
    do! writeFileAsync dayFilePathOne dayFileContentOne |> Async.AwaitPromise
    do! writeFileAsync dayFilePathTwo dayFileContentTwo |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    registerWikiJobForTest (pluginWikiRuntime p) "wiki-job-week" workspaceDir "weekly" (createObj [ "through", box "2026-06-16" ])
    let submitTool = submitWikiTool p
    let! result =
        ((get submitTool "execute")
            $ (createObj [ "entries", box [|
                wikiDraftEntry (Some "0a3f") "保留问题" "Merged answer"
                wikiDraftEntry None "新增周知识" "New weekly answer"
            |] ], createObj [ "directory", box workspaceDir; "sessionID", box "wiki-job-week" ]))
        |> unbox<JS.Promise<string>>
        |> Async.AwaitPromise
    check "submit_wiki weekly rewrite returns a response" (result <> "")

    let! files = readAllWikiFiles workspaceDir |> Async.AwaitPromise
    let snapshot = files |> List.find (fun file -> match file.header with SnapshotHeader _ -> true | _ -> false)
    check "submit_wiki weekly rewrite updates snapshot cutoff" (match snapshot.header with SnapshotHeader(Some through) -> through = "2026-06-16" | _ -> false)
    check "submit_wiki weekly rewrite keeps merged answer" (snapshot.entries |> List.exists (fun entry -> entry.q = "保留问题" && entry.a = "Merged answer"))
    let newWeekly = snapshot.entries |> List.tryFind (fun entry -> entry.q = "新增周知识")
    check "submit_wiki weekly rewrite allocates new id" (
        match newWeekly with
        | Some entry -> idValue entry.id <> ""
        | None -> false)
    let! dayFiles = listDayFiles workspaceDir |> Async.AwaitPromise
    check "submit_wiki weekly rewrite deletes cutoff day files" (not (dayFiles |> List.contains "2026-06-15") && not (dayFiles |> List.contains "2026-06-16"))
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

let bookkeeperAgentConfigSpec () = async {
    let! workspaceDir = mkdtempAsync "bookkeeper-agent-config-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let! cfg = (get p "config") $ (createObj []) |> unbox<JS.Promise<obj>> |> Async.AwaitPromise
    let agents = get cfg "agent"
    check "bookkeeper agent exists" (not (isNullish (get agents "bookkeeper")))
    check "bookkeeper agent mode subagent" (str (get agents "bookkeeper") "mode" = "subagent")
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let executorModeSchemaSpec () = async {
    let! workspaceDir = mkdtempAsync "executor-mode-schema-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let modeSchema = executorModeSchema p
    check "executor mode schema exists" (not (isNullish modeSchema))
    check "executor mode schema exposes mode" (not (isNullish modeSchema))
    check "executor mode schema enum ro/rw" (enumValues modeSchema = [| "ro"; "rw" |])
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let coderTriggersBookkeeperSpec () = async {
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
    let! workspaceDir = mkdtempAsync "coder-bookkeeper-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir; client = mockClient |}) |> Async.AwaitPromise
    let coder = get (get p "tool") "coder"
    let intents : obj array = [|
        createObj [
            "objective", box "fix bug"
            "background", box "test background"
            "targets", box [| createObj [ "file", box "a.ts"; "guide", box "test guide" ] |]
        ]
    |]
    let! result = (get coder "execute") $ (createObj [ "intents", box intents ], createObj [ "directory", box workspaceDir; "sessionID", box "coder-parent"; "abort", box null ]) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "coder tool returns subagent output" (result.Contains("Coder finished"))
    let launches = takeBookkeeperLaunchesForTesting p
    check "coder tool still launches bookkeeper hook" (launches.Length = 1)
    check "coder bookkeeper launch agent" (str launches.[0] "agent" = "bookkeeper")
    check "coder bookkeeper launch records prompt" (not (isNullish (get launches.[0] "prompt")) && str launches.[0] "prompt" <> "")
    do! waitForBackgroundJobsForTesting p |> Async.AwaitPromise
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let bookkeeperFireAndForgetSpec () = async {
    let promptCompleted = ResizeArray<bool>()
    let mockClient =
        createObj [
            "session", box (createObj [
                "create", box (System.Func<obj, JS.Promise<obj>>(fun _ -> (async { return box {| data = box {| id = "child-ff-session" |} |} } |> Async.StartAsPromise)))
                "prompt", box (System.Func<obj, JS.Promise<unit>>(fun _ -> (async { promptCompleted.Add(true) } |> Async.StartAsPromise)))
                "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ -> (async {
                    let msg = box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "done" |} |] |}
                    return box {| data = [| msg |] |}
                } |> Async.StartAsPromise)))
                "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> (async { () } |> Async.StartAsPromise)))
            ])
        ]
    let! workspaceDir = mkdtempAsync "bookkeeper-fireforget-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir; client = mockClient |}) |> Async.AwaitPromise
    let coder = get (get p "tool") "coder"
    let intents : obj array = [|
        createObj [
            "objective", box "do work"
            "background", box "bg"
            "targets", box [| createObj [ "file", box "a.ts"; "guide", box "g" ] |]
        ]
    |]
    let! result = (get coder "execute") $ (createObj [ "intents", box intents ], createObj [ "directory", box workspaceDir; "sessionID", box "ff-parent"; "abort", box null ]) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "fire-and-forget: coder returns result without awaiting bookkeeper completion" (result.Contains("done"))
    let launches = takeBookkeeperLaunchesForTesting p
    check "fire-and-forget: bookkeeper launch recorded" (launches.Length = 1)
    do! waitForBackgroundJobsForTesting p |> Async.AwaitPromise
    check "fire-and-forget: bookkeeper prompt ran in background after main returned" (promptCompleted.Count >= 1)
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let executorRoRwBookkeeperSpec () = async {
    let! workspaceDir = mkdtempAsync "executor-bookkeeper-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let executor = executorDefinition p
    check "executor tool exposes mode" (not (isNullish (executorModeSchema p)))
    let runExecutor mode =
        (get executor "execute") $
            (createObj [ "language", box "shell"; "program", box "printf ok"; "timeout_type", box "short"; "mode", box mode ],
             createObj [ "directory", box workspaceDir; "sessionID", box "executor-session" ])
        |> unbox<JS.Promise<string>>
    let! roResult = runExecutor "ro" |> Async.AwaitPromise
    check "executor ro returns output" (roResult <> "")
    check "executor ro does not launch bookkeeper" (takeBookkeeperLaunchesForTesting p |> Array.isEmpty)
    let! rwResult = runExecutor "rw" |> Async.AwaitPromise
    check "executor rw returns output" (rwResult <> "")
    let launches = takeBookkeeperLaunchesForTesting p
    check "executor rw launches bookkeeper once" (launches.Length = 1)
    check "executor rw launch agent" (str launches.[0] "agent" = "bookkeeper")
    do! waitForBackgroundJobsForTesting p |> Async.AwaitPromise
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
    let coderCreates =
        createCalls
        |> Seq.filter (fun call -> str (get call "body") "parentID" = "coder-parent")
        |> Seq.toArray
    check "coder tool creates one child per intent" (coderCreates.Length = 2)
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
    let releaseRequested = ref false
    let gateResolve = ref (fun () -> ())
    let gateAsync =
        Async.FromContinuations(fun (resolve, _, _) ->
            gateResolve.Value <- resolve
            if releaseRequested.Value then resolve ())
    let first = post "session-1" (fun () ->
        async {
            seen.Add "first-start"
            do! gateAsync
            seen.Add "first-end"
            return "one"
        } |> Async.StartAsPromise)
    let second = post "session-1" (fun () ->
        async {
            seen.Add "second-start"
            seen.Add "second-end"
            return "two"
        } |> Async.StartAsPromise)
    releaseRequested.Value <- true
    gateResolve.Value ()
    let! _ = first |> Async.AwaitPromise
    let! _ = second |> Async.AwaitPromise
    check "executor actor preserves order" (seen |> Seq.toArray = [| "first-start"; "first-end"; "second-start"; "second-end" |])
}

let wikiActorSpec () = async {
    let seen = System.Collections.Generic.List<string>()
    let actor = VibeFs.Opencode.WikiRuntime.WikiActor()
    let releaseRequested = ref false
    let gateResolve = ref (fun () -> ())
    let gateAsync =
        Async.FromContinuations(fun (resolve, _, _) ->
            gateResolve.Value <- resolve
            if releaseRequested.Value then resolve ())
    actor.Post("ws-1", fun () -> async {
        seen.Add "first-start"
        do! gateAsync
        seen.Add "first-end"
    })
    actor.Post("ws-1", fun () -> async {
        seen.Add "second-start"
        seen.Add "second-end"
    })
    releaseRequested.Value <- true
    gateResolve.Value ()
    let! _ = actor.Run("ws-1", fun () -> async { return "" }) |> Async.AwaitPromise
    check "wiki actor preserves order" (seen |> Seq.toArray = [| "first-start"; "first-end"; "second-start"; "second-end" |])
}

let jobContextCleanupOnAbortOrDeleteSpec () = async {
    let! workspaceDir = mkdtempAsync "job-cleanup-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let wikiRuntime = get (pluginWikiRuntime p) "rawInstance" :?> WikiRuntime

    wikiRuntime.RegisterJob("session-to-abort", { workspaceRoot = workspaceDir; kind = AppendAfterWork })
    check "job exists initially" (wikiRuntime.TakeJob("session-to-abort").IsSome)

    let eventHandler = get p "event" :?> System.Func<obj, JS.Promise<unit>>
    let abortEvent =
        box {|
            event = box {|
                ``type`` = box "stream-abort"
                properties = box {|
                    sessionID = box "session-to-abort"
                |}
            |}
        |}
    do! eventHandler.Invoke(abortEvent) |> Async.AwaitPromise
    check "job is removed after stream-abort" (wikiRuntime.TakeJob("session-to-abort").IsNone)

    wikiRuntime.RegisterJob("session-to-delete", { workspaceRoot = workspaceDir; kind = AppendAfterWork })
    check "second job exists initially" (wikiRuntime.TakeJob("session-to-delete").IsSome)

    let deleteEvent =
        box {|
            event = box {|
                ``type`` = box "session.delete"
                properties = box {|
                    info = box {|
                        id = box "session-to-delete"
                    |}
                |}
            |}
        |}
    do! eventHandler.Invoke(deleteEvent) |> Async.AwaitPromise
    check "second job is removed after session.delete" (wikiRuntime.TakeJob("session-to-delete").IsNone)

    do! rmAsync workspaceDir |> Async.AwaitPromise
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
        do! titleAgentInputProjectionSpec ()
        do! wikiPreludeWithoutCapsSpec ()
        do! fetchWikiSnapshotSpec ()
        do! directWriteTurnAggregationSpec ()
        do! dailyMaintenanceLaunchSpec ()
        do! weeklyMaintenanceLaunchSpec ()
        do! weeklyMaintenanceUsesLastSundaySpec ()
        do! weeklyMaintenanceWithoutSnapshotFileSpec ()
        do! directPatchWriteAggregationSpec ()
        do! submitWikiAppendSpec ()
        do! submitWikiAppendEmptySpec ()
        do! submitWikiDailyRewriteSpec ()
        do! submitWikiWeeklyRewriteSpec ()
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
        do! bookkeeperAgentConfigSpec ()
        do! executorModeSchemaSpec ()
        do! coderTriggersBookkeeperSpec ()
        do! bookkeeperFireAndForgetSpec ()
        do! executorRoRwBookkeeperSpec ()
        do! coderToolSpec ()
        do! investigatorToolSpec ()
        do! investigatorToolLateClientInjectionSpec ()
        do! executorActorSpec ()
        do! wikiActorSpec ()
        do! jobContextCleanupOnAbortOrDeleteSpec ()
    }
    |> Async.StartAsPromise
