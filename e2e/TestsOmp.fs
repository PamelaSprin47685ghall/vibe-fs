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
    abstract triggerTool: string -> obj -> string -> obj -> JS.Promise<obj>
    abstract emitEvent: string -> obj -> string -> JS.Promise<obj>
    abstract readNdjson: unit -> JS.Promise<string>
    abstract readFile: string -> JS.Promise<string>
    abstract fileExists: string -> JS.Promise<bool>
    abstract getCommands: unit -> JS.Promise<obj>
    abstract expectText: string -> JS.Promise<unit>
    abstract expectTool: string -> obj -> JS.Promise<unit>
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

        let expected = 23
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
        // Real OMP runtime registers a single unified "methodology" tool,
        // not 54 individual methodology_* tools. The 54 methodologies are
        // dispatched inside the tool body via select_methodology.
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

        let! fuzzyFindResult = h.triggerTool "fuzzy_find" (box {| pattern = "README.md" |}) sessionId (createObj [])
        let fuzzyFindStr = jsonStringify fuzzyFindResult
        chk "e2e-omp.fuzzy-find.responded" (fuzzyFindStr.Contains "content")

        let investigatorIntents = [|
            box {|
                objective = "Test investigator e2e"
                background = "Ensure no f.content crashes"
                questions = [| "Did it crash?" |]
                entries = [| "README.md" |]
            |}
        |]
        do! h.expectText "investigator e2e verification output: mock text content"
        let! investigatorResult = h.triggerTool "investigator" (box {| intents = investigatorIntents |}) sessionId (createObj [])
        let investigatorStr = jsonStringify investigatorResult
        chk "e2e-omp.investigator.ran" (investigatorStr.Contains "mock text content")

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

        let! _ = h.emitEvent "session_shutdown" (createObj []) sessionId
        do! h.dispose ()
        printfn "\n✓ %d/%d omp e2e checks passed" ok expected
        return summary ()
    }
