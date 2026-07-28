namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Identity
open AgentFactsFoldHelpers

module AgentFactsAuthority =

    let private empty =
        { LastAuthorityProfile = None
          ActiveLogicalRun = None
          PendingClaims = Map.empty
          AcceptedContinuationIds = Map.empty
          RepairClaims = [] }

    let foldAuthorityRootAccepted
        (proj: AgentProjectionSet)
        (sessionId: SessionId)
        logicalRunId
        hostMessageId
        authorityKind
        selectedAgent
        peerAgent
        canonicalRole
        selectedTier
        =
        let profile: AuthorityProfileProjection =
            { LogicalRunId = logicalRunId
              AuthorityRootUserMessageId = hostMessageId
              AuthorityKind = authorityKind
              SelectedAgent = selectedAgent
              PeerAgent = peerAgent
              CanonicalRole = canonicalRole
              SelectedTier = selectedTier }

        let sessions =
            updateSession
                sessionId
                (fun s ->
                    { s with
                        // New Authority Root creates a new Fallback epoch.
                        Fallback =
                            Some
                                { LogicalRunId = logicalRunId
                                  AuthorityRootUserMessageId = hostMessageId
                                  Offset = 0uy
                                  LastProviderAttempt = None
                                  RecentFailureIds = [] }
                        PromptAuthority =
                            Some
                                { LastAuthorityProfile = Some profile
                                  ActiveLogicalRun = Some profile
                                  PendingClaims = Map.empty
                                  AcceptedContinuationIds = Map.empty
                                  RepairClaims = [] } })
                proj.Sessions

        { proj with Sessions = sessions }

    let foldPluginPromptClaimed (proj: AgentProjectionSet) sessionId promptKey continuationKind =
        let sessions =
            updateSession
                sessionId
                (fun s ->
                    let authority = defaultArg s.PromptAuthority empty

                    { s with
                        PromptAuthority =
                            Some
                                { authority with
                                    PendingClaims = Map.add promptKey continuationKind authority.PendingClaims } })
                proj.Sessions

        { proj with Sessions = sessions }

    let foldPluginPromptAccepted (proj: AgentProjectionSet) sessionId promptKey hostMessageId =
        let sessions =
            updateSession
                sessionId
                (fun s ->
                    let authority = defaultArg s.PromptAuthority empty
                    let kind = defaultArg (Map.tryFind promptKey authority.PendingClaims) "unknown"

                    { s with
                        PromptAuthority =
                            Some
                                { authority with
                                    PendingClaims = Map.remove promptKey authority.PendingClaims
                                    AcceptedContinuationIds =
                                        Map.add hostMessageId kind authority.AcceptedContinuationIds } })
                proj.Sessions

        { proj with Sessions = sessions }

    let foldPluginPromptAbandoned (proj: AgentProjectionSet) sessionId promptKey =
        let sessions =
            updateSession
                sessionId
                (fun s ->
                    let authority = defaultArg s.PromptAuthority empty

                    { s with
                        PromptAuthority =
                            Some
                                { authority with
                                    PendingClaims = Map.remove promptKey authority.PendingClaims } })
                proj.Sessions

        { proj with Sessions = sessions }


    let foldInteractionRepairClaimed
        (proj: AgentProjectionSet)
        (sessionId: SessionId)
        logicalRunId
        authorityRootUserMessageId
        terminalAssistantMessageId
        repairKind
        =
        let identity =
            sprintf "%s|%s|%s|%s" logicalRunId authorityRootUserMessageId terminalAssistantMessageId repairKind

        let sessions =
            updateSession
                sessionId
                (fun s ->
                    let authority = defaultArg s.PromptAuthority empty

                    if List.contains identity authority.RepairClaims then
                        s
                    else
                        { s with
                            PromptAuthority =
                                Some
                                    { authority with
                                        RepairClaims = identity :: authority.RepairClaims } })
                proj.Sessions

        { proj with Sessions = sessions }
