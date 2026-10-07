namespace Wanxiangshu.Execution.Delegation.Handle.OpenCode

open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session.ChatExecution

[<RequireQualifiedAccess>]
module JoinWake =

    val observeAcceptedMessage: registry: IJoinAttemptRegistry -> evidence: AcceptedChatExecutionEvidence -> unit
