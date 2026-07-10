module Wanxiangshu.Tests.SubagentIoFallbackRecoveryTestsPart2

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

/// When a nudge is active (e.g. mid-nudge delivery), a prompt rejection must NOT
/// cause the subagent to return immediately.  The subagent should block until the
/// nudge completes (TaskComplete = true) before surfacing a result.  This test
/// exposes the premature-exit bug: without the wait, the subagent returns Error
/// while the nudge is still in flight.
let runSubagentWaitsForNudgeToComplete () =
    promise {
        let rt = FallbackRuntimeState()
        let registry = ChildAgentRegistry.Create()
        let childId = "child-nudge-wait"
        registry.RegisterChildAgent(childId, "coder", Some "parent-1")

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
                            box (
                                System.Func<obj, JS.Promise<unit>>(fun _ ->
                                    promise {
                                        // Simulate: a nudge is currently active when prompt rejects.
                                        // The runtime should expose this so the subagent can wait.
                                        rt.SetNudgeActive childId true
                                        return! Promise.reject (exn "model failed during nudge")
                                    })
                            )
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
                                                                         text = "nudge-recovered" |} |] |} |] |}
                                    })
                            )
                            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]
                  ) ]

        let runP =
            let rscr
                : FallbackRuntimeState
                      -> ChildAgentRegistry
                      -> obj
                      -> string
                      -> string
                      -> string
                      -> string
                      -> string
                      -> obj
                      -> obj
                      -> bool
                      -> string option
                      -> JS.Promise<Result<string, DomainError>> =
                runSubagentCoreResult

            rscr rt registry client "coder" "t" "go" "/tmp" "parent-1" (box null) (box null) false None

        // Let the prompt rejection land and the error path execute.
        do! waitForListenerRegistered rt childId

        // BUG: without the nudge-wait, the subagent has already returned Error here.
        // The text should NOT have been extracted because the nudge is still active.
        check "text not extracted while nudge active" (not textExtracted.Value)

        // Now the nudge completes — TaskComplete is set, nudge no longer active.
        rt.SetNudgeActive childId false

        let s1 = rt.GetOrCreateState childId

        rt.UpdateState
            childId
            { s1 with
                Lifecycle = FallbackLifecycle.TaskComplete }

        rt.ClearSubsessionPending childId

        let! result = runP

        // After nudge completes, the subagent should recover and return Ok.
        check "text extracted after nudge settled" textExtracted.Value

        match result with
        | Ok text -> check "nudge-recovered output present" (text.Contains "nudge-recovered")
        | Error _ -> failwith "expected Ok after nudge completed"
    }

/// When a continue is in progress (fallback state machine sent a continue action),
/// a prompt rejection must NOT cause the subagent to return immediately.  The
/// subagent should block until the continue resolves (TaskComplete = true).  This
/// test exposes the premature-exit bug: without the wait, the subagent returns
/// Error while the continue is still in flight.
let runSubagentWaitsForContinueToComplete () =
    promise {
        let rt = FallbackRuntimeState()
        let registry = ChildAgentRegistry.Create()
        let childId = "child-continue-wait"
        registry.RegisterChildAgent(childId, "investigator", Some "parent-1")

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
                            box (
                                System.Func<obj, JS.Promise<unit>>(fun _ ->
                                    promise {
                                        // Simulate: fallback state machine has dispatched a continue
                                        // action and is waiting for the model to respond.
                                        let s = rt.GetOrCreateState childId

                                        rt.UpdateState
                                            childId
                                            { s with
                                                Phase = FallbackPhase.Retrying 1 }

                                        rt.SetConsumed childId true
                                        rt.SetContinueActive childId true
                                        return! Promise.reject (exn "model failed during continue")
                                    })
                            )
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
                                                                         text = "continue-recovered" |} |] |} |] |}
                                    })
                            )
                            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]
                  ) ]

        let runP =
            let rscr
                : FallbackRuntimeState
                      -> ChildAgentRegistry
                      -> obj
                      -> string
                      -> string
                      -> string
                      -> string
                      -> string
                      -> obj
                      -> obj
                      -> bool
                      -> string option
                      -> JS.Promise<Result<string, DomainError>> =
                runSubagentCoreResult

            rscr rt registry client "investigator" "t" "go" "/tmp" "parent-1" (box null) (box null) false None

        // Let the prompt rejection land and the error path execute.
        do! waitForListenerRegistered rt childId

        // BUG: without the continue-wait, the subagent has already returned Error here.
        // The text should NOT have been extracted because the continue is still active.
        check "text not extracted while continue active" (not textExtracted.Value)

        // Now the continue resolves — TaskComplete is set, continue no longer active.
        rt.SetContinueActive childId false
        rt.SetTaskComplete childId true
        let s1 = rt.GetOrCreateState childId

        rt.UpdateState
            childId
            { s1 with
                Phase = FallbackPhase.Idle
                Lifecycle = FallbackLifecycle.TaskComplete }

        rt.SetConsumed childId false
        rt.ClearSubsessionPending childId

        let! result = runP

        // After continue resolves, the subagent should recover and return Ok.
        check "text extracted after continue settled" textExtracted.Value

        match result with
        | Ok text -> check "continue-recovered output present" (text.Contains "continue-recovered")
        | Error _ -> failwith "expected Ok after continue completed"
    }

let run () =
    promise {
        do! runSubagentWaitsForNudgeToComplete ()
        do! runSubagentWaitsForContinueToComplete ()
    }
