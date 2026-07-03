module Wanxiangshu.Shell.ReviewRuntime

open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Shell.Clock

/// The full host-facing review store: pure registry kernel plus effect side-table.
type ReviewStore =
    abstract member activateReview: sessionID: string * task: string * createdAt: int64 -> unit
    abstract member deactivateReview: sessionID: string -> unit
    abstract member clearReviewSessions: unit -> unit
    abstract member tryLockReview: sessionID: string -> bool
    abstract member unlockReview: sessionID: string -> unit
    abstract member setPendingReview: sessionID: string * resolve: (ReviewResult -> unit) -> unit
    abstract member setAbortSuppressor: sessionID: string * suppress: (unit -> unit) -> unit
    abstract member resolvePendingReview: sessionID: string * result: ReviewResult -> bool
    abstract member getReviewTask: sessionID: string -> string option
    abstract member getReviewState: sessionID: string -> ReviewState option
    abstract member addChild: parentID: string * childID: string -> unit
    abstract member hasSynced: sessionID: string -> bool
    abstract member markSynced: sessionID: string -> unit

/// Single atomic state cell: the pure registry projection plus the effect
/// side-table fold together so every store method is one `state <- { ... }`
/// transition, eliminating the prior split `mutable registry`/`mutable effects`
/// pair that could interleave mid-update.
type private ReviewStoreState =
    { Registry: Registry
      Effects: SessionEffects }

let createReviewStore () : ReviewStore =
    let mutable state : ReviewStoreState = { Registry = emptyRegistry; Effects = emptyEffects }
    let mutable syncedSessions = Set.empty<string>

    let allDescendantIds sessionId =
        let rec collect id =
            match Map.tryFind id state.Registry with
            | None -> [ id ]
            | Some session -> id :: (session.childIds |> List.collect collect)
        collect sessionId

    { new ReviewStore with
        member _.activateReview(sessionID, task, createdAt) =
            state <- { state with Registry = reduce state.Registry (RegistryAction.Activate(sessionID, task, createdAt)) }
        member _.deactivateReview(sessionID) =
            let nextEffects = disposeSessionTree state.Effects (allDescendantIds sessionID)
            state <- { state with Effects = nextEffects; Registry = reduce state.Registry (RegistryAction.Deactivate sessionID) }
        member _.clearReviewSessions() =
            let nextEffects = disposeSessionTree state.Effects (Map.keys state.Effects.pendingResolutions |> List.ofSeq)
            state <- { state with Effects = nextEffects; Registry = emptyRegistry }
        member _.tryLockReview(sessionID) =
            if not (canTransition state.Registry sessionID (ReviewCommand.Lock sessionID)) then false
            else
                state <- { state with Registry = reduce state.Registry (RegistryAction.Lock(sessionID, sessionID)) }
                true
        member _.unlockReview(sessionID) =
            state <- { state with Registry = reduce state.Registry (RegistryAction.Unlock sessionID) }
        member _.setPendingReview(sessionID, resolve) =
            state <- { state with Effects = setPending state.Effects sessionID resolve }
        member _.setAbortSuppressor(sessionID, suppress) =
            state <- { state with Effects = { state.Effects with abortSuppressors = Map.add sessionID suppress state.Effects.abortSuppressors } }
        member _.resolvePendingReview(sessionID, result) =
            let nextRegistry = reduce state.Registry (actionFor sessionID result)
            let nextEffects, fired = resolvePending state.Effects sessionID result
            state <- { state with Registry = nextRegistry; Effects = nextEffects }
            fired
        member _.getReviewTask(sessionID) = taskOf state.Registry sessionID
        member _.getReviewState(sessionID) = stateOf state.Registry sessionID
        member _.addChild(parentID, childID) =
            state <- { state with Registry = reduce state.Registry (RegistryAction.AddChild(parentID, childID)) }
        member _.hasSynced(sessionID) = Set.contains sessionID syncedSessions
        member _.markSynced(sessionID) = syncedSessions <- Set.add sessionID syncedSessions }

let syncReviewProjection (store: ReviewStore) (sessionID: string) (task: string option) : unit =
    if sessionID = "" then ()
    else
        match task with
        | Some nextTask ->
            if store.getReviewTask sessionID <> Some nextTask || store.getReviewState sessionID |> Option.isNone then
                if store.getReviewState sessionID |> Option.isSome then
                    store.deactivateReview sessionID
                store.activateReview(sessionID, nextTask, getTimestampMs())
        | None ->
            if store.getReviewState sessionID |> Option.isSome then
                store.deactivateReview sessionID
