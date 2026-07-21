module Wanxiangshu.Runtime.Fallback.ContinuationExecutionCore

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.ContinuationEventWriter
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchOps
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchGovernor
open Wanxiangshu.Runtime.Fallback.ContinuationExecution

let executeContinuation
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (model: FallbackModel)
    (agent: string)
    (dispatchAction: unit -> JS.Promise<unit>)
    (reenter: SessionReenter)
    : JS.Promise<unit> =
    promise {
        let! shouldRun =
            promise {
                let mutable ok = false

                do!
                    reenter (fun () ->
                        promise {
                            ok <-
                                verifyLease runtime sessionID lease
                                && ensureActiveAndOwner runtime sessionID lease
                        })

                return ok
            }

        if shouldRun then
            do! runWithRetryGovernor runtime executor workspaceRoot sessionID lease model agent dispatchAction reenter
        else
            do!
                reenter (fun () ->
                    finishContinuation
                        runtime
                        workspaceRoot
                        sessionID
                        lease
                        ContinuationOutcome.Cancelled
                        "Lease validation failed")
    }

let executeSendContinue
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (model: FallbackModel)
    (agent: string)
    (reenter: SessionReenter)
    : JS.Promise<unit> =
    executeContinuation
        runtime
        executor
        workspaceRoot
        sessionID
        lease
        model
        agent
        (fun () -> executor.SendContinue(sessionID, model, lease.ContinuationID))
        reenter

let executeRecoverWithPrompt
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (model: FallbackModel)
    (promptText: string)
    (agent: string)
    (reenter: SessionReenter)
    : JS.Promise<unit> =
    executeContinuation
        runtime
        executor
        workspaceRoot
        sessionID
        lease
        model
        agent
        (fun () -> executor.RecoverWithPrompt(sessionID, model, promptText, lease.ContinuationID))
        reenter

let executeContinuationIntent
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (intent: ContinuationIntent)
    (reenter: SessionReenter)
    : JS.Promise<unit> =
    promise {
        match intent with
        | SendContinueIntent(model, agent, turnId, gen, cancelGen, continuationID, continuationOrdinal) ->
            let lease =
                { ContinuationID = continuationID
                  ContinuationOrdinal = continuationOrdinal
                  SessionGeneration = gen
                  HumanTurnID = turnId
                  HostUserMessageId = ""
                  HostRunId = ""
                  CancelGeneration = cancelGen
                  Owner = SessionOwner.Fallback
                  Model = model
                  PromptText = None
                  Status = LeaseStatus.Requested }

            do! executeSendContinue runtime executor workspaceRoot sessionID lease model agent reenter

        | RecoverWithPromptIntent(model, promptText, agent, turnId, gen, cancelGen, continuationID, continuationOrdinal) ->
            let lease =
                { ContinuationID = continuationID
                  ContinuationOrdinal = continuationOrdinal
                  SessionGeneration = gen
                  HumanTurnID = turnId
                  HostUserMessageId = ""
                  HostRunId = ""
                  CancelGeneration = cancelGen
                  Owner = SessionOwner.Fallback
                  Model = model
                  PromptText = Some promptText
                  Status = LeaseStatus.Requested }

            do! executeRecoverWithPrompt runtime executor workspaceRoot sessionID lease model promptText agent reenter

        | PropagateFailureIntent -> do! reenter (fun () -> promise { do! executor.PropagateFailure sessionID })
    }
