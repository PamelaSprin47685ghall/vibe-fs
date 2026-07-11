module Wanxiangshu.E2e.TestsOmpSpecsPart

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.HarnessTypes

[<Emit("JSON.stringify($0)")>]
let jsonStringify (o: obj) : string = jsNative

let runOmpToolRegistry (h: OmpHarness) (chk: string -> bool -> unit) =
    promise {
        let! toolNames = withTimeout (h.getToolNames ())

        for t in
            [ "fuzzy_find"
              "fuzzy_grep"
              "executor"
              "todowrite"
              "submit_review"
              "return_reviewer"
              "websearch"
              "webfetch"
              "coder"
              "investigator"
              "meditator"
              "browser" ] do
            chk ("e2e-omp.tools." + t) (toolNames.Contains t)

        chk "e2e-omp.methodology.unified" (toolNames.Contains "meditator")
    }

let runOmpCommandsAndHandlers (h: OmpHarness) (chk: string -> bool -> unit) =
    promise {
        let! commands = withTimeout (h.getCommands ())
        let commandsStr = jsonStringify commands
        chk "e2e-omp.commands.returned" (commandsStr <> "null" && commandsStr.Length > 2)

        let handlerKeys =
            let s = jsonStringify h.handlers

            [ "session_start"
              "session_shutdown"
              "tool_call"
              "tool_result"
              "agent_end"
              "turn_start"
              "before_agent_start"
              "event" ]
            |> List.forall s.Contains

        chk "e2e-omp.handlers.subscribed" handlerKeys
    }

let runOmpFuzzyTools (h: OmpHarness) (chk: string -> bool -> unit) (sessionId: string) =
    promise {
        let! fuzzyFindResult =
            withTimeout (h.triggerTool "fuzzy_find" (box {| pattern = [| "README.md" |] |}) sessionId (createObj []))

        let fuzzyFindStr = jsonStringify fuzzyFindResult

        chk
            "e2e-omp.fuzzy-find.responded"
            (fuzzyFindStr.Contains "No matching files found"
             && not (fuzzyFindStr.Contains "error"))

        let! fuzzyGrepResult =
            withTimeout (h.triggerTool "fuzzy_grep" (box {| pattern = [| "wanxiangshu" |] |}) sessionId (createObj []))

        let fuzzyGrepStr = jsonStringify fuzzyGrepResult

        chk
            "e2e-omp.fuzzy-grep.responded"
            (fuzzyGrepStr.Contains "No matches found" && not (fuzzyGrepStr.Contains "error"))
    }

let runOmpExecutorTools (h: OmpHarness) (chk: string -> bool -> unit) (sessionId: string) =
    promise {
        let warnTdd =
            "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles-and-kept-todo-updated"

        let warn =
            "it-is-not-possible-to-do-it-using-other-tools-and-only-run-tests-when-static-analysis-cannot-handle-it"

        let! executorResult =
            withTimeout (
                h.triggerTool
                    "executor"
                    (box
                        {| language = "shell"
                           program = "echo hello"
                           timeout_type = "short"
                           mode = "ro"
                           what_to_summarize = "stdout"
                           warn_tdd = warnTdd
                           warn = warn |})
                    sessionId
                    (createObj [])
            )

        let execStr = jsonStringify executorResult
        chk "e2e-omp.executor.responded" (execStr.Contains "hello" && not (execStr.Contains "error"))
    }

let runOmpWebTools (h: OmpHarness) (chk: string -> bool -> unit) (sessionId: string) =
    promise {
        do! h.expectText "Test search summary details"

        let! websearchResult =
            withTimeout (
                h.triggerTool
                    "websearch"
                    (box
                        {| query = "test"
                           what_to_summarize = "summary" |})
                    sessionId
                    (createObj [])
            )

        let webStr = jsonStringify websearchResult

        chk
            "e2e-omp.websearch.responded"
            (webStr.Contains "Test search summary details" && not (webStr.Contains "error"))

        let! webfetchResult =
            withTimeout (h.triggerTool "webfetch" (box {| url = "http://example.com" |}) sessionId (createObj []))

        let fetchStr = jsonStringify webfetchResult
        chk "e2e-omp.webfetch.responded" (fetchStr.Contains "Example Domain" && not (fetchStr.Contains "error"))
    }

let runOmpAgentTools (h: OmpHarness) (chk: string -> bool -> unit) (sessionId: string) =
    promise {
        let warnTdd =
            "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles-and-kept-todo-updated"

        let! coderResult =
            withTimeout (
                h.triggerTool
                    "coder"
                    (box
                        {| intents = [||]
                           tdd = "green"
                           warn_tdd = warnTdd |})
                    sessionId
                    (createObj [])
            )

        let coderStr = jsonStringify coderResult

        chk
            "e2e-omp.coder.responded"
            (coderStr.Contains "Invalid LLM input for coder"
             && not (coderStr.Contains "\"isError\":false"))

        do! h.expectText "browser mock debug view text"

        let! browserResult =
            withTimeout (h.triggerTool "browser" (box {| intent = "browse" |}) sessionId (createObj []))

        let browserStr = jsonStringify browserResult

        chk
            "e2e-omp.browser.responded"
            (browserStr.Contains "browser mock debug view text"
             && not (browserStr.Contains "error"))
    }

let runOmpMethodology (h: OmpHarness) (chk: string -> bool -> unit) (sessionId: string) =
    promise {
        for m in [ "first_principles"; "risk_analysis"; "thought_experiment" ] do
            do! h.expectText (m.Replace("_", " ") + " output")

            let! res =
                withTimeout (
                    h.triggerTool
                        "meditator"
                        (box
                            {| methodology = m
                               note = String.replicate 1100 "n"
                               background = String.replicate 1100 "b"
                               intent = String.replicate 1100 "i" |})
                        sessionId
                        (createObj [])
                )

            let resStr = jsonStringify res
            let expected = m.Replace("_", " ") + " output"
            chk ("e2e-omp.meditator." + m + ".responded") (resStr.Contains expected && not (resStr.Contains "error"))

            let expectedBackground = String.replicate 1100 "b"
            let callsStr = jsonStringify h.calls
            chk ("e2e-omp.meditator." + m + ".prompt-contains-background") (callsStr.Contains expectedBackground)
    }

let runOmpInvestigator (h: OmpHarness) (chk: string -> bool -> unit) (sessionId: string) =
    promise {
        let investigatorIntents =
            [| box
                   {| objective = "Test investigator e2e"
                      background = "Ensure no f.content crashes"
                      questions = [| "Did it crash?" |]
                      entries = [| "README.md"; "DOES_NOT_EXIST_AT_ALL.md" |] |} |]

        do! h.expectText "investigator e2e verification output: mock text content"

        let! investigatorResult =
            withTimeout (
                h.triggerTool "investigator" (box {| intents = investigatorIntents |}) sessionId (createObj [])
            )

        let invStr = jsonStringify investigatorResult
        chk "e2e-omp.investigator.ran" (invStr.Contains "mock text content")
    }
