module Wanxiangshu.Integration.OpencodeNudgeHostIntegrationTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Integration.OpencodePluginToolLifecycleContractTests
open Wanxiangshu.Integration.OpencodeContinueContractTests

module Dyn = Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Dyn

let private deferred (name: string) : JS.Promise<unit> * (unit -> unit) =
    let resolver = ref (fun () -> ())
    let pending = Promise.create (fun resolve _ -> resolver.Value <- resolve)

    pending, (fun () -> resolver.Value())

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

        let! nudgeAbortHarnessObj = withTimeoutCustom 30000 (startHarness nudgeAbortOpts)
        let nudgeAbortHarness = unbox<Harness> nudgeAbortHarnessObj

        let! _ =
            withTimeout (
                nudgeAbortHarness.fireEvent (
                    box
                        {| event =
                            {| ``type`` = "session.idle"
                               properties = {| sessionID = nudgeAbortHarness.sessionId |} |} |}
                )
            )

        do! yieldMicrotask ()
        do! withTimeoutCustom 4900 (nudgeAbortHarness.dispose ())
        chk "op.nudge.skippedWhenLastAssistantFinishIsAbort" (nudgeAbortPromptCalls = 0)

        // --- 12. Fallback continue on tool-finish idle followed by error --------
        let mutable fbPromptCalls = 0
        let mutable fbPromptText = ""
        let fbPromptCalled, signalFbPromptCalled = deferred "fbPromptCalled"

        let fbOpts =
            createObj
                [ "messages",
                  box
                      [| box (
                             createObj
                                 [ "info", box (createObj [ "role", box "user"; "id", box "msg-user-1" ])
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
                                   "parts", box [| box (createObj [ "type", box "text"; "text", box "calling tool" ]) |] ]
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

                                signalFbPromptCalled ()
                                Promise.lift (box {| ok = true |}))
                            "model",
                            box
                                {| id = "test-model"
                                   providerID = "test" |} ]
                  )
                  "agentsContent",
                  box
                      "---\nmodels:\n  default:\n    - test/test-model\nfallback:\n  legacyZeroWidthContinue: true\n---\n" ]

        let! fbHarnessObj = withTimeoutCustom 30000 (startHarness fbOpts)
        let fbHarness = unbox<Harness> fbHarnessObj

        let fbConfigArgs =
            createObj [ "agent", box (createObj [ "build", box (createObj [ "model", box "test" ]) ]) ]

        let! _ = fbHarness.runConfigHook fbConfigArgs

        let! _ =
            withTimeout (
                fbHarness.fireEvent (
                    box
                        {| event =
                            {| ``type`` = "session.idle"
                               properties = {| sessionID = fbHarness.sessionId |} |} |}
                )
            )

        do! yieldMicrotask ()

        let! _ =
            withTimeout (
                fbHarness.runLifecycleHook
                    "session.post"
                    (createObj
                        [ "sessionID", box fbHarness.sessionId
                          "outcome", box "error"
                          "error", box (createObj [ "name", box "EmptyOutputError"; "message", box "empty output" ]) ])
                    (createObj [])
            )

        do! withTimeout fbPromptCalled

        do! withTimeoutCustom 4900 (fbHarness.dispose ())
        chk "op.fallback.continueOnToolFinishIdleError" (fbPromptCalls = 1)
        chk "op.fallback.continueBodyCorrect" (fbPromptText = "​")

        // --- 13. Bug1: loop active + empty text → nudge loop ---------------
        let mutable bug1PromptCalls = 0
        let bug1PromptCalled, signalBug1PromptCalled = deferred "bug1PromptCalled"

        let bug1Msgs: obj array =
            [| box (
                   createObj
                       [ "info",
                         box (
                             createObj
                                 [ "role", box "assistant"
                                   "agent", box "build"
                                   "finish", box "stop"
                                   "id", box "msg-bug1-1"
                                   "time", box {| completed = 2000.0 |} ]
                         )
                         "parts", box [||] ]
               ) |]

        let bug1Opts =
            createObj
                [ "messages", box bug1Msgs
                  "mockSessionClient",
                  box (
                      createObj
                          [ "todo", box (fun _ -> Promise.lift (box {| data = [||] |}))
                            "prompt",
                            box (fun _ ->
                                bug1PromptCalls <- bug1PromptCalls + 1
                                signalBug1PromptCalled ()
                                Promise.lift (box {| ok = true |}))
                            "create", box (fun _ -> Promise.lift (box {| data = {| id = "mock-review" |} |})) ]
                  ) ]

        let! bug1HarnessObj = withTimeoutCustom 30000 (startHarness bug1Opts)
        let bug1Harness = unbox<Harness> bug1HarnessObj

        let! _ = withTimeout (bug1Harness.runCommandExecuteBefore "loop" "fix the bug")

        let! _ =
            withTimeout (
                bug1Harness.fireEvent (
                    box
                        {| event =
                            {| ``type`` = "session.idle"
                               properties = {| sessionID = bug1Harness.sessionId |} |} |}
                )
            )

        do! withTimeout bug1PromptCalled
        do! withTimeoutCustom 4900 (bug1Harness.dispose ())
        chk "op.bug1.loopActiveEmptyTextTriggersNudge" (bug1PromptCalls >= 1)

        // --- 14. Bug2: loop active + missing finish/time + string status idle -> nudge loop ---
        let mutable bug2PromptCalls = 0
        let mutable bug2PromptText = ""

        let bug2Msgs: obj array =
            [| box (
                   createObj
                       [ "info",
                         box (createObj [ "role", box "assistant"; "agent", box "build"; "id", box "msg-bug2-1" ])
                         "parts", box [| box (createObj [ "type", box "text"; "text", box "work complete" ]) |] ]
               ) |]

        let bug2Opts =
            createObj
                [ "messages", box bug2Msgs
                  "mockSessionClient",
                  box (
                      createObj
                          [ "todo", box (fun _ -> Promise.lift (box {| data = [||] |}))
                            "prompt",
                            box (fun (arg: obj) ->
                                bug2PromptCalls <- bug2PromptCalls + 1
                                let body = Dyn.get arg "body"
                                let parts = Dyn.get body "parts"

                                if not (Dyn.isNullish parts) && Dyn.isArray parts then
                                    let partsArr = unbox<obj array> parts
                                    let firstPart = partsArr.[0]
                                    bug2PromptText <- Dyn.str firstPart "text"

                                Promise.lift (box {| ok = true |}))
                            "create", box (fun _ -> Promise.lift (box {| data = {| id = "mock-review" |} |})) ]
                  ) ]

        let! bug2HarnessObj = withTimeoutCustom 30000 (startHarness bug2Opts)
        let bug2Harness = unbox<Harness> bug2HarnessObj

        let! _ = withTimeout (bug2Harness.runCommandExecuteBefore "loop" "fix the bug")
        do! yieldMicrotask ()

        // status is a string instead of object
        let statusEvent =
            box
                {| event =
                    box
                        {| ``type`` = "session.status"
                           properties =
                            box
                                {| sessionID = bug2Harness.sessionId
                                   status = box "idle" |} |} |}

        let! _ = withTimeout (bug2Harness.fireEvent statusEvent)
        do! yieldMicrotask ()
        do! withTimeoutCustom 4900 (bug2Harness.dispose ())
        chk "op.bug2.loopActiveMissingFinishTriggersNudge" (bug2PromptCalls >= 1)

        chk
            "op.bug2.nudgeIsLoopNudge"
            (bug2PromptText.Contains("You are in loop mode. You must call the submit_review"))

        do! Wanxiangshu.Integration.OpencodeContinueContractTests.run startHarness chk createEmpty

        return summary ()
    }
