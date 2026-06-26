module Wanxiangshu.Tests.OmpPluginTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Omp.Plugin
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn

let registersCoreToolsIdempotent () = promise {
    resetPluginState ()
    let h1 = createPiHarness ()
    let pi1 = piObject h1
    do! wanxiangshuExtension pi1
    let count1 = h1.tools.Count
    do! wanxiangshuExtension pi1
    check "idempotent tool count" (h1.tools.Count = count1)
    let names = toolNames h1
    for expected in
        [ "fuzzy_find"; "fuzzy_grep"; "coder"; "investigator"; "meditator"; "browser"; "websearch"; "webfetch"; "executor"
          "submit_review"; "return_reviewer"; "todowrite" ] do
        check ("has tool " + expected) (names.Contains expected)
    let methodologyCount = names |> Set.filter (fun n -> n.StartsWith "methodology_") |> Set.count
    check "OMP parity: registers methodology_first_principles" (names.Contains "methodology_first_principles")
    check "OMP parity: registers at least 53 methodology_* tools" (methodologyCount >= 53)
    check "has loop command" (h1.commands |> Seq.exists (fun c -> Dyn.str c "name" = "loop"))
}

let methodologySchemaCarriesMinItems () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let abduction =
        h.tools |> Seq.tryFind (fun t -> Dyn.str t "name" = "methodology_abduction")
    check "methodology_abduction tool registered" (abduction.IsSome)
    match abduction with
    | None -> ()
    | Some tool ->
        let props = Dyn.get (Dyn.get tool "parameters") "properties"
        let dt = Dyn.get props "discriminating_tests"
        check "discriminating_tests present" (not (Dyn.isNullish dt))
        let mi = Dyn.get dt "minItems"
        check "discriminating_tests has minItems" (not (Dyn.isNullish mi))
        check "discriminating_tests minItems >= 2" (unbox<int> mi >= 2)
}

let sessionStartStripsMainSessionTools () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
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
    do! wanxiangshuExtension pi
    let fuzzyGrep = h.tools |> Seq.find (fun t -> Dyn.str t "name" = "fuzzy_grep")
    let parameters = Dyn.get fuzzyGrep "parameters"
    check "fuzzy_grep has parameters" (not (Dyn.isNullish parameters))
    let properties = Dyn.get parameters "properties"
    check "parameters has properties" (not (Dyn.isNullish properties))
    let exclude =
        if Dyn.has properties "exclude" then Dyn.get properties "exclude"
        else Dyn.undefinedValue
    check "properties has exclude" (not (Dyn.isNullish exclude))
    let anyOf = Dyn.get exclude "anyOf"
    check "exclude anyOf is array" (not (Dyn.isNullish anyOf) && Dyn.isArray anyOf)
    let anyOfLen =
        if Dyn.isArray anyOf then (unbox<obj array> anyOf).Length
        else 0
    equal "exclude anyOf length" 2 anyOfLen
}

let executorToolSchemaFourFields () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let runner = h.tools |> Seq.find (fun t -> Dyn.str t "name" = "executor")
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
    do! wanxiangshuExtension pi
    let browse = h.tools |> Seq.find (fun t -> Dyn.str t "name" = "browser")
    let execute = Dyn.get browse "execute"
    let jsUndef = emitJsExpr () "undefined"
    let! result =
        emitJsExpr (execute, "call-browse", createObj [ "intent", box "open example.com" ], jsUndef, jsUndef, createObj [ "cwd", box "/tmp" ])
            "Promise.resolve($0($1)($2)($3)($4)($5))"
        |> unbox<JS.Promise<obj>>
    let content = unbox<obj array> (Dyn.get result "content")
    let text = Dyn.str content.[0] "text"
    check "browse errors without browser host" (text.Contains "browser tool is unavailable")
}

let reviewChildInitialPromptUsesReturnReviewer () =
    let initial = buildOmpReviewInitialPrompt "report" [ "src/x.fs" ] (Some "original task")
    check "review child prompt has return_reviewer" (initial.Contains "return_reviewer")
    check "review child prompt PASS verdict" (initial.Contains "verdict")
    check "review child prompt no submit_review_result" (not (initial.Contains "submit_review_result"))

let extensionRegistersLifecycleHooks () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let events = Dyn.get h.hookStore "events"
    check "registers session_start hook" (Dyn.has events "session_start")
    check "registers before_agent_start hook" (Dyn.has events "before_agent_start")
    check "registers tool_call hook" (Dyn.has events "tool_call")
    check "registers tool_result hook" (Dyn.has events "tool_result")
    check "registers session.compacting hook" (Dyn.has events "session.compacting")
    check "registers agent_end hook" (Dyn.has events "agent_end")
    check "registers session_shutdown hook" (Dyn.has events "session_shutdown")
    check "registers turn_start hook" (Dyn.has events "turn_start")
}

let toolCallHookCanBeInvoked () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let handler = eventHandler h "tool_call"
    let event =
        createObj [
            "toolName", box "coder"
            "input", box(createObj [ "objective", box "test" ])
        ]
    let! _ =
        emitJsExpr (handler, event, createObj [ "cwd", box "/tmp" ])
            "Promise.resolve($0($1, $2))"
        |> unbox<JS.Promise<obj>>
    check "tool_call handler invoked without error" true
}

let toolCallBlocksChildOnlyInMainSession () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let handler = eventHandler h "tool_call"
    let event = createObj [ "toolName", box "edit"; "input", box (createObj []) ]
    let ctx = createObj [
        "sessionManager", box (createObj [ "getSessionId", box (fun () -> box "main-session") ])
        "cwd", box "/tmp"
    ]
    let! result =
        emitJsExpr (handler, event, ctx) "Promise.resolve($0($1, $2))"
        |> unbox<JS.Promise<obj>>
    check "tool_call blocks edit in main session" (unbox<bool> (Dyn.get result "block"))
    check "block reason present" (Dyn.str result "reason" <> "")
}

let turnStartRestoresMainSessionTools () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    h.hookStore?("activeTools") <- box [| "read"; "edit"; "write"; "coder"; "investigator"; "meditator"; "executor"; "submit_review"; "return_reviewer"; "websearch"; "webfetch"; "todowrite" |]
    let handler = eventHandler h "turn_start"
    do!
        emitJsExpr (handler, createObj [ "turnIndex", box 0 ], createObj [ "cwd", box "/tmp" ])
            "Promise.resolve($0($1, $2))"
        |> unbox<JS.Promise<unit>>
    let active = Set.ofArray (activeTools h)
    check "turn_start strips edit from main session" (not (active.Contains "edit"))
    check "turn_start keeps coder" (active.Contains "coder")
}

let sessionCompactingHookCanBeInvoked () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let handler = eventHandler h "session.compacting"
    let event =
        createObj [
            "sessionId", box "test-compact"
            "messages", box [||]
        ]
    let! result =
        emitJsExpr (handler, event, createObj [ "cwd", box "/tmp" ])
            "Promise.resolve($0($1, $2))"
        |> unbox<JS.Promise<obj>>
    check "session.compacting handler returns object" (not (Dyn.isNullish result))
}