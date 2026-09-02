namespace Wanxiangshu.Mission.Obligation

open System.Threading.Tasks

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

    [<RequireQualifiedAccess>]
    type AcceptanceFailure<'acceptFailure> = AcceptFailed of 'acceptFailure

    val prepareCheckpoint:
        admitNow: (unit -> Task<PreparationAttempt<'prepared, 'attemptFailure>>) ->
            Task<Result<'prepared, PreparationFailure<'attemptFailure>>>

    val acceptCheckpoint:
        acceptDurably: (unit -> Task<Result<'accepted, 'acceptFailure>>) ->
            Task<Result<'accepted, AcceptanceFailure<'acceptFailure>>>
