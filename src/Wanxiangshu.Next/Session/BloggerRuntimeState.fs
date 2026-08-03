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

/// Host-owned cell: state + pending offer. CurrentRequest lives in InFlight payload.
type BloggerRuntimeCell =
    { State: BloggerRuntimeState
      PendingOffer: BloggerRequestContext option }

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
          PendingOffer = None }

    let ofState (state: BloggerRuntimeState) : BloggerRuntimeCell =
        { State = state
          PendingOffer = None }

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
                  PendingOffer = None },
                Decision.Start ctx
            )
        | BloggerRuntimeState.InFlight _ -> Ok(cell, Decision.Skip)
        | BloggerRuntimeState.Parked ->
            Ok(
                { State = BloggerRuntimeState.Parked
                  PendingOffer = Some ctx },
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
                  PendingOffer = None }

    let onCycleCommitted (cell: BloggerRuntimeCell) : Result<BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.InFlight _ ->
            Ok
                { State = BloggerRuntimeState.Parked
                  PendingOffer = cell.PendingOffer }
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
                      PendingOffer = None },
                    Decision.Start ctx
                )
            | None ->
                Ok(
                    { State = BloggerRuntimeState.Parked
                      PendingOffer = None },
                    Decision.Ignore
                )
        | _ -> Error TransitionError.NotInFlight

    let onFail (cell: BloggerRuntimeCell) : Result<BloggerRuntimeCell, TransitionError> =
        match cell.State with
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | BloggerRuntimeState.InFlight _ ->
            Ok
                { State = BloggerRuntimeState.Idle
                  PendingOffer = None }
        | _ -> Error TransitionError.NotInFlight

    let onDispose (_cell: BloggerRuntimeCell) : BloggerRuntimeCell =
        { State = BloggerRuntimeState.Disposed
          PendingOffer = None }

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
                  PendingOffer = cell.PendingOffer }
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
                  PendingOffer = None }
