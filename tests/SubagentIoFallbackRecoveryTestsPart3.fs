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
open Wanxiangshu.Shell.ChildSessionMailbox
open Wanxiangshu.Tests.TempWorkspace

let private waitForListenerRegistered (runtime: FallbackRuntimeState) (sessionID: string) : JS.Promise<unit> =
    let rec poll () =
        promise {
            if
                runtime.HasListeners sessionID
                || ChildSessionMailboxRegistry.TryGet sessionID |> Option.isSome
            then
                return ()
            else
                do! yieldMicrotask ()
                return! poll ()
        }

    poll ()

/// session.prompt may resolve before child events; SubsessionPending must block settle.
let runSubagentDoesNotExtractTextWhilePendingAfterEarlyPromptResolve () =
    promise {
        let! dir = mkdtempAsync "subagent-early-prompt-"

        try
            let rt = FallbackRuntimeState()
            let registry = ChildAgentRegistry.Create()
            let childId = "child-early-prompt"
            registry.RegisterChildAgent(childId, "coder", Some "parent-1")

            let s0 = rt.GetOrCreateState childId

            rt.UpdateState
                childId
                { s0 with
                    Phase = FallbackPhase.Idle
                    Lifecycle = FallbackLifecycle.Active }

            rt.SetConsumed childId false
            let messagesCallCount = ref 0

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
                                            messagesCallCount.Value <- messagesCallCount.Value + 1

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
                    dir
                    "parent-1"
                    (box null)
                    (box null)
                    false
                    (Some childId)

            do! waitForListenerRegistered rt childId
            do! yieldMicrotask ()

            check "pending blocks extract" (messagesCallCount.Value = 1)

            rt.ClearSubsessionPending childId
            rt.SetBusyCount childId 1
            do! yieldMicrotask ()
            check "busy blocks extract" (messagesCallCount.Value = 1)

            rt.SetBusyCount childId 0
            let s0 = rt.GetOrCreateState childId

            rt.UpdateState
                childId
                { s0 with
                    Lifecycle = FallbackLifecycle.TaskComplete }

            match ChildSessionMailboxRegistry.TryGet childId with
            | Some mb ->
                do! mb.Post(Command.TaskComplete "")
                do! mb.Post(Command.SessionIdle)
            | None -> ()

            let! result = runP

            check "extract after complete" (messagesCallCount.Value = 2)

            do! rmAsync dir

            match result with
            | Ok text -> check "output present" (text.Contains "after-busy")
            | Error _ -> failwith "expected Ok"
        with ex ->
            do! rmAsync dir
            return! Promise.reject ex
    }

/// Regression: prompt network error → inner catch waitForSubagentSettle.
/// With Phase=Retrying + TaskComplete=true, old impl hangs because
/// fallbackGateOpen ignores TaskComplete. Must NOT hang; must extract text.
let runSubagentCompletesDespiteRetryingPhaseAfterNetworkError () =
    promise {
        let! dir = mkdtempAsync "subagent-retrying-complete-"

        try
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

            let messagesCallCount = ref 0

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
                              messagesCallCount.Value <- messagesCallCount.Value + 1
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
                    dir
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
                    Phase = FallbackPhase.Retrying 1
                    Lifecycle = FallbackLifecycle.Active }

            rt.SetConsumed childId true
            rt.ClearSubsessionPending childId

            promptReleaseResolver.Value()
            do! promptRejected
            check "continue error waits before completion" (messagesCallCount.Value = 1)
            rt.SetTaskComplete childId true

            match ChildSessionMailboxRegistry.TryGet childId with
            | Some mb ->
                do! mb.Post(Command.TaskComplete "")
                do! mb.Post(Command.SessionIdle)
            | None -> ()

            for _ in 1..8 do
                do! yieldMicrotask ()

            let completedBeforePhaseReset = (messagesCallCount.Value = 2)
            let! result = runP

            check "completed before residual phase reset" completedBeforePhaseReset
            check "extract after complete" (messagesCallCount.Value = 2)

            do! rmAsync dir

            match result with
            | Ok text -> check "output contains final-output" (text.Contains "final-output")
            | Error _ -> failwith "expected Ok result"
        with ex ->
            do! rmAsync dir
            return! Promise.reject ex
    }

let run () =
    promise {
        do! runSubagentDoesNotExtractTextWhilePendingAfterEarlyPromptResolve ()
        do! runSubagentCompletesDespiteRetryingPhaseAfterNetworkError ()
    }
