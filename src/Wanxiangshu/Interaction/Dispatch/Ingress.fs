namespace Wanxiangshu.Interaction.Dispatch

open Wanxiangshu.OpenCode
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Foundation
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Composition.Durable

[<RequireQualifiedAccess>]
module PromptIngress =

    let resolveDecision (journal: AgentJournal option) (message: PromptIngressCodec.DecodedMessage) =
        let authority =
            match journal, message.SessionId with
            | Some durable, Some sessionId ->
                let runtime = PromptDispatcher.forPrompts (PromptJournalAdapter.create durable)
                Some(runtime.ProjectionFor sessionId)
            | Some _, None -> Some PromptAuthority.empty
            | None, _ -> None

        ChatAdmissionIntent.resolve message { Authority = authority }
