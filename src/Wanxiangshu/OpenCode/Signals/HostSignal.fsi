namespace Wanxiangshu.OpenCode

open Wanxiangshu.Execution.Failure
open Wanxiangshu.Foundation.Identity

type HostFailureObservation =
    { SessionId: SessionId
      Failure: ExecutionFailure
      Diagnostic: string }

type RetrySignal =
    { SessionId: SessionId
      Attempt: string
      Failure: ExecutionFailure
      Diagnostic: string }

type HostSignal =
    | SessionIdle of SessionId
    | ProviderRetry of RetrySignal
    | ProviderFailure of HostFailureObservation
    | SessionDeleted of sessionId: SessionId * parentSessionId: SessionId option
    | AttemptAborted of HostFailureObservation
