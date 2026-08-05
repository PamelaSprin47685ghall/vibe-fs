namespace Wanxiangshu.Session

open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// ENFORCER-064: Blogger missing-tool recovery (NoRecovery | InteractionNudgeIssued | Aabb).
/// Type name avoids dsl-ownership Stage/Spent suffixes; cases match the clause.
[<RequireQualifiedAccess>]
type BloggerToolRecovery =
    | NoRecovery
    | InteractionNudgeIssued of ProviderRunIdentity
    | AabbRepairConsumed

/// ENFORCER-047: single Blogger coordinator state.
///
/// InFlight = provider still working on an uncommitted request (the only busy definition).
/// Parked = last cycle committed; waiting for future material (not busy).
/// Sealed = fork-child main handle is joinable/retired AND not reactivated by a new root.
/// PendingOffer is NOT part of the state tag — it is a separate physical slot on the cell.
[<RequireQualifiedAccess>]
type BloggerRuntimeState =
    | Idle
    | InFlight of BloggerRequestContext
    | Parked
    | Sealed
    | Disposed

/// Host-owned cell: state + pending offer + ENFORCER-064 recovery.
/// CurrentRequest lives in InFlight payload; Recovery is per logical request.
type BloggerRuntimeCell =
    {
        State: BloggerRuntimeState
        PendingOffer: BloggerRequestContext option
        /// ENFORCER-064 / FALLBACK-008: nudge once then optional AABB per logical request.
        Recovery: BloggerToolRecovery
        /// Handle is CompletedAwaitingJoin/Retired but main received a new Authority Root:
        /// Blogger may drain until catch-up; host forceSeals when durable sealed and no material.
        ReactivatedAfterSeal: bool
    }

