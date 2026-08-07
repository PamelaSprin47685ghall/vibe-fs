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

/// ENFORCER-047: Blogger coordinator cell (PARTIAL shadow — PR7 knife 1–2).
///
/// Physical busy ownership is the host flight registry (`IParkedTransformHost.HasFlight`):
/// dictionary entry present = a single-flight request owns the session.
/// `InFlight ctx` / `Idle` remain as dual-write shadow so transition APIs and tests
/// keep compiling; production busy checks must prefer HasFlight, not cell.State.
/// Whether a parked transform waits for the next material is the host dictionary's
/// physical fact (`IParkedTransformHost.HasParked`), passed into `onMaterial`
/// explicitly — the cell carries no mirror of it.
/// There is deliberately NO Sealed case: handle seal is a durable journal fact
/// (AgentProjection.mainSealedForBlogger), read on every decision — a sealed cell
/// mirror would only ever duplicate it and drift stale on reactivation.
/// The next-Main-material slot lives in the host dictionary (ENFORCER-050), not on
/// the cell — the cell never carries a PendingOffer mirror.
/// TODO (later knife): delete this DU once all busy reads use flight ownership and
/// Task finally removes the flight without onCycleCommitted/onFail transitions.
[<RequireQualifiedAccess>]
type BloggerRuntimeState =
    | Idle
    | InFlight of BloggerRequestContext

/// One drain-window opening. Module-private constructor: only the reactivation
/// path (a new Authority Root arriving on the main) can mint it, so no caller
/// can forge an open window for an arbitrary root.
type DrainPermit = private DrainPermit of AuthorityRootUserMessageId

/// After a durable handle seal, whether a new Authority Root reopened a drain
/// window. The handle lifecycle NEVER unseals (CompletedAwaitingJoin/Abandoned/
/// Retired stay sealed), so a reactivation can only be observed in-process — the
/// window carries the root that opened it (as an unforgeable permit). Closed =
/// seal blocks new Y work.
[<RequireQualifiedAccess>]
type DrainWindow =
    | Closed
    | Open of DrainPermit

/// Host-owned cell: State (shadow) + drain window.
/// CurrentRequest authority = flight ownership on the host; State.InFlight is
/// dual-written for transition-cell compat. PendingOffer is host dictionary
/// (ENFORCER-050). Recovery is derived, never stored (ENFORCER-153).
type BloggerRuntimeCell =
    {
        /// Shadow of flight presence for transition APIs; not the busy authority.
        State: BloggerRuntimeState
        /// Durable handle sealed + new Authority Root may open one drain window.
        Drain: DrainWindow
    }

[<RequireQualifiedAccess>]
module BloggerRuntime =

    type TransitionError =
        | AlreadyInFlight
        | NotInFlight
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
    /// `hasParkedWaiter` is the host dictionary's physical fact: when a
    /// ParkedTransform waits for this material, Decision.Offer only — the
    /// dictionary stages the offer (ENFORCER-050 physical slot). Handle seal is
    /// the durable journal check at every entry, not a cell case.
    let onMaterial
        (hasParkedWaiter: bool)
        (cell: BloggerRuntimeCell)
        (ctx: BloggerRequestContext)
        : Result<BloggerRuntimeCell * Decision, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.Idle when hasParkedWaiter -> Ok(cell, Decision.Offer ctx)
        | BloggerRuntimeState.Idle ->
            Ok(
                { cell with
                    State = BloggerRuntimeState.InFlight ctx },
                Decision.Start ctx
            )
        | BloggerRuntimeState.InFlight _ -> Ok(cell, Decision.Skip)

    let onCycleCommitted (cell: BloggerRuntimeCell) : Result<BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.InFlight _ ->
            Ok
                { cell with
                    State = BloggerRuntimeState.Idle }
        | _ -> Error TransitionError.NotInFlight

    let onSquashCommitted
        (cell: BloggerRuntimeCell)
        (pendingMain: BloggerRequestContext option)
        : Result<BloggerRuntimeCell * Decision, TransitionError> =
        match cell.State with
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
                        State = BloggerRuntimeState.Idle },
                    Decision.Ignore
                )
        | _ -> Error TransitionError.NotInFlight

    /// Final fail of the logical request: Idle.
    let onFail (cell: BloggerRuntimeCell) : Result<BloggerRuntimeCell, TransitionError> =
        match cell.State with
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
    /// seal flag to flip — only the drain window reopens. State stays as-is:
    /// the next material decides Start-vs-Offer from the host's parked-waiter
    /// fact, never from a demoted cell mirror.
    let onReactivate (cell: BloggerRuntimeCell) (root: AuthorityRootUserMessageId) : BloggerRuntimeCell =
        match cell.State with
        | BloggerRuntimeState.InFlight _
        | BloggerRuntimeState.Idle ->
            { cell with
                Drain = DrainWindow.Open(DrainPermit root) }

    let inFlightContext (cell: BloggerRuntimeCell) : BloggerRequestContext option =
        match cell.State with
        | BloggerRuntimeState.InFlight ctx -> Some ctx
        | _ -> None

    let isDrainOpen (cell: BloggerRuntimeCell) : bool =
        match cell.Drain with
        | DrainWindow.Open _ -> true
        | DrainWindow.Closed -> false

    /// Durable handle seal blocks new work unless the drain window is open.
    /// `durableHandleSealed` is the journal truth (AgentProjection.mainSealedForBlogger);
    /// the cell carries no sealed mirror, so it can never drift stale.
    let blocksNewRequest (durableHandleSealed: bool) (cell: BloggerRuntimeCell) : bool =
        match cell.State with
        | BloggerRuntimeState.InFlight _ -> false
        | BloggerRuntimeState.Idle -> durableHandleSealed && not (isDrainOpen cell)

    let adoptPendingAsCurrent
        (cell: BloggerRuntimeCell)
        (ctx: BloggerRequestContext)
        : Result<BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.InFlight _ -> Error TransitionError.AlreadyInFlight
        | BloggerRuntimeState.Idle ->
            Ok
                { cell with
                    State = BloggerRuntimeState.InFlight ctx }
