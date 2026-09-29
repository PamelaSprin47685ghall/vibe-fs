namespace Wanxiangshu.Interaction.Dispatch

open Wanxiangshu.OpenCode
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Foundation
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Composition.Durable

[<RequireQualifiedAccess>]
module PromptIngress =

    let private tryActiveHumanRootAgent (proj: PromptAuthority.PromptAuthorityProjection) =
        match proj.ActiveLogicalRun with
        | Some activeRun when activeRun.AuthorityKind = PromptAuthority.RootAuthorityKind.HumanRoot ->
            Some activeRun.SelectedAgent
        | _ -> None

    let private enrichHostMessage authority (message: PromptIngressCodec.DecodedMessage) =
        let agentOpt =
            match message.ExplicitAgent, message.PromptKey with
            | None, None -> authority |> Option.bind tryActiveHumanRootAgent
            | _ -> None

        match agentOpt with
        | Some agent ->
            { message with
                ExplicitAgent = Some agent }
        | None -> message

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
        let enrichedMessage = enrichHostMessage authority message

        ChatAdmissionIntent.resolve enrichedMessage { Authority = authority }
