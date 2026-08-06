namespace Wanxiangshu.Session

open Wanxiangshu.Domain
open Wanxiangshu.Kernel
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
/// The next-Main-material slot lives in the host dictionary (ENFORCER-050), not on
/// the cell — the cell never carries a PendingOffer mirror.
[<RequireQualifiedAccess>]
type BloggerRuntimeState =
    | Idle
    | InFlight of BloggerRequestContext
    | Parked
    | Sealed
    | Disposed

/// After durable handle seal, whether a new Authority Root reopened a drain window.
/// Closed = seal blocks new Y work; Open = drain until next seal/catch-up.
[<RequireQualifiedAccess>]
type DrainWindow =
    | Closed
    | Open

/// Host-owned cell: state + drain window. CurrentRequest lives in the InFlight
/// payload; the next-Main-material slot is the host dictionary (ENFORCER-050).
/// Recovery is derived, never stored (ENFORCER-153).
type BloggerRuntimeCell =
    {
        State: BloggerRuntimeState
        /// Durable handle sealed + new Authority Root may open one drain window.
        Drain: DrainWindow
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

    type Decision =
        | Start of BloggerRequestContext
        | Skip
        | Offer of BloggerRequestContext
        | Ignore

    let empty: BloggerRuntimeCell =
        { State = BloggerRuntimeState.Idle
          Drain = DrainWindow.Closed }

    let ofState (state: BloggerRuntimeState) : BloggerRuntimeCell =
        { State = state
          Drain = DrainWindow.Closed }

    let private withState (cell: BloggerRuntimeCell) (state: BloggerRuntimeState) : BloggerRuntimeCell =
        { cell with State = state }

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
                    State = BloggerRuntimeState.InFlight ctx },
                Decision.Start ctx
            )
        | BloggerRuntimeState.InFlight _ -> Ok(cell, Decision.Skip)
        | BloggerRuntimeState.Parked ->
            // The host dictionary stages the offer (ENFORCER-050 physical slot).
            Ok(cell, Decision.Offer ctx)
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
                    State = BloggerRuntimeState.InFlight ctx }

    let onCycleCommitted (cell: BloggerRuntimeCell) : Result<BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.InFlight _ ->
            Ok
                { cell with
                    State = BloggerRuntimeState.Parked }
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
                        State = BloggerRuntimeState.InFlight ctx },
                    Decision.Start ctx
                )
            | None ->
                Ok(
                    { cell with
                        State = BloggerRuntimeState.Parked },
                    Decision.Ignore
                )
        | _ -> Error TransitionError.NotInFlight

    /// Final fail of the logical request: Idle.
    let onFail (cell: BloggerRuntimeCell) : Result<BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | BloggerRuntimeState.Sealed -> Ok cell
        | BloggerRuntimeState.InFlight _ ->
            Ok
                { cell with
                    State = BloggerRuntimeState.Idle }
        | _ -> Error TransitionError.NotInFlight

    /// Fork-child handle became joinable/retired: stop new Y requests.
    let onSeal (cell: BloggerRuntimeCell) : BloggerRuntimeCell =
        match cell.State with
        | BloggerRuntimeState.Disposed -> cell
        | BloggerRuntimeState.InFlight _ ->
            // Let the in-flight cycle finish; seal after commit via coordinator re-check.
            // Mark not reactivated so post-commit paths see the durable seal.
            { cell with Drain = DrainWindow.Closed }
        | _ ->
            { State = BloggerRuntimeState.Sealed
              Drain = DrainWindow.Closed }

    /// Force sealed (no in-flight preserve). Used when dropping offers after join.
    let forceSeal (_cell: BloggerRuntimeCell) : BloggerRuntimeCell =
        { State = BloggerRuntimeState.Sealed
          Drain = DrainWindow.Closed }

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
              Drain = DrainWindow.Open }
        | BloggerRuntimeState.InFlight _
        | BloggerRuntimeState.Idle
        | BloggerRuntimeState.Parked -> { cell with Drain = DrainWindow.Open }

    let onDispose (_cell: BloggerRuntimeCell) : BloggerRuntimeCell =
        { State = BloggerRuntimeState.Disposed
          Drain = DrainWindow.Closed }

    let inFlightContext (cell: BloggerRuntimeCell) : BloggerRequestContext option =
        match cell.State with
        | BloggerRuntimeState.InFlight ctx -> Some ctx
        | _ -> None

    let isSealed (cell: BloggerRuntimeCell) : bool =
        match cell.State with
        | BloggerRuntimeState.Sealed -> true
        | _ -> false

    let isDrainOpen (cell: BloggerRuntimeCell) : bool =
        match cell.Drain with
        | DrainWindow.Open -> true
        | DrainWindow.Closed -> false

    /// Durable handle seal blocks new work unless DrainWindow.Open.
    let blocksNewRequest (durableHandleSealed: bool) (cell: BloggerRuntimeCell) : bool =
        match cell.State with
        | BloggerRuntimeState.Disposed
        | BloggerRuntimeState.Sealed -> true
        | BloggerRuntimeState.InFlight _ -> false
        | BloggerRuntimeState.Idle
        | BloggerRuntimeState.Parked -> durableHandleSealed && not (isDrainOpen cell)

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
                    State = BloggerRuntimeState.Parked }
            )
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | BloggerRuntimeState.Sealed -> Error TransitionError.Sealed
        | _ -> Error TransitionError.NoContext

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
                    State = BloggerRuntimeState.InFlight ctx }
