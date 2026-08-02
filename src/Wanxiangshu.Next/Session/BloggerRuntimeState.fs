namespace Wanxiangshu.Next.Session

open Wanxiangshu.Next.Domain

/// ENFORCER-047: single Blogger coordinator state.
///
/// InFlight = provider still working on an uncommitted request.
/// Parked = last cycle committed; waiting for future material (not busy).
[<RequireQualifiedAccess>]
type BloggerRuntimeState =
    | Idle
    | InFlight of BloggerRequestContext
    | Parked
    | Disposed

/// Pure transitions for the coordinator. Host wiring lives elsewhere.
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

    let onMaterial
        (state: BloggerRuntimeState)
        (ctx: BloggerRequestContext)
        : Result<BloggerRuntimeState * Decision, TransitionError> =
        match state with
        | BloggerRuntimeState.Idle -> Ok(BloggerRuntimeState.InFlight ctx, Decision.Start ctx)
        | BloggerRuntimeState.InFlight _ -> Ok(state, Decision.Skip)
        | BloggerRuntimeState.Parked -> Ok(BloggerRuntimeState.InFlight ctx, Decision.Offer ctx)
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed

    let onCycleCommitted (state: BloggerRuntimeState) : Result<BloggerRuntimeState, TransitionError> =
        match state with
        | BloggerRuntimeState.InFlight _ -> Ok BloggerRuntimeState.Parked
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | _ -> Error TransitionError.NotInFlight

    let onSquashCommitted
        (state: BloggerRuntimeState)
        (pendingMain: BloggerRequestContext option)
        : Result<BloggerRuntimeState * Decision, TransitionError> =
        match state with
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | BloggerRuntimeState.InFlight _ ->
            match pendingMain with
            | Some ctx -> Ok(BloggerRuntimeState.InFlight ctx, Decision.Start ctx)
            | None -> Ok(BloggerRuntimeState.Parked, Decision.Ignore)
        | _ -> Error TransitionError.NotInFlight

    let onDispose (_state: BloggerRuntimeState) : BloggerRuntimeState = BloggerRuntimeState.Disposed

    let inFlightContext (state: BloggerRuntimeState) : BloggerRequestContext option =
        match state with
        | BloggerRuntimeState.InFlight ctx -> Some ctx
        | _ -> None

    /// Consume the InFlight context for cycle commit. Missing context = fail closed.
    let tryTakeInFlight
        (state: BloggerRuntimeState)
        : Result<BloggerRequestContext * BloggerRuntimeState, TransitionError> =
        match state with
        | BloggerRuntimeState.InFlight ctx -> Ok(ctx, BloggerRuntimeState.Parked)
        | BloggerRuntimeState.Disposed -> Error TransitionError.Disposed
        | _ -> Error TransitionError.NoContext
