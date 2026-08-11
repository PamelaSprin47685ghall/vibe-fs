namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Domain.MagicTodoFacts
open Wanxiangshu.Domain.MagicTodoSurface
open Wanxiangshu.Kernel.Identity

/// Pure after-hook / recovery orchestration sketch (protocol §15).
/// Speculative / unwired — Host membrane calls these after live after or
/// RecoveredCompletedToolPart proof; Journal append stays at the call site.
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
        (previous: PreviousReviewView option)
        (settledCurrent: MagicTodoList)
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

            let enriched =
                buildEnrichedResult previous settledCurrent submitted |> renderEnrichedResult

            Ok
                { Accepted = accepted
                  NeedsDedicatedEnlist = not dedicatedExists
                  NeedsEnsureReview = not concludedExists
                  EnrichedResult = enriched
                  CompatibilityRows = toCompatibilityRows reviewingSink submitted }

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
