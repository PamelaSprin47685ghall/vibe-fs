module Wanxiangshu.E2e.MuxTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Tests.Assert

[<Import("start", "./mux-harness.js")>]
let private startMux: obj -> JS.Promise<obj> = jsNative

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

[<Import("existsSync", "node:fs")>]
let private fileExists (path: string) : bool = jsNative

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

let private harnessFromObj (o: obj) : Harness = unbox o
let private createEmpty () = createObj []

let private dynGet (o: obj) (k: string) = get o k
let private dynIsNull (o: obj) = isNullish o
let private dynIsArr (o: obj) = isArray o
let private dynTypeIs (o: obj) (t: string) = typeIs o t
let private dynStr (o: obj) (k: string) = str o k

[<Emit("JSON.stringify($0)")>]
let private jsonStringify (o: obj) : string = jsNative

let private nudgeCount (harness: Harness) : int =
    let nudges = dynGet harness.helpers "nudges"

    if dynIsNull nudges then
        0
    else
        let arr: obj[] = unbox<obj[]> nudges
        arr.Length

let private setTodos (harness: Harness) (todos: obj[]) : unit =
    let setter = dynGet harness.helpers "_setTodoList"

    if not (dynIsNull setter) then
        setter $ (box todos) |> ignore

let private toolSchemaProperties (harness: Harness) (name: string) : obj =
    let schema = harness.getToolSchema name

    if dynIsNull schema then
        null
    else
        dynGet schema "properties"

let private toolSchemaRequiredArray (harness: Harness) (name: string) : string array =
    let schema = harness.getToolSchema name

    if dynIsNull schema then
        [||]
    else
        let req = dynGet schema "required"

        if dynIsNull req || not (dynIsArr req) then
            [||]
        else
            unbox<string[]> req

