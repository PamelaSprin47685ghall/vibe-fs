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

        // interaction-authority-009: When host message omits explicit agent,
        // Ingress boundary projects active participant to admit as HumanMessage continuation
        let enrichedMessage =
            match message.ExplicitAgent, message.PromptKey, authority with
            | None, None, Some proj ->
                match proj.ActiveLogicalRun with
                | Some activeRun when activeRun.AuthorityKind = PromptAuthority.RootAuthorityKind.HumanRoot ->
                    { message with
                        ExplicitAgent = Some activeRun.SelectedAgent }
                | _ -> message
            | _ -> message

        ChatAdmissionIntent.resolve enrichedMessage { Authority = authority }
