module Wanxiangshu.Runtime.NudgeRuntimeTypes

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Runtime.Nudge.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Runtime.Fallback.ModelInjection
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Runtime.NudgeMessageClassifier

type NudgeRuntimeState =
    { retryPendingSessions: Set<string>
      forceStoppedSessions: Set<string> }

let emptyRuntimeState =
    { retryPendingSessions = Set.empty
      forceStoppedSessions = Set.empty }

let private resolveModelFromMessages (msgs: obj array) (lastAssistantModel: string option) : string option =
    if isNull msgs || msgs.Length = 0 then
        lastAssistantModel
    else
        let lastUserMsgOpt =
            msgs
            |> Array.tryFindBack (fun msg ->
                let role = Dyn.str msg "role"
                let info = Dyn.get msg "info"
                let msgRole = if not (Dyn.isNullish info) then Dyn.str info "role" else ""
                (role = "user" || msgRole = "user") && not (classifyUserMessage msg = "nudge"))

        match lastUserMsgOpt |> Option.bind tryGetModelStringFromMessage with
        | Some m -> Some m
        | None ->
            let lastAssistantModelFromMsg =
                msgs
                |> Array.tryFindBack (fun msg ->
                    let role = Dyn.str msg "role"
                    let info = Dyn.get msg "info"
                    let msgRole = if not (Dyn.isNullish info) then Dyn.str info "role" else ""

                    (role = "assistant" || msgRole = "assistant")
                    && not (isSyntheticAssistantAgent (Dyn.str info "agent")))
                |> Option.bind tryGetModelStringFromMessage

            match lastAssistantModelFromMsg with
            | Some m -> Some m
            | None -> lastAssistantModel

let resolveNudgeModel
    (msgs: obj array)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionID: string)
    (lastAssistantModel: string option)
    : string option =
    // PRIORITY:
    // 1. Latest human (real user) message model — NEVER overridden by injected models.
    // 2. Runtime model — from session/agent config.
    // 3. Last real user message from message history (non-nudge, non-synthetic).
    // 4. Last non-synthetic assistant model.
    // 5. `lastAssistantModel` parameter (caller-provided fallback).
    match fallbackRuntime.GetLatestHumanModel sessionID with
    | Some m -> Some m
    | None ->
        match fallbackRuntime.GetModel sessionID with
        | Some m -> Some(modelWithVariantString m)
        | None -> resolveModelFromMessages msgs lastAssistantModel

/// Nudge dispatch is lower priority than fallback and compaction.  In
/// particular, a settled fallback lease may still be visible to a concurrent
/// idle callback until its event projection catches up; treating that lease
/// as a terminal gate prevents the callback from manufacturing a second
/// synthetic turn.
let private nudgeBlockedByFallbackState (runtime: FallbackRuntimeStore) (sessionKey: string) : bool =
    let lifecycleTerminal =
        match runtime.TryGetState sessionKey with
        | Some state -> state.Lifecycle <> FallbackLifecycle.Active
        | None -> false

    let owner = runtime.GetSessionOwner sessionKey

    let fallbackOwnerActive =
        owner = SessionOwner.Fallback
        || owner = SessionOwner.Compaction
        || owner = SessionOwner.Nudge

    let settledFallbackLease =
        match runtime.TryGetPendingLease sessionKey with
        | Some lease -> lease.Status = LeaseStatus.Settled || lease.Status = LeaseStatus.Cancelled
        | None -> false

    lifecycleTerminal
    || fallbackOwnerActive
    || settledFallbackLease
    || runtime.IsCompacted sessionKey

