namespace Wanxiangshu.Next.Journal

open System
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact

type ManagerId = private ManagerId of string

module ManagerId =
    let create (value: string) = ManagerId value
    let value (ManagerId v) = v

type GitTreeHash = private GitTreeHash of string

module GitTreeHash =
    let create (value: string) = GitTreeHash value
    let value (GitTreeHash v) = v

type EffectId = private EffectId of string

module EffectId =
    let create (value: string) = EffectId value
    let value (EffectId v) = v

type CandidateId = private CandidateId of string

module CandidateId =
    let create (value: string) = CandidateId value
    let value (CandidateId v) = v

type ProjectionSnapshot = string
type BlogText = string

type CompanionProjection =
    { LastSuccessfulProjection: ProjectionSnapshot option
      CurrentB: BlogText option
      ReplacementActive: bool }

type AgentLinkageProjection =
    { LinkedChildren: Map<ChildId, string> }

type ReviewGuardProjection =
    { LastGitTreeHash: GitTreeHash option
      ConsecutivePerfects: int
      IsConfirmed: bool
      AcceptedGuardKeys: Set<string> }

type ModelSide =
    | SideA
    | SideB

type FallbackProjection =
    { Side: ModelSide
      FailuresOnCurrentSide: int
      TotalFailures: int
      IsDead: bool }

type CandidateStatus =
    | Registered of candidateId: CandidateId * branch: string * commitHash: string
    | Published of candidateId: CandidateId * commitHash: string
    | Rejected of candidateId: CandidateId * reason: string

type ManagerState =
    { Status: CandidateStatus option
      History: CandidateStatus list }

type OrchestratorProjection =
    { Managers: Map<ManagerId, ManagerState>
      PublishedCommits: string list }

type EffectStatus =
    | Requested of target: string * payload: string
    | Accepted of target: string * payload: string * result: string

type DurableEffectProjection =
    { Effects: Map<EffectId, EffectStatus> }

type SessionAgentProjection =
    { Companion: CompanionProjection option
      Linkage: AgentLinkageProjection option
      ReviewGuard: ReviewGuardProjection option
      Fallback: FallbackProjection option
      Effects: DurableEffectProjection option }

type AgentProjectionSet =
    { Sessions: Map<SessionId, SessionAgentProjection>
      Orchestrator: OrchestratorProjection }

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

    let private updateSession
        (sessionId: SessionId)
        (updateFn: SessionAgentProjection -> SessionAgentProjection)
        (map: Map<SessionId, SessionAgentProjection>)
        =
        let existing =
            match Map.tryFind sessionId map with
            | Some s -> s
            | None -> emptySessionProjection

        Map.add sessionId (updateFn existing) map

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
            let mgrId = ManagerId.create p.ManagerId
            let candId = CandidateId.create p.CandidateId
            let status = Registered(candId, p.Branch, p.CommitHash)

            let existingMgr =
                match Map.tryFind mgrId proj.Orchestrator.Managers with
                | Some m ->
                    { Status = Some status
                      History = status :: m.History }
                | None ->
                    { Status = Some status
                      History = [ status ] }

            let orch =
                { proj.Orchestrator with
                    Managers = Map.add mgrId existingMgr proj.Orchestrator.Managers }

            { proj with Orchestrator = orch }

        | AgentFact.OrchestratorPublished p ->
            let mgrId = ManagerId.create p.ManagerId
            let candId = CandidateId.create p.CandidateId
            let status = Published(candId, p.CommitHash)

            let existingMgr =
                match Map.tryFind mgrId proj.Orchestrator.Managers with
                | Some m ->
                    { Status = Some status
                      History = status :: m.History }
                | None ->
                    { Status = Some status
                      History = [ status ] }

            let published = proj.Orchestrator.PublishedCommits @ [ p.CommitHash ]

            let orch =
                { Managers = Map.add mgrId existingMgr proj.Orchestrator.Managers
                  PublishedCommits = published }

            { proj with Orchestrator = orch }

        | AgentFact.OrchestratorRejected p ->
            let mgrId = ManagerId.create p.ManagerId
            let candId = CandidateId.create p.CandidateId
            let status = Rejected(candId, p.Reason)

            let existingMgr =
                match Map.tryFind mgrId proj.Orchestrator.Managers with
                | Some m ->
                    { Status = Some status
                      History = status :: m.History }
                | None ->
                    { Status = Some status
                      History = [ status ] }

            let orch =
                { proj.Orchestrator with
                    Managers = Map.add mgrId existingMgr proj.Orchestrator.Managers }

            { proj with Orchestrator = orch }

        | AgentFact.DurableEffectRequested p ->
            let effId = EffectId.create p.EffectId

            let sessions =
                updateSession
                    p.SessionId
                    (fun s ->
                        let effs =
                            match s.Effects with
                            | Some existing ->
                                { Effects = Map.add effId (Requested(p.Target, p.Payload)) existing.Effects }
                            | None -> { Effects = Map.ofList [ (effId, Requested(p.Target, p.Payload)) ] }

                        { s with Effects = Some effs })
                    proj.Sessions

            { proj with Sessions = sessions }

        | AgentFact.DurableEffectAccepted p ->
            let effId = EffectId.create p.EffectId

            let sessions =
                updateSession
                    p.SessionId
                    (fun s ->
                        let effs =
                            match s.Effects with
                            | Some existing ->
                                let updated =
                                    match Map.tryFind effId existing.Effects with
                                    | Some(Requested(target, payload)) -> Accepted(target, payload, p.Result)
                                    | Some(Accepted(target, payload, _)) -> Accepted(target, payload, p.Result)
                                    | None -> Accepted("", "", p.Result)

                                { Effects = Map.add effId updated existing.Effects }
                            | None -> { Effects = Map.ofList [ (effId, Accepted("", "", p.Result)) ] }

                        { s with Effects = Some effs })
                    proj.Sessions

            { proj with Sessions = sessions }

    let foldEnvelope (proj: AgentProjectionSet) (env: Envelope) : AgentProjectionSet =
        match env.Fact with
        | Fact.Agent agentFact -> foldAgentFact proj agentFact
        | _ -> proj

    let apply (proj: AgentProjectionSet) (envelopes: Envelope list) : AgentProjectionSet =
        List.fold foldEnvelope proj envelopes
