module Wanxiangshu.E2e.OpencodePluginContinueRecoveryTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.E2e.OpencodePluginTestsPart2
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackRuntimeState

module Dyn = Wanxiangshu.Shell.Dyn

open Wanxiangshu.Kernel.PromptFrontMatter

let private deferred (name: string) : JS.Promise<unit> * (unit -> unit) =
    let resolver = ref (fun () -> ())
    let pending = Promise.create (fun resolve _ -> resolver.Value <- resolve)

    pending,
    (fun () ->
        printfn "signal %s called" name
        resolver.Value())

let private iteratorFromOutput (output: string) : string option =
    let parsed = parseFrontMatter output

    if isNull parsed then
        None
    else
        let iters = parsed?iterators

        if isNull iters then
            None
        else
            let arr: string array = unbox iters
            if arr.Length > 0 then Some arr.[0] else None

let run
    (startHarness: obj -> JS.Promise<obj>)
    (check: string -> bool -> unit)
    (createEmpty: unit -> obj)
    : JS.Promise<unit> =
    promise {
        let childID = "e2e-child-continue-network-error"
        let promptCount = ref 0
        let firstPromptSeen, signalFirstPrompt = deferred "firstPrompt"
        let continuePromptSeen, signalContinuePrompt = deferred "continuePrompt"
        let recoveryPromptSeen, signalRecoveryPrompt = deferred "recoveryPrompt"
        let messagesCalls = ref 0
        let runtimeRef = ref (box null)
        let finalExtractionCalls = ref 0

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
                      printfn "mockSessionClient.prompt called, count = %d" promptCount.Value

                      match promptCount.Value with
                      | 1 ->
                          signalFirstPrompt ()
                          Promise.lift ()
                      | 2 ->
                          signalContinuePrompt ()
                          Promise.reject (exn "network connection lost")
                      | _ ->
                          signalRecoveryPrompt ()
                          Promise.lift ())
                  "messages",
                  box (fun _ ->
                      let isComplete =
                          if not (Dyn.isNullish runtimeRef.Value) then
                              let rt: FallbackRuntimeState = unbox runtimeRef.Value
                              rt.GetOrCreateState(childID).Lifecycle = FallbackLifecycle.TaskComplete
                          else
                              false

                      if isComplete then
                          finalExtractionCalls.Value <- finalExtractionCalls.Value + 1
                      else
                          messagesCalls.Value <- messagesCalls.Value + 1

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
                                                       text = "continue final output" |} |] |} |] |}
                      ))
                  "abort", box (fun _ -> Promise.lift ()) ]

        let opts =
            createObj
                [ "agentsContent", box "---\nmodels:\n  default:\n    - test/test-model\n---\n"
                  "mockSessionClient", box mockSessionClient ]

        let! harnessObject = withTimeoutCustom 30000 (startHarness opts)
        let harness = unbox<Harness> harnessObject
        runtimeRef.Value <- harness.getFallbackRuntime ()

        let spawnArgs =
            createObj
                [ "intents",
                  box
                      [| box
                             {| objective = "spawn"
                                background = "continue recovery"
                                questions = [| "what happened?" |]
                                entries = [||] |} |] ]

        let spawnP = harness.executePluginTool "investigator" spawnArgs (createEmpty ())
        do! withTimeout firstPromptSeen

        let taskCompleteInput =
            createObj
                [ "tool", box "task_complete"
                  "sessionID", box childID
                  "args", box (createObj []) ]

        let! _ =
            withTimeout (
                harness.runLifecycleHook "tool.execute.after" taskCompleteInput (createObj [ "output", box "done" ])
            )

        let! spawnOutput = withTimeout spawnP
        let iterator = iteratorFromOutput spawnOutput
        check "continue e2e spawn returns iterator" iterator.IsSome

        let statusEvent =
            createObj
                [ "event",
                  box (
                      createObj
                          [ "type", box "session.status"
                            "properties",
                            box (
                                createObj
                                    [ "sessionID", box childID
                                      "status",
                                      box
                                          {| ``type`` = "idle"
                                             agent = "investigator"
                                             model =
                                              {| providerID = "test"
                                                 modelID = "test-model" |} |} ]
                            ) ]
                  ) ]

        let! _ = withTimeout (harness.fireEvent statusEvent)

        match iterator with
        | None -> failwith "spawn did not return an iterator"
        | Some iterator ->
            finalExtractionCalls.Value <- 0

            let continueArgs =
                createObj [ "iterator", box iterator; "prompt", box "resume work" ]

            // Fire session.status type=busy to simulate the host starting a new prompt turn and resetting TaskComplete
            let busyEvent =
                createObj
                    [ "event",
                      box (
                          createObj
                              [ "type", box "session.status"
                                "properties",
                                box (createObj [ "sessionID", box childID; "status", box {| ``type`` = "busy" |} ]) ]
                      ) ]

            let! _ = withTimeout (harness.fireEvent busyEvent)

            let continueP = harness.executePluginTool "continue" continueArgs (createEmpty ())
            do! withTimeout continuePromptSeen

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

            let! _ = withTimeout (harness.fireEvent errorEvent)

            do! withTimeout recoveryPromptSeen
            check "continue does not extract before task completion" (finalExtractionCalls.Value = 0)

            let! _ =
                withTimeout (
                    harness.runLifecycleHook "tool.execute.after" taskCompleteInput (createObj [ "output", box "done" ])
                )

            let! continueOutput = withTimeout continueP
            check "continue extracts after task completion" (finalExtractionCalls.Value > 0)
            check "continue returns final child output" (continueOutput.Contains "continue final output")

        do! withTimeoutCustom 4900 (harness.dispose ())
    }
