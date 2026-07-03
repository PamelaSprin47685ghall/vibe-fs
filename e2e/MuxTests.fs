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
    abstract runCompactingTransform: obj -> obj -> JS.Promise<obj>
    abstract runSystemTransform: obj -> obj -> JS.Promise<obj>
    abstract getToolDefinition: string -> obj
    abstract getToolSchema: string -> obj
    abstract executeTool: string -> obj -> obj -> JS.Promise<string>
    abstract runSlashCommand: string -> string -> string -> JS.Promise<string>
    abstract getChatHistoryCalled: unit -> bool
    abstract getLastLlmRequest: unit -> obj
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
    if dynIsNull nudges then 0
    else
        let arr : obj[] = unbox<obj[]> nudges
        arr.Length

let private setTodos (harness: Harness) (todos: obj[]) : unit =
    let setter = dynGet harness.helpers "_setTodoList"
    if not (dynIsNull setter) then
        setter $ (box todos) |> ignore

let private toolSchemaProperties (harness: Harness) (name: string) : obj =
    let schema = harness.getToolSchema name
    if dynIsNull schema then null
    else dynGet schema "properties"

let private toolSchemaRequiredArray (harness: Harness) (name: string) : string array =
    let schema = harness.getToolSchema name
    if dynIsNull schema then [||]
    else
        let req = dynGet schema "required"
        if dynIsNull req || not (dynIsArr req) then [||]
        else unbox<string[]> req

