module Wanxiangshu.E2e.OpencodePluginTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Tests.Assert

[<Import("start", "./opencode-harness.js")>]
let private startHarness: obj -> JS.Promise<obj> = jsNative

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

[<Import("existsSync", "node:fs")>]
let private fileExists (path: string) : bool = jsNative

type Harness =
    abstract plugin: obj
    abstract workDir: string
    abstract home: string
    abstract sessionId: string
    abstract getPlugin: unit -> obj
    abstract getToolNames: unit -> string[]
    abstract getToolEntry: string -> obj
    abstract runToolDefinition: string -> JS.Promise<obj>
    abstract executePluginTool: string -> obj -> obj -> JS.Promise<string>
    abstract runToolWithHooks: string -> obj -> obj -> JS.Promise<string>
    abstract runToolExecuteHooks: string -> obj -> string -> JS.Promise<obj>
    abstract runCommandExecuteBefore: string -> string -> JS.Promise<obj>
    abstract runMessageTransform: obj -> obj -> JS.Promise<obj>
    abstract runSystemTransform: obj -> JS.Promise<obj>
    abstract runConfigHook: obj -> JS.Promise<obj>
    abstract runLifecycleHook: string -> obj -> obj -> JS.Promise<obj>
    abstract fireEvent: obj -> JS.Promise<obj>
    abstract fireStreamAbort: string -> JS.Promise<obj>
    abstract getReviewStore: unit -> obj
    abstract readPartsText: obj -> string
    abstract readFile: string -> string
    abstract fileExists: string -> bool
    abstract dispose: unit -> JS.Promise<unit>

let private harnessFromObj (o: obj) : Harness = unbox o
let private createEmpty () = createObj []

let private dynGet (o: obj) (k: string) = get o k
let private dynIsNull (o: obj) = isNullish o
let private dynIsArr (o: obj) = isArray o
let private dynTypeIs (o: obj) (t: string) = typeIs o t
let private dynStr (o: obj) (k: string) = str o k
let private dynHasKey (o: obj) (k: string) =
    if dynIsNull o then false
    else not (dynIsNull (get o k))

[<Emit("JSON.stringify($0)")>]
let private jsonStringify (o: obj) : string = jsNative

let private toolSchemaProperties (harness: Harness) (name: string) : obj =
    let entry = harness.getToolEntry name
    if dynIsNull entry then null
    else
        // Opencode tool entries have .args (Zod shape) or we can get schema via tool.definition hook
        // For raw schema, try entry.args first
        let args = dynGet entry "args"
        if dynIsNull args then null
        else args

