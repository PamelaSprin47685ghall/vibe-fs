module Wanxiangshu.E2e.OpencodePluginSubagentRecoveryTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.E2e.OpencodePluginTestsPart2
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FallbackRuntimeState

let private deferred () : JS.Promise<unit> * (unit -> unit) =
    let resolver = ref (fun () -> ())
    let promise = Promise.create (fun resolve _ -> resolver.Value <- resolve)
    promise, (fun () -> resolver.Value())

let run
    (startHarness: obj -> JS.Promise<obj>)
    (check: string -> bool -> unit)
    (createEmpty: unit -> obj)
    : JS.Promise<unit> =
    promise {
        let firstPromptSeen, signalFirstPrompt = deferred ()
        let firstPromptRelease, releaseFirstPrompt = deferred ()
        let secondPromptSeen, signalSecondPrompt = deferred ()
        let promptCount = ref 0
        let textExtracted = ref false
        let childID = "e2e-child-network-error"
        let runtimeRef = ref (box null)

        let mockSessionClient =
            createObj
                [ "model",
                  box
                      {| providerID = "test"
                         modelID = "test-model" |}
                  "agent", box "investigator"
                  "create", box (fun _ -> Promise.lift (box {| data = {| id = childID |} |}))
                  "prompt",
                  box (fun _ ->
                      promptCount.Value <- promptCount.Value + 1

                      promise {
                          match promptCount.Value with
                          | 1 ->
                              signalFirstPrompt ()
                              do! firstPromptRelease
                              return! Promise.reject (exn "network connection lost")
                          | 2 ->
                              signalSecondPrompt ()
                              return ()
                          | _ -> return ()
                      })
                  "messages",
                  box (fun _ ->
                      textExtracted.Value <- true

                      Promise.lift (
                          box
                              {| data =
                                  [| box
                                         {| info =
                                             box
                                                 {| role = "assistant"
                                                    agent = "investigator"
                                                    model =
                                                     box
                                                         {| providerID = "test"
                                                            modelID = "test-model" |} |}
                                            parts =
                                             [| box
                                                    {| ``type`` = "text"
                                                       text = "final child output" |} |] |} |] |}
                      ))
                  "abort", box (fun _ -> Promise.lift ()) ]

        let opts =
            createObj
                [ "agentsContent", box "---\nmodels:\n  default:\n    - test/test-model\n---\n"
                  "mockSessionClient", box mockSessionClient ]

        let! harnessObj = startHarness opts
        let harness = unbox<Harness> harnessObj
        runtimeRef.Value <- harness.getFallbackRuntime ()

        let args =
            createObj
                [ "intents",
                  box
                      [| box
                             {| objective = "reproduce"
                                background = "network recovery"
                                questions = [| "what happened?" |]
                                entries = [||] |} |] ]

        let toolPromise = harness.executePluginTool "investigator" args (createEmpty ())
        do! firstPromptSeen

        let errorEvent =
            createObj
                [ "event",
                  box (
                      createObj
                          [ "type", box "session.error"
                            "properties",
                            box (
                                createObj
                                    [ "sessionID", box childID
                                      "error",
                                      box
                                          {| name = "APIError"
                                             message = "network connection lost"
                                             isRetryable = true |} ]
                            ) ]
                  ) ]

        let! _ = harness.fireEvent errorEvent

        do! secondPromptSeen
        let runtime: FallbackRuntimeState = unbox runtimeRef.Value
        runtime.SetContinueActive childID true
        runtime.SetConsumed childID true
        runtime.SetTaskComplete childID true
        runtime.ClearSubsessionPending childID
        releaseFirstPrompt ()

        let completion = promise { return! toolPromise }

        do! yieldMicrotask ()
        do! yieldMicrotask ()
        do! yieldMicrotask ()
        do! yieldMicrotask ()
        do! yieldMicrotask ()
        do! yieldMicrotask ()
        do! yieldMicrotask ()
        do! yieldMicrotask ()
        let completedBeforePhaseReset = textExtracted.Value

        if not completedBeforePhaseReset then
            runtime.SetContinueActive childID false

        let! result = completion
        check "e2e parent waits for child terminal state" completedBeforePhaseReset
        check "e2e child final output is complete" (result.Contains "final child output")
        do! harness.dispose ()
    }
