namespace Wanxiangshu.Execution.Session.ChatExecution

open Wanxiangshu.Execution.Failure
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type HostProviderCompletion =
    | Stop
    | Length
    | ContentFiltered

[<RequireQualifiedAccess>]
type HostProviderTerminalOutcome =
    | Completed of HostProviderCompletion
    | Cancelled of ExecutionFailure
    | Interrupted of ExecutionFailure
    | ProviderFailure of ExecutionFailure

type ExactProviderTerminalObservation =
    { SessionId: SessionId
      PhysicalUserMessageId: PhysicalUserMessageId
      ProviderRun: ProviderRunIdentity
      Outcome: HostProviderTerminalOutcome
      Disposition: ChatExecutionTerminalDisposition option }

type ExactProviderStartObservation =
    { SessionId: SessionId
      PhysicalUserMessageId: PhysicalUserMessageId
      ProviderRun: ProviderRunIdentity }
