namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Domain.MagicTodoFacts
open Wanxiangshu.Domain.MagicTodoSurface
open Wanxiangshu.Kernel.Identity

/// Pure after-hook / recovery orchestration (HOST-021 / TODO-006).
/// Host membrane calls these after live after or RecoveredCompletedToolPart
/// proof; Journal append and dedicated reviewer runtime stay at the call site.
module MagicTodoAfter =

    [<RequireQualifiedAccess>]
    type AcceptReject =
        | PreparedMissing
        | DigestMismatch of field: string
        | NotPhysicallySuccessful

    type AcceptPlan =
        {
            Accepted: TodoWriteAccepted
            /// True when DedicatedTodoReviewerEnlisted must be ensured first.
            NeedsDedicatedEnlist: bool
            /// True when TodoProcessReviewAssigned / reviewer submit must be ensured.
            NeedsEnsureReview: bool
            EnrichedResult: string
            CompatibilityRows: CompatibilityTodoRow list
        }

    /// Build TodoWriteAccepted once Prepared + physical success + digests converge.
    /// `inputDigest` is the provider-input digest frozen at prepare time.
    let planAccept
        (prepared: TodoWritePrepared)
        (physical: PhysicalSuccessEvidence)
        (expectedInputDigest: string)
        (observedInputDigest: string)
        (outputDigest: string)
        (preparedFactRef: EventId)
        (dedicatedExists: bool)
        (concludedExists: bool)
        (enrichedResult: string)
        (submitted: MagicTodoList)
        (reviewingSink: ReviewingSinkStrategy)
        : Result<AcceptPlan, AcceptReject> =
        if expectedInputDigest <> observedInputDigest then
            Error(AcceptReject.DigestMismatch "InputDigest")
        else
            let accepted =
                { ManagerLifeId = prepared.ManagerLifeId
                  TodoWriteId = prepared.TodoWriteId
                  ToolCallId = prepared.ToolCallId
                  PreparedFactRef = preparedFactRef
                  InputDigest = observedInputDigest
                  OutputDigest = outputDigest
                  PhysicalSuccessEvidence = physical
                  SemanticVersion = prepared.SemanticVersion }

            Ok
                { Accepted = accepted
                  NeedsDedicatedEnlist = not dedicatedExists
                  NeedsEnsureReview = not concludedExists
                  EnrichedResult = enrichedResult
                  CompatibilityRows = toCompatibilityRows reviewingSink submitted }

    /// How to deliver one process-review assignment (HOST-021 / TODO-006).
    ///
    /// `deferSend` Fork installs a pending run before any Authority Root exists.
    /// A second Fork would take sendToExistingChild's busy-nudge path and fail
    /// closed with "Busy nudge requires ActiveLogicalRun" — that is T1 red text.
    [<RequireQualifiedAccess>]
    type AssignmentDelivery =
        /// No Authority Root yet: first prompt is AgentOwnerRoot.
        | OwnerRoot
        /// T1 assignment already claimed; wait for XTrace head, do not send again.
        | AwaitHead
        /// Later checkpoint: assignment continues the dedicated session.
        | Continuation

    let assignmentDelivery (hasActiveProfile: bool) (isFirstAcceptedWrite: bool) : AssignmentDelivery =
        match hasActiveProfile, isFirstAcceptedWrite with
        | false, _ -> AssignmentDelivery.OwnerRoot
        | true, true -> AssignmentDelivery.AwaitHead
        | true, false -> AssignmentDelivery.Continuation

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

    /// When to append TodoReviewConcluded: VerdictKnown ∧ LWR record-ready same snapshot.
    let mayAppendConcluded (verdictKnown: bool) (processReviewLwrRecordReady: bool) : bool =
        verdictKnown && processReviewLwrRecordReady

    /// Compatibility sink reconciliation after REVISE settlement (§23.1 / goal #29).
    /// Not a checkpoint — no Prepared/Accepted/review.
    let reconcileCompatibilityAfterReviseSettlement
        (settled: MagicTodoList)
        (strategy: ReviewingSinkStrategy)
        : CompatibilityTodoRow list =
        toCompatibilityRows strategy settled
