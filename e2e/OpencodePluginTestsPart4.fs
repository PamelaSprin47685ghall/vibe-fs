module Wanxiangshu.E2e.OpencodePluginTestsPart4

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.OpencodePluginTestsPart2

module Dyn = Wanxiangshu.Shell.Dyn

let runPart4
    (harness: Harness)
    (chk: string -> bool -> unit)
    (startHarness: obj -> JS.Promise<obj>)
    (jsonStringify: obj -> string)
    (createEmpty: unit -> obj)
    : JS.Promise<unit> =
    promise {
        // --- 10. session.post error triggers nudge ----------------------------
        let mutable errNudgeCalls = 0

        let errNudgeOpts =
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
                                             "finish", box "stop"
                                             "id", box "msg-1"
                                             "time", box {| completed = 1000.0 |} ]
                                   )
                                   "parts",
                                   box [| box (createObj [ "type", box "text"; "text", box "assistant reply" ]) |] ]
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
                                errNudgeCalls <- errNudgeCalls + 1
                                Promise.lift (box {| ok = true |})) ]
                  ) ]

        let! errNudgeHarnessObj = startHarness errNudgeOpts
        let errNudgeHarness = unbox<Harness> errNudgeHarnessObj

        let! _ =
            errNudgeHarness.runLifecycleHook
                "session.post"
                (createObj
                    [ "sessionID", box errNudgeHarness.sessionId
                      "outcome", box "error"
                      "error", box "something went wrong" ])
                (createEmpty ())

        let mutable errTicks = 0

        while errNudgeCalls = 0 && errTicks < 20 do
            do! Promise.sleep 50
            errTicks <- errTicks + 1

        do! errNudgeHarness.dispose ()
        chk "op.sessionPost.errorTriggersNudge" (errNudgeCalls = 1)

        // --- 11. continue priority -------------------------------------------
        let mutable continueModel = ""

        let continueOpts =
            createObj
                [ "messages", box [||]
                  "mockSessionClient",
                  box (
                      createObj
                          [ "get",
                            box (fun _ -> Promise.lift (box {| data = box {| model = box "anthropic/claude-3-5" |} |}))
                            "prompt",
                            box (fun (arg: obj) ->
                                let realBody =
                                    let b = Dyn.get arg "body"
                                    if Dyn.isNullish b then arg else b

                                let modelVal = Dyn.get realBody "model"

                                if not (Dyn.isNullish modelVal) then
                                    continueModel <- Dyn.str modelVal "modelID"

                                Promise.lift (box {| ok = true |})) ]
                  ) ]

        let! continueHarnessObj = startHarness continueOpts
        let continueHarness = unbox<Harness> continueHarnessObj

        let! _ =
            continueHarness.fireEvent (
                box
                    {| event =
                        {| ``type`` = "session.error"
                           properties =
                            {| sessionID = continueHarness.sessionId
                               error =
                                {| name = "RateLimitError"
                                   message = "rate limit"
                                   statusCode = "429"
                                   isRetryable = "true" |} |} |} |}
            )

        let mutable continueTicks = 0

        while continueModel = "" && continueTicks < 20 do
            do! Promise.sleep 50
            continueTicks <- continueTicks + 1

        do! continueHarness.dispose ()
        chk "op.continue.prioritizesManualModel" (continueModel = "claude-3-5")

        do! harness.dispose ()
    }