let finishNudge
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionKey: string)
    (lease: NudgeLease)
    (outcome: NudgeOutcome)
    (errorOrReason: string)
    (actionStr: string)
    (anchor: string)
    : JS.Promise<unit> =
    promise {
        match runtime.TryGetPendingNudgeLease sessionKey with
        | Some nl when nl.NudgeID = lease.NudgeID ->
            match outcome with
            | NudgeOutcome.Failed ->
                do! appendNudgeFailedOrFail workspaceRoot sessionKey lease.NudgeID errorOrReason lease.NudgeOrdinal
            | NudgeOutcome.Cancelled ->
                do! appendNudgeCancelledOrFail workspaceRoot sessionKey lease.NudgeID errorOrReason lease.NudgeOrdinal
            | NudgeOutcome.Dispatched ->
                do!
                    appendNudgeDispatchedOrFail
                        workspaceRoot
                        sessionKey
                        lease.NudgeID
                        actionStr
                        anchor
                        lease.NudgeOrdinal
            | NudgeOutcome.Settled ->
                do! appendNudgeSettledOrFail workspaceRoot sessionKey lease.NudgeID errorOrReason lease.NudgeOrdinal

            if outcome <> NudgeOutcome.Dispatched then
                if runtime.TryClearPendingNudgeLease(sessionKey, lease.NudgeID) then
                    runtime.ClearActiveNudgeNonce sessionKey

                    if runtime.GetSessionOwner sessionKey = SessionOwner.Nudge then
                        runtime.SetSessionOwner sessionKey SessionOwner.NoOwner

                    runtime.SetNudgeActive sessionKey false
        | _ -> ()
    }

let private isLeaseValid (runtime: FallbackRuntimeStore) (sessionKey: string) (lease: NudgeLease) : bool =
    let currentGen = runtime.GetSessionGeneration sessionKey
    let currentCancelGen = runtime.GetCancelGeneration sessionKey
    let currentTurnId = runtime.GetHumanTurnId sessionKey
    let currentOwner = runtime.GetSessionOwner sessionKey

    let isLifecycleActive =
        match runtime.TryGetState sessionKey with
        | Some state -> state.Lifecycle = FallbackLifecycle.Active
        | None -> true

    lease.SessionGeneration = currentGen
    && lease.HumanTurnID = currentTurnId
    && lease.CancelGeneration = currentCancelGen
    && lease.Owner = SessionOwner.Nudge
    && currentOwner = SessionOwner.Nudge
    && not (runtime.IsForceStopped sessionKey)
    && isLifecycleActive

let private tryClaimAndRegisterLease
    (workspaceRoot: string)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionKey: string)
    (action: NudgeAction)
    (nudgeAnchorKey: string)
    : JS.Promise<NudgeLease option> =
    promise {
        let sessionGen = fallbackRuntime.GetSessionGeneration sessionKey
        let cancelGen = fallbackRuntime.GetCancelGeneration sessionKey
        let humanTurnId = fallbackRuntime.GetHumanTurnId sessionKey
        let nudgeOrdinal = fallbackRuntime.IncrementNudgeOrdinal sessionKey
        let nudgeId = "nudge-" + System.Guid.NewGuid().ToString("N")
        let nonce = "nudge_" + System.Guid.NewGuid().ToString("N")

        let! claimed =
            promise {
                try
                    return!
                        tryClaimNudgeDispatch
                            workspaceRoot
                            sessionKey
                            action
                            nudgeAnchorKey
                            nudgeId
                            nonce
                            sessionGen
                            cancelGen
                            humanTurnId
                            nudgeOrdinal
                with _ ->
                    return false
            }

        if not claimed then
            return None
        else
            let lease: NudgeLease =
                { NudgeID = nudgeId
                  NudgeOrdinal = nudgeOrdinal
                  Nonce = nonce
                  HumanTurnID = humanTurnId
                  SessionGeneration = sessionGen
                  CancelGeneration = cancelGen
                  Owner = SessionOwner.Nudge
                  Status = LeaseStatus.DispatchStarted }

            fallbackRuntime.SetPendingNudgeLease(sessionKey, lease)
            fallbackRuntime.SetSessionOwner sessionKey SessionOwner.Nudge
            fallbackRuntime.SetActiveNudgeNonce sessionKey nonce
            return Some lease
    }

