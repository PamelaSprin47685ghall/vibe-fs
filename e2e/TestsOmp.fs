module Wanxiangshu.E2e.OmpTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert

[<Import("start", "./omp-runner.js")>]
let start: obj -> JS.Promise<obj> = jsNative

type OmpHarness =
    abstract tools: ResizeArray<obj>
    abstract handlers: obj
    abstract getToolNames: unit -> JS.Promise<ResizeArray<string>>
    abstract runCommand: string -> string -> string -> JS.Promise<obj>
    abstract triggerTool: string -> obj -> string -> obj -> JS.Promise<obj>
    abstract emitEvent: string -> obj -> string -> JS.Promise<obj>
    abstract readNdjson: unit -> JS.Promise<string>
    abstract readFile: string -> JS.Promise<string>
    abstract fileExists: string -> JS.Promise<bool>
    abstract getCommands: unit -> JS.Promise<obj>
    abstract expectText: string -> JS.Promise<unit>
    abstract expectTool: string -> obj -> JS.Promise<unit>
    abstract getRemainingExpectations: unit -> int
    abstract waitForNdjson: int -> int -> JS.Promise<bool>
    abstract dispose: unit -> JS.Promise<unit>

let private harnessFromObj (o: obj) : OmpHarness = unbox o

[<Emit("JSON.stringify($0)")>]
let private jsonStringify (o: obj) : string = jsNative

