module Wanxiangshu.E2e.MuxTestsPart2

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Tests.Assert

[<Emit("JSON.stringify($0)")>]
let private jsonStringify (o: obj) : string = jsNative

type MockLLM =
    abstract expectTool: string -> obj -> unit
    abstract expectText: string -> unit
    abstract reset: unit -> unit
    abstract getRemainingExpectations: unit -> int
    abstract calls: ResizeArray<obj>

type Harness =
    abstract mockLLM: MockLLM
    abstract workDir: string
    abstract helpers: obj
    abstract registration: obj
    abstract fireEvent: obj -> JS.Promise<obj>
    abstract fireStreamEnd: string -> string[] -> JS.Promise<obj>
    abstract fireStreamAbort: string -> JS.Promise<obj>
    abstract runMessageTransform: obj -> obj -> JS.Promise<obj>
    abstract runSystemTransform: obj -> obj -> JS.Promise<obj>
    abstract getToolDefinition: string -> obj
    abstract getToolSchema: string -> obj
    abstract executeTool: string -> obj -> obj -> JS.Promise<string>
    abstract runSlashCommand: string -> string -> string -> JS.Promise<string>
    abstract getChatHistoryCalled: unit -> bool
    abstract getLastLlmRequest: unit -> obj
    abstract setMockReportMarkdown: string -> unit
    abstract dispose: unit -> JS.Promise<unit>