let private validateAndFinalizeOutcome
    (workspaceRoot: string)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionKey: string)
    (lease: NudgeLease)
    (action: NudgeAction)
    (nudgeAnchorKey: string)
    (outcome: SendOutcome)
    (abortRun: string -> JS.Promise<unit>)
    : JS.Promise<unit> =
    promise {
        if not (isLeaseValid fallbackRuntime sessionKey lease) then
            do! abortRun sessionKey

            do!
                finishNudge
                    fallbackRuntime
                    workspaceRoot
                    sessionKey
                    lease
                    NudgeOutcome.Cancelled
                    "Cancelled after dispatch"
                    ""
                    ""
        else
            match outcome with
            | Delivered ->
                let dispatchedLease =
                    { lease with
                        Status = LeaseStatus.Dispatched }

                do!
                    finishNudge
                        fallbackRuntime
                        workspaceRoot
                        sessionKey
                        dispatchedLease
                        NudgeOutcome.Dispatched
                        ""
                        (toString action)
                        nudgeAnchorKey

                if
                    not (
                        fallbackRuntime.TryTransitionPendingNudgeLease(
                            sessionKey,
                            lease.NudgeID,
                            LeaseStatus.DispatchStarted,
                            LeaseStatus.Dispatched
                        )
                    )
                then
                    do! abortRun sessionKey

                    do!
                        finishNudge
                            fallbackRuntime
                            workspaceRoot
                            sessionKey
                            lease
                            NudgeOutcome.Cancelled
                            "Cancelled after dispatch"
                            ""
                            ""
            | Busy ->
                do! finishNudge fallbackRuntime workspaceRoot sessionKey lease NudgeOutcome.Failed "Session busy" "" ""
            | Aborted ->
                do!
                    finishNudge
                        fallbackRuntime
                        workspaceRoot
                        sessionKey
                        lease
                        NudgeOutcome.Cancelled
                        "Aborted by client"
                        ""
                        ""
            | Failed ->
                do! finishNudge fallbackRuntime workspaceRoot sessionKey lease NudgeOutcome.Failed "Send failed" "" ""
    }

let private dispatchNudge
    (workspaceRoot: string)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionKey: string)
    (action: NudgeAction)
    (nudgeAnchorKey: string)
    (agentFromMessage: string option)
    (modelFromMessage: string option)
    (promptText: string)
    (sendNudge: string -> string option -> string option -> string -> string -> JS.Promise<SendOutcome>)
    (abortRun: string -> JS.Promise<unit>)
    : JS.Promise<unit> =
    promise {
        match! tryClaimAndRegisterLease workspaceRoot fallbackRuntime sessionKey action nudgeAnchorKey with
        | None -> ()
        | Some lease ->
            let isLifecycleActive =
                match fallbackRuntime.TryGetState sessionKey with
                | Some state -> state.Lifecycle = FallbackLifecycle.Active
                | None -> true

            if fallbackRuntime.IsForceStopped sessionKey || not isLifecycleActive then
                let reason =
                    if fallbackRuntime.IsForceStopped sessionKey then
                        "Force stopped"
                    else
                        "Session is not active"

                do! finishNudge fallbackRuntime workspaceRoot sessionKey lease NudgeOutcome.Cancelled reason "" ""
            else
                let! outcome = sendNudge promptText agentFromMessage modelFromMessage lease.NudgeID lease.Nonce

                do!
                    validateAndFinalizeOutcome
                        workspaceRoot
                        fallbackRuntime
                        sessionKey
                        lease
                        action
                        nudgeAnchorKey
                        outcome
                        abortRun
    }

let runNudgeFlowCore
    (host: Host)
    (workspaceRoot: string)
    (fallbackRuntime: FallbackRuntimeStore)
    (runtimeState: NudgeRuntimeState)
    (sessionKey: string)
    (takeSnapshot: unit -> JS.Promise<SessionSnapshot option>)
    (sendNudge: string -> string option -> string option -> string -> string -> JS.Promise<SendOutcome>)
    (abortRun: string -> JS.Promise<unit>)
    : JS.Promise<NudgeRuntimeState> =
    promise {
        if nudgeBlockedByFallbackState fallbackRuntime sessionKey then
            return runtimeState
        else
            match! takeSnapshot () with
            | None -> return runtimeState
            | Some snapshot ->
                match deriveAction snapshot with
                | NudgeNone -> return runtimeState
                | action ->
                    match selectNudgePrompt host action snapshot with
                    | None -> return runtimeState
                    | Some promptText ->
                        do!
                            dispatchNudge
                                workspaceRoot
                                fallbackRuntime
                                sessionKey
                                action
                                snapshot.nudgeAnchorKey
                                snapshot.agentFromMessage
                                snapshot.modelFromMessage
                                promptText
                                sendNudge
                                abortRun

                        return runtimeState
    }

type NudgeRuntimeEvent =
    | Ignore
    | StreamEnd of workspaceId: string * stopReason: string * lastAssistantMessage: string
    | StreamAbort of workspaceId: string
    | AbortedError of workspaceId: string
    | StepFailed of workspaceId: string
    | Prompted of workspaceId: string
