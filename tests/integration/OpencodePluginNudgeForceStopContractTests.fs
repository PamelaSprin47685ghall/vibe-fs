module Wanxiangshu.Integration.OpencodePluginNudgeForceStopContractTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Integration.OpencodePluginToolLifecycleContractTests

module Dyn = Wanxiangshu.Runtime.Dyn

let runNudgeForceStop
    (harness: Harness)
    (chk: string -> bool -> unit)
    (startHarness: obj -> JS.Promise<obj>)
    (jsonStringify: obj -> string)
    (createEmpty: unit -> obj)
    : JS.Promise<unit> =
    promise {
        // --- 9. Nudge & Force-Stop workflow -----------------------------------
        let mutable nudgePromptCalls = 0
        let mutable nudgePromptBody = ""

        let nudgeOpts =
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
                            box (fun (body: obj) ->
                                nudgePromptCalls <- nudgePromptCalls + 1
                                nudgePromptBody <- jsonStringify body
                                Promise.lift (box {| ok = true |})) ]
                  ) ]

        let! nudgeHarnessObj = withTimeoutCustom 30000 (startHarness nudgeOpts)
        let nudgeHarness = unbox<Harness> nudgeHarnessObj

        let! _ =
            withTimeout (
                nudgeHarness.fireEvent (
                    box
                        {| event =
                            {| ``type`` = "session.idle"
                               properties = {| sessionID = nudgeHarness.sessionId |} |} |}
                )
            )

        let mutable nudgeTicks = 0

        while nudgePromptCalls = 0 && nudgeTicks < 20 do
            do! yieldMicrotask ()
            nudgeTicks <- nudgeTicks + 1

        do! withTimeoutCustom 4900 (nudgeHarness.dispose ())
        chk "op.nudge.promptSentExactlyOnce" (nudgePromptCalls = 1)
        chk "op.nudge.promptContentValid" ((string nudgePromptBody).IndexOf("There are still incomplete todos") >= 0)

        let mutable rejectedNudgePromptCalls = 0
        let mutable rejectedNudgePromptBody = ""

        let rejectedNudgeOpts =
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
                                             "finish", box "tool"
                                             "id", box "msg-1"
                                             "time", box {| completed = 1000.0 |} ]
                                   )
                                   "parts",
                                   box [| box (createObj [ "type", box "text"; "text", box "call submit_review" ]) |] ]
                         )
                         box (
                             createObj
                                 [ "info",
                                   box (
                                       createObj
                                           [ "role", box "toolResult"
                                             "agent", box "build"
                                             "id", box "msg-2"
                                             "time", box {| completed = 1100.0 |} ]
                                   )
                                   "parts",
                                   box
                                       [| box (createObj [ "type", box "text"; "text", box "Rejected: needs revision" ]) |] ]
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
                            box (fun (body: obj) ->
                                rejectedNudgePromptCalls <- rejectedNudgePromptCalls + 1
                                rejectedNudgePromptBody <- jsonStringify body
                                Promise.lift (box {| ok = true |})) ]
                  ) ]

        let! rejectedNudgeHarnessObj = withTimeoutCustom 30000 (startHarness rejectedNudgeOpts)
        let rejectedNudgeHarness = unbox<Harness> rejectedNudgeHarnessObj

        let! _ =
            withTimeout (
                rejectedNudgeHarness.fireEvent (
                    box
                        {| event =
                            {| ``type`` = "session.idle"
                               properties = {| sessionID = rejectedNudgeHarness.sessionId |} |} |}
                )
            )

        let mutable rejectedNudgeTicks = 0

        while rejectedNudgePromptCalls = 0 && rejectedNudgeTicks < 20 do
            do! yieldMicrotask ()
            rejectedNudgeTicks <- rejectedNudgeTicks + 1

        do! withTimeoutCustom 4900 (rejectedNudgeHarness.dispose ())
        chk "op.nudge.submitReviewRejectedTriggersNudge" (rejectedNudgePromptCalls = 1)

        let mutable abortPromptCalls = 0

        let abortOpts =
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
                                abortPromptCalls <- abortPromptCalls + 1
                                Promise.lift (box {| ok = true |})) ]
                  ) ]

        let! abortHarnessObj = withTimeoutCustom 30000 (startHarness abortOpts)
        let abortHarness = unbox<Harness> abortHarnessObj
        let! _ = withTimeout (abortHarness.fireStreamAbort abortHarness.sessionId)

        let! _ =
            withTimeout (
                abortHarness.fireEvent (
                    box
                        {| event =
                            {| ``type`` = "session.idle"
                               properties = {| sessionID = abortHarness.sessionId |} |} |}
                )
            )

        do! yieldMicrotask ()
        do! withTimeout (abortHarness.dispose ())
        chk "op.nudge.aborted.notCalled" (abortPromptCalls = 0)
    }