let runRest
    (harness: Harness)
    (chk: string -> bool -> unit)
    (runTool: string -> obj -> (string -> bool) -> string -> JS.Promise<unit>)
    (warnTddValue: string)
    (reg: obj)
    (fileExists: string -> bool)
    (readFileSync: string -> string -> string)
    (dynGet: obj -> string -> obj)
    (dynIsNull: obj -> bool)
    (dynIsArr: obj -> bool)
    (dynTypeIs: obj -> string -> bool)
    (dynStr: obj -> string -> string)
    (nudgeCount: Harness -> int)
    (setTodos: Harness -> obj[] -> unit)
    (createEmpty: unit -> obj)
    : JS.Promise<unit> =
    promise {
        let investigatorIntents =
            [| box
                   {| objective = "Test"
                      background = "none"
                      questions = [| "ok?" |]
                      entries = [| "mux-e2e-test.md" |] |} |]

        harness.setMockReportMarkdown "investigator output mock text"
        harness.mockLLM.expectText "investigator output mock text"

        do!
            runTool
                "investigator"
                (createObj
                    [ "intents", box investigatorIntents
                      "tdd", box "green"
                      "warn_tdd", box warnTddValue
                      "warn_reuse", box "this-task-is-not-suitable-to-be-completed-via-continue-tool" ])
                (fun r -> r.Contains "investigator output mock text")
                "mux.execute.investigator.success"

        harness.setMockReportMarkdown "browser mock debug view text"
        harness.mockLLM.expectText "browser mock debug view text"

        do!
            runTool
                "browser"
                (createObj
                    [ "intent", box "browse"
                      "warn_reuse", box "this-task-is-not-suitable-to-be-completed-via-continue-tool" ])
                (fun r -> r.Contains "browser mock debug view text")
                "mux.execute.browser.success"

        harness.setMockReportMarkdown "websearch mock output summary text"
        harness.mockLLM.expectText "websearch mock output summary text"

        do!
            runTool
                "websearch"
                (createObj [ "query", box "ai"; "what_to_summarize", box "summary" ])
                (fun r -> r.Contains "websearch mock output summary text")
                "mux.execute.websearch.success"

        do!
            runTool
                "webfetch"
                (createObj [ "url", box "http://localhost" ])
                (fun r -> r.Contains "Example Domain")
                "mux.execute.webfetch.success"

        harness.setMockReportMarkdown "meditator mock report output"
        harness.mockLLM.expectText "meditator mock report output"

        do!
            runTool
                "meditator"
                (createObj
                    [ "methodology", box "first_principles"
                      "note", box (String.replicate 1100 "n")
                      "background", box (String.replicate 1100 "b")
                      "intent", box (String.replicate 1100 "i")
                      "warn_reuse", box "this-task-is-not-suitable-to-be-completed-via-continue-tool" ])
                (fun r -> r.Contains "meditator mock report output")
                "mux.execute.meditator.success"

        let lastReq = harness.getLastLlmRequest ()
        let lastReqStr = jsonStringify lastReq
        chk "mux.meditator.prompt-contains-background" (lastReqStr.Contains(String.replicate 1100 "b"))

        let coderIntents =
            [| box
                   {| objective = "Fix spelling"
                      background = "none"
                      targets =
                       [| box
                              {| file = "mux-e2e-test.md"
                                 guide = "Fix all typos" |} |] |} |]

        harness.setMockReportMarkdown "coder mock execution output"
        harness.mockLLM.expectText "coder mock execution output"

        do!
            runTool
                "coder"
                (createObj
                    [ "intents", box coderIntents
                      "tdd", box "green"
                      "warn_tdd", box warnTddValue
                      "warn_reuse", box "this-task-is-not-suitable-to-be-completed-via-continue-tool" ])
                (fun r -> r.Contains "coder mock execution output")
                "mux.execute.coder.success"

        do!
            runTool
                "coder"
                (createObj
                    [ "intents", box coderIntents
                      "tdd", box "green"
                      "warn_tdd", box "wrong_warn_tdd"
                      "warn_reuse", box "this-task-is-not-suitable-to-be-completed-via-continue-tool" ])
                (fun r -> r.Contains "acknowledge")
                "mux.execute.coder.warnTddRejected"

        // --- 4. Event hook: nudge via stream-end with open todos -------------
        let todoItem: obj =
            createObj [ "content", box "open-task"; "status", box "in_progress" ]

        setTodos harness [| todoItem |]
        let nudgeBefore = nudgeCount harness

        let! _ =
            withTimeout (
                harness.fireStreamEnd
                    "mux-e2e-session"
                    [| "I have completed some work; let me know if there is anything else to do." |]
            )

        let nudgeAfter = nudgeCount harness
        chk "mux.eventHook.nudgeCalledOnStreamEnd" (nudgeAfter > nudgeBefore)
        chk "mux.eventHook.getChatHistoryCalled" (harness.getChatHistoryCalled ())

        // --- 5. Message transform: caps injection ---------------------------
        let textPart: obj =
            createObj [ "type", box "text"; "text", box "initial user message"; "state", box "done" ]

        let userMsg =
            createObj [ "id", box "user-turn-1"; "role", box "user"; "parts", box [| textPart |] ]

        let outputObj = createObj [ "messages", box [| userMsg |] ]

        let inputObj =
            createObj [ "agent", box "manager"; "sessionID", box "mux-e2e-session" ]

        let! transformedOutput = withTimeout (harness.runMessageTransform inputObj outputObj)
        let messagesOut: obj[] = unbox<obj[]> (dynGet transformedOutput "messages")
        chk "mux.messageTransform.capsAdded" (messagesOut.Length > 1)
        let firstMsg = messagesOut.[0]
        let firstId = dynStr firstMsg "id"
        chk "mux.messageTransform.firstMessageIsCaps" (firstId.StartsWith "caps-synth-user-")
        let firstParts: obj[] = unbox<obj[]> (dynGet firstMsg "parts")

        let firstText =
            if firstParts.Length > 0 && dynStr firstParts.[0] "type" = "text" then
                dynStr firstParts.[0] "text"
            else
                ""

        chk "mux.messageTransform.capsHasKolmolgorov" (firstText.Contains "# Kolmolgorov 宝典")
        chk "mux.messageTransform.capsHasIronLaw" (firstText.Contains "铁律")

        // --- 5c. Message transform: system transform injection ----------------
        let systemObj =
            createObj [ "content", box "long system prompt"; "length", box 1000 ]

        let systemOutput = createObj [ "system", box systemObj ]
        let! _ = withTimeout (harness.runSystemTransform (createEmpty ()) systemOutput)
        let systemOut = dynGet systemOutput "system"
        chk "mux.messageTransform.systemTransform.runOk" (dynIsArr systemOut)
        let systemArr = unbox<obj[]> systemOut

        chk
            "mux.messageTransform.systemTransform.hasDirectory"
            (systemArr.Length = 1 && string systemArr.[0] = harness.workDir)

        // --- 6. Slash command: /loop activates review -----------------------
        let! loopResponse = withTimeout (harness.runSlashCommand "loop" "mux-e2e-session" "implement feature X")
        chk "mux.slash.loop.responseContainsWithReview" (loopResponse.Contains "With-Review Mode")
        chk "mux.slash.loop.eventLogCreated" (fileExists (harness.workDir + "/.wanxiangshu.ndjson"))

        // --- 6a. Tool execution: submit_review ------------------------------
        do!
            runTool
                "submit_review"
                (createObj [ "report", box "wip progress"; "affectedFiles", box [||]; "wip", box true ])
                (fun r -> r.Contains "recorded")
                "mux.execute.submit_review.wip_true.success"

        harness.setMockReportMarkdown "PERFECT: everything looks perfectly clean"

        do!
            runTool
                "submit_review"
                (createObj
                    [ "report", box "final description"
                      "affectedFiles", box [||]
                      "wip", box false ])
                (fun r -> r.Contains "With-Review Mode has ended")
                "mux.execute.submit_review.wip_false.accepted"

        let! _ = withTimeout (harness.runSlashCommand "loop" "mux-e2e-session" "implement feature X version 2")
        harness.setMockReportMarkdown "REVISE: please verify extreme cases"

        do!
            runTool
                "submit_review"
                (createObj
                    [ "report", box "final description"
                      "affectedFiles", box [||]
                      "wip", box false ])
                (fun r -> r.Contains "With-Review Mode is still active")
                "mux.execute.submit_review.wip_false.needs_revision"

        let! _ = withTimeout (harness.runSlashCommand "loop" "mux-e2e-session" "") // deactivate

        // --- 6b. Slash command: /loop-review --------------------------------
        harness.setMockReportMarkdown "PERFECT: precheck passed without revisions"
        let! loopRevRes1 = withTimeout (harness.runSlashCommand "loop-review" "mux-e2e-session" "implement task Alpha")
        chk "mux.slash.loop-review.accepted" (loopRevRes1.Contains "Pre-review passed")

        let! _ = withTimeout (harness.runSlashCommand "loop" "mux-e2e-session" "") // deactivate

        harness.setMockReportMarkdown "REVISE: precheck requires clarifying objectives"
        let! loopRevRes2 = withTimeout (harness.runSlashCommand "loop-review" "mux-e2e-session" "implement task Beta")
        chk "mux.slash.loop-review.needs_revision" (loopRevRes2.Contains "Pre-review feedback")

        let! _ = withTimeout (harness.runSlashCommand "loop" "mux-e2e-session" "") // deactivate

        // --- 6d. Slash command: /loop empty task & stream abort --------------
        let! emptyResponse = withTimeout (harness.runSlashCommand "loop" "mux-e2e-session" "")
        chk "mux.slash.loop.emptyTaskReturnsCancelled" (emptyResponse.Contains "With-Review Mode cancelled")

        let! loopResponseAbort = withTimeout (harness.runSlashCommand "loop" "mux-e2e-session" "test stream-abort")
        chk "mux.eventHook.abort.activateOk" (loopResponseAbort.Contains "With-Review Mode is active")
        let! _ = withTimeout (harness.fireStreamAbort "mux-e2e-session")

        let reviewStoreSurface = dynGet reg "__reviewStore"
        let getReviewTask = dynGet reviewStoreSurface "getReviewTask"
        let taskResult = getReviewTask $ "mux-e2e-session"
        chk "mux.eventHook.abort.deactivated" (dynIsNull taskResult)

        let! _ = withTimeout (harness.runSlashCommand "loop" "mux-e2e-session" "")

        // --- 7. Verify expectations empty -----------------------------------
        let remaining = harness.mockLLM.getRemainingExpectations ()
        chk "mux.mock-llm.expectations-empty" (remaining = 0)

        do! withTimeout (harness.dispose ())
    }
