namespace Wanxiangshu.Kernel.Subsession.Types

open Wanxiangshu.Kernel.FallbackKernel.Types

type Command =
    | StartRun of StartRunRequest
    | DispatchAccepted of TurnId * HostStartReceipt
    | DispatchRejected of TurnId * DispatchFailure
    | TurnErrorObserved of ErrorInput
    | SessionIdleObserved
    /// Host confirmed dispatch status for reconciliation (AcceptanceUnknown path).
    | DispatchStatusResolved of DispatchStatus
    | EvidenceUpdated of TurnObservation
    | CancelRequested
    | TurnDeadlineExpired of TurnId
    | AbortDeadlineExpired of TurnId
    /// Deadline for dispatch reconciliation has expired; query again or poison.
    | ReconciliationDeadlineExpired of TurnId
    /// Host confirmed session stopped (safe to apply AfterAbort).
    | AbortConfirmed of TurnId
    /// Host accepted abort request; subsequent idle may settle.
    | AbortHostAccepted of TurnId
    /// Host abort call failed or API unavailable.
    | AbortRequestFailed of TurnId * ErrorInput
    /// Host confirmed whether the session is still running after abort.
    | SessionQuiescenceResolved of QuiescenceStatus
    | PhysicalCloseResolved of QuiescenceStatus
    | SessionClosed
