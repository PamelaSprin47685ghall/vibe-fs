module Wanxiangshu.Runtime.ReviewRuntimeState

open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewSession.StateMachine
open Wanxiangshu.Kernel.ReviewSession.Registry
open Wanxiangshu.Kernel.ReviewSession.Effects
open Wanxiangshu.Kernel.ReviewSession.Query
open Wanxiangshu.Runtime.Clock

/// Single atomic state cell: the pure registry projection plus the effect
/// side-table fold together so every store method is one `state <- { ... }`
/// transition, eliminating the prior split `mutable registry`/`mutable effects`
/// pair that could interleave mid-update.
type ReviewStoreState =
    { Registry: Registry
      Effects: SessionEffects }

let computeAllDescendantIds (state: ReviewStoreState) (sessionId: string) : string list =
    let rec collect id =
        match Map.tryFind id state.Registry with
        | None -> [ id ]
        | Some session -> id :: (session.childIds |> List.collect collect)

    collect sessionId

let applyTaskProjection (state: ReviewStoreState) (sessionID: string) (task: string option) : ReviewStoreState =
    if sessionID = "" then
        state
    else
        let normalizedTask =
            task
            |> Option.bind (fun value ->
                if System.String.IsNullOrWhiteSpace value then
                    None
                else
                    Some value)

        match normalizedTask with
        | Some nextTask when
            taskOf state.Registry sessionID <> Some nextTask
            || stateOf state.Registry sessionID |> Option.isNone
            ->
            let afterDispose =
                if stateOf state.Registry sessionID |> Option.isSome then
                    let nextEffects = disposeSessionTree state.Effects [ sessionID ]

                    { state with
                        Effects = nextEffects
                        Registry = reduce state.Registry (RegistryAction.Deactivate sessionID) }
                else
                    state

            { afterDispose with
                Registry =
                    reduce afterDispose.Registry (RegistryAction.Activate(sessionID, nextTask, getTimestampMs ())) }
        | None when stateOf state.Registry sessionID |> Option.isSome ->
            let nextEffects = disposeSessionTree state.Effects [ sessionID ]

            { state with
                Effects = nextEffects
                Registry = reduce state.Registry (RegistryAction.Deactivate sessionID) }
        | _ -> state

let resolvePendingReview
    (state: ReviewStoreState)
    (sessionID: string)
    (result: ReviewResult)
    : ReviewStoreState * bool =
    let targetID =
        if Map.containsKey sessionID state.Effects.pendingResolutions then
            sessionID
        else
            let descendants = computeAllDescendantIds state sessionID

            descendants
            |> List.tryFind (fun id -> Map.containsKey id state.Effects.pendingResolutions)
            |> Option.defaultValue sessionID

    let nextRegistry = reduce state.Registry (actionFor targetID result)
    let nextEffects, fired = resolvePending state.Effects targetID result

    ({ state with
        Registry = nextRegistry
        Effects = nextEffects },
     fired)

let clearAllReviewSessions (state: ReviewStoreState) : ReviewStoreState =
    let nextEffects =
        disposeSessionTree state.Effects (Map.keys state.Effects.pendingResolutions |> List.ofSeq)

    { state with
        Effects = nextEffects
        Registry = emptyRegistry }

let cleanupSessionInState (state: ReviewStoreState) (sessionID: string) : ReviewStoreState =
    if sessionID = "" then
        state
    else
        let ids = computeAllDescendantIds state sessionID
        let nextEffects = disposeSessionTree state.Effects ids

        let nextRegistry =
            ids
            |> List.fold (fun reg id -> reduce reg (RegistryAction.Deactivate id)) state.Registry

        { Registry = nextRegistry
          Effects = nextEffects }

let canLockReview (state: ReviewStoreState) (sessionID: string) : bool =
    canTransition state.Registry sessionID (ReviewCommand.Lock sessionID)

let setAbortSuppressorInState
    (state: ReviewStoreState)
    (sessionID: string)
    (suppress: unit -> unit)
    : ReviewStoreState =
    { state with
        Effects =
            { state.Effects with
                abortSuppressors = Map.add sessionID suppress state.Effects.abortSuppressors } }

let unlockReviewInState (state: ReviewStoreState) (sessionID: string) : ReviewStoreState =
    { state with
        Registry = reduce state.Registry (RegistryAction.Unlock sessionID) }

let setPendingInState (state: ReviewStoreState) (sessionID: string) (resolve: ReviewResult -> unit) : ReviewStoreState =
    { state with
        Effects = setPending state.Effects sessionID resolve }

let addChildInState (state: ReviewStoreState) (parentID: string) (childID: string) : ReviewStoreState =
    { state with
        Registry = reduce state.Registry (RegistryAction.AddChild(parentID, childID)) }

let tryLockReviewInState (state: ReviewStoreState) (sessionID: string) : ReviewStoreState option =
    if not (canLockReview state sessionID) then
        None
    else
        Some
            { state with
                Registry = reduce state.Registry (RegistryAction.Lock(sessionID, sessionID)) }
