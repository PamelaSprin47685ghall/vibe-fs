module Wanxiangshu.Kernel.Review.ReviewProjection

/// Independent projection for With-Review (loop) state.
///
/// Owner: Review subsystem
/// Input events: loop_activated, loop_cancelled, review_verdict
/// Query: IsLoopActive, ActiveTask, CurrentRound

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.Review.ReviewLoopFold

/// Fold a single event over the review loop state.
/// Replaces the standalone `reviewLoopFolder` in Fold.fs.
let foldReviewLoop (current: ReviewLoopFold) (e: WanEvent) : ReviewLoopFold = ReviewLoopFold.foldEvent current e

/// Derive the active review task string from the review loop state.
let activeTask (loop: ReviewLoopFold) : string option = ReviewLoopFold.activeTask loop

/// Fold a full event stream into a ReviewLoopFold.
let foldReviewLoopStream (sessionId: string) (events: WanEvent list) : ReviewLoopFold =
    events
    |> List.filter (fun e -> e.Session = sessionId)
    |> List.fold foldReviewLoop ReviewLoopFold.initial

/// Fold a full event stream, extracting just the active task.
let foldReviewTask (sessionId: string) (events: WanEvent list) : string option =
    foldReviewLoopStream sessionId events |> activeTask
