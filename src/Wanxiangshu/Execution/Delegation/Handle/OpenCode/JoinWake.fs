namespace Wanxiangshu.Execution.Delegation.Handle.OpenCode

open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Interaction.Authority

[<RequireQualifiedAccess>]
module JoinWake =

    let observeAcceptedMessage (registry: IJoinAttemptRegistry) (evidence: AcceptedChatExecutionEvidence) =
        match evidence.Origin with
        | PromptOrigin.AuthorityRoot _
        | PromptOrigin.Continuation _ ->
            registry.SignalVisibleUserMessage(evidence.SessionId, evidence.PhysicalUserMessageId)
        | PromptOrigin.HostInternal
        | PromptOrigin.UnknownOrigin -> ()
