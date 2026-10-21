namespace Wanxiangshu.OpenCode

open Wanxiangshu.Execution.Failure
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity

module HostEventCodec =
    val tryDecode: rawInput: obj -> HostSignal option
    val tryDecodeExactProviderStart: rawInput: obj -> ExactProviderStartObservation option
    val tryDecodeExactProviderTerminal: rawInput: obj -> ExactProviderTerminalObservation option
    val tryDecodeProviderStepEnd: rawInput: obj -> (SessionId * PhysicalUserMessageId * ProviderRunIdentity) option
    val tryDecodePhysicalExecutionEnd: rawInput: obj -> (SessionId * PhysicalUserMessageId) option
