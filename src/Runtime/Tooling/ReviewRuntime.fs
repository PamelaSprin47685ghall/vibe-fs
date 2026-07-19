module Wanxiangshu.Runtime.ReviewRuntime

open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewSession.StateMachine
open Wanxiangshu.Kernel.ReviewSession.Registry
open Wanxiangshu.Kernel.ReviewSession.Effects
open Wanxiangshu.Kernel.ReviewSession.Query
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.ReviewRuntimeState

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
    abstract member CleanupSession: sessionID: string -> unit

let createReviewStore () : ReviewStore =
    let mutable state: ReviewStoreState =
        { Registry = emptyRegistry
          Effects = emptyEffects }

    { new ReviewStore with
        member _.applyReviewTaskProjection(sessionID, task) =
            state <- applyTaskProjection state sessionID task

        member _.clearReviewSessions() = state <- clearAllReviewSessions state

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

        member _.CleanupSession(sessionID) =
            state <- cleanupSessionInState state sessionID

        member _.getPendingReviewIds() =
            Map.keys state.Effects.pendingResolutions |> List.ofSeq

        member _.getActiveSessionIds() =
            state.Registry
            |> Map.filter (fun _ s -> isActive s.state)
            |> Map.keys
            |> List.ofSeq }

let syncReviewProjection (store: ReviewStore) (sessionID: string) (task: string option) : unit =
    store.applyReviewTaskProjection (sessionID, task)
