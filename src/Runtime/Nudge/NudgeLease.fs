module Wanxiangshu.Runtime.NudgeLease

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Kernel.FallbackKernel.Types

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
                    runtime.UpdateSession(sessionKey, disarmNudgeNonce)

                    if (runtime.GetSession sessionKey).Owner = SessionOwner.Nudge then
                        runtime.UpdateSession(sessionKey, transferOwnership SessionOwner.NoOwner)

                    runtime.Update(sessionKey, setNudgeActive false)
        | _ -> ()
    }

let isLeaseValid (runtime: FallbackRuntimeStore) (sessionKey: string) (lease: NudgeLease) : bool =
    let currentGen = (runtime.GetSession sessionKey).SessionGeneration
    let currentCancelGen = (runtime.GetSession sessionKey).CancelGeneration
    let currentTurnId = (runtime.GetSession sessionKey).HumanTurnId
    let currentOwner = (runtime.GetSession sessionKey).Owner

    let isLifecycleNotCancelled =
        match runtime.TryGetState sessionKey with
        | Some state -> state.Lifecycle <> FallbackLifecycle.Cancelled
        | None -> true

    lease.SessionGeneration = currentGen
    && lease.HumanTurnID = currentTurnId
    && lease.CancelGeneration = currentCancelGen
    && lease.Owner = SessionOwner.Nudge
    && currentOwner = SessionOwner.Nudge
    && not ((runtime.GetSession sessionKey).CompactionForceStopped)
    && isLifecycleNotCancelled

let tryClaimAndRegisterLease
    (workspaceRoot: string)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionKey: string)
    (action: NudgeAction)
    (nudgeAnchorKey: string)
    : JS.Promise<NudgeLease option> =
    promise {
        let sessionGen = (fallbackRuntime.GetSession sessionKey).SessionGeneration
        let cancelGen = (fallbackRuntime.GetSession sessionKey).CancelGeneration
        let humanTurnId = (fallbackRuntime.GetSession sessionKey).HumanTurnId

        let nudgeOrdinal =
            fallbackRuntime.UpdateSessionReturning(sessionKey, incrementNudgeOrdinal)

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
            fallbackRuntime.UpdateSession(sessionKey, transferOwnership SessionOwner.Nudge)
            fallbackRuntime.UpdateSession(sessionKey, armNudgeNonce nonce)
            return Some lease
    }

let private processDeliveredOutcome
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionKey: string)
    (lease: NudgeLease)
    (action: NudgeAction)
    (nudgeAnchorKey: string)
    (abortRun: string -> JS.Promise<unit>)
    : JS.Promise<unit> =
    promise {
        let dispatchedLease =
            { lease with
                Status = LeaseStatus.Dispatched }

        do!
            finishNudge
                runtime
                workspaceRoot
                sessionKey
                dispatchedLease
                NudgeOutcome.Dispatched
                ""
                (toString action)
                nudgeAnchorKey

        if
            not (
                runtime.TryTransitionPendingNudgeLease(
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
                    runtime
                    workspaceRoot
                    sessionKey
                    lease
                    NudgeOutcome.Cancelled
                    "Cancelled after dispatch"
                    ""
                    ""
    }

let validateAndFinalizeOutcome
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
                do!
                    processDeliveredOutcome
                        fallbackRuntime
                        workspaceRoot
                        sessionKey
                        lease
                        action
                        nudgeAnchorKey
                        abortRun
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
