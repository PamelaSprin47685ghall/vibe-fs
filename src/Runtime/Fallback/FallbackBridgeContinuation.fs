module Wanxiangshu.Runtime.Fallback.FallbackBridgeContinuation

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Runtime.Fallback.FallbackBridgePorts
open Wanxiangshu.Runtime.EventLogAppendSession
open Wanxiangshu.Runtime.Fallback.FallbackBridgeLease
open Wanxiangshu.Runtime.EventLogRuntimeAppend
open Wanxiangshu.Runtime.Clock

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

let appendOutcomeIfNeeded
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (outcome: ContinuationOutcome)
    (errorOrReason: string)
    : JS.Promise<unit> =
    promise {
        match outcome with
        | ContinuationOutcome.Failed ->
            do!
                appendContinuationFailedOrFail
                    workspaceRoot
                    sessionID
                    lease.ContinuationID
                    errorOrReason
                    lease.ContinuationOrdinal
        | ContinuationOutcome.Cancelled ->
            do!
                appendContinuationCancelledOrFail
                    workspaceRoot
                    sessionID
                    lease.ContinuationID
                    errorOrReason
                    lease.ContinuationOrdinal
        | ContinuationOutcome.Settled ->
            do!
                appendContinuationSettledOrFail
                    workspaceRoot
                    sessionID
                    lease.ContinuationID
                    lease.HumanTurnID
                    lease.SessionGeneration
                    errorOrReason
                    lease.ContinuationOrdinal
    }

let finishContinuation
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (outcome: ContinuationOutcome)
    (errorOrReason: string)
    : JS.Promise<unit> =
    promise {
        let isLeaseStillActive =
            match runtime.TryGetPendingLease sessionID with
            | Some pending when pending.ContinuationID = lease.ContinuationID -> true
            | _ -> false

        if isLeaseStillActive then
            do! appendOutcomeIfNeeded workspaceRoot sessionID lease outcome errorOrReason

        let cleared = runtime.TryClearPendingLease(sessionID, lease.ContinuationID)

        if cleared then
            if runtime.GetSessionOwner sessionID = SessionOwner.Fallback then
                runtime.SetSessionOwner sessionID SessionOwner.NoOwner

            runtime.SetMainContinuationAwaitingStart sessionID false

        runtime.UpdateState sessionID (runtime.GetOrCreateState sessionID)
    }

let handleDispatchComplete
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (model: FallbackModel)
    (agent: string)
    : JS.Promise<unit> =
    promise {
        let isValid =
            verifyLeaseWithStatus LeaseStatus.DispatchStarted runtime sessionID lease

        if not isValid then
            do! executor.AbortRun sessionID

            do!
                finishContinuation
                    runtime
                    workspaceRoot
                    sessionID
                    lease
                    ContinuationOutcome.Cancelled
                    "Cancelled after dispatch"
        else
            let modelStr =
                match model.Variant with
                | Some v -> model.ProviderID + "/" + model.ModelID + ":" + v
                | None -> model.ProviderID + "/" + model.ModelID

            let atMs = getTimestampMs ()

            do!
                appendContinuationDispatchedOrFail
                    workspaceRoot
                    sessionID
                    lease.ContinuationID
                    modelStr
                    agent
                    atMs
                    lease.ContinuationOrdinal

            if
                not (
                    runtime.TryTransitionPendingLease(
                        sessionID,
                        lease.ContinuationID,
                        LeaseStatus.DispatchStarted,
                        LeaseStatus.Dispatched
                    )
                )
            then
                do! executor.AbortRun sessionID

                do!
                    finishContinuation
                        runtime
                        workspaceRoot
                        sessionID
                        lease
                        ContinuationOutcome.Cancelled
                        "Cancelled after dispatch"
            else
                runtime.SetInjectedAt sessionID atMs
                runtime.SetInjectedModel sessionID model
    }

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
            try
                do!
                    appendContinuationDispatchStartedOrFail
                        workspaceRoot
                        sessionID
                        lease.ContinuationID
                        lease.ContinuationOrdinal

                let isLeaseStillValid =
                    runtime.TryTransitionPendingLease(
                        sessionID,
                        lease.ContinuationID,
                        LeaseStatus.Requested,
                        LeaseStatus.DispatchStarted
                    )

                if not isLeaseStillValid then
                    do! executor.AbortRun sessionID

                    do!
                        finishContinuation
                            runtime
                            workspaceRoot
                            sessionID
                            lease
                            ContinuationOutcome.Cancelled
                            "Lease invalid at dispatch"
                else
                    do! dispatchAction ()
                    do! handleDispatchComplete runtime executor workspaceRoot sessionID lease model agent
            with ex ->
                do! finishContinuation runtime workspaceRoot sessionID lease ContinuationOutcome.Failed ex.Message
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

let setupContinuationLease
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (model: FallbackModel)
    (promptTextOpt: string option)
    : PendingLease =
    runtime.SetSessionOwner sessionID SessionOwner.Fallback
    runtime.SetMainContinuationAwaitingStart sessionID true
    let currentGen = runtime.GetSessionGeneration sessionID
    let currentCancelGen = runtime.GetCancelGeneration sessionID
    runtime.SetActiveContinuationGeneration sessionID currentGen
    runtime.SetActiveContinuationCancelGeneration sessionID currentCancelGen

    let continuationID = System.Guid.NewGuid().ToString("N")
    let continuationOrdinal = runtime.IncrementContinuationOrdinal sessionID

    let lease =
        { ContinuationID = continuationID
          ContinuationOrdinal = continuationOrdinal
          SessionGeneration = currentGen
          HumanTurnID = runtime.GetHumanTurnId sessionID
          CancelGeneration = currentCancelGen
          Owner = SessionOwner.Fallback
          Model = model
          PromptText = promptTextOpt
          Status = LeaseStatus.Requested }

    runtime.SetPendingLease(sessionID, lease)
    lease

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
            | Some v -> model.ProviderID + "/" + model.ModelID + ":" + v
            | None -> model.ProviderID + "/" + model.ModelID

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
