module Wanxiangshu.Tests.SubagentIoFallbackRecoveryTestsPart3

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Opencode.SubagentIo

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
                            "prompt",
                            box (System.Func<obj, JS.Promise<unit>>(fun _ -> promise { return () }))
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

let run () =
    promise { do! runSubagentDoesNotExtractTextWhilePendingAfterEarlyPromptResolve () }