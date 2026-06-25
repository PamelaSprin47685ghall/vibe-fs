module VibeFs.Tests.OmpPluginTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Shell.Dyn
module Dyn = VibeFs.Shell.Dyn
open VibeFs.Omp.Plugin
open VibeFs.Shell.RunnerBackground
open VibeFs.Kernel.FuzzyQuery
open VibeFs.Omp.MessagingCodec
open VibeFs.Kernel.OmpSessionTools
open VibeFs.Kernel.ReviewPrompts
open VibeFs.Kernel.SubagentPrompts
open VibeFs.Kernel.SubagentIntents
type private PiHarness =
    { hookStore: obj
      tools: ResizeArray<obj>
      commands: ResizeArray<obj>
      messages: ResizeArray<obj> }

let private createPiHarness () : PiHarness =
    let tools = ResizeArray<obj>()
    let commands = ResizeArray<obj>()
    let messages = ResizeArray<obj>()
    let hookStore =
        createObj [
            "tools", box tools
            "commands", box commands
            "messages", box messages
            "events", box(createObj [])
            "activeTools",
                box
                    [| "read"; "edit"; "write"; "find"; "fuzzy_find"; "fuzzy_grep"; "lsp"; "browser"; "search"; "glob"
                       "bash"; "coder"; "investigator"; "meditator"; "executor"; "executor_wait"; "executor_abort"
                       "submit_review"; "return_reviewer"; "websearch"; "webfetch"; "todowrite" |]
        ]
    { hookStore = hookStore; tools = tools; commands = commands; messages = messages }

let private piObject (h: PiHarness) : obj =
    let tb =
        createObj [
            "Type",
                box(
                    createObj [
                        "Object", box(fun (p: obj) -> createObj [ "type", box "object"; "properties", box p ])
                        "String", box(fun (o: obj) -> createObj [ "type", box "string" ])
                        "Number", box(fun (o: obj) -> createObj [ "type", box "number" ])
                        "Boolean", box(fun (o: obj) -> createObj [ "type", box "boolean" ])
                        "Null", box(fun (_: obj) -> createObj [ "type", box "null" ])
                        "Union", box(fun (items: obj array) -> createObj [ "anyOf", box items ])
                        "Enum", box(fun (values: obj array) (o: obj) -> createObj [ "type", box "enum"; "values", box values ])
                        "Array", box(fun (items: obj) -> createObj [ "type", box "array"; "items", box items ])
                        "Optional", box(fun (schema: obj) -> schema)
                    ])
        ]
    let pi =
        emitJsExpr h.hookStore
            """((hs) => ({
            on(event, handler) {
                if (!hs.events[event]) hs.events[event] = [];
                hs.events[event].push(handler);
            },
            registerTool(tool) { hs.tools.push(tool); },
            registerCommand(name, config) { hs.commands.push({ name, config }); },
            sendMessage(message, options) { hs.messages.push({ message, options }); },
            getActiveTools() { return hs.activeTools; },
            setActiveTools(names) {
                hs.activeTools = names;
                return Promise.resolve();
            },
            getAllTools() { return hs.activeTools; }
        }))($0)"""
        |> unbox<obj>
    pi?("typebox") <- tb
    pi

let private eventHandler (h: PiHarness) (event: string) : obj =
    let handlers = Dyn.get (Dyn.get h.hookStore "events") event
    if Dyn.isArray handlers then
        let arr = unbox<obj array> handlers
        if arr.Length > 0 then arr.[0]
        else failwith ("missing handler for " + event)
    else
        failwith ("missing handler for " + event)

let private activeTools (h: PiHarness) : string array =
    unbox<string array> (Dyn.get h.hookStore "activeTools")

let private toolNames (h: PiHarness) =
    h.tools |> Seq.map (fun t -> str t "name") |> Seq.toList |> List.rev |> Set.ofList

let private resetPluginState () =
    resetOmpPluginTestState ()

let private lastMessageCustomType (h: PiHarness) : string =
    let entry = h.messages.[h.messages.Count - 1]
    str (Dyn.get entry "message") "customType"

let private invokeHandler (h: PiHarness) (event: string) (eventObj: obj) (ctx: obj) =
    emitJsExpr (eventHandler h event, eventObj, ctx)
        "Promise.resolve($0($1, $2))"
    |> unbox<JS.Promise<unit>>

let registersCoreToolsIdempotent () = promise {
    resetPluginState ()
    let h1 = createPiHarness ()
    let pi1 = piObject h1
    do! kunweiExtension pi1
    let count1 = h1.tools.Count
    do! kunweiExtension pi1
    check "idempotent tool count" (h1.tools.Count = count1)
    let names = toolNames h1
    for expected in
        [ "fuzzy_find"; "fuzzy_grep"; "coder"; "investigator"; "meditator"; "browser"; "websearch"; "webfetch"; "executor"
          "submit_review"; "return_reviewer"; "todowrite" ] do
        check ("has tool " + expected) (names.Contains expected)
    check "has loop command" (h1.commands |> Seq.exists (fun c -> str c "name" = "loop"))
}