[<RequireQualifiedAccess>]
module BloggerRuntime =

    type TransitionError =
        | AlreadyInFlight
        | NotInFlight
        | NotParked
        | Disposed
        | Sealed
        | NoContext
        | PendingWhileInFlight

    type Decision =
        | Start of BloggerRequestContext
        | Skip
        | Offer of BloggerRequestContext
        | Ignore

    let empty: BloggerRuntimeCell =
        { State = BloggerRuntimeState.Idle
          PendingOffer = None
          Recovery = BloggerToolRecovery.NoRecovery
          ReactivatedAfterSeal = false }

    let ofState (state: BloggerRuntimeState) : BloggerRuntimeCell =
        { State = state
          PendingOffer = None
          Recovery = BloggerToolRecovery.NoRecovery
          ReactivatedAfterSeal = false }

    let private withState (cell: BloggerRuntimeCell) (state: BloggerRuntimeState) : BloggerRuntimeCell =
        { cell with
            State = state
            PendingOffer = None
            Recovery = BloggerToolRecovery.NoRecovery }

    /// Main-session material arrived. InFlight never queues (XTrace keeps backlog).
    /// Parked: Decision.Offer only — physical PendingOffer is the host dictionary.
    /// Sealed ignores all material until onReactivate.
    let onMaterial
        (cell: BloggerRuntimeCell)
        (ctx: BloggerRequestContext)
        : Result<BloggerRuntimeCell * Decision, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.Idle ->
            Ok(
                { cell with
                    State = BloggerRuntimeState.InFlight ctx
                    PendingOffer = None
                    Recovery = BloggerToolRecovery.NoRecovery },
                Decision.Start ctx
            )
        | BloggerRuntimeState.InFlight _ -> Ok(cell, Decision.Skip)
        | BloggerRuntimeState.Parked ->
            // Host dictionary is sole PendingOffer authority (ENFORCER-050 physical slot).
            Ok(
                { cell with
                    State = BloggerRuntimeState.Parked
                    PendingOffer = None
                    Recovery = BloggerToolRecovery.NoRecovery },
                Decision.Offer ctx
            )
        | BloggerRuntimeState.Sealed -> Ok(cell, Decision.Ignore)
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed

    /// Physical send is about to leave: freeze CurrentRequest (InFlight payload).
    let beginRequest
        (cell: BloggerRuntimeCell)
        (ctx: BloggerRequestContext)
        : Result<BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | BloggerRuntimeState.Sealed -> Error TransitionError.Sealed
        | BloggerRuntimeState.InFlight _ -> Error TransitionError.AlreadyInFlight
        | BloggerRuntimeState.Idle
        | BloggerRuntimeState.Parked ->
            Ok
                { cell with
                    State = BloggerRuntimeState.InFlight ctx
                    PendingOffer = None
                    Recovery = BloggerToolRecovery.NoRecovery }

    let onCycleCommitted (cell: BloggerRuntimeCell) : Result<BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.InFlight _ ->
            Ok
                { cell with
                    State = BloggerRuntimeState.Parked
                    // keep PendingOffer
                    Recovery = BloggerToolRecovery.NoRecovery }
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | BloggerRuntimeState.Sealed -> Error TransitionError.Sealed
        | _ -> Error TransitionError.NotInFlight

    let onSquashCommitted
        (cell: BloggerRuntimeCell)
        (pendingMain: BloggerRequestContext option)
        : Result<BloggerRuntimeCell * Decision, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | BloggerRuntimeState.Sealed -> Error TransitionError.Sealed
        | BloggerRuntimeState.InFlight _ ->
            match pendingMain with
            | Some ctx ->
                Ok(
                    { cell with
                        State = BloggerRuntimeState.InFlight ctx
                        PendingOffer = None
                        Recovery = BloggerToolRecovery.NoRecovery },
                    Decision.Start ctx
                )
            | None ->
                Ok(
                    { cell with
                        State = BloggerRuntimeState.Parked
                        PendingOffer = None
                        Recovery = BloggerToolRecovery.NoRecovery },
                    Decision.Ignore
                )
        | _ -> Error TransitionError.NotInFlight

    /// Final fail of the logical request: Idle + clear PendingOffer + reset recovery.
    let onFail (cell: BloggerRuntimeCell) : Result<BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | BloggerRuntimeState.Sealed ->
            Ok
                { cell with
                    PendingOffer = None
                    Recovery = BloggerToolRecovery.NoRecovery }
        | BloggerRuntimeState.InFlight _ ->
            Ok
                { cell with
                    State = BloggerRuntimeState.Idle
                    PendingOffer = None
                    Recovery = BloggerToolRecovery.NoRecovery }
        | _ -> Error TransitionError.NotInFlight

    let markInteractionNudgeIssued (cell: BloggerRuntimeCell) (run: ProviderRunIdentity) : BloggerRuntimeCell =
        { cell with
            Recovery = BloggerToolRecovery.InteractionNudgeIssued run }

    let markAabbRepairConsumed (cell: BloggerRuntimeCell) : BloggerRuntimeCell =
        { cell with
            Recovery = BloggerToolRecovery.AabbRepairConsumed }

    /// Fork-child handle became joinable/retired: stop new Y requests.
    let onSeal (cell: BloggerRuntimeCell) : BloggerRuntimeCell =
        match cell.State with
        | BloggerRuntimeState.Disposed -> cell
        | BloggerRuntimeState.InFlight _ ->
            // Let the in-flight cycle finish; seal after commit via coordinator re-check.
            // Mark not reactivated so post-commit paths see the durable seal.
            { cell with
                PendingOffer = None
                ReactivatedAfterSeal = false }
        | _ ->
            { State = BloggerRuntimeState.Sealed
              PendingOffer = None
              Recovery = BloggerToolRecovery.NoRecovery
              ReactivatedAfterSeal = false }

    /// Force sealed (no in-flight preserve). Used when dropping offers after join.
    let forceSeal (_cell: BloggerRuntimeCell) : BloggerRuntimeCell =
        { State = BloggerRuntimeState.Sealed
          PendingOffer = None
          Recovery = BloggerToolRecovery.NoRecovery
          ReactivatedAfterSeal = false }

    /// New Authority Root on this main: Blogger may run again after a handle seal.
    ///
    /// Parked/Idle/InFlight keep their state — only the seal flag flips. Demoting
    /// Parked→Idle made the next material Decision.Start (new prompt_async) while
    /// the Host step loop was still parked on the prior request, so the new send
    /// never reached the provider and the parked waiter never received an Offer.
    /// Sealed alone must reopen to Idle so material can Start after join/return.
    let onReactivate (cell: BloggerRuntimeCell) : BloggerRuntimeCell =
        match cell.State with
        | BloggerRuntimeState.Disposed -> cell
        | BloggerRuntimeState.Sealed ->
            { State = BloggerRuntimeState.Idle
              PendingOffer = None
              Recovery = BloggerToolRecovery.NoRecovery
              ReactivatedAfterSeal = true }
        | BloggerRuntimeState.InFlight _
        | BloggerRuntimeState.Idle
        | BloggerRuntimeState.Parked ->
            { cell with
                ReactivatedAfterSeal = true }

    let onDispose (_cell: BloggerRuntimeCell) : BloggerRuntimeCell =
        { State = BloggerRuntimeState.Disposed
          PendingOffer = None
          Recovery = BloggerToolRecovery.NoRecovery
          ReactivatedAfterSeal = false }

    let inFlightContext (cell: BloggerRuntimeCell) : BloggerRequestContext option =
        match cell.State with
        | BloggerRuntimeState.InFlight ctx -> Some ctx
        | _ -> None

    let isSealed (cell: BloggerRuntimeCell) : bool =
        match cell.State with
        | BloggerRuntimeState.Sealed -> true
        | _ -> false

    /// Durable handle seal blocks new work unless ReactivatedAfterSeal.
    let blocksNewRequest (durableHandleSealed: bool) (cell: BloggerRuntimeCell) : bool =
        match cell.State with
        | BloggerRuntimeState.Disposed
        | BloggerRuntimeState.Sealed -> true
        | BloggerRuntimeState.InFlight _ -> false
        | BloggerRuntimeState.Idle
        | BloggerRuntimeState.Parked -> durableHandleSealed && not cell.ReactivatedAfterSeal

    let tryPeekInFlight (cell: BloggerRuntimeCell) : Result<BloggerRequestContext, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.InFlight ctx -> Ok ctx
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | BloggerRuntimeState.Sealed -> Error TransitionError.Sealed
        | _ -> Error TransitionError.NoContext

    let tryTakeInFlight
        (cell: BloggerRuntimeCell)
        : Result<BloggerRequestContext * BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.InFlight ctx ->
            Ok(
                ctx,
                { cell with
                    State = BloggerRuntimeState.Parked
                    Recovery = BloggerToolRecovery.NoRecovery }
            )
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | BloggerRuntimeState.Sealed -> Error TransitionError.Sealed
        | _ -> Error TransitionError.NoContext

    let tryTakePending
        (cell: BloggerRuntimeCell)
        : Result<BloggerRequestContext option * BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | BloggerRuntimeState.Sealed -> Ok(None, { cell with PendingOffer = None })
        | BloggerRuntimeState.InFlight _ when cell.PendingOffer.IsSome -> Error TransitionError.PendingWhileInFlight
        | _ -> Ok(cell.PendingOffer, { cell with PendingOffer = None })

    let adoptPendingAsCurrent
        (cell: BloggerRuntimeCell)
        (ctx: BloggerRequestContext)
        : Result<BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | BloggerRuntimeState.Sealed -> Error TransitionError.Sealed
        | BloggerRuntimeState.InFlight _ -> Error TransitionError.AlreadyInFlight
        | BloggerRuntimeState.Idle
        | BloggerRuntimeState.Parked ->
            Ok
                { cell with
                    State = BloggerRuntimeState.InFlight ctx
                    PendingOffer = None
                    Recovery = BloggerToolRecovery.NoRecovery }