let private warnTddValue = "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles"

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let histTextPart : obj = createObj [ "type", box "text"; "text", box "I have completed the previous task." ]
        let histParts : obj[] = [| histTextPart |]
        let histMsg : obj = createObj [ "id", box "hist-1"; "role", box "assistant"; "parts", box histParts ]
        let chatHistory : obj[] = [| histMsg |]
        let startOpts : obj = createObj [ "workspaceId", box "mux-e2e-session"; "chatHistory", box chatHistory ]
        let! apiObj = startMux startOpts
        let harness = harnessFromObj apiObj
        let reg = harness.registration

        let mutable ok = 0
        let chk label cond =
            check label cond
            if cond then ok <- ok + 1

        // --- 1. Plugin registration structure --------------------------------
        let tools = dynGet reg "tools"
        chk "mux.reg.tools.isArray"
            (not (dynIsNull tools) && dynIsArr tools)
        chk "mux.reg.tools.nonEmpty"
            (not (dynIsNull tools) && (unbox<obj[]> tools).Length > 0)

        let eventHook = dynGet reg "eventHook"
        chk "mux.reg.eventHook.isFunction"
            (not (dynIsNull eventHook) && dynTypeIs eventHook "function")

        let messagesTransform = dynGet reg "messagesTransform"
        chk "mux.reg.messagesTransform.isFunction"
            (not (dynIsNull messagesTransform) && dynTypeIs messagesTransform "function")

        let slashCommands = dynGet reg "slashCommands"
        chk "mux.reg.slashCommands.isArray"
            (not (dynIsNull slashCommands) && dynIsArr slashCommands)
        chk "mux.reg.slashCommands.hasLoop"
            (let cmds : obj[] = unbox<obj[]> slashCommands in
             cmds |> Array.exists (fun c -> dynStr c "key" = "loop"))
        chk "mux.reg.slashCommands.hasLoopReview"
            (let cmds : obj[] = unbox<obj[]> slashCommands in
             cmds |> Array.exists (fun c -> dynStr c "key" = "loop-review"))

        // --- 2. Tool schemas -------------------------------------------------
        let propsWrite = toolSchemaProperties harness "write"
        chk "mux.schema.write.hasFilePath" (not (dynIsNull (dynGet propsWrite "file_path")))
        chk "mux.schema.write.hasContent" (not (dynIsNull (dynGet propsWrite "content")))
        chk "mux.schema.write.requiredFilePath"
            (toolSchemaRequiredArray harness "write" |> Array.contains "file_path")
        chk "mux.schema.write.requiredContent"
            (toolSchemaRequiredArray harness "write" |> Array.contains "content")

        let propsRead = toolSchemaProperties harness "read"
        chk "mux.schema.read.hasPath" (not (dynIsNull (dynGet propsRead "path")))
        chk "mux.schema.read.requiredPath"
            (toolSchemaRequiredArray harness "read" |> Array.contains "path")

        let propsExec = toolSchemaProperties harness "executor"
        chk "mux.schema.executor.hasProgram" (not (dynIsNull (dynGet propsExec "program")))
        chk "mux.schema.executor.hasLanguage" (not (dynIsNull (dynGet propsExec "language")))
        chk "mux.schema.executor.requiredProgram"
            (toolSchemaRequiredArray harness "executor" |> Array.contains "program")

        let propsFuzzy = toolSchemaProperties harness "fuzzy_find"
        chk "mux.schema.fuzzyFind.hasPattern" (not (dynIsNull (dynGet propsFuzzy "pattern")))

        // --- 3. Tool execution: write ----------------------------------------
        let writeArgs =
            createObj [ "file_path", box "mux-e2e-test.txt"
                        "content", box "hello from mux e2e"
                        "warn_tdd", box warnTddValue ]
        let! writeResult = harness.executeTool "write" writeArgs (createEmpty ())
        let writeOk = fileExists (harness.workDir + "/mux-e2e-test.txt")
        chk "mux.execute.write.fileCreated" writeOk
        chk "mux.execute.write.success"
            ((jsonStringify writeResult).Contains "Successfully")

        if writeOk then
            let content = readFileSync (harness.workDir + "/mux-e2e-test.txt") "utf8"
            chk "mux.execute.write.contentCorrect" (content.Contains "hello from mux e2e")

        // --- 3b. Tool execution: write with warn_tdd failure path ------------
        let writeArgsNoWarn =
            createObj [ "file_path", box "mux-e2e-fail.txt"
                        "content", box "should fail"
                        "warn_tdd", box "wrong-value" ]
        let! writeFailResult = harness.executeTool "write" writeArgsNoWarn (createEmpty ())
        chk "mux.execute.write.warnTddRejected"
            ((jsonStringify writeFailResult).Contains "error")

        // --- 3c. Tool execution: read success --------------------------------
        let readArgs =
            createObj [ "path", box "mux-e2e-test.txt" ]
        let! readResult = harness.executeTool "read" readArgs (createEmpty ())
        chk "mux.execute.read.success" (readResult.Contains "hello from mux e2e")

        // --- 3d. Tool execution: executor success ----------------------------
        let execArgs =
            createObj [ "program", box "echo hello-executor"
                        "language", box "shell"
                        "mode", box "ro"
                        "timeout_type", box "short"
                        "what_to_summarize", box "keep stdout only"
                        "warn_tdd", box warnTddValue
                        "warn", box "it-is-not-possible-to-do-it-using-other-tools" ]
        let! execResult = harness.executeTool "executor" execArgs (createEmpty ())
        chk "mux.execute.executor.success" (execResult.Contains "hello-executor")

        // --- 3e. Tool execution: fuzzy_find success --------------------------
        let fuzzyArgs =
            createObj [ "pattern", box "mux-e2e" ]
        let! fuzzyResult = harness.executeTool "fuzzy_find" fuzzyArgs (createEmpty ())
        chk "mux.execute.fuzzyFind.success" (fuzzyResult.Contains "mux-e2e-test.txt")

        // --- 4. Event hook: nudge via stream-end with open todos -------------
        let todoItem : obj = createObj [ "content", box "open-task"; "status", box "in_progress" ]
        setTodos harness [| todoItem |]
        let nudgeBefore = nudgeCount harness
        let! _ = harness.fireStreamEnd "mux-e2e-session" [| "I have completed some work; let me know if there is anything else to do." |]
        let nudgeAfter = nudgeCount harness
        chk "mux.eventHook.nudgeCalledOnStreamEnd" (nudgeAfter > nudgeBefore)
        chk "mux.eventHook.getChatHistoryCalled" (harness.getChatHistoryCalled())

        // --- 5. Message transform: caps injection ---------------------------
        let textPart : obj = createObj [ "type", box "text"; "text", box "initial user message"; "state", box "done" ]
        let userMsg =
            createObj [ "id", box "user-turn-1"
                        "role", box "user"
                        "parts", box [| textPart |] ]
        let outputObj = createObj [ "messages", box [| userMsg |] ]
        let inputObj = createObj [ "agent", box "manager"; "sessionID", box "mux-e2e-session" ]
        let! transformedOutput = harness.runMessageTransform inputObj outputObj
        let messagesOut : obj[] = unbox<obj[]> (dynGet transformedOutput "messages")
        chk "mux.messageTransform.capsAdded" (messagesOut.Length > 1)
        let firstMsg = messagesOut.[0]
        let firstId = dynStr firstMsg "id"
        chk "mux.messageTransform.firstMessageIsCaps" (firstId.StartsWith "caps-synth-user-")
        let firstParts : obj[] = unbox<obj[]> (dynGet firstMsg "parts")
        let firstText =
            if firstParts.Length > 0 && dynStr firstParts.[0] "type" = "text"
            then dynStr firstParts.[0] "text"
            else ""
        chk "mux.messageTransform.capsHasKolmolgorov" (firstText.Contains "# Kolmolgorov 宝典")
        chk "mux.messageTransform.capsHasIronLaw" (firstText.Contains "铁律")

        // --- 5b. Message transform: compactingTransform ------------------------
        let msgTextPart : obj = createObj [ "type", box "text"; "text", box "compact message test" ]
        let testMsg = createObj [ "id", box "msg-1"; "role", box "user"; "parts", box [| msgTextPart |] ]
        let compactOutput = createObj [ "messages", box [| testMsg |] ]
        let compactInput = createObj [ "sessionID", box "mux-e2e-session" ]
        let! _ = harness.runCompactingTransform compactInput compactOutput
        let compactMsgsOut : obj[] = unbox<obj[]> (dynGet compactOutput "messages")
        chk "mux.messageTransform.compactingTransform.runOk" (compactMsgsOut.Length = 1)

        // --- 5c. Message transform: system transform injection ----------------
        let systemObj = createObj [ "content", box "long system prompt"; "length", box 1000 ]
        let systemOutput = createObj [ "system", box systemObj ]
        let! _ = harness.runSystemTransform (createEmpty ()) systemOutput
        let systemOut = dynGet systemOutput "system"
        chk "mux.messageTransform.systemTransform.runOk" (dynIsArr systemOut)
        let systemArr = unbox<obj[]> systemOut
        chk "mux.messageTransform.systemTransform.hasDirectory" (systemArr.Length = 1 && string systemArr.[0] = harness.workDir)

        // --- 6. Slash command: /loop activates review -----------------------
        let! loopResponse = harness.runSlashCommand "loop" "mux-e2e-session" "implement feature X"
        chk "mux.slash.loop.responseContainsWithReview"
            (loopResponse.Contains "With-Review Mode")

        let eventLogPath = harness.workDir + "/.wanxiangshu.ndjson"
        chk "mux.slash.loop.eventLogCreated" (fileExists eventLogPath)
        if fileExists eventLogPath then
            let eventLogContent = readFileSync eventLogPath "utf8"
            chk "mux.slash.loop.eventLogContainsLoopActivated" (eventLogContent.Contains "loop_activated")
            chk "mux.slash.loop.eventLogContainsTaskText" (eventLogContent.Contains "implement feature X")

        // --- 6b. Slash command: /loop with empty task returns cancelled -----
        let! emptyResponse = harness.runSlashCommand "loop" "mux-e2e-session" ""
        chk "mux.slash.loop.emptyTaskReturnsCancelled" (emptyResponse.Contains "With-Review Mode cancelled")

        // --- 6c. Event hook: stream abort ------------------------------------
        let! loopResponseAbort = harness.runSlashCommand "loop" "mux-e2e-session" "test stream-abort"
        chk "mux.eventHook.abort.activateOk" (loopResponseAbort.Contains "With-Review Mode is active")
        let! _ = harness.fireStreamAbort "mux-e2e-session"

        let reviewStoreSurface = dynGet reg "__reviewStore"
        let getReviewTask = dynGet reviewStoreSurface "getReviewTask"
        let taskResult = getReviewTask $ "mux-e2e-session"
        chk "mux.eventHook.abort.deactivated" (dynIsNull taskResult)

        let! _ = harness.runSlashCommand "loop" "mux-e2e-session" ""

        do! harness.dispose ()

        printfn "\n✓ %d mux e2e checks passed" ok
        return summary ()
    }
