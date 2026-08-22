namespace Wanxiangshu.Mission.Obligation

open System.Threading.Tasks
open Wanxiangshu.Foundation

/// Direct-CE business sequencing for the obligation ledger.
///
/// The types below are one-call outcomes, not durable control state.
module ObligationLedgerWorkflow =

    [<RequireQualifiedAccess>]
    type PreparationAttempt<'prepared, 'failure> =
        | Prepared of 'prepared
        | Failed of 'failure

    [<RequireQualifiedAccess>]
    type PreparationFailure<'attemptFailure> = AttemptFailed of 'attemptFailure

    let prepareCheckpoint
        (admitNow: unit -> Task<PreparationAttempt<'prepared, 'attemptFailure>>)
        : Task<Result<'prepared, PreparationFailure<'attemptFailure>>> =
        taskResult {
            let! firstAttempt = admitNow () |> TaskResultCE.ofTask

            match firstAttempt with
            | PreparationAttempt.Prepared prepared -> return prepared
            | PreparationAttempt.Failed failure -> return! Error(PreparationFailure.AttemptFailed failure)
        }

    [<RequireQualifiedAccess>]
    type AcceptanceFailure<'acceptFailure> = AcceptFailed of 'acceptFailure

    let acceptCheckpoint
        (acceptDurably: unit -> Task<Result<'accepted, 'acceptFailure>>)
        : Task<Result<'accepted, AcceptanceFailure<'acceptFailure>>> =
        taskResult {
            let! accepted = acceptDurably () |> TaskResult.mapError AcceptanceFailure.AcceptFailed
            return accepted
        }
