module Wanxiangshu.Tests.FallbackQueueEffectTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.Coordinator
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchOps
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Tests.FallbackEventBridgeTests

/// F-01: after SendContinue is decided, concurrent NewUserMessage on the same
/// session must prevent the physical prompt (counting IActionExecutor).
type private CountingExecutor() =
    let mutable continueCount = 0

    interface IActionExecutor with
        member _.SendContinue(_sessionID, _model, _continuationID) =
            promise {
                continueCount <- continueCount + 1
            }

        member _.RecoverWithPrompt(_sessionID, _model, _promptText, _continuationID) = Promise.lift ()
        member _.FetchMessages _ = Promise.lift [||]
        member _.PropagateFailure _ = Promise.lift ()
        member _.CaptureCurrentModel _ = Promise.lift None
        member _.AbortRun _ = Promise.lift ()

    member _.ContinueCount = continueCount

let private flushMicrotasks () =
    promise {
        do! Promise.sleep 0
        do! Promise.sleep 0
        do! Promise.sleep 0
    }

let createHandler_humanCancelPreventsPhysicalSendContinue () =
    promise {
        resetRetryGovernorForTests ()
        let! workspaceRoot = mkdtempAsync "fallback-f01-queue-"
        let model = mkModel "f01-provider" "f01-model"
        let runtime = FallbackRuntimeStore()
        let sessionID = "f01-queue-session"
        runtime.UpdateSession(sessionID, selectChain [ model ])
        runtime.UpdateSession(sessionID, recordAgentName "reviewer")

        let translator = SwitchingTranslator(sessionID) :> IEventTranslator
        let executor = CountingExecutor()
        let handler = createHandler translator runtime defaultCfgLookup executor workspaceRoot None

        // Enqueue decision + concurrent human cancel before awaiting either.
        // Queue order: error decision → human cancel → effect claim reenter.
        // Effect must observe cleared lease and skip physical SendContinue.
        let errorHook = handler (createObj [ "kind", box "error" ])
        let humanHook = handler (createObj [ "kind", box "human" ])

        let! errorResult = errorHook
        let! humanResult = humanHook

        equal "retry event consumed" true errorResult.Consumed
        equal "human event is not consumed" false humanResult.Consumed

        do! flushMicrotasks ()

        equal "physical SendContinue never ran" 0 executor.ContinueCount

        let sessionFinal = runtime.GetSession sessionID
        equal "human clears pending lease" None sessionFinal.PendingLease
        equal "human owns session" SessionOwner.Human sessionFinal.Owner
        equal "stale effect does not restore lease" None sessionFinal.PendingLease
        equal "stale effect does not steal ownership" SessionOwner.Human sessionFinal.Owner

        do! rmAsync workspaceRoot
    }

let run () =
    promise {
        do! createHandler_humanCancelPreventsPhysicalSendContinue ()
    }
