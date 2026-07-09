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

open Wanxiangshu.E2e.OpencodePluginTestsPart2

let private harnessFromObj (o: obj) : Harness = unbox o
let private createEmpty () = createObj []

let private dynGet (o: obj) (k: string) = get o k
let private dynIsNull (o: obj) = isNullish o
let private dynIsArr (o: obj) = isArray o
let private dynTypeIs (o: obj) (t: string) = typeIs o t
let private dynStr (o: obj) (k: string) = str o k

let private dynHasKey (o: obj) (k: string) =
    if dynIsNull o then false else not (dynIsNull (get o k))

[<Emit("JSON.stringify($0)")>]
let private jsonStringify (o: obj) : string = jsNative

let private toolSchemaProperties (harness: Harness) (name: string) : obj =
    let entry = harness.getToolEntry name

    if dynIsNull entry then
        null
    else
        // Opencode tool entries have .args (Zod shape) or we can get schema via tool.definition hook
        // For raw schema, try entry.args first
        let args = dynGet entry "args"
        if dynIsNull args then null else args

let private warnTddValue =
    "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles-and-kept-todo-updated"

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

            if cond then
                ok <- ok + 1

        // --- 1. Identity & Presence -------------------------------------------
        chk "op.id" (dynStr plugin "id" = "wanxiangshu")
        chk "op.name" (dynStr plugin "name" = "wanxiangshu")
        let toolNames = harness.getToolNames ()

        for t in
            [| "coder"
               "methodology"
               "pty_spawn"
               "pty_write"
               "pty_read"
               "pty_list"
               "pty_kill"
               "return_reviewer"
               "websearch"
               "webfetch"
               "executor"
               "fuzzy_find"
               "submit_review" |] do
            chk ("op.tool.has." + t) (Array.contains t toolNames)

        let mcp = dynGet plugin "mcp"
        chk "op.mcp.notNull" (not (dynIsNull mcp))
        let stealthMcp = dynGet mcp "stealth-browser-mcp"

        if not (dynIsNull stealthMcp) then
            chk "op.mcp.stealthType" (dynStr stealthMcp "type" = "local")
            let cmd = dynGet stealthMcp "command"
            chk "op.mcp.stealthCommandIsArray" (not (dynIsNull cmd) && dynIsArr cmd)

        for h in
            [| "tool.definition"
               "tool.execute.before"
               "tool.execute.after"
               "command.execute.before"
               "event"
               "experimental.chat.messages.transform"
               "experimental.chat.system.transform"
               "chat.message" |] do
            let hook = dynGet plugin h in
            chk ("op.hook." + h + ".isFunction") (not (dynIsNull hook) && dynTypeIs hook "function")

        // --- 2. Definition & Tools execution ----------------------------------
        let! todowriteDef = harness.runToolDefinition "todowrite"
        let todowriteSchema = dynGet todowriteDef "jsonSchema"
        chk "op.todowrite.jsonSchema.notNull" (not (dynIsNull todowriteSchema))

        if not (dynIsNull todowriteSchema) then
            let props = dynGet todowriteSchema "properties"

            for p in
                [| "ahaMoments"
                   "changesAndReasons"
                   "gotchas"
                   "lessonsAndConventions"
                   "plan"
                   "select_methodology"
                   "todos" |] do
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

        let execArgs =
            createObj
                [ "program", box "echo hello-executor"
                  "language", box "shell"
                  "mode", box "ro"
                  "timeout_type", box "short"
                  "what_to_summarize", box "keep stdout only"
                  "warn_tdd", box warnTddValue
                  "warn", box warnValue ]

        let! execResult = harness.runToolWithHooks "executor" execArgs (createEmpty ())
        chk "op.executor.echoSuccess" (execResult.IndexOf "hello-executor" >= 0)

        let! fuzzyResult =
            harness.executePluginTool "fuzzy_find" (createObj [ "pattern", box [| "README" |] ]) (createEmpty ())

        chk "op.fuzzyFind.findsReadme" (fuzzyResult.IndexOf "README" >= 0)

        chk
            "op.pty.toolsRegistered"
            (Array.contains "pty_spawn" toolNames
             && Array.contains "pty_list" toolNames
             && not (dynIsNull (dynGet (harness.getToolEntry "pty_spawn") "execute")))

        // --- 3. Command & Message Transform -----------------------------------
        let! loopOutput = harness.runCommandExecuteBefore "loop" "implement feature X"
        chk "op.loop.active" ((harness.readPartsText loopOutput).IndexOf "With-Review Mode is active" >= 0)

        chk
            "op.loop.eventLogCreated"
            (harness.fileExists ".wanxiangshu.ndjson"
             && (harness.readFile ".wanxiangshu.ndjson").IndexOf "loop_activated" >= 0)

        let! emptyOutput = harness.runCommandExecuteBefore "loop" ""
        chk "op.loop.emptyCancelled" ((harness.readPartsText emptyOutput).IndexOf "With-Review Mode cancelled" >= 0)

        let! loopForAbort = harness.runCommandExecuteBefore "loop" "test stream-abort"
        chk "op.abort.active" ((harness.readPartsText loopForAbort).IndexOf "With-Review Mode is active" >= 0)
        let! _ = harness.fireStreamAbort harness.sessionId

        let reviewTaskResult =
            (dynGet (harness.getReviewStore ()) "getReviewTask") $ harness.sessionId

        chk "op.abort.deactivated" (dynIsNull reviewTaskResult)

        let userMsg =
            createObj
                [ "info",
                  box (
                      createObj
                          [ "id", box "user-turn-1"
                            "role", box "user"
                            "agent", box "build"
                            "sessionID", box harness.sessionId ]
                  )
                  "parts", box [| createObj [ "type", box "text"; "text", box "initial user message" ] |] ]

        let! transformed =
            harness.runMessageTransform
                (createObj [ "agent", box "build"; "sessionID", box harness.sessionId ])
                [| userMsg |]

        let msgsOut: obj[] = unbox (dynGet transformed "messages")
        chk "op.msgTrans.capsAdded" (msgsOut.Length > 1)

        let fTxt =
            if msgsOut.Length > 1 && (unbox<obj[]> (dynGet msgsOut.[0] "parts")).Length > 0 then
                dynStr (unbox<obj[]> (dynGet msgsOut.[0] "parts")).[0] "text"
            else
                ""

        chk "op.msgTrans.hasPrelude" ((string fTxt).IndexOf "# Kolmolgorov 宝典" >= 0 && (string fTxt).IndexOf "铁律" >= 0)

        do!
            OpencodePluginTestsPart2.runPart2
                harness
                chk
                warnTddValue
                warnValue
                execArgs
                createEmpty
                dynGet
                dynIsNull
                dynStr

        return! OpencodePluginTestsPart3.runPart3 harness chk startHarness jsonStringify ok summary
    }