let runAll (_args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let! apiObj = start (createObj [])
        let h = harnessFromObj apiObj

        let expected = 40
        let mutable ok = 0
        let chk l c =
            check l c
            if c then ok <- ok + 1

        let! toolNames = h.getToolNames ()
        chk "e2e-omp.tools.fuzzy_find" (toolNames.Contains "fuzzy_find")
        chk "e2e-omp.tools.fuzzy_grep" (toolNames.Contains "fuzzy_grep")
        chk "e2e-omp.tools.executor" (toolNames.Contains "executor")
        chk "e2e-omp.tools.executor_wait" (toolNames.Contains "executor_wait")
        chk "e2e-omp.tools.executor_abort" (toolNames.Contains "executor_abort")
        chk "e2e-omp.tools.todowrite" (toolNames.Contains "todowrite")
        chk "e2e-omp.tools.submit_review" (toolNames.Contains "submit_review")
        chk "e2e-omp.tools.return_reviewer" (toolNames.Contains "return_reviewer")
        chk "e2e-omp.tools.websearch" (toolNames.Contains "websearch")
        chk "e2e-omp.tools.webfetch" (toolNames.Contains "webfetch")
        chk "e2e-omp.tools.coder" (toolNames.Contains "coder")
        chk "e2e-omp.tools.investigator" (toolNames.Contains "investigator")
        chk "e2e-omp.tools.meditator" (toolNames.Contains "meditator")
        chk "e2e-omp.tools.browser" (toolNames.Contains "browser")
        chk "e2e-omp.methodology.unified" (toolNames.Contains "methodology")

        let! commands = h.getCommands ()
        chk "e2e-omp.commands.returned" ((jsonStringify commands) <> "null" && (jsonStringify commands).Length > 2)
        let handlerKeys =
            let s = jsonStringify h.handlers
            [ "session_start"; "session_shutdown"; "tool_call"; "tool_result"
              "agent_end"; "turn_start"; "before_agent_start"; "event" ]
            |> List.forall s.Contains
        chk "e2e-omp.handlers.subscribed" handlerKeys

        let sessionId = "e2e-omp-session-1"

        let! _ = h.emitEvent "session_start" (createObj [ "reason", box "start" ]) sessionId
        let! _ = h.emitEvent "turn_start" (createObj []) sessionId

        // 1. fuzzy_find
        let! fuzzyFindResult = h.triggerTool "fuzzy_find" (box {| pattern = [| "README.md" |] |}) sessionId (createObj [])
        let fuzzyFindStr = jsonStringify fuzzyFindResult
        chk "e2e-omp.fuzzy-find.responded" (fuzzyFindStr.Contains "No matching files found" && not (fuzzyFindStr.Contains "error"))

        // 2. fuzzy_grep
        let! fuzzyGrepResult = h.triggerTool "fuzzy_grep" (box {| pattern = [| "wanxiangshu" |] |}) sessionId (createObj [])
        let fuzzyGrepStr = jsonStringify fuzzyGrepResult
        chk "e2e-omp.fuzzy-grep.responded" (fuzzyGrepStr.Contains "No matches found" && not (fuzzyGrepStr.Contains "error"))

        // 3. executor, executor_wait, executor_abort
        let! executorResult = h.triggerTool "executor" (box {| language = "shell"; program = "echo hello"; timeout_type = "short"; mode = "ro"; what_to_summarize = "stdout"; warn_tdd = "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles"; warn = "it-is-not-possible-to-do-it-using-other-tools" |}) sessionId (createObj [])
        chk "e2e-omp.executor.responded" ((jsonStringify executorResult).Contains "hello" && not ((jsonStringify executorResult).Contains "error"))

        let! execWaitResult = h.triggerTool "executor_wait" (box {| ms = 100 |}) sessionId (createObj [])
        chk "e2e-omp.executor_wait.responded" (not ((jsonStringify execWaitResult).Contains "error"))

        let! execAbortResult = h.triggerTool "executor_abort" (createObj []) sessionId (createObj [])
        chk "e2e-omp.executor_abort.responded" ((jsonStringify execAbortResult).Contains "Runner abort requested." && not ((jsonStringify execAbortResult).Contains "error"))

        // 4. websearch & webfetch
        do! h.expectText "Test search summary details"
        let! websearchResult = h.triggerTool "websearch" (box {| query = "test"; what_to_summarize = "summary" |}) sessionId (createObj [])
        chk "e2e-omp.websearch.responded" ((jsonStringify websearchResult).Contains "Test search summary details" && not ((jsonStringify websearchResult).Contains "error"))

        let! webfetchResult = h.triggerTool "webfetch" (box {| url = "http://example.com" |}) sessionId (createObj [])
        chk "e2e-omp.webfetch.responded" ((jsonStringify webfetchResult).Contains "Example Domain" && not ((jsonStringify webfetchResult).Contains "error"))

        // 5. coder, meditator, browser
        let! coderResult = h.triggerTool "coder" (box {| intents = [||]; tdd = "green"; warn_tdd = "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles" |}) sessionId (createObj [])
        chk "e2e-omp.coder.responded" ((jsonStringify coderResult).Contains "Invalid LLM input for coder" && not ((jsonStringify coderResult).Contains "\"isError\":false"))

        do! h.expectText "meditator output mock analysis"
        let! meditatorResult = h.triggerTool "meditator" (box {| intent = "analyze"; files = [||] |}) sessionId (createObj [])
        chk "e2e-omp.meditator.responded" ((jsonStringify meditatorResult).Contains "meditator output mock analysis" && not ((jsonStringify meditatorResult).Contains "error"))

        do! h.expectText "browser mock debug view text"
        let! browserResult = h.triggerTool "browser" (box {| intent = "browse" |}) sessionId (createObj [])
        chk "e2e-omp.browser.responded" ((jsonStringify browserResult).Contains "browser mock debug view text" && not ((jsonStringify browserResult).Contains "error"))

        // 6. methodology (unified)
        do! h.expectText "methodology analysis output"
        let! methodologyResult = h.triggerTool "methodology" (box {| methodology = "first_principles"; note = String.replicate 1100 "n"; background = String.replicate 1100 "b"; intent = String.replicate 1100 "i" |}) sessionId (createObj [])
        chk "e2e-omp.methodology.responded" ((jsonStringify methodologyResult).Contains "methodology analysis output" && not ((jsonStringify methodologyResult).Contains "error"))

        do! h.expectText "risk analysis output"
        let! riskResult = h.triggerTool "methodology" (box {| methodology = "risk_analysis"; note = String.replicate 1100 "n"; background = String.replicate 1100 "b"; intent = String.replicate 1100 "i" |}) sessionId (createObj [])
        chk "e2e-omp.methodology.risk_analysis.responded" ((jsonStringify riskResult).Contains "risk analysis output" && not ((jsonStringify riskResult).Contains "error"))

        do! h.expectText "thought experiment output"
        let! thoughtResult = h.triggerTool "methodology" (box {| methodology = "thought_experiment"; note = String.replicate 1100 "n"; background = String.replicate 1100 "b"; intent = String.replicate 1100 "i" |}) sessionId (createObj [])
        chk "e2e-omp.methodology.thought_experiment.responded" ((jsonStringify thoughtResult).Contains "thought experiment output" && not ((jsonStringify thoughtResult).Contains "error"))

        // 7. investigator
        let investigatorIntents = [|
            box {|
                objective = "Test investigator e2e"
                background = "Ensure no f.content crashes"
                questions = [| "Did it crash?" |]
                entries = [| "README.md"; "DOES_NOT_EXIST_AT_ALL.md" |]
            |}
        |]
        do! h.expectText "investigator e2e verification output: mock text content"
        let! investigatorResult = h.triggerTool "investigator" (box {| intents = investigatorIntents |}) sessionId (createObj [])
        let investigatorStr = jsonStringify investigatorResult
        chk "e2e-omp.investigator.ran" (investigatorStr.Contains "mock text content")

        // 8. todowrite
        let args = box {|
            todos = ResizeArray([box {| content = "verify omp e2e"; status = "in_progress"; priority = "high" |}])
            ahaMoments = String.replicate 1100 "a"
            changesAndReasons = String.replicate 1100 "c"
            gotchas = String.replicate 1100 "g"
            lessonsAndConventions = String.replicate 1100 "l"
            plan = String.replicate 1100 "p"
            select_methodology = ResizeArray([box "first_principles"])
        |}
        let! _ = h.triggerTool "todowrite" args sessionId (createObj [])
        chk "e2e-omp.todowrite.ran" true
        let! ndWritten = h.waitForNdjson 1 5000
        chk "e2e-omp.todowrite.ndjson-written" ndWritten
        let! ndLines = h.readNdjson ()
        chk "e2e-omp.todowrite.ndjson-has-work-backlog" (ndLines.Contains "work_backlog_committed")
        chk "e2e-omp.todowrite.ndjson-has-session" (ndLines.Contains sessionId)

        // 9. Commands: loop
        let! _ = h.runCommand "loop" "implement task X" sessionId
        let! ndWritten2 = h.waitForNdjson 2 5000
        let! ndLines2 = h.readNdjson ()
        chk "e2e-omp.cmd.loop.success" (ndLines2.Contains "loop_activated" && ndLines2.Contains "implement task X")

        // 10. submit_review wip = true
        let! submitWipRes = h.triggerTool "submit_review" (createObj [ "report", box "wip progress report"; "affectedFiles", box [||]; "wip", box true ]) sessionId (createObj [])
        chk "e2e-omp.submit_review.wip_true.success" ((jsonStringify submitWipRes).Contains "Your progress report was recorded" && not ((jsonStringify submitWipRes).Contains "error"))

        // 11. submit_review wip = false (starts child review loop)
        do! h.expectTool "return_reviewer" (box {| verdict = "PERFECT"; feedback = "" |})
        do! h.expectTool "return_reviewer" (box {| verdict = "REVISE"; feedback = "precheck requires details" |})
        let! submitFinalRes = h.triggerTool "submit_review" (createObj [ "report", box "final review submission"; "affectedFiles", box [||]; "wip", box false ]) sessionId (createObj [])
        let submitFinalResStr = jsonStringify submitFinalRes
        chk "e2e-omp.submit_review.wip_false.success" (submitFinalResStr.Contains "Review passed. Loop mode ended." && not (submitFinalResStr.Contains "error"))

        do! Promise.sleep 100

        // 12. Commands: loop-review
        let! _ = h.runCommand "loop-review" "implement task Y" sessionId
        let! ndWritten3 = h.waitForNdjson 5 5000
        let! ndLines3 = h.readNdjson ()
        chk "e2e-omp.cmd.loop-review.success" (ndLines3.Contains "implement task Y")

        // 13. Verify expectations empty
        let remaining = h.getRemainingExpectations()
        chk "e2e-omp.mock-llm.expectations-empty" (remaining = 0)

        let! _ = h.emitEvent "session_shutdown" (createObj []) sessionId
        do! h.dispose ()
        printfn "\n✓ %d/%d omp e2e checks passed" ok expected
        return summary ()
    }
