module Wanxiangshu.Runtime.Fallback.ContinuationExecution

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

let private modelString (model: FallbackModel) =
    match model.Variant with
    | Some v -> $"{model.ProviderID}/{model.ModelID}:{v}"
    | None -> $"{model.ProviderID}/{model.ModelID}"

let private continuationIntent
    (model: FallbackModel)
    (agent: string)
    (promptTextOpt: string option)
    (lease: PendingLease)
    : ContinuationIntent =
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

let private appendRequested
    (workspaceRoot: string)
    (sessionID: string)
    (model: FallbackModel)
    (agent: string)
    (lease: PendingLease)
    : JS.Promise<unit> =
    appendContinuationRequestedOrFail
        workspaceRoot
        sessionID
        lease.ContinuationID
        (modelString model)
        agent
        (getTimestampMs ())
        lease.SessionGeneration
        lease.CancelGeneration
        lease.HumanTurnID
        "Fallback"
        lease.ContinuationOrdinal

let handleContinuationAction
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (finalState: SessionFallbackState)
    (model: FallbackModel)
    (promptTextOpt: string option)
    : JS.Promise<SessionFallbackState * ContinuationIntent option> =
    promise {
        let currentState = runtime.GetSession sessionID
        match tryReserveContinuationLease currentState model promptTextOpt with
        | None ->
            return finalState, None
        | Some (nextState, lease) ->
            let agent = currentState.AgentName
            do! appendRequested workspaceRoot sessionID model agent lease
            let committed = commitContinuationLease runtime sessionID currentState nextState None
            if not committed then
                return finalState, None
            else
                let intent = continuationIntent model agent promptTextOpt lease
                return finalState, Some intent
    }

let handleSendContinueAction runtime workspaceRoot sessionID finalState model =
    handleContinuationAction runtime workspaceRoot sessionID finalState model None

let handleRecoverWithPromptAction runtime workspaceRoot sessionID finalState model promptText =
    handleContinuationAction runtime workspaceRoot sessionID finalState model (Some promptText)
