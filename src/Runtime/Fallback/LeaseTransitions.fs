module Wanxiangshu.Runtime.Fallback.LeaseTransitions

/// Lease lifecycle transitions over the fallback runtime store: pending
/// continuation/nudge leases, status CAS, cancel/episode reset. All mutations
/// route through the store's UpdateSession aggregate surface.

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Kernel.FallbackRuntimeLifecycle
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.RuntimeStore

type FallbackRuntimeStore with

    member this.UpdateState (sessionID: string) (state: SessionFallbackState) : unit =
        if state.Lifecycle = FallbackLifecycle.Cancelled then
            this.CancelEpisode sessionID

        this.UpdateSession(sessionID, (fun s -> { s with Core = state }))
        this.TriggerStateChanged sessionID

    member this.SetPendingLease(sessionID: string, lease: PendingLease) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with PendingLease = Some lease }))

    member this.TryGetPendingLease(sessionID: string) : PendingLease option =
        (this.GetSession sessionID).PendingLease

    member this.TryClearPendingLease(sessionID: string, continuationID: string) : bool =
        match (this.GetSession sessionID).PendingLease with
        | Some lease when lease.ContinuationID = continuationID ->
            this.UpdateSession(sessionID, (fun s -> { s with PendingLease = None }))
            true
        | _ -> false

    member this.ClearPendingLease(sessionID: string) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with PendingLease = None }))

    member this.TryTransitionPendingLease
        (sessionID: string, expectedID: string, expectedStatus: LeaseStatus, nextStatus: LeaseStatus)
        : bool =
        let s = this.GetSession sessionID

        match s.PendingLease with
        | Some lease ->
            let isCurrent =
                lease.ContinuationID = expectedID
                && lease.Status = expectedStatus
                && lease.SessionGeneration = s.SessionGeneration
                && lease.HumanTurnID = s.HumanTurnId
                && lease.CancelGeneration = s.CancelGeneration
                && lease.Owner = SessionOwner.Fallback
                && s.Owner = SessionOwner.Fallback
                && s.Core.Lifecycle = FallbackLifecycle.Active

            if isCurrent then
                this.UpdateSession(
                    sessionID,
                    fun s ->
                        { s with
                            PendingLease = Some { lease with Status = nextStatus } }
                )

                true
            else
                false
        | None -> false

    member this.SetPendingNudgeLease(sessionID: string, lease: NudgeLease) : unit =
        this.UpdateSession(
            sessionID,
            fun s ->
                { s with
                    PendingNudgeLease = Some lease }
        )

    member this.TryGetPendingNudgeLease(sessionID: string) : NudgeLease option =
        (this.GetSession sessionID).PendingNudgeLease

    member this.ClearPendingNudgeLease(sessionID: string) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with PendingNudgeLease = None }))

    member this.TryClearPendingNudgeLease(sessionID: string, expectedNudgeID: string) : bool =
        match (this.GetSession sessionID).PendingNudgeLease with
        | Some lease when lease.NudgeID = expectedNudgeID ->
            this.UpdateSession(sessionID, (fun s -> { s with PendingNudgeLease = None }))
            true
        | _ -> false

    member this.TryTransitionPendingNudgeLease
        (sessionID: string, expectedID: string, expectedStatus: LeaseStatus, nextStatus: LeaseStatus)
        : bool =
        let s = this.GetSession sessionID

        match s.PendingNudgeLease with
        | Some lease ->
            let isCurrent =
                lease.NudgeID = expectedID
                && lease.Status = expectedStatus
                && lease.SessionGeneration = s.SessionGeneration
                && lease.HumanTurnID = s.HumanTurnId
                && lease.CancelGeneration = s.CancelGeneration
                && lease.Owner = SessionOwner.Nudge
                && s.Owner = SessionOwner.Nudge
                && s.Core.Lifecycle = FallbackLifecycle.Active

            if isCurrent then
                this.UpdateSession(
                    sessionID,
                    fun s ->
                        { s with
                            PendingNudgeLease = Some { lease with Status = nextStatus } }
                )

                true
            else
                false
        | None -> false

    member this.ApplyCancelNudgeLease(sessionID: string, expectedNudgeID: string) : bool =
        let s = this.GetSession sessionID

        match s.PendingNudgeLease with
        | Some lease when lease.NudgeID = expectedNudgeID ->
            this.UpdateSession(
                sessionID,
                fun s ->
                    { s with
                        PendingNudgeLease = None
                        ActiveNudgeNonce = ""
                        ActiveGates = Set.remove FallbackSessionGateFlag.NudgeActive s.ActiveGates }
            )

            this.TriggerStateChanged sessionID

            if s.Owner = SessionOwner.Nudge then
                this.UpdateSession(sessionID, (fun s -> { s with Owner = SessionOwner.NoOwner }))

            true
        | _ -> false

    /// The episode is ending — delegate to the unified domain transition which
    /// atomically resets state and clears all gate flags via the record field.
    member this.CancelEpisode(sessionID: string) : unit =
        this.UpdateSession(sessionID, cancelEpisode)
        this.TriggerStateChanged sessionID
