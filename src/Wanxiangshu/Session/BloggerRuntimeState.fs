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
/// There is deliberately NO Sealed case: handle seal is a durable journal fact
/// (AgentProjection.mainSealedForBlogger), read on every decision — a sealed cell
/// mirror would only ever duplicate it and drift stale on reactivation.
/// The next-Main-material slot lives in the host dictionary (ENFORCER-050), not on
/// the cell — the cell never carries a PendingOffer mirror.
[<RequireQualifiedAccess>]
type BloggerRuntimeState =
    | Idle
    | InFlight of BloggerRequestContext
    | Parked
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
    /// Handle seal is the durable journal check at every entry, not a cell case.
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
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed

    /// Physical send is about to leave: freeze CurrentRequest (InFlight payload).
    let beginRequest
        (cell: BloggerRuntimeCell)
        (ctx: BloggerRequestContext)
        : Result<BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
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
        | _ -> Error TransitionError.NotInFlight

    let onSquashCommitted
        (cell: BloggerRuntimeCell)
        (pendingMain: BloggerRequestContext option)
        : Result<BloggerRuntimeCell * Decision, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
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
        | BloggerRuntimeState.InFlight _ ->
            Ok
                { cell with
                    State = BloggerRuntimeState.Idle }
        | _ -> Error TransitionError.NotInFlight

    /// Fork-child handle became joinable/retired: stop new Y requests.
    /// Seal is a durable journal fact; forceSeal only closes the in-memory drain
    /// window so the next durable check re-blocks. No Sealed mirror is written —
    /// a cell state could only ever duplicate (and drift from) the journal.
    let forceSeal (cell: BloggerRuntimeCell) : BloggerRuntimeCell =
        { cell with Drain = DrainWindow.Closed }

    /// New Authority Root on this main: Blogger may run again after a handle seal.
    ///
    /// Reactivation is a durable journal fact (the new root), so the cell has no
    /// seal flag to flip — only the drain window reopens. Parked/Idle/InFlight
    /// keep their state: demoting Parked→Idle made the next material
    /// Decision.Start while the Host step loop was still parked on the prior
    /// request, so the new send never reached the provider.
    let onReactivate (cell: BloggerRuntimeCell) : BloggerRuntimeCell =
        match cell.State with
        | BloggerRuntimeState.Disposed -> cell
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

    let isDrainOpen (cell: BloggerRuntimeCell) : bool =
        match cell.Drain with
        | DrainWindow.Open -> true
        | DrainWindow.Closed -> false

    /// Durable handle seal blocks new work unless the drain window is open.
    /// `durableHandleSealed` is the journal truth (AgentProjection.mainSealedForBlogger);
    /// the cell carries no sealed mirror, so it can never drift stale.
    let blocksNewRequest (durableHandleSealed: bool) (cell: BloggerRuntimeCell) : bool =
        match cell.State with
        | BloggerRuntimeState.Disposed -> true
        | BloggerRuntimeState.InFlight _ -> false
        | BloggerRuntimeState.Idle
        | BloggerRuntimeState.Parked -> durableHandleSealed && not (isDrainOpen cell)

    let tryPeekInFlight (cell: BloggerRuntimeCell) : Result<BloggerRequestContext, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.InFlight ctx -> Ok ctx
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
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
        | _ -> Error TransitionError.NoContext

    let adoptPendingAsCurrent
        (cell: BloggerRuntimeCell)
        (ctx: BloggerRequestContext)
        : Result<BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | BloggerRuntimeState.InFlight _ -> Error TransitionError.AlreadyInFlight
        | BloggerRuntimeState.Idle
        | BloggerRuntimeState.Parked ->
            Ok
                { cell with
                    State = BloggerRuntimeState.InFlight ctx }
