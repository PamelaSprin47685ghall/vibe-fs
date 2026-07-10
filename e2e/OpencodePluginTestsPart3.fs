module Wanxiangshu.E2e.OpencodePluginTestsPart3

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.OpencodePluginTestsPart2

let runPart3
    (harness: Harness)
    (chk: string -> bool -> unit)
    (startHarness: obj -> JS.Promise<obj>)
    (jsonStringify: obj -> string)
    (ok: int)
    (summary: unit -> int)
    (createEmpty: unit -> obj)
    : JS.Promise<int> =
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

        let! nudgeHarnessObj = startHarness nudgeOpts
        let nudgeHarness = unbox<Harness> nudgeHarnessObj

        let! _ =
            nudgeHarness.fireEvent (
                box
                    {| event =
                        {| ``type`` = "session.idle"
                           properties = {| sessionID = nudgeHarness.sessionId |} |} |}
            )

        let mutable nudgeTicks = 0

        while nudgePromptCalls = 0 && nudgeTicks < 20 do
            do! Promise.sleep 50
            nudgeTicks <- nudgeTicks + 1

        do! nudgeHarness.dispose ()
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

        let! rejectedNudgeHarnessObj = startHarness rejectedNudgeOpts
        let rejectedNudgeHarness = unbox<Harness> rejectedNudgeHarnessObj

        let! _ =
            rejectedNudgeHarness.fireEvent (
                box
                    {| event =
                        {| ``type`` = "session.idle"
                           properties = {| sessionID = rejectedNudgeHarness.sessionId |} |} |}
            )

        let mutable rejectedNudgeTicks = 0

        while rejectedNudgePromptCalls = 0 && rejectedNudgeTicks < 20 do
            do! Promise.sleep 50
            rejectedNudgeTicks <- rejectedNudgeTicks + 1

        do! rejectedNudgeHarness.dispose ()
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

        let! abortHarnessObj = startHarness abortOpts
        let abortHarness = unbox<Harness> abortHarnessObj
        let! _ = abortHarness.fireStreamAbort abortHarness.sessionId

        let! _ =
            abortHarness.fireEvent (
                box
                    {| event =
                        {| ``type`` = "session.idle"
                           properties = {| sessionID = abortHarness.sessionId |} |} |}
            )

        do! Promise.sleep 200
        do! abortHarness.dispose ()
        chk "op.nudge.aborted.notCalled" (abortPromptCalls = 0)

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

        do! harness.dispose ()

        printfn "\n✓ %d opencode plugin e2e checks passed" ok
        return summary ()
    }
