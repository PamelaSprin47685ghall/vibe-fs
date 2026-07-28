namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open AgentFactsFoldHelpers

module AgentFactsAuthority =

    let foldAuthorityRootAccepted
        (proj: AgentProjectionSet)
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
        let promptAuthority =
            let authority =
                defaultArg
                    (Map.tryFind p.SessionId proj.Sessions
                     |> Option.bind (fun s -> s.PromptAuthority))
                    PromptAuthorityLedger.empty

            PromptAuthorityLedger.foldAuthorityRootAccepted authority p

        let sessions =
            updateSession
                p.SessionId
                (fun s ->
                    { s with
                        // New Authority Root creates a new Fallback epoch.
                        Fallback =
                            Some
                                { LogicalRunId = p.LogicalRunId
                                  AuthorityRootUserMessageId = p.HostMessageId
                                  Offset = 0uy
                                  LastProviderAttempt = None
                                  RecentFailureIds = [] }
                        PromptAuthority = Some promptAuthority })
                proj.Sessions

        { proj with Sessions = sessions }

    let foldPluginPromptClaimed
        (proj: AgentProjectionSet)
        (p:
            {| PromptKey: string
               SessionId: SessionId
               LogicalRunId: string
               AuthorityRootUserMessageId: string
               ContinuationKind: string
               EffectiveAgent: string option |})
        =
        let sessions =
            updateSession
                p.SessionId
                (fun s ->
                    let authority = defaultArg s.PromptAuthority PromptAuthorityLedger.empty

                    { s with
                        PromptAuthority = Some(PromptAuthorityLedger.foldPluginPromptClaimed authority p) })
                proj.Sessions

        { proj with Sessions = sessions }

    let foldPluginPromptAccepted
        (proj: AgentProjectionSet)
        (p:
            {| PromptKey: string
               SessionId: SessionId
               HostMessageId: string |})
        =
        let sessions =
            updateSession
                p.SessionId
                (fun s ->
                    let authority = defaultArg s.PromptAuthority PromptAuthorityLedger.empty

                    { s with
                        PromptAuthority = Some(PromptAuthorityLedger.foldPluginPromptAccepted authority p) })
                proj.Sessions

        { proj with Sessions = sessions }

    let foldPluginPromptAbandoned
        (proj: AgentProjectionSet)
        (p:
            {| PromptKey: string
               SessionId: SessionId
               Reason: string |})
        =
        let sessions =
            updateSession
                p.SessionId
                (fun s ->
                    let authority = defaultArg s.PromptAuthority PromptAuthorityLedger.empty

                    { s with
                        PromptAuthority = Some(PromptAuthorityLedger.foldPluginPromptAbandoned authority p) })
                proj.Sessions

        { proj with Sessions = sessions }

    let foldInteractionRepairClaimed
        (proj: AgentProjectionSet)
        (p:
            {| SessionId: SessionId
               LogicalRunId: string
               AuthorityRootUserMessageId: string
               TerminalAssistantMessageId: string
               RepairKind: string |})
        =
        let sessions =
            updateSession
                p.SessionId
                (fun s ->
                    let authority = defaultArg s.PromptAuthority PromptAuthorityLedger.empty

                    { s with
                        PromptAuthority = Some(PromptAuthorityLedger.foldInteractionRepairClaimed authority p) })
                proj.Sessions

        { proj with Sessions = sessions }
