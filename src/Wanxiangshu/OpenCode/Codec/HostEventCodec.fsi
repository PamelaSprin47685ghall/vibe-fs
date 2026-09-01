namespace Wanxiangshu.OpenCode

open Wanxiangshu.Execution.Failure
open Wanxiangshu.Execution.Session.ChatExecution
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

module HostEventCodec =
    val unwrap: rawInput: obj -> obj
    val eventTypeOf: raw: obj -> string
    val trySessionId: raw: obj -> SessionId option
    val tryDecode: rawInput: obj -> HostSignal option
    val tryMessageSessionId: rawInput: obj -> SessionId option
    val tryDecodeExactProviderStart: rawInput: obj -> ExactProviderStartObservation option
    val tryDecodeExactProviderTerminal: rawInput: obj -> ExactProviderTerminalObservation option
    val tryDecodeProviderStepEnd: rawInput: obj -> (SessionId * PhysicalUserMessageId * ProviderRunIdentity) option
    val tryDecodePhysicalExecutionEnd: rawInput: obj -> (SessionId * PhysicalUserMessageId) option
