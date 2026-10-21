namespace Wanxiangshu.Execution.Session.ChatExecution

open Wanxiangshu.Execution.Failure
open Wanxiangshu.Foundation.Identity

/// How a provider attempt finished, as the chat execution needs to know it.
/// Host adapters decode their own events into these records.
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

/// The exact terminal of one provider run of one physical user message.
type ExactProviderTerminalObservation =
    { SessionId: SessionId
      PhysicalUserMessageId: PhysicalUserMessageId
      ProviderRun: ProviderRunIdentity
      Outcome: HostProviderTerminalOutcome
      Disposition: ChatExecutionTerminalDisposition option }

/// The exact start of one provider run of one physical user message.
type ExactProviderStartObservation =
    { SessionId: SessionId
      PhysicalUserMessageId: PhysicalUserMessageId
      ProviderRun: ProviderRunIdentity }
