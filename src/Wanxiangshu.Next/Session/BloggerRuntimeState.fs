namespace Wanxiangshu.Next.Session

open Wanxiangshu.Next.Domain

/// ENFORCER-047: single Blogger coordinator state.
///
/// InFlight = provider still working on an uncommitted request (the only busy definition).
/// Parked = last cycle committed; waiting for future material (not busy).
/// PendingOffer is NOT part of the state tag — it is a separate physical slot on the cell.
[<RequireQualifiedAccess>]
type BloggerRuntimeState =
    | Idle
    | InFlight of BloggerRequestContext
    | Parked
    | Disposed

/// Host-owned cell: state + pending offer + one-repair budget.
/// CurrentRequest lives in InFlight payload; RepairSpent is per logical request.
type BloggerRuntimeCell =
    { State: BloggerRuntimeState
      PendingOffer: BloggerRequestContext option
      /// FALLBACK-008 / item 15: at most one repair per logical request.
      RepairSpent: bool }

[<RequireQualifiedAccess>]
module BloggerRuntime =

    type TransitionError =
        | AlreadyInFlight
        | NotInFlight
        | NotParked
        | Disposed
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
          RepairSpent = false }

    let ofState (state: BloggerRuntimeState) : BloggerRuntimeCell =
        { State = state
          PendingOffer = None
          RepairSpent = false }

    /// Main-session material arrived. InFlight never writes PendingOffer (XTrace keeps it).
    /// Parked writes PendingOffer and stays Parked until the continuation takes it.
    let onMaterial
        (cell: BloggerRuntimeCell)
        (ctx: BloggerRequestContext)
        : Result<BloggerRuntimeCell * Decision, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.Idle ->
            Ok(
                { State = BloggerRuntimeState.InFlight ctx
                  PendingOffer = None
                  RepairSpent = false },
                Decision.Start ctx
            )
        | BloggerRuntimeState.InFlight _ -> Ok(cell, Decision.Skip)
        | BloggerRuntimeState.Parked ->
            Ok(
                { State = BloggerRuntimeState.Parked
                  PendingOffer = Some ctx
                  RepairSpent = false },
                Decision.Offer ctx
            )
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
                { State = BloggerRuntimeState.InFlight ctx
                  PendingOffer = None
                  RepairSpent = false }

    let onCycleCommitted (cell: BloggerRuntimeCell) : Result<BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.InFlight _ ->
            Ok
                { State = BloggerRuntimeState.Parked
                  PendingOffer = cell.PendingOffer
                  RepairSpent = false }
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
                    { State = BloggerRuntimeState.InFlight ctx
                      PendingOffer = None
                      RepairSpent = false },
                    Decision.Start ctx
                )
            | None ->
                Ok(
                    { State = BloggerRuntimeState.Parked
                      PendingOffer = None
                      RepairSpent = false },
                    Decision.Ignore
                )
        | _ -> Error TransitionError.NotInFlight

    let onFail (cell: BloggerRuntimeCell) : Result<BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | BloggerRuntimeState.InFlight _ ->
            Ok
                { State = BloggerRuntimeState.Idle
                  PendingOffer = None
                  RepairSpent = false }
        | _ -> Error TransitionError.NotInFlight

    let markRepairSpent (cell: BloggerRuntimeCell) : BloggerRuntimeCell =
        { cell with
            RepairSpent = true }

    let onDispose (_cell: BloggerRuntimeCell) : BloggerRuntimeCell =
        { State = BloggerRuntimeState.Disposed
          PendingOffer = None
          RepairSpent = false }

    let inFlightContext (cell: BloggerRuntimeCell) : BloggerRequestContext option =
        match cell.State with
        | BloggerRuntimeState.InFlight ctx -> Some ctx
        | _ -> None

    /// Peek CurrentRequest without leaving InFlight (commit path).
    let tryPeekInFlight (cell: BloggerRuntimeCell) : Result<BloggerRequestContext, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.InFlight ctx -> Ok ctx
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | _ -> Error TransitionError.NoContext

    /// Consume InFlight for a successful cycle commit → Parked. PendingOffer untouched.
    let tryTakeInFlight
        (cell: BloggerRuntimeCell)
        : Result<BloggerRequestContext * BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.InFlight ctx ->
            Ok(
                ctx,
                { State = BloggerRuntimeState.Parked
                  PendingOffer = cell.PendingOffer
                  RepairSpent = false }
            )
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | _ -> Error TransitionError.NoContext

    /// Consume PendingOffer once (parked resume). Fail closed if InFlight.
    let tryTakePending
        (cell: BloggerRuntimeCell)
        : Result<BloggerRequestContext option * BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | BloggerRuntimeState.InFlight _ when cell.PendingOffer.IsSome -> Error TransitionError.PendingWhileInFlight
        | _ ->
            Ok(
                cell.PendingOffer,
                { cell with
                    PendingOffer = None }
            )

    /// After taking a pending offer, move to InFlight as the next CurrentRequest.
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
                { State = BloggerRuntimeState.InFlight ctx
                  PendingOffer = None
                  RepairSpent = false }
