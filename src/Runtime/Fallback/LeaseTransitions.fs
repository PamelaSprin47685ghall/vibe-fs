module Wanxiangshu.Runtime.Fallback.LeaseTransitions

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure

type FallbackRuntimeStore with
    member this.UpdateState (sessionID: string) (state: SessionFallbackState) : unit =
        this.Update(sessionID, setCore state)

    member this.SetPendingLease(sessionID: string, lease: PendingLease) : unit =
        this.UpdateSession(sessionID, setPendingLease lease)

    member this.TryGetPendingLease(sessionID: string) : PendingLease option =
        (this.GetSession sessionID).PendingLease

    member this.TryClearPendingLease(sessionID: string, continuationID: string) : bool =
        let mutable cleared = false

        this.UpdateSession(
            sessionID,
            fun s ->
                match tryClearPendingLease continuationID s with
                | Some s' ->
                    cleared <- true
                    s'
                | None -> s
        )

        cleared

    member this.ClearPendingLease(sessionID: string) : unit =
        this.UpdateSession(sessionID, clearPendingLease)

    member this.TryTransitionPendingLease
        (sessionID: string, expectedID: string, expectedStatus: LeaseStatus, nextStatus: LeaseStatus)
        : bool =
        let mutable transitioned = false

        this.UpdateSession(
            sessionID,
            fun s ->
                match tryTransitionPendingLease expectedID expectedStatus nextStatus s with
                | Some s' ->
                    transitioned <- true
                    s'
                | None -> s
        )

        transitioned

    member this.SetPendingNudgeLease(sessionID: string, lease: NudgeLease) : unit =
        this.UpdateSession(sessionID, setPendingNudgeLease lease)

    member this.TryGetPendingNudgeLease(sessionID: string) : NudgeLease option =
        (this.GetSession sessionID).PendingNudgeLease

    member this.ClearPendingNudgeLease(sessionID: string) : unit =
        this.UpdateSession(sessionID, clearPendingNudgeLease)

    member this.TryClearPendingNudgeLease(sessionID: string, expectedNudgeID: string) : bool =
        let mutable cleared = false

        this.UpdateSession(
            sessionID,
            fun s ->
                match tryClearPendingNudgeLease expectedNudgeID s with
                | Some s' ->
                    cleared <- true
                    s'
                | None -> s
        )

        cleared

    member this.TryTransitionPendingNudgeLease
        (sessionID: string, expectedID: string, expectedStatus: LeaseStatus, nextStatus: LeaseStatus)
        : bool =
        let mutable transitioned = false

        this.UpdateSession(
            sessionID,
            fun s ->
                match tryTransitionPendingNudgeLease expectedID expectedStatus nextStatus s with
                | Some s' ->
                    transitioned <- true
                    s'
                | None -> s
        )

        transitioned

    member this.ApplyCancelNudgeLease(sessionID: string, expectedNudgeID: string) : bool =
        let mutable cancelled = false

        this.UpdateSession(
            sessionID,
            fun s ->
                match applyCancelNudgeLease expectedNudgeID s with
                | Some s' ->
                    cancelled <- true
                    s'
                | None -> s
        )

        if cancelled then
            this.TriggerStateChanged sessionID

        cancelled

    member this.CancelEpisode(sessionID: string) : unit = this.Update(sessionID, cancelEpisode)
