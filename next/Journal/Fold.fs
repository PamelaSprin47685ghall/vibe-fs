namespace Wanxiangshu.Next.Journal

open System
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity

/// Pure envelope dispatch. Each bounded projection owns its own fold algorithm.
module Fold =

    let empty: ProjectionSet =
        { AgentProjections = AgentProjection.empty
          RuntimeId = None }

    let private reviewOwner target projection =
        let child = ChildId.create (SessionId.value target)

        projection.Sessions
        |> Map.tryPick (fun parentId session ->
            session.Linkage
            |> Option.bind (fun linkage ->
                if Map.containsKey child linkage.LinkedChildren then Some parentId else None))
        |> Option.defaultValue target

    let foldAgentFact (projection: AgentProjectionSet) (fact: AgentFact) =
        match fact with
        | AgentFact.AgentLinked p ->
            AgentProjection.update
                p.ParentId
                (fun session ->
                    { session with
                        Linkage = Some(LinkageProjection.link p.ChildId p.TargetAgent p.Role session.Linkage) })
                projection
        | AgentFact.AgentForked p ->
            AgentProjection.update
                p.ParentId
                (fun session ->
                    { session with
                        Linkage = Some(LinkageProjection.fork p.ChildId p.TargetAgent p.Role session.Linkage) })
                projection
        | AgentFact.AgentUnlinked p ->
            AgentProjection.update
                p.ParentId
                (fun session ->
                    { session with Linkage = Some(LinkageProjection.unlink p.ChildId session.Linkage) })
                projection
        | AgentFact.CompanionBaselineSet p ->
            AgentProjection.update
                p.SessionId
                (fun session ->
                    { session with Companion = Some(CompanionProjection.baseline p.Projection session.Companion) })
                projection
        | AgentFact.CompanionCheckpointReplaced p ->
            AgentProjection.update
                p.SessionId
                (fun session ->
                    { session with Companion = Some(CompanionProjection.checkpoint p.Content session.Companion) })
                projection
        | AgentFact.CompanionAdvanced p ->
            AgentProjection.update
                p.SessionId
                (fun session ->
                    { session with
                        Companion = Some(CompanionProjection.advance p.Projection p.Content session.Companion) })
                projection
        | AgentFact.CompanionEpochSwitched p ->
            AgentProjection.update
                p.SessionId
                (fun session ->
                    { session with
                        Companion =
                            Some(
                                CompanionProjection.switchEpoch
                                    p.EpochId
                                    p.FrozenB
                                    p.CutoffMessageIndex
                                    p.CoveredPrefixDigest
                                    session.Companion
                            ) })
                projection
        | AgentFact.CompanionReplacementActiveSet p ->
            AgentProjection.update
                p.SessionId
                (fun session ->
                    { session with
                        Companion = Some(CompanionProjection.setReplacement p.Active session.Companion) })
                projection
        | AgentFact.ReviewBarrierStarted p ->
            AgentProjection.update
                p.ManagerSessionId
                (fun session ->
                    { session with
                        ReviewGuard = Some(ReviewProjection.startBarrier p.BarrierKey session.ReviewGuard) })
                projection
        | AgentFact.ReviewVerdictRecorded p ->
            let existing =
                Map.tryFind p.ManagerSessionId projection.Sessions
                |> Option.bind (fun session -> session.ReviewGuard)

            let secondPerfect =
                existing
                |> Option.exists (fun guard ->
                    ReviewConfirmation.isSecondPerfectConfirmed
                        projection
                        guard
                        p.ReviewerSessionId
                        p.ProviderRunId
                        p.UserMessageId)

            AgentProjection.update
                p.ManagerSessionId
                (fun session ->
                    { session with
                        ReviewGuard = Some(ReviewProjection.recordVerdict secondPerfect p session.ReviewGuard) })
                projection
        | AgentFact.GuardPromptAccepted p ->
            let owner = reviewOwner p.TargetSessionId projection
            let hostMessageId =
                if String.IsNullOrWhiteSpace p.HostMessageId then None else Some p.HostMessageId

            let isConfirmation =
                Map.tryFind owner projection.Sessions
                |> Option.bind (fun session -> session.ReviewGuard)
                |> Option.exists (fun guard -> ReviewWitness.isPerfectPending guard.Witness)

            let apply session =
                { session with
                    ReviewGuard =
                        Some(
                            ReviewProjection.acceptGuard
                                p.GuardKey
                                hostMessageId
                                isConfirmation
                                session.ReviewGuard
                        ) }

            let withOwner = AgentProjection.update owner apply projection
            if owner = p.TargetSessionId then withOwner else AgentProjection.update p.TargetSessionId apply withOwner
        | AgentFact.HumanPromptAccepted p ->
            AgentProjection.update
                p.SessionId
                (fun session ->
                    { session with
                        ReviewRequirements =
                            Some(
                                ReviewProjection.addRequirement
                                    p.SourceSessionId
                                    p.MessageId
                                    session.ReviewRequirements
                            ) })
                projection
        | AgentFact.ReviewConfirmedIdle p ->
            AgentProjection.update
                p.SessionId
                (fun session ->
                    { session with
                        ReviewRequirements =
                            Some(
                                ReviewProjection.confirmIdle
                                    p.AssistantMessageId
                                    session.ReviewRequirements
                            ) })
                projection
        | AgentFact.FallbackCursorAdvanced p ->
            AgentProjection.update
                p.SessionId
                (fun session ->
                    { session with
                        Fallback =
                            Some(
                                FallbackProjection.recordRetry
                                    p.LogicalRunId
                                    p.AuthorityRootUserMessageId
                                    p.ProviderAttempt
                                    session.Fallback
                            ) })
                projection
        | AgentFact.AuthorityRootAccepted p ->
            AgentProjection.update
                p.SessionId
                (fun session ->
                    { session with
                        PromptAuthority = Some(AuthorityProjection.acceptRoot session.PromptAuthority p)
                        Fallback = Some(FallbackProjection.forAuthority p.LogicalRunId p.HostMessageId) })
                projection
        | AgentFact.PluginPromptClaimed p ->
            AgentProjection.update
                p.SessionId
                (fun session ->
                    { session with PromptAuthority = Some(AuthorityProjection.claim session.PromptAuthority p) })
                projection
        | AgentFact.PluginPromptAccepted p ->
            AgentProjection.update
                p.SessionId
                (fun session ->
                    { session with PromptAuthority = Some(AuthorityProjection.accept session.PromptAuthority p) })
                projection
        | AgentFact.PluginPromptAbandoned p ->
            AgentProjection.update
                p.SessionId
                (fun session ->
                    { session with PromptAuthority = Some(AuthorityProjection.abandon session.PromptAuthority p) })
                projection
        | AgentFact.InteractionRepairClaimed p ->
            AgentProjection.update
                p.SessionId
                (fun session ->
                    { session with PromptAuthority = Some(AuthorityProjection.claimRepair session.PromptAuthority p) })
                projection
        | AgentFact.OrchestratorManagerJobCreated p ->
            { projection with
                Orchestrator =
                    OrchestratorProjection.createManager
                        p.ManagerId
                        p.WorktreePath
                        p.Branch
                        p.Prompt
                        projection.Orchestrator }
        | AgentFact.OrchestratorCandidateRegistered p ->
            { projection with
                Orchestrator =
                    OrchestratorProjection.registerCandidate
                        p.ManagerId
                        p.CandidateId
                        p.Branch
                        p.CommitHash
                        projection.Orchestrator }
        | AgentFact.OrchestratorPublished p ->
            { projection with
                Orchestrator = OrchestratorProjection.published p.ManagerId p.CommitHash projection.Orchestrator }
        | AgentFact.OrchestratorRejected p ->
            { projection with Orchestrator = OrchestratorProjection.rejected p.ManagerId projection.Orchestrator }
        | AgentFact.OrchestratorPreRebaseReviewConfirmed p ->
            { projection with
                Orchestrator =
                    OrchestratorProjection.preRebaseReviewed p.ManagerId p.CommitHash projection.Orchestrator }
        | AgentFact.OrchestratorRebased p ->
            { projection with
                Orchestrator = OrchestratorProjection.rebased p.ManagerId p.RebasedCommit projection.Orchestrator }
        | AgentFact.OrchestratorConflictDetected p ->
            { projection with
                Orchestrator = OrchestratorProjection.conflict p.ManagerId p.Files projection.Orchestrator }
        | AgentFact.OrchestratorPostRebaseReviewConfirmed p ->
            { projection with
                Orchestrator =
                    OrchestratorProjection.postRebaseReviewed p.ManagerId p.RebasedCommit projection.Orchestrator }
        | AgentFact.OrchestratorPublishClaimed p ->
            { projection with
                Orchestrator =
                    OrchestratorProjection.claimed p.ManagerId p.ExpectedTargetHead projection.Orchestrator }
        | AgentFact.DurableEffectRequested p ->
            AgentProjection.update
                p.SessionId
                (fun session ->
                    { session with Effects = Some(EffectProjection.request p.EffectId p.Target p.Payload session.Effects) })
                projection
        | AgentFact.DurableEffectAccepted p ->
            AgentProjection.update
                p.SessionId
                (fun session ->
                    { session with Effects = Some(EffectProjection.accept p.EffectId p.Result session.Effects) })
                projection

    let foldAgentEnvelope projection envelope =
        match envelope.Fact with
        | Fact.Agent fact -> foldAgentFact projection fact
        | _ -> projection

    let applyAgentFacts projection envelopes = List.fold foldAgentEnvelope projection envelopes

    let foldEnvelope (projection: ProjectionSet) (envelope: Envelope) =
        match envelope.Fact with
        | Runtime(RuntimeStarted runtime) -> { projection with RuntimeId = Some runtime.RuntimeId }
        | Agent fact ->
            { projection with
                AgentProjections = foldAgentFact projection.AgentProjections fact }

    let apply projection envelopes = List.fold foldEnvelope projection envelopes
