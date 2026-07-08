module Wanxiangshu.E2e.MimocodePluginTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Tests.Assert

[<Import("start", "./opencode-harness.js")>]
let private startHarness: obj -> JS.Promise<obj> = jsNative

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
    abstract runConfigHook: obj -> JS.Promise<obj>
    abstract runSessionPost: obj -> JS.Promise<obj>
    abstract runSessionUserQueryPost: obj -> JS.Promise<obj>
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

let private warnTddValue =
    "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles"

let private warnValue = "it-is-not-possible-to-do-it-using-other-tools"

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let! apiObj = startHarness (createObj [ "variant", box "mimocode" ])
        let harness = harnessFromObj apiObj
        let plugin = harness.getPlugin ()

        let mutable ok = 0

        let chk label cond =
            check label cond

            if cond then
                ok <- ok + 1

        // --- 1. Plugin identity -----------------------------------------------
        chk "mimo.id" (dynStr plugin "id" = "wanxiangshu")
        chk "mimo.name" (dynStr plugin "name" = "wanxiangshu")

        // --- 2. Tool presence -------------------------------------------------
        let toolNames = harness.getToolNames ()
        chk "mimo.tool.has.task" (Array.contains "task" toolNames)
        chk "mimo.tool.has.coder" (Array.contains "coder" toolNames)
        chk "mimo.tool.has.executor" (Array.contains "executor" toolNames)
        chk "mimo.tool.has.fuzzy_find" (Array.contains "fuzzy_find" toolNames)
        chk "mimo.tool.has.submit_review" (Array.contains "submit_review" toolNames)
        chk "mimo.tool.has.methodology" (Array.contains "methodology" toolNames)

        // --- 3. task schema: ahaMoments + 1024 min ---------------------------
        let! taskDef = harness.runToolDefinition "task"
        let taskSchema = dynGet taskDef "jsonSchema"
        chk "mimo.task.jsonSchema.notNull" (not (dynIsNull taskSchema))

        if not (dynIsNull taskSchema) then
            let taskProps = dynGet taskSchema "properties"
            chk "mimo.task.hasAhaMoments" (not (dynIsNull (dynGet taskProps "ahaMoments")))
            chk "mimo.task.hasChangesAndReasons" (not (dynIsNull (dynGet taskProps "changesAndReasons")))
            chk "mimo.task.hasGotchas" (not (dynIsNull (dynGet taskProps "gotchas")))
            chk "mimo.task.hasLessonsAndConventions" (not (dynIsNull (dynGet taskProps "lessonsAndConventions")))
            chk "mimo.task.hasPlan" (not (dynIsNull (dynGet taskProps "plan")))
            chk "mimo.task.hasSelectMethodology" (not (dynIsNull (dynGet taskProps "select_methodology")))
            chk "mimo.task.hasTodos" (not (dynIsNull (dynGet taskProps "todos")))
            // Check ahaMoments min=1024
            let ahaField = dynGet taskProps "ahaMoments"

            let ahaMin =
                if dynIsNull ahaField then
                    0
                else
                    unbox<int> (dynGet ahaField "minLength")

            chk "mimo.task.ahaMin1024" (ahaMin >= 1024)
            // Check required includes ahaMoments
            let req = dynGet taskSchema "required"

            if not (dynIsNull req) && dynIsArr req then
                let reqArr: string[] = unbox req
                chk "mimo.task.requiredIncludesAhaMoments" (Array.contains "ahaMoments" reqArr)
            else
                chk "mimo.task.requiredIncludesAhaMoments" false

        // --- 4. task execute: missing ahaMoments -----------------------------
        let taskArgsMissing =
            createObj
                [ "todos",
                  box
                      [| box
                             {| content = "do something"
                                status = "pending"
                                priority = "high" |} |]
                  "select_methodology", box [| "first_principles" |] ]

        let! rMissing = harness.executePluginTool "task" taskArgsMissing (createEmpty ())
        chk "mimo.task.missingAha" (rMissing.Contains "ahaMoments")

        // --- 5. task execute: ahaMoments too short --------------------------
        let exactly1024 = System.String('x', 1024)

        let taskArgsShort =
            createObj
                [ "ahaMoments", box "short"
                  "changesAndReasons", box exactly1024
                  "gotchas", box exactly1024
                  "lessonsAndConventions", box exactly1024
                  "plan", box exactly1024
                  "todos",
                  box
                      [| box
                             {| content = "do something"
                                status = "pending"
                                priority = "high" |} |]
                  "select_methodology", box [| "first_principles" |] ]

        let! rShort = harness.executePluginTool "task" taskArgsShort (createEmpty ())
        chk "mimo.task.shortAha" (rShort.Contains "min 1024")

        // --- 6. task execute: all fields satisfied --------------------------
        let taskArgsFull =
            createObj
                [ "ahaMoments", box exactly1024
                  "changesAndReasons", box exactly1024
                  "gotchas", box exactly1024
                  "lessonsAndConventions", box exactly1024
                  "plan", box exactly1024
                  "todos",
                  box
                      [| box
                             {| content = "do something"
                                status = "pending"
                                priority = "high" |} |]
                  "select_methodology", box [| "first_principles" |] ]

        let! rFull = harness.executePluginTool "task" taskArgsFull (createEmpty ())
        chk "mimo.task.success" (rFull.Contains "hint:")

        // --- 7. task execute: missing select_methodology ---------------------
        let taskArgsNoMeth =
            createObj
                [ "ahaMoments", box exactly1024
                  "changesAndReasons", box exactly1024
                  "gotchas", box exactly1024
                  "lessonsAndConventions", box exactly1024
                  "plan", box exactly1024
                  "todos",
                  box
                      [| box
                             {| content = "do something"
                                status = "pending"
                                priority = "high" |} |] ]

        let! rNoMeth = harness.executePluginTool "task" taskArgsNoMeth (createEmpty ())
        chk "mimo.task.noMethodology" (rNoMeth.Contains "select_methodology")

        // --- 8. task execute: empty todo content ----------------------------
        let taskArgsEmptyTodo =
            createObj
                [ "ahaMoments", box exactly1024
                  "changesAndReasons", box exactly1024
                  "gotchas", box exactly1024
                  "lessonsAndConventions", box exactly1024
                  "plan", box exactly1024
                  "todos",
                  box
                      [| box
                             {| content = ""
                                status = "pending"
                                priority = "high" |} |]
                  "select_methodology", box [| "first_principles" |] ]

        let! rEmptyTodo = harness.executePluginTool "task" taskArgsEmptyTodo (createEmpty ())
        chk "mimo.task.emptyTodoContent" (rEmptyTodo.Contains "content")

        // --- 9. coder schema has warn_tdd -----------------------------------
        let! coderDef = harness.runToolDefinition "coder"
        let coderJsonSchema = dynGet coderDef "jsonSchema"

        if not (dynIsNull coderJsonSchema) then
            let coderProps = dynGet coderJsonSchema "properties"
            chk "mimo.coder.hasWarnTdd" (not (dynIsNull (dynGet coderProps "warn_tdd")))
        else
            chk "mimo.coder.hasWarnTdd" false

        // --- 10. executor echo ----------------------------------------------
        let execArgs =
            createObj
                [ "program", box "echo hello-mimo"
                  "language", box "shell"
                  "mode", box "ro"
                  "timeout_type", box "short"
                  "what_to_summarize", box "keep stdout only"
                  "warn_tdd", box warnTddValue
                  "warn", box warnValue ]

        let! execResult = harness.runToolWithHooks "executor" execArgs (createEmpty ())
        chk "mimo.executor.echo" (execResult.Contains "hello-mimo")

        // --- 11. fuzzy_find --------------------------------------------------
        let fuzzyArgs = createObj [ "pattern", box [| "README" |] ]
        let! fuzzyResult = harness.executePluginTool "fuzzy_find" fuzzyArgs (createEmpty ())
        chk "mimo.fuzzyFind.findsReadme" (fuzzyResult.Contains "README")

        // --- 12. /loop command ----------------------------------------------
        let! loopOutput = harness.runCommandExecuteBefore "loop" "implement mimo feature"
        let loopText = harness.readPartsText loopOutput
        chk "mimo.loop.active" (loopText.Contains "With-Review Mode is active")

        if harness.fileExists ".wanxiangshu.ndjson" then
            let eventLog = harness.readFile ".wanxiangshu.ndjson"
            chk "mimo.loop.eventLog" (eventLog.Contains "loop_activated")

        // --- 13. stream-abort ------------------------------------------------
        let! _ = harness.fireStreamAbort harness.sessionId
        let reviewStore = harness.getReviewStore ()
        let getReviewTask = dynGet reviewStore "getReviewTask"
        let taskResult = getReviewTask $ harness.sessionId
        chk "mimo.abort.deactivated" (dynIsNull taskResult)

        // --- 14. message transform: caps ------------------------------------
        let textPart = createObj [ "type", box "text"; "text", box "mimo test message" ]

        let userInfo =
            createObj
                [ "id", box "mimo-user-turn"
                  "role", box "user"
                  "agent", box "build"
                  "sessionID", box harness.sessionId ]

        let userMsg = createObj [ "info", box userInfo; "parts", box [| textPart |] ]

        let transformInput =
            createObj [ "agent", box "build"; "sessionID", box harness.sessionId ]

        let! transformedOutput = harness.runMessageTransform transformInput [| userMsg |]
        let messagesOut: obj[] = unbox<obj[]> (dynGet transformedOutput "messages")
        chk "mimo.messageTransform.capsAdded" (messagesOut.Length > 1)

        if messagesOut.Length > 1 then
            let firstMsg = messagesOut.[0]
            let firstParts: obj[] = unbox<obj[]> (dynGet firstMsg "parts")

            let firstText =
                if firstParts.Length > 0 && dynStr firstParts.[0] "type" = "text" then
                    dynStr firstParts.[0] "text"
                else
                    ""

            chk "mimo.messageTransform.capsHasKolmolgorov" (firstText.Contains "# Kolmolgorov 宝典")

        // --- 15. system transform: workDir ----------------------------------
        let! systemOutput = harness.runSystemTransform (createEmpty ())
        let systemOut = dynGet systemOutput "system"

        chk
            "mimo.systemTransform.hasWorkDir"
            (not (dynIsNull systemOut)
             && dynIsArr systemOut
             && (unbox<obj[]> systemOut
                 |> Array.exists (fun s -> (string s).Contains(harness.workDir))))

        // --- 16. config/session.post/session.userQuery.post hooks -----------
        let configArgs =
            createObj [ "agent", box (createObj [ "build", box (createObj [ "model", box "test" ]) ]) ]

        let! _ = harness.runConfigHook configArgs
        chk "mimo.configHook.run" true

        let sessionPostArgs =
            createObj
                [ "sessionID", box harness.sessionId
                  "outcome", box "success"
                  "error", box "" ]

        let! _ = harness.runSessionPost sessionPostArgs
        chk "mimo.sessionPostHook.run" true

        let sessionUserQueryPostArgs =
            createObj [ "sessionID", box harness.sessionId; "error", box "" ]

        let! _ = harness.runSessionUserQueryPost sessionUserQueryPostArgs
        chk "mimo.sessionUserQueryPostHook.run" true

        chk "mimo.hook.config.isFunction" (dynTypeIs (dynGet plugin "config") "function")
        chk "mimo.hook.sessionPost.isFunction" (dynTypeIs (dynGet plugin "session.post") "function")
        chk "mimo.hook.sessionUserQueryPost.isFunction" (dynTypeIs (dynGet plugin "session.userQuery.post") "function")

        // --- 17. event: session.deleted no-throw ---------------------------
        let sessionDeletedEvent =
            box
                {| event =
                    {| ``type`` = "session.deleted"
                       properties = {| sessionID = harness.sessionId |} |} |}

        let! _ = harness.fireEvent sessionDeletedEvent
        chk "mimo.event.sessionDeleted.noThrow" true

        do! harness.dispose ()

        printfn "\n✓ %d mimocode plugin e2e checks passed" ok
        return summary ()
    }
