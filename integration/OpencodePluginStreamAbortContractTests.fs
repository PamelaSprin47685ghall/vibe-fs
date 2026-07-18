module Wanxiangshu.Integration.OpencodePluginStreamAbortContractTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Integration.OpencodePluginToolLifecycleContractTests

module Dyn = Wanxiangshu.Runtime.Dyn

let runStreamAbort
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
                [ "agentsContent", box "---\nmodels:\n  default:\n    - test/test-model\n---\n"
                  "messages",
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
                                             "time", box {| completed = 1000.0 |}
                                             "model",
                                             box
                                                 {| providerID = "test"
                                                    modelID = "test-model" |} ]
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
                                Promise.lift (box {| ok = true |}))
                            "abort", box (fun _ -> Promise.lift (box ())) ]
                  ) ]

        let! errNudgeHarnessObj = withTimeoutCustom 30000 (startHarness errNudgeOpts)
        let errNudgeHarness = unbox<Harness> errNudgeHarnessObj

        let! _ =
            withTimeout (
                errNudgeHarness.runLifecycleHook
                    "session.post"
                    (createObj
                        [ "sessionID", box errNudgeHarness.sessionId
                          "outcome", box "error"
                          "error", box "something went wrong" ])
                    (createEmpty ())
            )

        let mutable errTicks = 0

        while errNudgeCalls = 0 && errTicks < 20 do
            do! yieldMicrotask ()
            errTicks <- errTicks + 1

        do! withTimeout (errNudgeHarness.dispose ())
        chk "op.sessionPost.errorTriggersNudge" (errNudgeCalls = 1)

        // --- 11. continue priority -------------------------------------------
        let mutable continueModel = ""

        let continueOpts =
            createObj
                [ "agentsContent", box "---\nmodels:\n  default:\n    - anthropic/claude-3-5\n---\n"
                  "messages", box [||]
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

        let! continueHarnessObj = withTimeoutCustom 30000 (startHarness continueOpts)
        let continueHarness = unbox<Harness> continueHarnessObj

        let! _ =
            withTimeout (
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
            )

        let mutable continueTicks = 0

        while continueModel = "" && continueTicks < 20 do
            do! yieldMicrotask ()
            continueTicks <- continueTicks + 1

        do! withTimeoutCustom 4000 (continueHarness.dispose ())
        chk "op.continue.prioritizesManualModel" (continueModel = "claude-3-5")

        do! withTimeoutCustom 4000 (harness.dispose ())
    }
