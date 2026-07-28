namespace Wanxiangshu.Next.Journal

open System
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact

module PromptAuthorityLedger =

    let empty = PromptAuthority.empty

    let private toOptionMessageId (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            Some(MessageId.create value)

    let private parseAuthorityKind (value: string) =
        match value with
        | "AgentOwnerRoot" -> PromptAuthority.RootAuthorityKind.AgentOwnerRoot
        | _ -> PromptAuthority.RootAuthorityKind.HumanRoot

    let foldAuthorityRootAccepted
        (projection: PromptAuthority.PromptAuthorityProjection)
        (p:
            {| SessionId: SessionId
               LogicalRunId: string
               HostMessageId: string
               AuthorityKind: string
               SelectedAgent: string
               PeerAgent: string
               CanonicalRole: string
               SelectedTier: string |})
        =
        match PromptAuthority.parseAgentName p.SelectedAgent with
        | Error _ -> projection
        | Ok(name, role, tier, peer) ->
            let peerAgent =
                if String.IsNullOrWhiteSpace p.PeerAgent then
                    peer
                else
                    p.PeerAgent

            let profile: PromptAuthority.AuthorityExecutionProfile =
                { SessionId = p.SessionId
                  LogicalRunId = p.LogicalRunId
                  AuthorityRootUserMessageId = MessageId.create p.HostMessageId
                  AuthorityKind = parseAuthorityKind p.AuthorityKind
                  SelectedAgent = name
                  PeerAgent = peerAgent
                  CanonicalRole = role
                  SelectedTier = tier }

            PromptAuthorityRun.registerAuthority profile projection

    let foldPluginPromptClaimed
        (projection: PromptAuthority.PromptAuthorityProjection)
        (p:
            {| PromptKey: string
               SessionId: SessionId
               LogicalRunId: string
               AuthorityRootUserMessageId: string
               ContinuationKind: string
               EffectiveAgent: string option |})
        =
        let origin =
            if p.ContinuationKind = "AgentOwnerRoot" then
                PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot
            else
                match PromptAuthority.tryParseContinuationKind p.ContinuationKind with
                | Some kind -> PromptAuthority.PromptOrigin.Continuation kind
                | None -> PromptAuthority.PromptOrigin.UnknownOrigin

        if origin = PromptAuthority.PromptOrigin.UnknownOrigin then
            projection
        else
            let claim: PromptAuthority.PromptClaim =
                { PromptAuthority.PromptClaim.PromptKey = PromptKeyRef.create p.PromptKey
                  SessionId = p.SessionId
                  Origin = origin
                  LogicalRunId = p.LogicalRunId
                  AuthorityRootUserMessageId = toOptionMessageId p.AuthorityRootUserMessageId
                  EffectiveAgent = p.EffectiveAgent }

            PromptAuthorityRun.registerClaim claim projection

    let foldPluginPromptAccepted
        (projection: PromptAuthority.PromptAuthorityProjection)
        (p:
            {| PromptKey: string
               SessionId: SessionId
               HostMessageId: string |})
        =
        PromptAuthorityRun.acceptClaim (PromptKeyRef.create p.PromptKey) (MessageId.create p.HostMessageId) projection

    let foldPluginPromptAbandoned
        (projection: PromptAuthority.PromptAuthorityProjection)
        (p:
            {| PromptKey: string
               SessionId: SessionId
               Reason: string |})
        =
        PromptAuthorityRun.abandonClaim (PromptKeyRef.create p.PromptKey) projection

    let foldInteractionRepairClaimed
        (projection: PromptAuthority.PromptAuthorityProjection)
        (p:
            {| SessionId: SessionId
               LogicalRunId: string
               AuthorityRootUserMessageId: string
               TerminalAssistantMessageId: string
               RepairKind: string |})
        =
        let identity =
            PromptAuthority.repairIdentity
                p.LogicalRunId
                (MessageId.create p.AuthorityRootUserMessageId)
                (MessageId.create p.TerminalAssistantMessageId)
                p.RepairKind

        PromptAuthorityRun.tryClaimRepair identity projection
        |> Option.defaultValue projection

    let projectionFor (sessionId: SessionId) (agentProjections: AgentProjectionSet) =
        Map.tryFind sessionId agentProjections.Sessions
        |> Option.bind (fun s -> s.PromptAuthority)

    let activeProfile (sessionId: SessionId) (agentProjections: AgentProjectionSet) =
        projectionFor sessionId agentProjections
        |> Option.bind (fun a -> a.ActiveLogicalRun)

    let lastAuthorityProfile (sessionId: SessionId) (agentProjections: AgentProjectionSet) =
        projectionFor sessionId agentProjections
        |> Option.bind (fun a -> a.LastAuthorityProfile)

    let pendingClaim (sessionId: SessionId) (promptKey: PromptKeyRef) (agentProjections: AgentProjectionSet) =
        projectionFor sessionId agentProjections
        |> Option.bind (fun a -> Map.tryFind promptKey a.PendingClaims)

    let acceptedContinuation (messageId: MessageId) (agentProjections: AgentProjectionSet) =
        agentProjections.Sessions
        |> Map.tryPick (fun _ s ->
            s.PromptAuthority
            |> Option.bind (fun a -> Map.tryFind messageId a.AcceptedContinuationIds))
