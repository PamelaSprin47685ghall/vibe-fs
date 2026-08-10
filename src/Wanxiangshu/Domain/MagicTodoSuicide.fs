namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Domain.MagicTodoFacts
open Wanxiangshu.Kernel.Identity

/// Suicide / lag-1 consume helpers (protocol §21 / §0 first-unblessed gate).
/// Speculative / unwired — FinalityController will call these later.
module MagicTodoSuicide =

    type DrainResult =
        {
            /// Settled current after consuming the latest ConsumableReview, if any.
            SettledCurrent: MagicTodoList
            /// Latest consumable process review drained into Manager-facing evidence.
            DrainedReview: TodoReviewConcluded option
            /// Compatibility sink should be reconciled to SettledCurrent when REVISE
            /// was consumed and no fresh Accepted follows immediately (§23.1).
            NeedsCompatibilityReconcile: bool
        }

    /// Consume latest TodoReviewConcluded into settlement without creating a checkpoint.
    let drainLatestConsumable
        (decodeList: BlobRef -> BlobDigest -> MagicTodoList)
        (settledBefore: MagicTodoList)
        (latestAccepted: TodoWriteAccepted option)
        (preparedForLatest: TodoWritePrepared option)
        (concluded: TodoReviewConcluded option)
        : DrainResult =
        match latestAccepted, preparedForLatest, concluded with
        | Some _, Some prepared, Some review ->
            let baseList = decodeList prepared.BaseTodoRef prepared.BaseTodoDigest
            let proposed = decodeList prepared.ProposedTodoRef prepared.ProposedTodoDigest
            let settled = MagicTodo.settle baseList proposed review.Verdict

            { SettledCurrent = settled
              DrainedReview = Some review
              NeedsCompatibilityReconcile = review.Verdict = ProcessReviewVerdict.Revise }
        | _ ->
            { SettledCurrent = settledBefore
              DrainedReview = None
              NeedsCompatibilityReconcile = false }

    /// First unblessed suicide: require ≥1 TodoWriteAccepted.
    let gateFirstUnblessed (acceptedCount: int) : Result<unit, MagicTodoReject> =
        MagicTodo.requireCheckpointBeforeFirstSuicide acceptedCount
