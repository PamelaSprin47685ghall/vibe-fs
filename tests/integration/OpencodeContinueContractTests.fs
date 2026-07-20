module Wanxiangshu.Integration.OpencodeContinueContractTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Integration.OpencodePluginToolLifecycleContractTests
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.SubsessionActorRegistry

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.PromptFrontMatter

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

let private completedMessages () : obj =
    let model = createObj [ "providerID", box "test"; "modelID", box "test-model" ]

    let info =
        createObj [ "role", box "assistant"; "agent", box "inspector"; "model", box model ]

    let part = createObj [ "type", box "text"; "text", box "continue final output" ]
    let message = createObj [ "info", box info; "parts", box [| box part |] ]
    box (createObj [ "data", box [| box message |] ])

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
                  "agent", box "inspector"
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
                              let rt: FallbackRuntimeStore = unbox runtimeRef.Value
                              rt.GetOrCreateState(childID).Lifecycle = FallbackLifecycle.TaskComplete
                          else
                              false

                      if isComplete then
                          finalExtractionCalls.Value <- finalExtractionCalls.Value + 1
                          Promise.lift (completedMessages ())
                      else
                          messagesCalls.Value <- messagesCalls.Value + 1
                          Promise.lift (box {| data = [||] |}))
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

        let spawnP = harness.executePluginTool "inspector" spawnArgs (createEmpty ())
        do! withTimeout firstPromptSeen
        do! yieldMicrotask ()
        do! sleep 20

        let taskCompleteInput =
            createObj
                [ "tool", box "task_complete"
                  "sessionID", box childID
                  "args", box (createObj [ "output", box "done" ]) ]

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
                                             agent = "inspector"
                                             model =
                                              {| providerID = "test"
                                                 modelID = "test-model" |} |} ]
                            ) ]
                  ) ]

        let! _ =
            withTimeout (
                harness.runLifecycleHook "tool.execute.after" taskCompleteInput (createObj [ "output", box "done" ])
            )

        let! _ = withTimeout (harness.fireEvent statusEvent)

        let! spawnOutput = withTimeout spawnP
        let iterator = iteratorFromOutput spawnOutput
        check "continue e2e spawn returns iterator" iterator.IsSome

        check
            "continue child actor remains registered after initial run"
            (SubsessionActorRegistry.TryGet harness.workDir childID |> Option.isSome)

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

            // Regression: a pending continuation transport must not block the
            // per-session event boundary from accepting a host error fact.
            let! _ = withTimeoutCustom 1000 (harness.fireEvent errorEvent)

            let! _ = withTimeout (harness.fireEvent statusEvent)

            do! withTimeout recoveryPromptSeen
            do! yieldMicrotask ()
            do! sleep 20
            equal "continue does not extract before task completion" 0 finalExtractionCalls.Value

            let! _ =
                withTimeout (
                    harness.runLifecycleHook "tool.execute.after" taskCompleteInput (createObj [ "output", box "done" ])
                )

            do! yieldMicrotask ()
            do! sleep 20

            let! _ = withTimeout (harness.fireEvent statusEvent)

            let! continueOutput = withTimeout continueP
            check "continue extracts after task completion" (finalExtractionCalls.Value > 0)
            equal "continue returns final child output" true (continueOutput.Contains "continue final output")

        do! sleep 200
        do! withTimeoutCustom 4900 (harness.dispose ())
    }