let sessionStartStripsMainSessionTools () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! kunweiExtension pi
    let handler = eventHandler h "session_start"
    do!
        emitJsExpr (handler, createObj [], createObj [])
            "Promise.resolve($0($1, $2))"
        |> unbox<JS.Promise<unit>>
    let active = Set.ofArray (activeTools h)
    let childOnly = Set.ofArray ompChildOnlyToolNames
    for name in ompSubagentToolNames do
        if not (childOnly.Contains name) then
            check ("session_start keeps " + name) (active.Contains name)
    for name in [| "executor"; "submit_review"; "websearch"; "webfetch"; "todowrite"; "read" |] do
        check ("session_start keeps " + name) (active.Contains name)
    for name in ompAlwaysStripToolNames do
        check ("session_start strips " + name) (not (active.Contains name))
    for name in ompChildOnlyToolNames do
        check ("session_start strips " + name) (not (active.Contains name))
}

let fuzzyDescriptionsMatchMuxWording () =
    check "fuzzy_find regex disclaimer" (fuzzyFindDescriptionOmp.Contains "Regex and glob syntax are not supported.")
    check "fuzzy_find iterator hint" (fuzzyFindDescriptionOmp.Contains "Every result ends with iterator=")
    check "fuzzy_grep smart-case" (fuzzyGrepDescriptionOmp.Contains "Smart-case, git-aware, frecency-ranked.")

let readAssistantTextFromEntries () =
    let sm =
        createObj [
            "getEntries",
                box(fun () ->
                    box
                        [| createObj [
                               "info", box(createObj [ "role", box "user" ])
                               "parts", box [| createObj [ "type", box "text"; "text", box "prompt" ] |]
                           ]
                           createObj [
                               "info", box(createObj [ "role", box "assistant" ])
                               "parts", box [| createObj [ "type", box "text"; "text", box "done" ] |]
                           ] |])
        ]
    equal "readAssistantText" (Some "done") (readAssistantText sm 0 "\n\n")

let subagentPromptsContainKernelFragments () =
    let coder =
        coderPrompt
            { objective = "verify static edits"
              background = "test"
              targets = []
              doNotTouch = [||] }
    check "coder implementation agent" (coder.Contains "implementation agent")
    check "coder static verify" (coder.Contains "Do NOT run tests or execute code")
    let investigator =
        investigatorPrompt
            { objective = "find auth"
              background = "test"
              questions = [||]
              entries = [||] }
    check "investigator fuzzy_find" (investigator.Contains "fuzzy_find")
    check "investigator no glob tool" (not (investigator.Contains "glob tool"))
    check "investigator no executor tool name" (not (investigator.Contains "executor("))
    let browser = browserPrompt "open example.com"
    check "browser stealth" (browser.Contains "stealth-browser-mcp")

let fuzzyGrepExcludeAnyOfLength2 () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! kunweiExtension pi
    let fuzzyGrep = h.tools |> Seq.find (fun t -> str t "name" = "fuzzy_grep")
    let parameters = Dyn.get fuzzyGrep "parameters"
    check "fuzzy_grep has parameters" (not (Dyn.isNullish parameters))
    let properties = Dyn.get parameters "properties"
    check "parameters has properties" (not (Dyn.isNullish properties))
    let exclude =
        if Dyn.has properties "exclude" then Dyn.get properties "exclude"
        else undefinedValue
    check "properties has exclude" (not (Dyn.isNullish exclude))
    let anyOf = Dyn.get exclude "anyOf"
    check "exclude anyOf is array" (not (Dyn.isNullish anyOf) && Dyn.isArray anyOf)
    let anyOfLen =
        if Dyn.isArray anyOf then (unbox<obj array> anyOf).Length
        else 0
    equal "exclude anyOf length" 2 anyOfLen
}

let agentEndRunnerNudgeBeforeLoop () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! kunweiExtension pi
    setRunnerJobStateForTest "session-1" "running"
    let ctx =
        createObj [
            "sessionManager",
                box(
                    createObj [
                        "getSessionId", box(fun () -> box "session-1")
                        "getEntries", box(fun () -> box [||])
                    ])
            "hasPendingMessages", box(fun () -> box false)
        ]
    do! invokeHandler h "agent_end" (createObj []) ctx
    equal "runner reminder type" "kunwei-runner-reminder" (lastMessageCustomType h)
}

