namespace Wanxiangshu.Manager

open System.Threading.Tasks

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
        task {
            let! firstAttempt = admitNow ()

            match firstAttempt with
            | PreparationAttempt.Prepared prepared -> return Ok prepared
            | PreparationAttempt.Failed failure ->
                return Error(PreparationFailure.AttemptFailed failure)
            | PreparationAttempt.AwaitPreviousReview ->
                match currentPendingReview () with
                | None -> return Error PreparationFailure.MissingPendingReview
                | Some reviewToken ->
                    let! waited = awaitReview reviewToken

                    match waited with
                    | Error failure -> return Error(PreparationFailure.ReviewWaitFailed failure)
                    | Ok() ->
                        let! secondAttempt = admitNow ()

                        match secondAttempt with
                        | PreparationAttempt.Prepared prepared -> return Ok prepared
                        | PreparationAttempt.Failed failure ->
                            return Error(PreparationFailure.AttemptFailed failure)
                        | PreparationAttempt.AwaitPreviousReview ->
                            return Error PreparationFailure.ReviewDidNotConverge
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
        task {
            let! acceptedResult = acceptDurably ()

            match acceptedResult with
            | Error failure -> return Error(AcceptanceFailure.AcceptFailed failure)
            | Ok accepted when not (shouldEnsureReview accepted) -> return Ok accepted
            | Ok accepted ->
                let! reviewResult = ensureReview accepted

                match reviewResult with
                | Error failure -> return Error(AcceptanceFailure.ReviewFailed failure)
                | Ok() -> return Ok accepted
        }
