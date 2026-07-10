module Wanxiangshu.E2e.OpencodePluginNudgeTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.OpencodePluginTestsPart2

module Dyn = Wanxiangshu.Shell.Dyn

let runNudgeTests
    (harness: Harness)
    (chk: string -> bool -> unit)
    (startHarness: obj -> JS.Promise<obj>)
    (jsonStringify: obj -> string)
    (ok: int)
    (summary: unit -> int)
    (createEmpty: unit -> obj)
    : JS.Promise<int> =
    promise {
        // --- 11. nudge skipped when last assistant finish is abort ------------
        let mutable nudgeAbortPromptCalls = 0

        let nudgeAbortOpts =
            createObj
                [ "messages",
                  box
                      [| box (
                             createObj
                                 [ "info",
                                   box (
                                       createObj
                                           [ "role", box "assistant"
                                             "agent", box "build"
                                             "finish", box "abort"
                                             "id", box "msg-1"
                                             "time", box {| completed = 1000.0 |} ]
                                   )
                                   "parts",
                                   box [| box (createObj [ "type", box "text"; "text", box "aborted output" ]) |] ]
                         ) |]
                  "mockSessionClient",
                  box (
                      createObj
                          [ "todo",
                            box (fun _ ->
                                Promise.lift (
                                    box
                                        {| data =
                                            [| {| content = "layout"
                                                  status = "pending" |} |] |}
                                ))
                            "prompt",
                            box (fun _ ->
                                nudgeAbortPromptCalls <- nudgeAbortPromptCalls + 1
                                Promise.lift (box {| ok = true |})) ]
                  ) ]

        let! nudgeAbortHarnessObj = startHarness nudgeAbortOpts
        let nudgeAbortHarness = unbox<Harness> nudgeAbortHarnessObj

        let! _ =
            nudgeAbortHarness.fireEvent (
                box
                    {| event =
                        {| ``type`` = "session.idle"
                           properties = {| sessionID = nudgeAbortHarness.sessionId |} |} |}
            )

        do! Promise.sleep 100
        do! nudgeAbortHarness.dispose ()
        chk "op.nudge.skippedWhenLastAssistantFinishIsAbort" (nudgeAbortPromptCalls = 0)

        // --- 12. Fallback continue on tool-finish idle followed by error --------
        let mutable fbPromptCalls = 0
        let mutable fbPromptText = ""

        let fbOpts =
            createObj
                [ "messages",
                  box
                      [| box (
                             createObj
                                 [ "info",
                                   box (
                                       createObj
                                           [ "role", box "user"
                                             "id", box "msg-user-1" ]
                                   )
                                   "parts",
                                   box [| box (createObj [ "type", box "text"; "text", box "implement layout" ]) |] ]
                         )
                         box (
                             createObj
                                 [ "info",
                                   box (
                                       createObj
                                           [ "role", box "assistant"
                                             "agent", box "build"
                                             "finish", box "tool"
                                             "id", box "msg-assistant-1"
                                             "time", box {| completed = 1000.0 |} ]
                                   )
                                   "parts",
                                   box [| box (createObj [ "type", box "text"; "text", box "calling tool" ]) |] ]
                         ) |]
                  "mockSessionClient",
                  box (
                      createObj
                          [ "prompt",
                            box (fun (arg: obj) ->
                                fbPromptCalls <- fbPromptCalls + 1
                                let body = Dyn.get arg "body"
                                let parts = Dyn.get body "parts"
                                if not (Dyn.isNullish parts) && Dyn.isArray parts then
                                    let partsArr = unbox<obj array> parts
                                    let firstPart = partsArr.[0]
                                    fbPromptText <- Dyn.str firstPart "text"
                                Promise.lift (box {| ok = true |}))
                            "model",
                            box {| id = "test-model"; providerID = "test" |} ]
                  ) ]

        let! fbHarnessObj = startHarness fbOpts
        let fbHarness = unbox<Harness> fbHarnessObj

        let fbConfigArgs =
            createObj [ "agent", box (createObj [ "build", box (createObj [ "model", box "test" ]) ]) ]
        let! _ = fbHarness.runConfigHook fbConfigArgs

        let! _ =
            fbHarness.fireEvent (
                box
                    {| event =
                        {| ``type`` = "session.idle"
                           properties = {| sessionID = fbHarness.sessionId |} |} |}
            )
        do! Promise.sleep 100

        let! _ =
            fbHarness.runLifecycleHook
                "session.post"
                (createObj
                    [ "sessionID", box fbHarness.sessionId
                      "outcome", box "error"
                      "error",
                      box (
                          createObj
                              [ "name", box "EmptyOutputError"
                                "message", box "empty output" ]
                      ) ])
                (createObj [])

        let mutable fbTicks = 0
        while fbPromptCalls = 0 && fbTicks < 20 do
            do! Promise.sleep 50
            fbTicks <- fbTicks + 1

        do! fbHarness.dispose ()
        chk "op.fallback.continueOnToolFinishIdleError" (fbPromptCalls = 1)
        chk "op.fallback.continueBodyCorrect" (fbPromptText = "continue")

        return summary ()
    }