let private warnTddValue = "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles"
let private warnValue = "it-is-not-possible-to-do-it-using-other-tools"

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let! apiObj = startHarness (createEmpty ())
        let harness = harnessFromObj apiObj
        let plugin = harness.getPlugin ()

        let mutable ok = 0
        let chk label cond =
            check label cond
            if cond then ok <- ok + 1

        // --- 1. Identity & Presence -------------------------------------------
        chk "op.id" (dynStr plugin "id" = "wanxiangshu")
        chk "op.name" (dynStr plugin "name" = "wanxiangshu")
        let toolNames = harness.getToolNames ()
        for t in [| "coder"; "methodology"; "pty_spawn"; "pty_write"; "pty_read"; "pty_list"; "pty_kill"; "return_reviewer"; "websearch"; "webfetch"; "executor"; "fuzzy_find"; "submit_review" |] do chk ("op.tool.has." + t) (Array.contains t toolNames)

        let mcp = dynGet plugin "mcp"
        chk "op.mcp.notNull" (not (dynIsNull mcp))
        let stealthMcp = dynGet mcp "stealth-browser-mcp"
        if not (dynIsNull stealthMcp) then
            chk "op.mcp.stealthType" (dynStr stealthMcp "type" = "local")
            let cmd = dynGet stealthMcp "command"
            chk "op.mcp.stealthCommandIsArray" (not (dynIsNull cmd) && dynIsArr cmd)

        for h in [| "tool.definition"; "tool.execute.before"; "tool.execute.after"; "command.execute.before"; "event"; "experimental.chat.messages.transform"; "experimental.chat.system.transform"; "chat.message" |] do
            let hook = dynGet plugin h in chk ("op.hook." + h + ".isFunction") (not (dynIsNull hook) && dynTypeIs hook "function")

        // --- 2. Definition & Tools execution ----------------------------------
        let! todowriteDef = harness.runToolDefinition "todowrite"
        let todowriteSchema = dynGet todowriteDef "jsonSchema"
        chk "op.todowrite.jsonSchema.notNull" (not (dynIsNull todowriteSchema))
        if not (dynIsNull todowriteSchema) then
            let props = dynGet todowriteSchema "properties"
            for p in [| "ahaMoments"; "changesAndReasons"; "gotchas"; "lessonsAndConventions"; "plan"; "select_methodology"; "todos" |] do
                chk ("op.todowrite.has." + p) (not (dynIsNull (dynGet props p)))
            let req = dynGet todowriteSchema "required"
            chk "op.todowrite.req.aha" (not (dynIsNull req) && dynIsArr req && Array.contains "ahaMoments" (unbox req))

        let! coderDef = harness.runToolDefinition "coder"
        let coderJsonSchema = dynGet coderDef "jsonSchema"
        chk "op.coder.jsonSchema.notNull" (not (dynIsNull coderJsonSchema))
        if not (dynIsNull coderJsonSchema) then
            let props = dynGet coderJsonSchema "properties"
            chk "op.coder.hasWarnTdd" (not (dynIsNull (dynGet props "warn_tdd")))
            let req = dynGet coderJsonSchema "required"
            chk "op.coder.req.warnTdd" (not (dynIsNull req) && dynIsArr req && Array.contains "warn_tdd" (unbox req))

        let execArgs = createObj [ "program", box "echo hello-executor"; "language", box "shell"; "mode", box "ro"; "timeout_type", box "short"; "what_to_summarize", box "keep stdout only"; "warn_tdd", box warnTddValue; "warn", box warnValue ]
        let! execResult = harness.runToolWithHooks "executor" execArgs (createEmpty ())
        chk "op.executor.echoSuccess" (execResult.IndexOf "hello-executor" >= 0)

        let! fuzzyResult = harness.executePluginTool "fuzzy_find" (createObj [ "pattern", box [| "README" |] ]) (createEmpty ())
        chk "op.fuzzyFind.findsReadme" (fuzzyResult.IndexOf "README" >= 0)
        chk "op.pty.toolsRegistered" (Array.contains "pty_spawn" toolNames && Array.contains "pty_list" toolNames && not (dynIsNull (dynGet (harness.getToolEntry "pty_spawn") "execute")))

        // --- 3. Command & Message Transform -----------------------------------
        let! loopOutput = harness.runCommandExecuteBefore "loop" "implement feature X"
        chk "op.loop.active" ((harness.readPartsText loopOutput).IndexOf "With-Review Mode is active" >= 0)
        chk "op.loop.eventLogCreated" (harness.fileExists ".wanxiangshu.ndjson" && (harness.readFile ".wanxiangshu.ndjson").IndexOf "loop_activated" >= 0)

        let! emptyOutput = harness.runCommandExecuteBefore "loop" ""
        chk "op.loop.emptyCancelled" ((harness.readPartsText emptyOutput).IndexOf "With-Review Mode cancelled" >= 0)

        let! loopForAbort = harness.runCommandExecuteBefore "loop" "test stream-abort"
        chk "op.abort.active" ((harness.readPartsText loopForAbort).IndexOf "With-Review Mode is active" >= 0)
        let! _ = harness.fireStreamAbort harness.sessionId
        let reviewTaskResult = (dynGet (harness.getReviewStore ()) "getReviewTask") $ harness.sessionId
        chk "op.abort.deactivated" (dynIsNull reviewTaskResult)

        let userMsg = createObj [ "info", box (createObj [ "id", box "user-turn-1"; "role", box "user"; "agent", box "build"; "sessionID", box harness.sessionId ]); "parts", box [| createObj [ "type", box "text"; "text", box "initial user message" ] |] ]
        let! transformed = harness.runMessageTransform (createObj [ "agent", box "build"; "sessionID", box harness.sessionId ]) [| userMsg |]
        let msgsOut : obj[] = unbox (dynGet transformed "messages")
        chk "op.msgTrans.capsAdded" (msgsOut.Length > 1)
        let fTxt = if msgsOut.Length > 1 && (unbox<obj[]> (dynGet msgsOut.[0] "parts")).Length > 0 then dynStr (unbox<obj[]> (dynGet msgsOut.[0] "parts")).[0] "text" else ""
        chk "op.msgTrans.hasPrelude" ((string fTxt).IndexOf "# Kolmolgorov 宝典" >= 0 && (string fTxt).IndexOf "铁律" >= 0)

        // --- 4. System Transform & Tools --------------------------------------
        let! systemOutput = harness.runSystemTransform (createEmpty ())
        chk "op.sysTrans.hasWorkDir" ((string (dynGet systemOutput "system")).IndexOf(harness.workDir) >= 0)
        chk "op.meth.ok" (not (dynIsNull (harness.getToolEntry "methodology")))
        let! wsResult = harness.executePluginTool "websearch" (createObj [ "query", box "test query"; "numResults", box 5; "what_to_summarize", box "keep all" ]) (createEmpty ())
        chk "op.websearch.missingKey" ((string wsResult).IndexOf "failed" >= 0 || (string wsResult).IndexOf "Missing" >= 0 || (string wsResult).IndexOf "(no output)" >= 0)
        let! rrResult = harness.executePluginTool "return_reviewer" (createObj [ "verdict", box "PERFECT"; "feedback", box "" ]) (createEmpty ())
        chk "op.returnReviewer.ok" (not (isNull rrResult) && ((string rrResult).IndexOf "No active review" >= 0 || (string rrResult).IndexOf "double-check" >= 0))

        // --- 5. Lifecycle hooks & /loop-review --------------------------------
        let configArgs = createObj [ "agent", box (createObj [ "build", box (createObj [ "model", box "test" ]) ]) ]
        let! configRes = harness.runConfigHook configArgs
        chk "op.configHook.run" (not (dynIsNull configRes) && not (dynIsNull (dynGet configRes "command")))

        let! loopReviewOut = harness.runCommandExecuteBefore "loop-review" "test precheck"
        chk "op.loopReview.run" ((harness.readPartsText loopReviewOut).IndexOf "Mode" >= 0 || (harness.readPartsText loopReviewOut).IndexOf "precheck" >= 0 || (harness.readPartsText loopReviewOut).IndexOf "reviewer" >= 0)

        let chatOutput = createObj [ "message", box (createObj [ "tools", box [| box {| name = "executor" |}; box {| name = "pty_spawn" |} |] ]) ]
        let! chatRes = harness.runLifecycleHook "chat.message" (createObj [ "sessionID", box harness.sessionId ]) chatOutput
        chk "op.chatMessage.processed" (not (dynIsNull (dynGet chatRes "message")))

        // --- 6. tool.execute.before check ------------------------------------
        let! coderBeforeRes = harness.runToolExecuteHooks "coder" (createObj [ "intents", box [||]; "tdd", box "green" ]) "success" in chk "op.coder.before.missingWarnTdd" (not (dynIsNull (dynGet coderBeforeRes "error")))
        let! execBeforeRes = harness.runToolExecuteHooks "executor" (createObj [ "program", box "echo" ]) "success" in chk "op.executor.before.missingWarn" (not (dynIsNull (dynGet execBeforeRes "error")))

        // --- 7. tool.execute.after boundaries --------------------------------
        let! netRes = harness.runToolExecuteHooks "executor" execArgs "network error"
        chk "op.executor.networkErrorConverted" ((string (dynGet netRes "error")) = "network connection lost")

        let execArgs2 = createObj [ "program", box "echo hello-executor"; "language", box "shell"; "mode", box "ro"; "timeout_type", box "short"; "what_to_summarize", box "keep stdout only"; "warn_tdd", box warnTddValue; "warn", box warnValue ]
        let! liveRes1 = harness.runToolExecuteHooks "executor" execArgs2 "hello-livelock"
        let! liveRes2 = harness.runToolExecuteHooks "executor" execArgs2 "hello-livelock"
        let! liveRes3 = harness.runToolExecuteHooks "executor" execArgs2 "hello-livelock"
        chk "op.executor.livelockIntercepted" ((string (dynGet liveRes3 "error")).IndexOf("livelock guard") >= 0)

        // --- 8. todowrite intercept flow --------------------------------------
        let pad1024 = String.replicate 1024 "x"
        let twArgs = createObj [
            "ahaMoments", box pad1024; "changesAndReasons", box pad1024; "gotchas", box pad1024
            "lessonsAndConventions", box pad1024; "plan", box pad1024
            "todos", box [| box {| content = "do task"; status = "pending"; priority = "high" |} |]
            "select_methodology", box [| "first_principles" |]
        ]
        let! twRes = harness.runToolExecuteHooks "todowrite" twArgs "success"
        chk "op.todowrite.rewritten" ((dynStr twRes "output").IndexOf("first_principles") >= 0); chk "op.todowrite.noErr" (dynIsNull (dynGet twRes "error"))
        chk "op.todowrite.eventAppended" (((if harness.fileExists ".wanxiangshu.ndjson" then harness.readFile ".wanxiangshu.ndjson" else "").IndexOf("work_backlog_committed") >= 0))

        let chkTwErr label (errSub: string) extra =
            promise {
                let beforeLog = if harness.fileExists ".wanxiangshu.ndjson" then harness.readFile ".wanxiangshu.ndjson" else ""
                let baseArgs = [
                    "ahaMoments", box pad1024; "changesAndReasons", box pad1024; "gotchas", box pad1024
                    "lessonsAndConventions", box pad1024; "plan", box pad1024
                    "todos", box [| box {| content = "do task"; status = "pending"; priority = "high" |} |]
                    "select_methodology", box [| "first_principles" |]
                ]
                let merged = createObj (baseArgs @ extra)
                let! _ = harness.runToolExecuteHooks "todowrite" merged "success"
                let afterLog = if harness.fileExists ".wanxiangshu.ndjson" then harness.readFile ".wanxiangshu.ndjson" else ""
                chk label (beforeLog = afterLog)
            }
        do! chkTwErr "op.todowrite.shortErr" "must be at least 1024 characters" [ "ahaMoments", box "short" ]
        do! chkTwErr "op.todowrite.badTodoErr" "content" [ "todos", box [| box {| content = ""; status = "pending"; priority = "high" |} |] ]

        // --- 9. Nudge & Force-Stop workflow -----------------------------------
        let mutable nudgePromptCalls = 0
        let mutable nudgePromptBody = ""
        let nudgeOpts = createObj [
            "messages", box [| box (createObj [ "info", box (createObj [ "role", box "assistant"; "agent", box "build"; "finish", box "stop"; "id", box "msg-1"; "time", box {| completed = 1000.0 |} ]); "parts", box [| box (createObj [ "type", box "text"; "text", box "assistant reply" ]) |] ]) |]
            "mockSessionClient", box (createObj [
                "todo", box (fun _ -> Promise.lift (box {| data = [| {| content = "layout"; status = "pending" |} |] |}))
                "prompt", box (fun (body: obj) ->
                    nudgePromptCalls <- nudgePromptCalls + 1
                    nudgePromptBody <- jsonStringify body
                    Promise.lift (box {| ok = true |}))
            ])
        ]
        let! nudgeHarnessObj = startHarness nudgeOpts
        let nudgeHarness = harnessFromObj nudgeHarnessObj
        let! _ = nudgeHarness.fireEvent (box {| event = {| ``type`` = "session.idle"; properties = {| sessionID = nudgeHarness.sessionId |} |} |})
        let mutable nudgeTicks = 0
        while nudgePromptCalls = 0 && nudgeTicks < 20 do
            do! Promise.sleep 50
            nudgeTicks <- nudgeTicks + 1
        do! nudgeHarness.dispose ()
        chk "op.nudge.promptSentExactlyOnce" (nudgePromptCalls = 1); chk "op.nudge.promptContentValid" ((string nudgePromptBody).IndexOf("There are still incomplete todos") >= 0)

        let mutable abortPromptCalls = 0
        let abortOpts = createObj [
            "messages", box [| box (createObj [ "info", box (createObj [ "role", box "assistant"; "agent", box "build"; "finish", box "stop"; "id", box "msg-1"; "time", box {| completed = 1000.0 |} ]); "parts", box [| box (createObj [ "type", box "text"; "text", box "assistant reply" ]) |] ]) |]
            "mockSessionClient", box (createObj [
                "todo", box (fun _ -> Promise.lift (box {| data = [| {| content = "layout"; status = "pending" |} |] |}))
                "prompt", box (fun _ -> abortPromptCalls <- abortPromptCalls + 1; Promise.lift (box {| ok = true |}))
            ])
        ]
        let! abortHarnessObj = startHarness abortOpts
        let abortHarness = harnessFromObj abortHarnessObj
        let! _ = abortHarness.fireStreamAbort abortHarness.sessionId
        let! _ = abortHarness.fireEvent (box {| event = {| ``type`` = "session.idle"; properties = {| sessionID = abortHarness.sessionId |} |} |})
        do! Promise.sleep 200
        do! abortHarness.dispose ()
        chk "op.nudge.aborted.notCalled" (abortPromptCalls = 0)

        // --- 10. session.post error triggers nudge ----------------------------
        let mutable errNudgeCalls = 0
        let errNudgeOpts = createObj [
            "messages", box [| box (createObj [ "info", box (createObj [ "role", box "assistant"; "agent", box "build"; "finish", box "stop"; "id", box "msg-1"; "time", box {| completed = 1000.0 |} ]); "parts", box [| box (createObj [ "type", box "text"; "text", box "assistant reply" ]) |] ]) |]
            "mockSessionClient", box (createObj [
                "todo", box (fun _ -> Promise.lift (box {| data = [| {| content = "layout"; status = "pending" |} |] |}))
                "prompt", box (fun _ -> errNudgeCalls <- errNudgeCalls + 1; Promise.lift (box {| ok = true |}))
            ])
        ]
        let! errNudgeHarnessObj = startHarness errNudgeOpts
        let errNudgeHarness = harnessFromObj errNudgeHarnessObj
        let! _ = errNudgeHarness.runLifecycleHook "session.post" (createObj [ "sessionID", box errNudgeHarness.sessionId; "outcome", box "error"; "error", box "something went wrong" ]) (createEmpty())
        let mutable errTicks = 0
        while errNudgeCalls = 0 && errTicks < 20 do
            do! Promise.sleep 50
            errTicks <- errTicks + 1
        do! errNudgeHarness.dispose ()
        chk "op.sessionPost.errorTriggersNudge" (errNudgeCalls = 1)

        do! harness.dispose ()

        printfn "\n✓ %d opencode plugin e2e checks passed" ok
        return summary ()
    }
