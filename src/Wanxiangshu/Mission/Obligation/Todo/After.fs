namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection

open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open Wanxiangshu.Foundation.Identity

/// Pure after-hook / recovery orchestration (HOST-021 / TODO-006).
/// Host membrane calls these after live after or RecoveredCompletedToolPart
/// proof; Journal append and dedicated reviewer runtime stay at the call site.
module MagicTodoAfter =

    /// How to deliver one process-review assignment (HOST-021 / TODO-006).
    ///
    /// `deferSend` Fork installs a pending run before any Authority Root exists.
    /// A second Fork would take sendToExistingChild's busy-nudge path and fail
    /// closed with "Busy nudge requires ActiveLogicalRun" — that is T1 red text.
    ///
    /// Reentry after a claim never re-decides from the XTrace head: whether the
    /// assignment still needs a physical send is answered by PromptAuthority's
    /// durable dispatch evidence (Accepted / Pending / Dispatchable), not by a
    /// head watermark (REVIEW-018).
    [<RequireQualifiedAccess>]
    type AssignmentDelivery =
        /// No Authority Root yet: first prompt is AgentOwnerRoot.
        | OwnerRoot
        /// Later checkpoint: assignment continues the dedicated session.
        | Continuation

    let assignmentDelivery (hasActiveProfile: bool) : AssignmentDelivery =
        if hasActiveProfile then
            AssignmentDelivery.Continuation
        else
            AssignmentDelivery.OwnerRoot

    /// ensureReview plan: Assignment payload when obligation pending.
    let planEnsureReview
        (sha256: string -> string)
        (prepared: TodoWritePrepared)
        (dedicated: DedicatedTodoReviewerEnlisted)
        (reviewWorkStart: XTraceCursor)
        : TodoProcessReviewAssigned =
        let reviewId =
            MagicTodo.todoReviewId sha256 prepared.ManagerLifeId prepared.TodoWriteId

        { ManagerLifeId = prepared.ManagerLifeId
          TodoWriteId = prepared.TodoWriteId
          TodoReviewId = reviewId
          DedicatedReviewerId = dedicated.DedicatedReviewerId
          ReviewerSessionId = dedicated.ReviewerSessionId
          ReviewWorkStartCursor = reviewWorkStart
          ManagerReviewFrontier = prepared.ReviewFrontier }
