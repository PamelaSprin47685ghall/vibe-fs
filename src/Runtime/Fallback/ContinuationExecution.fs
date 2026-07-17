module Wanxiangshu.Runtime.Fallback.ContinuationExecution

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.Fallback.ModelInjection
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.ContinuationEventWriter
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchHelpers

type ContinuationIntent =
    | SendContinueIntent of
        model: FallbackModel *
        agent: string *
        turnId: string *
        gen: int *
        cancelGen: int *
        continuationID: string *
        continuationOrdinal: int
    | RecoverWithPromptIntent of
        model: FallbackModel *
        promptText: string *
        agent: string *
        turnId: string *
        gen: int *
        cancelGen: int *
        continuationID: string *
        continuationOrdinal: int
    | PropagateFailureIntent

let executeContinuation
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (model: FallbackModel)
    (agent: string)
    (dispatchAction: unit -> JS.Promise<unit>)
    : JS.Promise<unit> =
    promise {
        if
            verifyLease runtime sessionID lease
            && ensureActiveAndOwner runtime sessionID lease
        then
            do! runWithRetryGovernor runtime executor workspaceRoot sessionID lease model agent dispatchAction
        else
            do!
                finishContinuation
                    runtime
                    workspaceRoot
                    sessionID
                    lease
                    ContinuationOutcome.Cancelled
                    "Lease validation failed"
    }

let executeSendContinue
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (model: FallbackModel)
    (agent: string)
    : JS.Promise<unit> =
    executeContinuation runtime executor workspaceRoot sessionID lease model agent (fun () ->
        executor.SendContinue(sessionID, model, lease.ContinuationID))

let executeRecoverWithPrompt
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (model: FallbackModel)
    (promptText: string)
    (agent: string)
    : JS.Promise<unit> =
    executeContinuation runtime executor workspaceRoot sessionID lease model agent (fun () ->
        executor.RecoverWithPrompt(sessionID, model, promptText, lease.ContinuationID))

let executeContinuationIntent
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (intent: ContinuationIntent)
    : JS.Promise<unit> =
    promise {
        match intent with
        | SendContinueIntent(model, agent, turnId, gen, cancelGen, continuationID, continuationOrdinal) ->
            let lease =
                { ContinuationID = continuationID
                  ContinuationOrdinal = continuationOrdinal
                  SessionGeneration = gen
                  HumanTurnID = turnId
                  CancelGeneration = cancelGen
                  Owner = SessionOwner.Fallback
                  Model = model
                  PromptText = None
                  Status = LeaseStatus.Requested }

            do! executeSendContinue runtime executor workspaceRoot sessionID lease model agent

        | RecoverWithPromptIntent(model, promptText, agent, turnId, gen, cancelGen, continuationID, continuationOrdinal) ->
            let lease =
                { ContinuationID = continuationID
                  ContinuationOrdinal = continuationOrdinal
                  SessionGeneration = gen
                  HumanTurnID = turnId
                  CancelGeneration = cancelGen
                  Owner = SessionOwner.Fallback
                  Model = model
                  PromptText = Some promptText
                  Status = LeaseStatus.Requested }

            do! executeRecoverWithPrompt runtime executor workspaceRoot sessionID lease model promptText agent

        | PropagateFailureIntent -> do! executor.PropagateFailure sessionID
    }

let handleContinuationAction
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (finalState: SessionFallbackState)
    (model: FallbackModel)
    (promptTextOpt: string option)
    : JS.Promise<SessionFallbackState * ContinuationIntent option> =
    promise {
        let lease = setupContinuationLease runtime sessionID model promptTextOpt
        let agent = runtime.GetAgentName sessionID

        let modelStr =
            match model.Variant with
            | Some v -> $"{model.ProviderID}/{model.ModelID}:{v}"
            | None -> $"{model.ProviderID}/{model.ModelID}"

        let atMs = getTimestampMs ()

        do!
            appendContinuationRequestedOrFail
                workspaceRoot
                sessionID
                lease.ContinuationID
                modelStr
                agent
                atMs
                lease.SessionGeneration
                lease.CancelGeneration
                lease.HumanTurnID
                "Fallback"
                lease.ContinuationOrdinal

        let intent =
            match promptTextOpt with
            | None ->
                SendContinueIntent(
                    model,
                    agent,
                    lease.HumanTurnID,
                    lease.SessionGeneration,
                    lease.CancelGeneration,
                    lease.ContinuationID,
                    lease.ContinuationOrdinal
                )
            | Some promptText ->
                RecoverWithPromptIntent(
                    model,
                    promptText,
                    agent,
                    lease.HumanTurnID,
                    lease.SessionGeneration,
                    lease.CancelGeneration,
                    lease.ContinuationID,
                    lease.ContinuationOrdinal
                )

        return finalState, Some intent
    }

let handleSendContinueAction runtime workspaceRoot sessionID finalState model =
    handleContinuationAction runtime workspaceRoot sessionID finalState model None

let handleRecoverWithPromptAction runtime workspaceRoot sessionID finalState model promptText =
    handleContinuationAction runtime workspaceRoot sessionID finalState model (Some promptText)
