module Wanxiangshu.Runtime.Session.SessionActorState

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Session.SessionFact

/// Authoritative mailbox epoch for one physical session.
type SessionActorSnapshot =
    { Generation: int
      Closed: bool
      Owner: SessionOwner
      ActiveDispatchId: string option
      AcceptedCount: int
      DroppedCount: int }

module SessionActorSnapshot =
    let empty: SessionActorSnapshot =
        { Generation = 0
          Closed = false
          Owner = SessionOwner.NoOwner
          ActiveDispatchId = None
          AcceptedCount = 0
          DroppedCount = 0 }

/// Pure admission decision for a fact against the current epoch.
[<RequireQualifiedAccess>]
type FactAdmission =
    | Accept
    | DropClosed
    | DropStaleGeneration
    | DropOwnerMismatch
    | DropDispatchMismatch

module FactAdmission =
    let decide (snap: SessionActorSnapshot) (fact: SessionFact) : FactAdmission =
        match fact with
        | SessionFact.SessionClosed -> FactAdmission.Accept
        | _ when snap.Closed -> FactAdmission.DropClosed
        | _ ->
            match SessionFact.tryEffectIdentity fact with
            | None -> FactAdmission.Accept
            | Some identity ->
                if identity.ExpectedGeneration <> snap.Generation then
                    FactAdmission.DropStaleGeneration
                else
                    match identity.ExpectedOwner with
                    | Some expected when expected <> snap.Owner -> FactAdmission.DropOwnerMismatch
                    | _ ->
                        match identity.ExpectedDispatchId, snap.ActiveDispatchId with
                        | Some expected, Some active when expected = active -> FactAdmission.Accept
                        | Some _, _ -> FactAdmission.DropDispatchMismatch
                        | None, _ -> FactAdmission.Accept

    let isAccepted (decision: FactAdmission) : bool =
        match decision with
        | FactAdmission.Accept -> true
        | _ -> false

module SessionActorTransition =
    let bumpGeneration (snap: SessionActorSnapshot) : SessionActorSnapshot =
        { snap with
            Generation = snap.Generation + 1
            ActiveDispatchId = None }

    let markClosed (snap: SessionActorSnapshot) : SessionActorSnapshot =
        { snap with
            Closed = true
            ActiveDispatchId = None
            Owner = SessionOwner.NoOwner
            Generation = snap.Generation + 1 }

    let setOwner (owner: SessionOwner) (snap: SessionActorSnapshot) : SessionActorSnapshot = { snap with Owner = owner }

    let setActiveDispatch (dispatchId: string option) (snap: SessionActorSnapshot) : SessionActorSnapshot =
        { snap with
            ActiveDispatchId = dispatchId }

    let recordAccepted (snap: SessionActorSnapshot) : SessionActorSnapshot =
        { snap with
            AcceptedCount = snap.AcceptedCount + 1 }

    let recordDropped (snap: SessionActorSnapshot) : SessionActorSnapshot =
        { snap with
            DroppedCount = snap.DroppedCount + 1 }

    /// Apply fact-local epoch bookkeeping before the domain handler runs.
    let applyFactEpoch (fact: SessionFact) (snap: SessionActorSnapshot) : SessionActorSnapshot =
        match fact with
        | SessionFact.SessionClosed -> markClosed snap
        | SessionFact.HumanTurnObserved _ ->
            { snap with
                Owner = SessionOwner.Human
                Generation = snap.Generation + 1
                ActiveDispatchId = None }
        | _ -> snap
