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
    abstract runCommandExecuteBefore: string -> string -> JS.Promise<obj>
    abstract runMessageTransform: obj -> obj -> JS.Promise<obj>
    abstract runSystemTransform: obj -> JS.Promise<obj>
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

        // --- 1. Plugin identity -----------------------------------------------
        chk "op.id" (dynStr plugin "id" = "wanxiangshu")
        chk "op.name" (dynStr plugin "name" = "wanxiangshu")

        // --- 2. Tool presence -------------------------------------------------
        let toolNames = harness.getToolNames ()
        chk "op.tool.has.coder" (Array.contains "coder" toolNames)
        chk "op.tool.has.methodology" (Array.contains "methodology" toolNames)
        chk "op.tool.has.pty_spawn" (Array.contains "pty_spawn" toolNames)
        chk "op.tool.has.pty_write" (Array.contains "pty_write" toolNames)
        chk "op.tool.has.pty_read" (Array.contains "pty_read" toolNames)
        chk "op.tool.has.pty_list" (Array.contains "pty_list" toolNames)
        chk "op.tool.has.pty_kill" (Array.contains "pty_kill" toolNames)
        chk "op.tool.has.return_reviewer" (Array.contains "return_reviewer" toolNames)
        chk "op.tool.has.websearch" (Array.contains "websearch" toolNames)
        chk "op.tool.has.webfetch" (Array.contains "webfetch" toolNames)
        chk "op.tool.has.executor" (Array.contains "executor" toolNames)
        chk "op.tool.has.fuzzy_find" (Array.contains "fuzzy_find" toolNames)
        chk "op.tool.has.submit_review" (Array.contains "submit_review" toolNames)

        // --- 3. MCP registration ----------------------------------------------
        let mcp = dynGet plugin "mcp"
        chk "op.mcp.notNull" (not (dynIsNull mcp))
        let stealthMcp = dynGet mcp "stealth-browser-mcp"
        chk "op.mcp.hasStealthBrowser" (not (dynIsNull stealthMcp))
        if not (dynIsNull stealthMcp) then
            chk "op.mcp.stealthType" (dynStr stealthMcp "type" = "local")
            let cmd = dynGet stealthMcp "command"
            chk "op.mcp.stealthCommandIsArray" (not (dynIsNull cmd) && dynIsArr cmd)

        // --- 4. Hooks are functions -------------------------------------------
        chk "op.hook.toolDefinition.isFunction"
            (not (dynIsNull (dynGet plugin "tool.definition")) && dynTypeIs (dynGet plugin "tool.definition") "function")
        chk "op.hook.toolExecuteBefore.isFunction"
            (not (dynIsNull (dynGet plugin "tool.execute.before")) && dynTypeIs (dynGet plugin "tool.execute.before") "function")
        chk "op.hook.toolExecuteAfter.isFunction"
            (not (dynIsNull (dynGet plugin "tool.execute.after")) && dynTypeIs (dynGet plugin "tool.execute.after") "function")
        chk "op.hook.commandExecuteBefore.isFunction"
            (not (dynIsNull (dynGet plugin "command.execute.before")) && dynTypeIs (dynGet plugin "command.execute.before") "function")
        chk "op.hook.event.isFunction"
            (not (dynIsNull (dynGet plugin "event")) && dynTypeIs (dynGet plugin "event") "function")
        chk "op.hook.messagesTransform.isFunction"
            (not (dynIsNull (dynGet plugin "experimental.chat.messages.transform")) && dynTypeIs (dynGet plugin "experimental.chat.messages.transform") "function")
        chk "op.hook.systemTransform.isFunction"
            (not (dynIsNull (dynGet plugin "experimental.chat.system.transform")) && dynTypeIs (dynGet plugin "experimental.chat.system.transform") "function")
        chk "op.hook.chatMessage.isFunction"
            (not (dynIsNull (dynGet plugin "chat.message")) && dynTypeIs (dynGet plugin "chat.message") "function")

        // --- 5. tool.definition: todowrite jsonSchema has ahaMoments -----------
        let! todowriteDef = harness.runToolDefinition "todowrite"
        let todowriteSchema = dynGet todowriteDef "jsonSchema"
        chk "op.todowrite.jsonSchema.notNull" (not (dynIsNull todowriteSchema))
        if not (dynIsNull todowriteSchema) then
            let todowriteProps = dynGet todowriteSchema "properties"
            chk "op.todowrite.hasAhaMoments" (not (dynIsNull (dynGet todowriteProps "ahaMoments")))
            chk "op.todowrite.hasChangesAndReasons" (not (dynIsNull (dynGet todowriteProps "changesAndReasons")))
            chk "op.todowrite.hasGotchas" (not (dynIsNull (dynGet todowriteProps "gotchas")))
            chk "op.todowrite.hasLessonsAndConventions" (not (dynIsNull (dynGet todowriteProps "lessonsAndConventions")))
            chk "op.todowrite.hasPlan" (not (dynIsNull (dynGet todowriteProps "plan")))
            chk "op.todowrite.hasSelectMethodology" (not (dynIsNull (dynGet todowriteProps "select_methodology")))
            chk "op.todowrite.hasTodos" (not (dynIsNull (dynGet todowriteProps "todos")))
            // Check required array includes ahaMoments
            let req = dynGet todowriteSchema "required"
            if not (dynIsNull req) && dynIsArr req then
                let reqArr : string[] = unbox req
                chk "op.todowrite.requiredIncludesAhaMoments" (Array.contains "ahaMoments" reqArr)
            else
                chk "op.todowrite.requiredIncludesAhaMoments" false

        // --- 6. tool.definition: coder jsonSchema has warn_tdd -----------------
        let! coderDef = harness.runToolDefinition "coder"
        let coderJsonSchema = dynGet coderDef "jsonSchema"
        chk "op.coder.jsonSchema.notNull" (not (dynIsNull coderJsonSchema))
        if not (dynIsNull coderJsonSchema) then
            let coderProps = dynGet coderJsonSchema "properties"
            chk "op.coder.hasWarnTdd" (not (dynIsNull (dynGet coderProps "warn_tdd")))
            // Check warn_tdd is in required
            let coderReq = dynGet coderJsonSchema "required"
            if not (dynIsNull coderReq) && dynIsArr coderReq then
                let coderReqArr : string[] = unbox coderReq
                chk "op.coder.requiredIncludesWarnTdd" (Array.contains "warn_tdd" coderReqArr)
            else
                chk "op.coder.requiredIncludesWarnTdd" false

        // --- 7. executor: echo command ----------------------------------------
        let execArgs =
            createObj [ "program", box "echo hello-executor"
                        "language", box "shell"
                        "mode", box "ro"
                        "timeout_type", box "short"
                        "what_to_summarize", box "keep stdout only"
                        "warn_tdd", box warnTddValue
                        "warn", box warnValue ]
        let! execResult = harness.runToolWithHooks "executor" execArgs (createEmpty ())
        chk "op.executor.echoSuccess" (execResult.Contains "hello-executor")

        // --- 8. fuzzy_find: finds workspace files ------------------------------
        let fuzzyArgs =
            createObj [ "pattern", box [| "README" |] ]
        let! fuzzyResult = harness.executePluginTool "fuzzy_find" fuzzyArgs (createEmpty ())
        chk "op.fuzzyFind.findsReadme" (fuzzyResult.Contains "README")

        // --- 9. PTY tools registered (no execute: opencode-pty pulls optional bun runtime) ---
        chk "op.pty.toolsRegistered"
            (Array.contains "pty_spawn" toolNames
             && Array.contains "pty_list" toolNames
             && not (dynIsNull (dynGet (harness.getToolEntry "pty_spawn") "execute")))

        // --- 10. Command: /loop activates review ------------------------------
        let! loopOutput = harness.runCommandExecuteBefore "loop" "implement feature X"
        let loopText = harness.readPartsText loopOutput
        chk "op.loop.responseTextContainsWithReview" (loopText.Contains "With-Review Mode is active")

        let eventLogPath = harness.workDir + "/.wanxiangshu.ndjson"
        chk "op.loop.eventLogCreated" (harness.fileExists ".wanxiangshu.ndjson")
        if harness.fileExists ".wanxiangshu.ndjson" then
            let eventLogContent = harness.readFile ".wanxiangshu.ndjson"
            chk "op.loop.eventLogContainsLoopActivated" (eventLogContent.Contains "loop_activated")
            chk "op.loop.eventLogContainsTaskText" (eventLogContent.Contains "implement feature X")

        // --- 11. Command: empty /loop returns cancelled -----------------------
        let! emptyOutput = harness.runCommandExecuteBefore "loop" ""
        let emptyText = harness.readPartsText emptyOutput
        chk "op.loop.emptyTaskCancelled" (emptyText.Contains "With-Review Mode cancelled")

        // --- 12. Stream-abort clears getReviewTask ---------------------------
        // First activate a review
        let! loopForAbort = harness.runCommandExecuteBefore "loop" "test stream-abort"
        chk "op.abort.activateOk" ((harness.readPartsText loopForAbort).Contains "With-Review Mode is active")
        // Then fire stream-abort
        let! _ = harness.fireStreamAbort harness.sessionId
        let reviewStore = harness.getReviewStore ()
        let getReviewTask = dynGet reviewStore "getReviewTask"
        let taskResult = getReviewTask $ harness.sessionId
        chk "op.abort.deactivated" (dynIsNull taskResult)

        // --- 13. Message transform: caps injection ---------------------------
        let textPart : obj = createObj [ "type", box "text"; "text", box "initial user message" ]
        let userInfo =
            createObj [ "id", box "user-turn-1"
                        "role", box "user"
                        "agent", box "build"
                        "sessionID", box harness.sessionId ]
        let userMsg = createObj [ "info", box userInfo; "parts", box [| textPart |] ]
        let transformInput = createObj [ "agent", box "build"; "sessionID", box harness.sessionId ]
        let! transformedOutput = harness.runMessageTransform transformInput [| userMsg |]
        let messagesOut : obj[] = unbox<obj[]> (dynGet transformedOutput "messages")
        chk "op.messageTransform.capsAdded" (messagesOut.Length > 1)
        if messagesOut.Length > 1 then
            let firstMsg = messagesOut.[0]
            let firstParts : obj[] = unbox<obj[]> (dynGet firstMsg "parts")
            let firstText =
                if firstParts.Length > 0 && dynStr firstParts.[0] "type" = "text"
                then dynStr firstParts.[0] "text"
                else ""
            chk "op.messageTransform.capsHasKolmolgorov" (firstText.Contains "# Kolmolgorov 宝典")
            chk "op.messageTransform.capsHasIronLaw" (firstText.Contains "铁律")

        // --- 14. System transform: workDir injection -------------------------
        let! systemOutput = harness.runSystemTransform (createEmpty ())
        let systemOut = dynGet systemOutput "system"
        chk "op.systemTransform.producesArray" (not (dynIsNull systemOut) && dynIsArr systemOut)
        if not (dynIsNull systemOut) && dynIsArr systemOut then
            let systemArr = unbox<obj[]> systemOut
            chk "op.systemTransform.hasWorkDir"
                (systemArr.Length > 0 && (string systemArr.[0]).Contains (harness.workDir))

        // --- 15. Methodology args properties ----------------------------------
        let methEntry = harness.getToolEntry "methodology"
        chk "op.methodology.entryExists" (not (dynIsNull methEntry))
        let methArgs = dynGet methEntry "args"
        chk "op.methodology.argsNotNull" (not (dynIsNull methArgs))
        chk "op.methodology.executeIsFunction" (dynTypeIs (dynGet methEntry "execute") "function")

        // --- 16. websearch: missing API key error -----------------------------
        let wsArgs =
            createObj [ "query", box "test query"
                        "numResults", box 5
                        "what_to_summarize", box "keep all" ]
        let! wsResult = harness.executePluginTool "websearch" wsArgs (createEmpty ())
        chk "op.websearch.missingApiKeyError"
            (wsResult.Contains "OLLAMA"
             || wsResult.Contains "failed"
             || wsResult.Contains "Missing"
             || wsResult.Contains "upstream"
             || wsResult.Contains "Web search"
             || wsResult.Contains "(no output)")

        // --- 17. return_reviewer: execute without pending returns sensible string
        let returnReviewerArgs =
            createObj [ "verdict", box "PERFECT"
                        "feedback", box "" ]
        let! rrResult = harness.executePluginTool "return_reviewer" returnReviewerArgs (createEmpty ())
        chk "op.returnReviewer.noThrow" (not (isNull rrResult))
        // Without an active review, it should return a message indicating no active review
        // or a double-check prompt, not throw.
        chk "op.returnReviewer.sensibleString"
            (rrResult.Contains "No active review" || rrResult.Contains "double-check" || rrResult.Contains "Verdict submitted" || rrResult.Contains "review")

        do! harness.dispose ()

        printfn "\n✓ %d opencode plugin e2e checks passed" ok
        return summary ()
    }
