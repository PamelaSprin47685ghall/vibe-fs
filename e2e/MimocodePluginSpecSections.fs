module Wanxiangshu.E2e.MimocodePluginSpecSections

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.E2e.MimocodePluginTaskAndArgsTests
open Wanxiangshu.Runtime.Dyn

let runMimoPluginIdentity (h: Harness) (chk: string -> bool -> unit) =
    let plugin = h.getPlugin ()
    chk "mimo.id" (dynStr plugin "id" = "wanxiangshu")
    chk "mimo.name" (dynStr plugin "name" = "wanxiangshu")

let runMimoToolPresence (h: Harness) (chk: string -> bool -> unit) =
    let toolNames = h.getToolNames ()

    for t in [ "task"; "coder"; "executor"; "fuzzy_find"; "submit_review"; "meditator" ] do
        chk ("mimo.tool.has." + t) (Array.contains t toolNames)

let runMimoTaskSchema (h: Harness) (chk: string -> bool -> unit) =
    promise {
        let! taskDef = h.runToolDefinition "task"
        let taskSchema = dynGet taskDef "jsonSchema"
        chk "mimo.task.jsonSchema.notNull" (not (dynIsNull taskSchema))

        if not (dynIsNull taskSchema) then
            let taskProps = dynGet taskSchema "properties"

            for f in [ "select_methodology"; "todos" ] do
                chk ("mimo.task.has" + f) (not (dynIsNull (dynGet taskProps f)))

            let req = dynGet taskSchema "required"

            if not (dynIsNull req) && dynIsArr req then
                let reqArr: string[] = unbox req
                chk "mimo.task.requiredHasTodos" (Array.contains "todos" reqArr)
            else
                chk "mimo.task.requiredHasTodos" true
    }

let runMimoTaskSuccess (h: Harness) (chk: string -> bool -> unit) =
    promise {
        let todos =
            [| box
                   {| content = "do something"
                      status = "pending"
                      priority = "high" |} |]

        let! r = h.executePluginTool "task" (taskArgsBase todos [| "first_principles" |]) (createEmpty ())
        chk "mimo.task.success" (r.Contains "hint:")
    }

let runMimoTaskNoMethodology (h: Harness) (chk: string -> bool -> unit) =
    promise {
        let args =
            createObj
                [ "todos",
                  box
                      [| box
                             {| content = "do something"
                                status = "pending"
                                priority = "high" |} |] ]

        let! r = h.executePluginTool "task" args (createEmpty ())
        chk "mimo.task.noMethodology" (r.Contains "select_methodology")
    }

let runMimoTaskEmptyTodo (h: Harness) (chk: string -> bool -> unit) =
    promise {
        let todos =
            [| box
                   {| content = ""
                      status = "pending"
                      priority = "high" |} |]

        let! r = h.executePluginTool "task" (taskArgsBase todos [| "first_principles" |]) (createEmpty ())
        chk "mimo.task.emptyTodoContent" (r.Contains "content")
    }

let runMimoCoderSchema (h: Harness) (chk: string -> bool -> unit) =
    promise {
        let! coderDef = h.runToolDefinition "coder"
        let coderJsonSchema = dynGet coderDef "jsonSchema"

        if not (dynIsNull coderJsonSchema) then
            let coderProps = dynGet coderJsonSchema "properties"
            chk "mimo.coder.hasWarnTdd" (not (dynIsNull (dynGet coderProps "follow-tdd-and-kolmogorov-principles")))
        else
            chk "mimo.coder.hasWarnTdd" false
    }

let runMimoExecutor (h: Harness) (chk: string -> bool -> unit) =
    promise {
        let execArgs =
            createObj
                [ "command", box "echo hello-mimo"
                  "language", box "shell"
                  "mode", box "ro"
                  "timeout_type", box "short"
                  "what_to_summarize", box "keep stdout only"
                  "warn_tdd", box warnTddValue
                  "warn", box warnValue ]

        let! execResult = h.runToolWithHooks "executor" execArgs (createEmpty ())
        chk "mimo.executor.echo" (execResult.Contains "hello-mimo")
    }

