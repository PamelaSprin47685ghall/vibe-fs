module Wanxiangshu.Tests.SubagentIoFallbackRecoveryTestsPart3

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Opencode.SubagentIo
open Wanxiangshu.Kernel.Domain

let private waitForListenerRegistered (runtime: FallbackRuntimeState) (sessionID: string) : JS.Promise<unit> =
    let rec poll () =
        promise {
            if runtime.HasListeners sessionID then
                return ()
            else
                do! yieldMicrotask ()
                return! poll ()
        }

    poll ()

/// session.prompt may resolve before child events; SubsessionPending must block settle.
let runSubagentDoesNotExtractTextWhilePendingAfterEarlyPromptResolve () =
    promise {
        let rt = FallbackRuntimeState()
        let registry = ChildAgentRegistry.Create()
        let childId = "child-early-prompt"
        registry.RegisterChildAgent(childId, "coder", Some "parent-1")

        let s0 = rt.GetOrCreateState childId

        rt.UpdateState
            childId
            { s0 with
                Phase = FallbackPhase.Idle
                TaskComplete = false }

        rt.SetConsumed childId false
        let textExtracted = ref false

        let client =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "create",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise { return box {| data = box {| id = childId |} |} })
                            )
                            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun _ -> promise { return () }))
                            "messages",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise {
                                        textExtracted.Value <- true

                                        return
                                            box
                                                {| data =
                                                    [| box
                                                           {| info = box {| role = "assistant" |}
                                                              parts =
                                                               [| box
                                                                      {| ``type`` = "text"
                                                                         text = "after-busy" |} |] |} |] |}
                                    })
                            )
                            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]
                  ) ]

        let runP =
            runSubagentCoreResult
                rt
                registry
                client
                "coder"
                "Continue"
                "go"
                "/tmp"
                "parent-1"
                (box null)
                (box null)
                false
                (Some childId)

        do! waitForListenerRegistered rt childId
        do! yieldMicrotask ()

        check "pending blocks extract" (not textExtracted.Value)

        rt.ClearSubsessionPending childId
        rt.SetBusyCount childId 1
        do! yieldMicrotask ()
        check "busy blocks extract" (not textExtracted.Value)

        rt.SetBusyCount childId 0
        rt.SetTaskComplete childId true

        let! result = runP

        check "extract after complete" textExtracted.Value

        match result with
        | Ok text -> check "output present" (text.Contains "after-busy")
        | Error _ -> failwith "expected Ok"
    }

