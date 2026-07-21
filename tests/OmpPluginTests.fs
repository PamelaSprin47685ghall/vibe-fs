module Wanxiangshu.Tests.OmpPluginTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Hosts.Omp.Plugin
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Runtime.ReviewPrompts
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

let registersCoreToolsIdempotent () =
    promise {
        resetPluginState ()
        let h1 = createPiHarness ()
        let pi1 = piObject h1
        do! wanxiangshuExtension pi1
        let count1 = h1.tools.Count
        do! wanxiangshuExtension pi1
        check "idempotent tool count" (h1.tools.Count = count1)
        let names = toolNames h1

        for expected in
            [ "fuzzy_find"
              "fuzzy_grep"
              "coder"
              "inspector"
              "meditator"
              "browser"
              "websearch"
              "webfetch"
              "executor"
              "submit_review"
              "return_reviewer" ] do
            check ("has tool " + expected) (names.Contains expected)

        check "OMP parity: registers meditator tool" (names.Contains "meditator")
        check "has loop command" (h1.commands |> Seq.exists (fun c -> Dyn.str c "name" = "loop"))
    }

let meditatorSchemaUnifiedNote () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        do! wanxiangshuExtension pi
        let meditator = h.tools |> Seq.tryFind (fun t -> Dyn.str t "name" = "meditator")
        check "meditator tool registered" (meditator.IsSome)

        match meditator with
        | None -> ()
        | Some tool ->
            let props = Dyn.get (Dyn.get tool "parameters") "properties"
            // The unified methodology tool has a single note: string parameter.
            let note = Dyn.get props "note"
            check "note field present" (not (Dyn.isNullish note))
    }

let sessionStartStripsMainSessionTools () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        do! wanxiangshuExtension pi
        let handler = eventHandler h "session_start"

        do!
            emitJsExpr (handler, createObj [], createObj []) "Promise.resolve($0($1, $2))"
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
        createObj
            [ "getEntries",
              box (fun () ->
                  box
                      [| createObj
                             [ "info", box (createObj [ "role", box "user" ])
                               "parts", box [| createObj [ "type", box "text"; "text", box "prompt" ] |] ]
                         createObj
                             [ "info", box (createObj [ "role", box "assistant" ])
                               "parts", box [| createObj [ "type", box "text"; "text", box "done" ] |] ] |]) ]

    equal "readAssistantText" (Some "done") (readAssistantText (unbox<ISessionManager> sm) 0 "\n\n")

let subagentPromptsContainKernelFragments () =
    let coder =
        coderPrompt
            { objective = "verify static edits"
              background = "test"
              targets = []
              doNotTouch = [||] }

    check "coder implementation agent" (coder.Contains "implementation agent")
    check "coder static verify" (coder.Contains "Do NOT run tests or execute code")

    let inspector =
        inspectorPrompt
            { objective = "find auth"
              background = "test"
              questions = [||]
              entries = [||] }

    check "inspector fuzzy_find" (inspector.Contains "fuzzy_find")
    check "inspector no glob tool" (not (inspector.Contains "glob tool"))
    check "inspector no executor tool name" (not (inspector.Contains "executor("))
    let browser = browserPrompt "open example.com"
    check "browser stealth" (browser.Contains "stealth-browser-mcp")

let fuzzyGrepExcludeAnyOfLength2 () =
    promise {
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
            if Dyn.has properties "exclude" then
                Dyn.get properties "exclude"
            else
                Dyn.undefinedValue

        check "properties has exclude" (not (Dyn.isNullish exclude))
        let anyOf = Dyn.get exclude "anyOf"
        check "exclude anyOf is array" (not (Dyn.isNullish anyOf) && Dyn.isArray anyOf)

        let anyOfLen =
            if Dyn.isArray anyOf then
                (unbox<obj array> anyOf).Length
            else
                0

        equal "exclude anyOf length" 2 anyOfLen
    }

let executorToolSchemaFourFields () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        do! wanxiangshuExtension pi
        let runner = h.tools |> Seq.find (fun t -> Dyn.str t "name" = "executor")
        let parameters = Dyn.get runner "parameters"
        let properties = Dyn.get parameters "properties"

        for field in
            [| "language"
               "command"
               "dependencies"
               "timeout_type"
               "mode"
               "what_to_summarize"
               "warn" |] do
            check ("runner schema has " + field) (Dyn.has properties field)

        match Dyn.get parameters "required" with
        | null -> check "executor schema has required array" false
        | req when Dyn.isArray req ->
            let reqArr = unbox<string array> req
            check "executor schema requires what_to_summarize" (Array.contains "what_to_summarize" reqArr)
        | _ -> check "executor schema has required array" false
    }

let browserErrorsWithoutBrowserHost () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        h.hookStore?("activeTools") <- box [| "read"; "coder" |]
        let pi = piObject h
        do! wanxiangshuExtension pi
        let browse = h.tools |> Seq.find (fun t -> Dyn.str t "name" = "browser")
        let execute = Dyn.get browse "execute"
        let jsUndef = emitJsExpr () "undefined"

        let! result =
            emitJsExpr
                (execute,
                 "call-browse",
                 createObj [ "intent", box "open example.com" ],
                 jsUndef,
                 jsUndef,
                 createObj [ "cwd", box "/tmp" ])
                "Promise.resolve($0($1)($2)($3)($4)($5))"
            |> unbox<JS.Promise<obj>>

        let content = unbox<obj array> (Dyn.get result "content")
        let text = Dyn.str content.[0] "text"
        check "browse errors without browser host" (text.Contains "browser tool is unavailable")
    }

let reviewChildInitialPromptUsesReturnReviewer () =
    let initial =
        buildOmpReviewInitialPrompt "report" [ "src/x.fs" ] (Some "original task")

    check "review child prompt has return_reviewer" (initial.Contains "return_reviewer")
    check "review child prompt PERFECT verdict" (initial.Contains "PERFECT")
    check "review child prompt no submit_review_result" (not (initial.Contains "submit_review_result"))
