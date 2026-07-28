namespace Wanxiangshu.Next.Journal

open System
open Wanxiangshu.Next.Kernel.Fact
open AgentFactsFoldHelpers

module AgentFacts =

    let emptyCompanion: CompanionProjection =
        { LastSuccessfulProjection = None
          LatestB = None
          ActivePrefixEpoch = None
          ReplacementActive = false }

    let emptyLinkage: AgentLinkageProjection =
        { LinkedChildren = Map.empty
          LinkedRoles = Map.empty }

    let emptyReviewGuard: ReviewGuardProjection =
        { LastGitTreeHash = None
          ConsecutivePerfects = 0
          IsConfirmed = false
          AcceptedGuardKey = None
          RecentToolCallIds = []
          RecentProviderRunIds = []
          ConfirmationPhysicalMessageId = None
          AuthorityRootUserMessageId = None
          CurrentBarrierKey = None }

    let emptyPromptAuthority: PromptAuthorityProjection =
        { LastAuthorityProfile = None
          ActiveLogicalRun = None
          PendingClaims = Map.empty
          AcceptedContinuationIds = Map.empty
          RepairClaims = [] }

    let emptyFallback: FallbackProjection =
        { LogicalRunId = ""
          AuthorityRootUserMessageId = ""
          Side = SideA
          FailuresOnCurrentSide = 0
          TotalFailures = 0
          IsDead = false
          RecentFailureIds = [] }

    let emptyOrchestrator: OrchestratorProjection =
        { ManagerJobs = Map.empty
          Managers = Map.empty
          PublishedCommit = None }

    let emptyEffects: DurableEffectProjection = { Current = None }

    let emptySessionProjection: SessionAgentProjection =
        { Companion = None
          Linkage = None
          ReviewGuard = None
          Fallback = None
          PromptAuthority = None
          Effects = None }

    let empty: AgentProjectionSet =
        { Sessions = Map.empty
          Orchestrator = emptyOrchestrator }

    let foldAgentFact (proj: AgentProjectionSet) (fact: AgentFact) : AgentProjectionSet =
        match fact with
        | AgentFact.AgentLinked _ -> proj
        | AgentFact.AgentUnlinked _ -> proj
        | AgentFact.CompanionBaselineSet p ->
            let sessions =
                updateSession
                    p.SessionId
                    (fun s ->
                        let comp =
                            match s.Companion with
                            | Some existing ->
                                { existing with
                                    LastSuccessfulProjection = Some p.Projection }
                            | None ->
                                { LastSuccessfulProjection = Some p.Projection
                                  LatestB = None
                                  ActivePrefixEpoch = None
                                  ReplacementActive = false }

                        { s with Companion = Some comp })
                    proj.Sessions

            { proj with Sessions = sessions }

        | AgentFact.CompanionCheckpointReplaced p ->
            let sessions =
                updateSession
                    p.SessionId
                    (fun s ->
                        let comp =
                            match s.Companion with
                            | Some existing ->
                                { existing with
                                    LatestB = Some p.Content }
                            | None ->
                                { LastSuccessfulProjection = None
                                  LatestB = Some p.Content
                                  ActivePrefixEpoch = None
                                  ReplacementActive = false }

                        { s with Companion = Some comp })
                    proj.Sessions

            { proj with Sessions = sessions }

        | AgentFact.CompanionAdvanced p ->
            let sessions =
                updateSession
                    p.SessionId
                    (fun s ->
                        let comp =
                            match s.Companion with
                            | Some existing ->
                                { existing with
                                    LastSuccessfulProjection = Some p.Projection
                                    LatestB = Some p.Content }
                            | None ->
                                { LastSuccessfulProjection = Some p.Projection
                                  LatestB = Some p.Content
                                  ActivePrefixEpoch = None
                                  ReplacementActive = false }

                        { s with Companion = Some comp })
                    proj.Sessions

            { proj with Sessions = sessions }

        | AgentFact.CompanionEpochSwitched p ->
            let sessions =
                updateSession
                    p.SessionId
                    (fun s ->
                        let comp =
                            match s.Companion with
                            | Some existing ->
                                { existing with
                                    ActivePrefixEpoch =
                                        Some
                                            { EpochId = p.EpochId
                                              FrozenB = p.FrozenB
                                              CutoffMessageIndex = p.CutoffMessageIndex
                                              CoveredPrefixDigest = p.CoveredPrefixDigest } }
                            | None ->
                                { LastSuccessfulProjection = None
                                  LatestB = None
                                  ActivePrefixEpoch =
                                    Some
                                        { EpochId = p.EpochId
                                          FrozenB = p.FrozenB
                                          CutoffMessageIndex = p.CutoffMessageIndex
                                          CoveredPrefixDigest = p.CoveredPrefixDigest }
                                  ReplacementActive = true }

                        { s with Companion = Some comp })
                    proj.Sessions

            { proj with Sessions = sessions }

        | AgentFact.CompanionReplacementActiveSet p ->
            let sessions =
                updateSession
                    p.SessionId
                    (fun s ->
                        let comp =
                            match s.Companion with
                            | Some existing ->
                                { existing with
                                    ReplacementActive = p.Active }
                            | None ->
                                { LastSuccessfulProjection = None
                                  LatestB = None
                                  ActivePrefixEpoch = None
                                  ReplacementActive = p.Active }

                        { s with Companion = Some comp })
                    proj.Sessions

            { proj with Sessions = sessions }

        | AgentFact.GuardPromptAccepted p -> AgentFactsReview.foldGuardPromptAccepted proj p

        | AgentFact.ReviewBarrierStarted p -> AgentFactsReview.foldReviewBarrierStarted proj p

        | AgentFact.ReviewVerdictRecorded p -> AgentFactsReview.foldReviewVerdictRecorded proj p

        | AgentFact.FallbackFailureRecorded p -> AgentFactsFallback.foldFallbackFailureRecorded proj p

        | AgentFact.AuthorityRootAccepted p ->
            AgentFactsAuthority.foldAuthorityRootAccepted
                proj
                p.SessionId
                p.LogicalRunId
                p.HostMessageId
                p.AuthorityKind
                p.Agent
                p.BaseProviderID
                p.BaseModelID
                p.Variant
        | AgentFact.PluginPromptClaimed p ->
            AgentFactsAuthority.foldPluginPromptClaimed proj p.SessionId p.PromptKey p.ContinuationKind
        | AgentFact.PluginPromptAccepted p ->
            AgentFactsAuthority.foldPluginPromptAccepted proj p.SessionId p.PromptKey p.HostMessageId
        | AgentFact.PluginPromptAbandoned p ->
            AgentFactsAuthority.foldPluginPromptAbandoned proj p.SessionId p.PromptKey
        | AgentFact.InteractionRepairClaimed p ->
            AgentFactsAuthority.foldInteractionRepairClaimed
                proj
                p.SessionId
                p.LogicalRunId
                p.AuthorityRootUserMessageId
                p.TerminalAssistantMessageId
                p.RepairKind

        | AgentFact.OrchestratorManagerJobCreated p ->
            AgentFactsFoldHelpers.foldOrchestratorManagerJobCreated proj p.ManagerId p.WorktreePath p.Branch p.Prompt

        | AgentFact.OrchestratorCandidateRegistered p ->
            AgentFactsFoldHelpers.foldOrchestratorCandidateRegistered
                proj
                p.ManagerId
                p.CandidateId
                p.Branch
                p.CommitHash

        | AgentFact.OrchestratorPublished p ->
            AgentFactsFoldHelpers.foldOrchestratorPublished proj p.ManagerId p.CandidateId p.CommitHash

        | AgentFact.OrchestratorRejected p ->
            AgentFactsFoldHelpers.foldOrchestratorRejected proj p.ManagerId p.CandidateId p.Reason

        | AgentFact.OrchestratorPreRebaseReviewConfirmed p ->
            AgentFactsFoldHelpers.foldOrchestratorPreRebaseReviewConfirmed proj p.ManagerId p.CandidateId p.CommitHash

        | AgentFact.OrchestratorRebased p ->
            AgentFactsFoldHelpers.foldOrchestratorRebased proj p.ManagerId p.CandidateId p.RebasedCommit

        | AgentFact.OrchestratorConflictDetected p ->
            AgentFactsFoldHelpers.foldOrchestratorConflictDetected proj p.ManagerId p.CandidateId p.Files

        | AgentFact.OrchestratorPostRebaseReviewConfirmed p ->
            AgentFactsFoldHelpers.foldOrchestratorPostRebaseReviewConfirmed
                proj
                p.ManagerId
                p.CandidateId
                p.RebasedCommit

        | AgentFact.OrchestratorPublishClaimed p ->
            AgentFactsFoldHelpers.foldOrchestratorPublishClaimed proj p.ManagerId p.CandidateId p.ExpectedTargetHead

        | AgentFact.DurableEffectRequested p ->
            AgentFactsFoldHelpers.foldDurableEffectRequested proj p.SessionId p.EffectId p.Target p.Payload

        | AgentFact.DurableEffectAccepted p ->
            AgentFactsFoldHelpers.foldDurableEffectAccepted proj p.SessionId p.EffectId p.Result

    let rec foldAgentFactWithEnvelope (proj: AgentProjectionSet) (envelope: Envelope) : AgentProjectionSet =
        match envelope.Fact with
        | Fact.Agent fact -> foldAgentFactEnvelope proj envelope fact
        | _ -> proj

    and foldAgentFactEnvelope (proj: AgentProjectionSet) (envelope: Envelope) (fact: AgentFact) : AgentProjectionSet =
        match fact with
        | AgentFact.AgentLinked p -> AgentFactsLinkage.foldLinked proj p.ParentId p.ChildId p.TargetAgent p.Role
        | AgentFact.AgentUnlinked p -> AgentFactsLinkage.foldUnlinked proj p.ParentId p.ChildId
        | fact -> foldAgentFact proj fact

    let foldEnvelope (proj: AgentProjectionSet) (env: Envelope) : AgentProjectionSet =
        match env.Fact with
        | Fact.Agent agentFact -> foldAgentFactEnvelope proj env agentFact
        | _ -> proj

    let apply (proj: AgentProjectionSet) (envelopes: Envelope list) : AgentProjectionSet =
        List.fold foldEnvelope proj envelopes