let private warnTddValue =
    "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles"

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()

        let histTextPart: obj =
            createObj [ "type", box "text"; "text", box "I have completed the previous task." ]

        let histParts: obj[] = [| histTextPart |]

        let histMsg: obj =
            createObj [ "id", box "hist-1"; "role", box "assistant"; "parts", box histParts ]

        let chatHistory: obj[] = [| histMsg |]

        let startOpts: obj =
            createObj [ "workspaceId", box "mux-e2e-session"; "chatHistory", box chatHistory ]

        let! apiObj = startMux startOpts
        let harness = harnessFromObj apiObj
        let reg = harness.registration

        let mutable ok = 0

        let chk label cond =
            check label cond

            if cond then
                ok <- ok + 1

        let runTool name args pred label =
            promise {
                let! res = harness.executeTool name args (createEmpty ())
                let cond = pred res

                if not cond then
                    printfn "DEBUG FAIL: %s -> %s" label res

                chk label cond
            }

        // --- 1. Plugin registration structure --------------------------------
        let tools = dynGet reg "tools"
        chk "mux.reg.tools.isArray" (not (dynIsNull tools) && dynIsArr tools)
        chk "mux.reg.tools.nonEmpty" (not (dynIsNull tools) && (unbox<obj[]> tools).Length > 0)

        let eventHook = dynGet reg "eventHook"
        chk "mux.reg.eventHook.isFunction" (not (dynIsNull eventHook) && dynTypeIs eventHook "function")

        let messagesTransform = dynGet reg "messagesTransform"

        chk
            "mux.reg.messagesTransform.isFunction"
            (not (dynIsNull messagesTransform) && dynTypeIs messagesTransform "function")

        let slashCommands = dynGet reg "slashCommands"
        chk "mux.reg.slashCommands.isArray" (not (dynIsNull slashCommands) && dynIsArr slashCommands)

        chk
            "mux.reg.slashCommands.hasLoop"
            (let cmds: obj[] = unbox<obj[]> slashCommands in cmds |> Array.exists (fun c -> dynStr c "key" = "loop"))

        chk
            "mux.reg.slashCommands.hasLoopReview"
            (let cmds: obj[] = unbox<obj[]> slashCommands in
             cmds |> Array.exists (fun c -> dynStr c "key" = "loop-review"))

        // --- 2. Tool schemas -------------------------------------------------
        let propsWrite = toolSchemaProperties harness "write"
        chk "mux.schema.write.hasFilePath" (not (dynIsNull (dynGet propsWrite "file_path")))
        chk "mux.schema.write.hasContent" (not (dynIsNull (dynGet propsWrite "content")))
        chk "mux.schema.write.requiredFilePath" (toolSchemaRequiredArray harness "write" |> Array.contains "file_path")
        chk "mux.schema.write.requiredContent" (toolSchemaRequiredArray harness "write" |> Array.contains "content")

        let propsRead = toolSchemaProperties harness "read"
        chk "mux.schema.read.hasPath" (not (dynIsNull (dynGet propsRead "path")))
        chk "mux.schema.read.requiredPath" (toolSchemaRequiredArray harness "read" |> Array.contains "path")

        let propsExec = toolSchemaProperties harness "executor"
        chk "mux.schema.executor.hasProgram" (not (dynIsNull (dynGet propsExec "program")))
        chk "mux.schema.executor.hasLanguage" (not (dynIsNull (dynGet propsExec "language")))

        chk
            "mux.schema.executor.requiredProgram"
            (toolSchemaRequiredArray harness "executor" |> Array.contains "program")

        let propsFuzzy = toolSchemaProperties harness "fuzzy_find"
        chk "mux.schema.fuzzyFind.hasPattern" (not (dynIsNull (dynGet propsFuzzy "pattern")))

        // --- 3. Tool execution -----------------------------------------------
        do!
            runTool
                "write"
                (createObj
                    [ "file_path", box "mux-e2e-test.txt"
                      "content", box "hello from mux e2e"
                      "warn_tdd", box warnTddValue ])
                (fun r -> fileExists (harness.workDir + "/mux-e2e-test.txt") && r.Contains "Successfully")
                "mux.execute.write.success"

        let writeOk = fileExists (harness.workDir + "/mux-e2e-test.txt")

        chk
            "mux.execute.write.contentCorrect"
            (writeOk
             && (readFileSync (harness.workDir + "/mux-e2e-test.txt") "utf8").Contains "hello from mux e2e")

        do!
            runTool
                "write"
                (createObj
                    [ "file_path", box "mux-e2e-fail.txt"
                      "content", box "should fail"
                      "warn_tdd", box "wrong" ])
                (fun r -> r.Contains "error")
                "mux.execute.write.warnTddRejected"

        do!
            runTool
                "read"
                (createObj [ "path", box "mux-e2e-test.txt" ])
                (fun r -> r.Contains "hello from mux e2e")
                "mux.execute.read.success"

        do!
            runTool
                "executor"
                (createObj
                    [ "program", box "echo hello-executor"
                      "language", box "shell"
                      "mode", box "ro"
                      "timeout_type", box "short"
                      "what_to_summarize", box "keep stdout only"
                      "warn_tdd", box warnTddValue
                      "warn", box "it-is-not-possible-to-do-it-using-other-tools" ])
                (fun r -> r.Contains "hello-executor")
                "mux.execute.executor.success"

        do!
            runTool
                "fuzzy_find"
                (createObj [ "pattern", box [| "mux-e2e" |] ])
                (fun r -> r.Contains "mux-e2e-test.txt")
                "mux.execute.fuzzyFind.success"

        do!
            runTool
                "fuzzy_grep"
                (createObj [ "pattern", box [| "hello" |] ])
                (fun r -> r.Contains "mux-e2e-test.txt")
                "mux.execute.fuzzyGrep.success"

        let investigatorIntents =
            [| box
                   {| objective = "Test"
                      background = "none"
                      questions = [| "ok?" |]
                      entries = [| "mux-e2e-test.txt" |] |} |]

        harness.mockLLM.expectText "investigator output mock text"

        do!
            runTool
                "investigator"
                (createObj
                    [ "intents", box investigatorIntents
                      "tdd", box "green"
                      "warn_tdd", box warnTddValue ])
                (fun r -> r.Contains "investigator output mock text")
                "mux.execute.investigator.success"

        harness.mockLLM.expectText "meditator output mock analysis"

        do!
            runTool
                "meditator"
                (createObj [ "intent", box "analyze"; "files", box [| "mux-e2e-test.txt" |] ])
                (fun r -> r.Contains "meditator output mock analysis")
                "mux.execute.meditator.success"

        harness.mockLLM.expectText "browser mock debug view text"

        do!
            runTool
                "browser"
                (createObj [ "intent", box "browse" ])
                (fun r -> r.Contains "browser mock debug view text")
                "mux.execute.browser.success"

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

        harness.mockLLM.expectText "methodology mock report output"

        do!
            runTool
                "methodology"
                (createObj
                    [ "methodology", box "first_principles"
                      "note", box (String.replicate 1100 "n")
                      "background", box (String.replicate 1100 "b")
                      "intent", box (String.replicate 1100 "i") ])
                (fun r -> r.Contains "methodology mock report output")
                "mux.execute.methodology.success"

        let coderIntents =
            [| box
                   {| objective = "Fix spelling"
                      background = "none"
                      targets =
                       [| box
                              {| file = "mux-e2e-test.txt"
                                 guide = "Fix all typos" |} |] |} |]

        harness.mockLLM.expectText "coder mock execution output"

        do!
            runTool
                "coder"
                (createObj
                    [ "intents", box coderIntents
                      "tdd", box "green"
                      "warn_tdd", box warnTddValue ])
                (fun r -> r.Contains "coder mock execution output")
                "mux.execute.coder.success"

        // --- 4. Event hook: nudge via stream-end with open todos -------------
        let todoItem: obj =
            createObj [ "content", box "open-task"; "status", box "in_progress" ]

        setTodos harness [| todoItem |]
        let nudgeBefore = nudgeCount harness

        let! _ =
            harness.fireStreamEnd
                "mux-e2e-session"
                [| "I have completed some work; let me know if there is anything else to do." |]

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

        let! transformedOutput = harness.runMessageTransform inputObj outputObj
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
        let! _ = harness.runSystemTransform (createEmpty ()) systemOutput
        let systemOut = dynGet systemOutput "system"
        chk "mux.messageTransform.systemTransform.runOk" (dynIsArr systemOut)
        let systemArr = unbox<obj[]> systemOut

        chk
            "mux.messageTransform.systemTransform.hasDirectory"
            (systemArr.Length = 1 && string systemArr.[0] = harness.workDir)

        // --- 6. Slash command: /loop activates review -----------------------
        let! loopResponse = harness.runSlashCommand "loop" "mux-e2e-session" "implement feature X"
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

        let! _ = harness.runSlashCommand "loop" "mux-e2e-session" "implement feature X version 2"
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

        let! _ = harness.runSlashCommand "loop" "mux-e2e-session" "" // deactivate

        // --- 6b. Slash command: /loop-review --------------------------------
        harness.setMockReportMarkdown "PERFECT: precheck passed without revisions"
        let! loopRevRes1 = harness.runSlashCommand "loop-review" "mux-e2e-session" "implement task Alpha"
        chk "mux.slash.loop-review.accepted" (loopRevRes1.Contains "Pre-review passed")

        let! _ = harness.runSlashCommand "loop" "mux-e2e-session" "" // deactivate

        harness.setMockReportMarkdown "REVISE: precheck requires clarifying objectives"
        let! loopRevRes2 = harness.runSlashCommand "loop-review" "mux-e2e-session" "implement task Beta"
        chk "mux.slash.loop-review.needs_revision" (loopRevRes2.Contains "Pre-review feedback")

        let! _ = harness.runSlashCommand "loop" "mux-e2e-session" "" // deactivate

        // --- 6d. Slash command: /loop empty task & stream abort --------------
        let! emptyResponse = harness.runSlashCommand "loop" "mux-e2e-session" ""
        chk "mux.slash.loop.emptyTaskReturnsCancelled" (emptyResponse.Contains "With-Review Mode cancelled")

        let! loopResponseAbort = harness.runSlashCommand "loop" "mux-e2e-session" "test stream-abort"
        chk "mux.eventHook.abort.activateOk" (loopResponseAbort.Contains "With-Review Mode is active")
        let! _ = harness.fireStreamAbort "mux-e2e-session"

        let reviewStoreSurface = dynGet reg "__reviewStore"
        let getReviewTask = dynGet reviewStoreSurface "getReviewTask"
        let taskResult = getReviewTask $ "mux-e2e-session"
        chk "mux.eventHook.abort.deactivated" (dynIsNull taskResult)

        let! _ = harness.runSlashCommand "loop" "mux-e2e-session" ""

        // --- 7. Verify expectations empty -----------------------------------
        let remaining = harness.mockLLM.getRemainingExpectations ()
        chk "mux.mock-llm.expectations-empty" (remaining = 0)

        do! harness.dispose ()
        printfn "\n✓ %d/53 mux e2e checks passed" ok
        return summary ()
    }
