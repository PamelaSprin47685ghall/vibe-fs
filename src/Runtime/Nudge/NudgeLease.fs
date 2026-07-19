module Wanxiangshu.Runtime.NudgeLease

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
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
        match (runtime.GetSession sessionKey).PendingNudgeLease with
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
                if runtime.UpdateSessionReturning(sessionKey, tryClearPendingNudgeLeaseReturning lease.NudgeID) then
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

let private claimDispatch
    (workspaceRoot: string)
    (sessionKey: string)
    (action: NudgeAction)
    (nudgeAnchorKey: string)
    (nudgeId: string)
    (nonce: string)
    (sessionGen: int)
    (cancelGen: int)
    (humanTurnId: string)
    (nudgeOrdinal: int)
    : JS.Promise<bool> =
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
        with ex ->
            JS.console.error (
                box
                    {| feature = "nudge"
                       session = sessionKey
                       phase = "claim"
                       error = ex.Message |}
            )

            return raise ex
    }

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
            claimDispatch
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

            fallbackRuntime.UpdateSession(sessionKey, setPendingNudgeLease lease)
            fallbackRuntime.UpdateSession(sessionKey, transferOwnership SessionOwner.Nudge)
            fallbackRuntime.UpdateSession(sessionKey, armNudgeNonce nonce)
            return Some lease
    }
