module Wanxiangshu.Runtime.ReviewRuntime

open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Runtime.Clock

/// The full host-facing review store: pure registry kernel plus effect side-table.
type ReviewStore =
    abstract member applyReviewTaskProjection: sessionID: string * task: string option -> unit
    abstract member clearReviewSessions: unit -> unit
    abstract member tryLockReview: sessionID: string -> bool
    abstract member unlockReview: sessionID: string -> unit
    abstract member setPendingReview: sessionID: string * resolve: (ReviewResult -> unit) -> unit
    abstract member setAbortSuppressor: sessionID: string * suppress: (unit -> unit) -> unit
    abstract member getPendingReviewIds: unit -> string list
    abstract member getActiveSessionIds: unit -> string list
    abstract member resolvePendingReview: sessionID: string * result: ReviewResult -> bool
    abstract member getReviewTask: sessionID: string -> string option
    abstract member getReviewState: sessionID: string -> ReviewState option
    abstract member addChild: parentID: string * childID: string -> unit

/// Single atomic state cell: the pure registry projection plus the effect
/// side-table fold together so every store method is one `state <- { ... }`
/// transition, eliminating the prior split `mutable registry`/`mutable effects`
/// pair that could interleave mid-update.
type private ReviewStoreState =
    { Registry: Registry
      Effects: SessionEffects }

let private computeAllDescendantIds (state: ReviewStoreState) (sessionId: string) : string list =
    let rec collect id =
        match Map.tryFind id state.Registry with
        | None -> [ id ]
        | Some session -> id :: (session.childIds |> List.collect collect)

    collect sessionId

let private applyTaskProjection (state: ReviewStoreState) (sessionID: string) (task: string option) : ReviewStoreState =
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

let private resolvePendingReview
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

let private clearAllReviewSessions (state: ReviewStoreState) : ReviewStoreState =
    let nextEffects =
        disposeSessionTree state.Effects (Map.keys state.Effects.pendingResolutions |> List.ofSeq)

    { state with
        Effects = nextEffects
        Registry = emptyRegistry }

let private canLockReview (state: ReviewStoreState) (sessionID: string) : bool =
    canTransition state.Registry sessionID (ReviewCommand.Lock sessionID)

let private setAbortSuppressorInState
    (state: ReviewStoreState)
    (sessionID: string)
    (suppress: unit -> unit)
    : ReviewStoreState =
    { state with
        Effects =
            { state.Effects with
                abortSuppressors = Map.add sessionID suppress state.Effects.abortSuppressors } }

let private unlockReviewInState (state: ReviewStoreState) (sessionID: string) : ReviewStoreState =
    { state with
        Registry = reduce state.Registry (RegistryAction.Unlock sessionID) }

let private setPendingInState
    (state: ReviewStoreState)
    (sessionID: string)
    (resolve: ReviewResult -> unit)
    : ReviewStoreState =
    { state with
        Effects = setPending state.Effects sessionID resolve }

let private addChildInState (state: ReviewStoreState) (parentID: string) (childID: string) : ReviewStoreState =
    { state with
        Registry = reduce state.Registry (RegistryAction.AddChild(parentID, childID)) }

let private tryLockReviewInState (state: ReviewStoreState) (sessionID: string) : ReviewStoreState option =
    if not (canLockReview state sessionID) then
        None
    else
        Some
            { state with
                Registry = reduce state.Registry (RegistryAction.Lock(sessionID, sessionID)) }

let createReviewStore () : ReviewStore =
    let mutable state: ReviewStoreState =
        { Registry = emptyRegistry
          Effects = emptyEffects }

    let applyTaskProjection sessionID task =
        state <- applyTaskProjection state sessionID task

    let clearReviewSessions () = state <- clearAllReviewSessions state

    { new ReviewStore with
        member _.applyReviewTaskProjection(sessionID, task) = applyTaskProjection sessionID task

        member _.clearReviewSessions() = clearReviewSessions ()

        member _.tryLockReview(sessionID) =
            match tryLockReviewInState state sessionID with
            | Some next ->
                state <- next
                true
            | None -> false

        member _.unlockReview(sessionID) =
            state <- unlockReviewInState state sessionID

        member _.setPendingReview(sessionID, resolve) =
            state <- setPendingInState state sessionID resolve

        member _.setAbortSuppressor(sessionID, suppress) =
            state <- setAbortSuppressorInState state sessionID suppress

        member _.resolvePendingReview(sessionID, result) =
            let nextState, fired = resolvePendingReview state sessionID result
            state <- nextState
            fired

        member _.getReviewTask(sessionID) = taskOf state.Registry sessionID
        member _.getReviewState(sessionID) = stateOf state.Registry sessionID

        member _.addChild(parentID, childID) =
            state <- addChildInState state parentID childID

        member _.getPendingReviewIds() =
            Map.keys state.Effects.pendingResolutions |> List.ofSeq

        member _.getActiveSessionIds() =
            state.Registry
            |> Map.filter (fun _ s -> Wanxiangshu.Kernel.ReviewSession.StateMachine.isActive s.state)
            |> Map.keys
            |> List.ofSeq }

let syncReviewProjection (store: ReviewStore) (sessionID: string) (task: string option) : unit =
    store.applyReviewTaskProjection (sessionID, task)