let agentEndLoopNudgeWhenActive () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! kunweiExtension pi
    let ctxLoop =
        createObj [
            "sessionManager", box(createObj [ "getSessionId", box(fun () -> box "session-2") ])
            "ui", box(createObj [ "notify", box(fun (_: obj) (_: obj) -> ()) ])
        ]
    do!
        emitJsExpr (eventHandler h "input", createObj [ "text", box "/loop do task" ], ctxLoop)
            "Promise.resolve($0($1, $2))"
        |> unbox<JS.Promise<unit>>
    let ctxEnd =
        createObj [
            "sessionManager",
                box(
                    createObj [
                        "getSessionId", box(fun () -> box "session-2")
                        "getEntries", box(fun () -> box [||])
                    ])
            "hasPendingMessages", box(fun () -> box false)
        ]
    do! invokeHandler h "agent_end" (createObj []) ctxEnd
    equal "loop reminder type" "kunwei-loop-reminder" (lastMessageCustomType h)
}

let executorToolSchemaFourFields () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! kunweiExtension pi
    let runner = h.tools |> Seq.find (fun t -> str t "name" = "executor")
    let parameters = Dyn.get runner "parameters"
    let properties = Dyn.get parameters "properties"
    for field in [| "language"; "program"; "dependencies"; "timeout_type"; "mode" |] do
        check ("runner schema has " + field) (Dyn.has properties field)
}

let browserErrorsWithoutBrowserHost () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    h.hookStore?("activeTools") <- box [| "read"; "coder" |]
    let pi = piObject h
    do! kunweiExtension pi
    let browse = h.tools |> Seq.find (fun t -> str t "name" = "browser")
    let execute = Dyn.get browse "execute"
    let jsUndef = emitJsExpr () "undefined"
    let! result =
        emitJsExpr (execute, "call-browse", createObj [ "intent", box "open example.com" ], jsUndef, jsUndef, createObj [ "cwd", box "/tmp" ])
            "Promise.resolve($0($1)($2)($3)($4)($5))"
        |> unbox<JS.Promise<obj>>
    let content = unbox<obj array> (Dyn.get result "content")
    let text = str content.[0] "text"
    check "browse errors without browser host" (text.Contains "browser tool is unavailable")
}

let reviewChildInitialPromptUsesReturnReviewer () =
    let initial = buildOmpReviewInitialPrompt "report" [ "src/x.fs" ] (Some "original task")
    check "review child prompt has return_reviewer" (initial.Contains "return_reviewer")
    check "review child prompt PASS verdict" (initial.Contains "verdict")
    check "review child prompt no submit_review_result" (not (initial.Contains "submit_review_result"))

let agentEndSkipsLoopNudgeWhenPendingMessages () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! kunweiExtension pi
    let ctxLoop =
        createObj [
            "sessionManager", box(createObj [ "getSessionId", box(fun () -> box "session-3") ])
            "ui", box(createObj [ "notify", box(fun (_: obj) (_: obj) -> ()) ])
        ]
    do!
        emitJsExpr (eventHandler h "input", createObj [ "text", box "/loop gated" ], ctxLoop)
            "Promise.resolve($0($1, $2))"
        |> unbox<JS.Promise<unit>>
    let countAfterLoop = h.messages.Count
    let ctxEnd =
        createObj [
            "sessionManager",
                box(
                    createObj [
                        "getSessionId", box(fun () -> box "session-3")
                        "getEntries", box(fun () -> box [||])
                    ])
            "hasPendingMessages", box(fun () -> box true)
        ]
    do! invokeHandler h "agent_end" (createObj []) ctxEnd
    check "no loop reminder when pending" (h.messages.Count = countAfterLoop)
}

let private todoPhaseEntries () : obj array =
    let task = createObj [ "status", box "pending" ]
    let phase = createObj [ "tasks", box [| task |] ]
    let entry =
        createObj [
            "customType", box "todo-phases"
            "content", box [| phase |]
        ]
    [| entry |]

let agentEndTodoNudgeWhenOpenPhases () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! kunweiExtension pi
    let sm =
        createObj [
            "getSessionId", box(fun () -> box "session-todo")
            "getEntries", box(fun () -> box (todoPhaseEntries ()))
        ]
    let ctxEnd =
        createObj [
            "sessionManager", box sm
            "hasPendingMessages", box(fun () -> box false)
        ]
    do! invokeHandler h "agent_end" (createObj []) ctxEnd
    equal "todo reminder type" "kunwei-todo-reminder" (lastMessageCustomType h)
}

let runnerNudgePromptUsesExecutorToolNames () =
    let text = VibeFs.Omp.NudgeRuntime.runnerReminderContent ()
    check "runner nudge names executor_wait" (text.Contains "executor_wait")
    check "runner nudge names executor_abort" (text.Contains "executor_abort")
    check "runner nudge avoids legacy runner_wait" (not (text.Contains "runner_wait"))



