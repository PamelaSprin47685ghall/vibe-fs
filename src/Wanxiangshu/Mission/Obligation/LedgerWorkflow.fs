namespace Wanxiangshu.Mission.Obligation

open Wanxiangshu.Change
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Strength.Replica

open System.Threading.Tasks
open Wanxiangshu.Foundation

/// Direct-CE business sequencing for the obligation ledger.
///
/// The types below are one-call outcomes, not durable control state. Nothing in
/// this module is persisted as "where the program stopped"; recovery observes
/// durable facts again and re-enters these ordinary functions.
module ObligationLedgerWorkflow =

    [<RequireQualifiedAccess>]
    type PreparationAttempt<'prepared, 'failure> =
        | Prepared of 'prepared
        | AwaitPreviousReview
        | Failed of 'failure

    [<RequireQualifiedAccess>]
    type PreparationFailure<'attemptFailure, 'reviewFailure> =
        | AttemptFailed of 'attemptFailure
        | ReviewWaitFailed of 'reviewFailure
        | MissingPendingReview
        | ReviewDidNotConverge

    /// At most one causal review wait is needed for one checkpoint admission:
    /// no pending review can be created by the waiting call before Prepared is
    /// durable, so a second AwaitPreviousReview is an invariant failure rather
    /// than an unbounded retry loop.
    let prepareCheckpoint
        (admitNow: unit -> Task<PreparationAttempt<'prepared, 'attemptFailure>>)
        (currentPendingReview: unit -> 'reviewToken option)
        (awaitReview: 'reviewToken -> Task<Result<unit, 'reviewFailure>>)
        : Task<Result<'prepared, PreparationFailure<'attemptFailure, 'reviewFailure>>> =
        let rec settleFirstAttempt firstAttempt =
            match firstAttempt with
            | PreparationAttempt.Prepared prepared -> Task.FromResult(Ok prepared)
            | PreparationAttempt.Failed failure ->
                Task.FromResult(Error(PreparationFailure.AttemptFailed failure))
            | PreparationAttempt.AwaitPreviousReview -> awaitThenRetry ()

        and awaitThenRetry () =
            match currentPendingReview () with
            | None -> Task.FromResult(Error PreparationFailure.MissingPendingReview)
            | Some reviewToken -> retryAfterReview reviewToken

        and retryAfterReview reviewToken =
            taskResult {
                do! awaitReview reviewToken |> TaskResult.mapError PreparationFailure.ReviewWaitFailed
                let! secondAttempt = admitNow () |> TaskResultCE.ofTask

                match secondAttempt with
                | PreparationAttempt.Prepared prepared -> return prepared
                | PreparationAttempt.Failed failure -> return! Error(PreparationFailure.AttemptFailed failure)
                | PreparationAttempt.AwaitPreviousReview -> return! Error PreparationFailure.ReviewDidNotConverge
            }

        taskResult {
            let! firstAttempt = admitNow () |> TaskResultCE.ofTask
            return! settleFirstAttempt firstAttempt
        }

    [<RequireQualifiedAccess>]
    type AcceptanceFailure<'acceptFailure, 'reviewFailure> =
        | AcceptFailed of 'acceptFailure
        | ReviewFailed of 'reviewFailure

    /// Accepted must become durable before the process-review obligation is
    /// ensured. This order is expressed by the F# CE itself, not by a stage field.
    let acceptCheckpoint
        (acceptDurably: unit -> Task<Result<'accepted, 'acceptFailure>>)
        (shouldEnsureReview: 'accepted -> bool)
        (ensureReview: 'accepted -> Task<Result<unit, 'reviewFailure>>)
        : Task<Result<'accepted, AcceptanceFailure<'acceptFailure, 'reviewFailure>>> =
        taskResult {
            let! accepted = acceptDurably () |> TaskResult.mapError AcceptanceFailure.AcceptFailed

            if not (shouldEnsureReview accepted) then
                return accepted
            else
                do! ensureReview accepted |> TaskResult.mapError AcceptanceFailure.ReviewFailed
                return accepted
        }