let runMimoFuzzyFind (h: Harness) (chk: string -> bool -> unit) =
    promise {
        let fuzzyArgs = createObj [ "pattern", box [| "README" |] ]
        let! fuzzyResult = h.executePluginTool "fuzzy_find" fuzzyArgs (createEmpty ())
        chk "mimo.fuzzyFind.findsReadme" (fuzzyResult.Contains "README")
    }

let runMimoLoopCommand (h: Harness) (chk: string -> bool -> unit) =
    promise {
        let! loopOutput = h.runCommandExecuteBefore "loop" "implement mimo feature"
        let loopText = h.readPartsText loopOutput
        chk "mimo.loop.active" (loopText.Contains "With-Review Mode is active")

        if h.fileExists ".wanxiangshu.ndjson" then
            let eventLog = h.readFile ".wanxiangshu.ndjson"
            chk "mimo.loop.eventLog" (eventLog.Contains "loop_activated")
    }

let runMimoStreamAbort (h: Harness) (chk: string -> bool -> unit) =
    promise {
        let! _ = h.fireStreamAbort h.sessionId
        let reviewStore = h.getReviewStore ()
        let getReviewTask = dynGet reviewStore "getReviewTask"
        let taskResult = getReviewTask $ h.sessionId
        chk "mimo.abort.deactivated" (dynIsNull taskResult)
    }

let runMimoMessageTransform (h: Harness) (chk: string -> bool -> unit) =
    promise {
        let textPart = createObj [ "type", box "text"; "text", box "mimo test message" ]

        let userInfo =
            createObj
                [ "id", box "mimo-user-turn"
                  "role", box "user"
                  "agent", box "build"
                  "sessionID", box h.sessionId ]

        let userMsg = createObj [ "info", box userInfo; "parts", box [| textPart |] ]

        let transformInput =
            createObj [ "agent", box "build"; "sessionID", box h.sessionId ]

        let! transformedOutput = h.runMessageTransform transformInput [| userMsg |]
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

            chk "mimo.messageTransform.capsHasKolmogorov" (firstText.Contains "# Kolmogorov 宝典")
    }

let runMimoSystemTransform (h: Harness) (chk: string -> bool -> unit) =
    promise {
        let! systemOutput = h.runSystemTransform (createEmpty ())
        let systemOut = dynGet systemOutput "system"

        let hasWorkDir =
            not (dynIsNull systemOut)
            && dynIsArr systemOut
            && (unbox<obj[]> systemOut |> Array.exists (fun s -> (string s).Contains(h.workDir)))

        chk "mimo.systemTransform.hasWorkDir" hasWorkDir
    }

let runMimoConfigAndSessionHooks (h: Harness) (chk: string -> bool -> unit) =
    promise {
        let! _ =
            h.runConfigHook (
                createObj [ "agent", box (createObj [ "build", box (createObj [ "model", box "test" ]) ]) ]
            )

        chk "mimo.configHook.run" true

        let! _ =
            h.runSessionPost (createObj [ "sessionID", box h.sessionId; "outcome", box "success"; "error", box "" ])

        chk "mimo.sessionPostHook.run" true

        let! _ = h.runSessionUserQueryPost (createObj [ "sessionID", box h.sessionId; "error", box "" ])
        chk "mimo.sessionUserQueryPostHook.run" true

        let plugin = h.getPlugin ()
        chk "mimo.hook.config.isFunction" (dynTypeIs (dynGet plugin "config") "function")
        chk "mimo.hook.sessionPost.isFunction" (dynTypeIs (dynGet plugin "session.post") "function")
        chk "mimo.hook.sessionUserQueryPost.isFunction" (dynTypeIs (dynGet plugin "session.userQuery.post") "function")
    }

let runMimoSessionDeletedEvent (h: Harness) (chk: string -> bool -> unit) =
    promise {
        let! _ =
            h.fireEvent (
                box
                    {| event =
                        {| ``type`` = "session.deleted"
                           properties = {| sessionID = h.sessionId |} |} |}
            )

        chk "mimo.event.sessionDeleted.noThrow" true
    }
