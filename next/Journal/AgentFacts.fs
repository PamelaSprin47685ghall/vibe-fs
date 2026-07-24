namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open AgentFactsFoldHelpers

module AgentFacts =

    let emptyCompanion: CompanionProjection =
        { LastSuccessfulProjection = None
          CurrentB = None
          ReplacementActive = false }

    let emptyLinkage: AgentLinkageProjection = { LinkedChildren = Map.empty }

    let emptyReviewGuard: ReviewGuardProjection =
        { LastGitTreeHash = None
          ConsecutivePerfects = 0
          IsConfirmed = false
          AcceptedGuardKeys = Set.empty }

    let emptyFallback: FallbackProjection =
        { Side = SideA
          FailuresOnCurrentSide = 0
          TotalFailures = 0
          IsDead = false }

    let emptyOrchestrator: OrchestratorProjection =
        { Managers = Map.empty
          PublishedCommits = [] }

    let emptyEffects: DurableEffectProjection = { Effects = Map.empty }

    let emptySessionProjection: SessionAgentProjection =
        { Companion = None
          Linkage = None
          ReviewGuard = None
          Fallback = None
          Effects = None }

    let empty: AgentProjectionSet =
        { Sessions = Map.empty
          Orchestrator = emptyOrchestrator }

    let foldAgentFact (proj: AgentProjectionSet) (fact: AgentFact) : AgentProjectionSet =
        match fact with
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
                                  CurrentB = None
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
                                    CurrentB = Some p.Content }
                            | None ->
                                { LastSuccessfulProjection = None
                                  CurrentB = Some p.Content
                                  ReplacementActive = false }

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
                                  CurrentB = None
                                  ReplacementActive = p.Active }

                        { s with Companion = Some comp })
                    proj.Sessions

            { proj with Sessions = sessions }

        | AgentFact.AgentLinked p ->
            let sessions =
                updateSession
                    p.ParentId
                    (fun s ->
                        let link =
                            match s.Linkage with
                            | Some existing ->
                                { LinkedChildren = Map.add p.ChildId p.TargetAgent existing.LinkedChildren }
                            | None -> { LinkedChildren = Map.ofList [ (p.ChildId, p.TargetAgent) ] }

                        { s with Linkage = Some link })
                    proj.Sessions

            { proj with Sessions = sessions }

        | AgentFact.AgentUnlinked p ->
            let sessions =
                updateSession
                    p.ParentId
                    (fun s ->
                        let link =
                            match s.Linkage with
                            | Some existing -> { LinkedChildren = Map.remove p.ChildId existing.LinkedChildren }
                            | None -> emptyLinkage

                        { s with Linkage = Some link })
                    proj.Sessions

            { proj with Sessions = sessions }

        | AgentFact.ReviewVerdictRecorded p ->
            let hash = GitTreeHash.create p.GitTreeHash

            let sessions =
                updateSession
                    p.ManagerSessionId
                    (fun s ->
                        let rg =
                            match s.ReviewGuard with
                            | Some existing ->
                                match existing.LastGitTreeHash with
                                | Some lastHash when lastHash = hash ->
                                    match p.Verdict with
                                    | ReviewGuardVerdict.Perfect ->
                                        let count = existing.ConsecutivePerfects + 1

                                        { existing with
                                            LastGitTreeHash = Some hash
                                            ConsecutivePerfects = count
                                            IsConfirmed = count >= 2 }
                                    | ReviewGuardVerdict.Revise ->
                                        { existing with
                                            LastGitTreeHash = Some hash
                                            ConsecutivePerfects = 0
                                            IsConfirmed = false }
                                | _ ->
                                    match p.Verdict with
                                    | ReviewGuardVerdict.Perfect ->
                                        { existing with
                                            LastGitTreeHash = Some hash
                                            ConsecutivePerfects = 1
                                            IsConfirmed = false }
                                    | ReviewGuardVerdict.Revise ->
                                        { existing with
                                            LastGitTreeHash = Some hash
                                            ConsecutivePerfects = 0
                                            IsConfirmed = false }
                            | None ->
                                match p.Verdict with
                                | ReviewGuardVerdict.Perfect ->
                                    { LastGitTreeHash = Some hash
                                      ConsecutivePerfects = 1
                                      IsConfirmed = false
                                      AcceptedGuardKeys = Set.empty }
                                | ReviewGuardVerdict.Revise ->
                                    { LastGitTreeHash = Some hash
                                      ConsecutivePerfects = 0
                                      IsConfirmed = false
                                      AcceptedGuardKeys = Set.empty }

                        { s with ReviewGuard = Some rg })
                    proj.Sessions

            { proj with Sessions = sessions }

        | AgentFact.GuardPromptAccepted p ->
            let sessions =
                updateSession
                    p.TargetSessionId
                    (fun s ->
                        let rg =
                            match s.ReviewGuard with
                            | Some existing ->
                                { existing with
                                    AcceptedGuardKeys = Set.add p.GuardKey existing.AcceptedGuardKeys }
                            | None ->
                                { LastGitTreeHash = None
                                  ConsecutivePerfects = 0
                                  IsConfirmed = false
                                  AcceptedGuardKeys = Set.singleton p.GuardKey }

                        { s with ReviewGuard = Some rg })
                    proj.Sessions

            { proj with Sessions = sessions }

        | AgentFact.FallbackFailureRecorded p ->
            let sessions =
                updateSession
                    p.SessionId
                    (fun s ->
                        let fb =
                            match s.Fallback with
                            | Some existing ->
                                if existing.IsDead then
                                    existing
                                else
                                    let newTotal = existing.TotalFailures + 1

                                    match existing.Side with
                                    | SideA ->
                                        if existing.FailuresOnCurrentSide < 1 then
                                            { Side = SideA
                                              FailuresOnCurrentSide = existing.FailuresOnCurrentSide + 1
                                              TotalFailures = newTotal
                                              IsDead = false }
                                        else
                                            { Side = SideB
                                              FailuresOnCurrentSide = 0
                                              TotalFailures = newTotal
                                              IsDead = false }
                                    | SideB ->
                                        if existing.FailuresOnCurrentSide < 1 then
                                            { Side = SideB
                                              FailuresOnCurrentSide = existing.FailuresOnCurrentSide + 1
                                              TotalFailures = newTotal
                                              IsDead = false }
                                        else
                                            { Side = SideB
                                              FailuresOnCurrentSide = 2
                                              TotalFailures = newTotal
                                              IsDead = true }
                            | None ->
                                { Side = SideA
                                  FailuresOnCurrentSide = 1
                                  TotalFailures = 1
                                  IsDead = false }

                        { s with Fallback = Some fb })
                    proj.Sessions

            { proj with Sessions = sessions }

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

        | AgentFact.DurableEffectRequested p ->
            AgentFactsFoldHelpers.foldDurableEffectRequested proj p.SessionId p.EffectId p.Target p.Payload

        | AgentFact.DurableEffectAccepted p ->
            AgentFactsFoldHelpers.foldDurableEffectAccepted proj p.SessionId p.EffectId p.Result

    let foldEnvelope (proj: AgentProjectionSet) (env: Envelope) : AgentProjectionSet =
        match env.Fact with
        | Fact.Agent agentFact -> foldAgentFact proj agentFact
        | _ -> proj

    let apply (proj: AgentProjectionSet) (envelopes: Envelope list) : AgentProjectionSet =
        List.fold foldEnvelope proj envelopes