/// Regression: prompt network error → inner catch waitForSubagentSettle.
/// With Phase=Retrying + TaskComplete=true, old impl hangs because
/// fallbackGateOpen ignores TaskComplete. Must NOT hang; must extract text.
let runSubagentCompletesDespiteRetryingPhaseAfterNetworkError () =
    promise {
        let rt = FallbackRuntimeState()
        let registry = ChildAgentRegistry.Create()
        let childId = "child-retrying-complete"
        registry.RegisterChildAgent(childId, "investigator", Some "parent-2")

        let promptStartedResolver = ref (fun () -> ())

        let promptStarted: JS.Promise<unit> =
            Promise.create (fun resolve _ -> promptStartedResolver.Value <- resolve)

        let promptReleaseResolver = ref (fun () -> ())

        let promptRelease: JS.Promise<unit> =
            Promise.create (fun resolve _ -> promptReleaseResolver.Value <- resolve)

        let promptRejectedResolver = ref (fun () -> ())

        let promptRejected: JS.Promise<unit> =
            Promise.create (fun resolve _ -> promptRejectedResolver.Value <- resolve)

        let textExtracted = ref false

        let finalMessagePart = createObj [ "type", box "text"; "text", box "final-output" ]

        let finalMessage =
            createObj
                [ "info", box (createObj [ "role", box "assistant" ])
                  "parts", box [| box finalMessagePart |] ]

        let finalMessages = createObj [ "data", box [| box finalMessage |] ]

        let session =
            createObj
                [ "create",
                  box (
                      System.Func<obj, JS.Promise<obj>>(fun _ ->
                          promise { return box {| data = box {| id = childId |} |} })
                  )
                  "prompt",
                  box (
                      System.Func<obj, JS.Promise<unit>>(fun _ ->
                          promise {
                              promptStartedResolver.Value()
                              do! promptRelease
                              promptRejectedResolver.Value()
                              return! Promise.reject (exn "network connection lost")
                          })
                  )
                  "messages",
                  box (
                      System.Func<obj, JS.Promise<obj>>(fun _ ->
                          textExtracted.Value <- true
                          Promise.lift finalMessages)
                  )
                  "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]

        let client = createObj [ "session", box session ]

        let runP =
            runSubagentCoreResult
                rt
                registry
                client
                "investigator"
                "Continue"
                "go"
                "/tmp"
                "parent-2"
                (box null)
                (box null)
                false
                (Some childId)

        do! promptStarted

        let s0 = rt.GetOrCreateState childId

        rt.UpdateState
            childId
            { s0 with
                Phase = FallbackPhase.Retrying 1 }

        rt.SetConsumed childId true
        rt.ClearSubsessionPending childId

        promptReleaseResolver.Value()
        do! promptRejected
        check "continue error waits before completion" (not textExtracted.Value)
        rt.SetTaskComplete childId true
        do! yieldMicrotask ()
        do! yieldMicrotask ()
        do! yieldMicrotask ()
        do! yieldMicrotask ()
        do! yieldMicrotask ()
        do! yieldMicrotask ()
        do! yieldMicrotask ()
        do! yieldMicrotask ()

        let completedBeforePhaseReset = textExtracted.Value

        let! result = runP

        check "completed before residual phase reset" completedBeforePhaseReset

        match result with
        | Ok text -> check "output contains final-output" (text.Contains "final-output")
        | Error _ -> failwith "expected Ok result"
    }

let runSubagentContinueResetsTaskComplete () =
    promise {
        let rt = FallbackRuntimeState()
        let registry = ChildAgentRegistry.Create()
        let childId = "child-continue-reset"
        registry.RegisterChildAgent(childId, "investigator", Some "parent-3")

        let s0 = rt.GetOrCreateState childId
        rt.UpdateState childId { s0 with TaskComplete = true }

        let textExtracted = ref false
        let promptStartedResolver = ref (fun () -> ())

        let promptStarted =
            Promise.create (fun resolve _ -> promptStartedResolver.Value <- resolve)

        let finalMessages =
            createObj
                [ "data",
                  box
                      [| createObj
                             [ "info", box (createObj [ "role", box "assistant" ])
                               "parts", box [| box (createObj [ "type", box "text"; "text", box "final-output" ]) |] ] |] ]

        let session =
            createObj
                [ "create",
                  box (
                      System.Func<obj, JS.Promise<obj>>(fun _ -> Promise.lift (box {| data = box {| id = childId |} |}))
                  )
                  "prompt",
                  box (
                      System.Func<obj, JS.Promise<unit>>(fun _ ->
                          promise {
                              promptStartedResolver.Value()
                              do! yieldMicrotask ()
                          })
                  )
                  "messages",
                  box (
                      System.Func<obj, JS.Promise<obj>>(fun _ ->
                          textExtracted.Value <- true
                          Promise.lift finalMessages)
                  )
                  "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]

        let client = createObj [ "session", box session ]

        let runP =
            runSubagentCoreResult
                rt
                registry
                client
                "investigator"
                "Continue"
                "go"
                "/tmp"
                "parent-3"
                (box null)
                (box null)
                false
                (Some childId)

        do! promptStarted
        do! yieldMicrotask ()
        do! yieldMicrotask ()
        check "TaskComplete reset to false blocks early extract on continue" (not textExtracted.Value)

        rt.SetTaskComplete childId true
        let! result = runP
        check "text extracted after continue task completes" textExtracted.Value

        match result with
        | Ok text -> check "continue output present" (text.Contains "final-output")
        | Error _ -> failwith "expected Ok"
    }

let run () =
    promise {
        do! runSubagentDoesNotExtractTextWhilePendingAfterEarlyPromptResolve ()
        do! runSubagentCompletesDespiteRetryingPhaseAfterNetworkError ()
        do! runSubagentContinueResetsTaskComplete ()
    }
