namespace Wanxiangshu.Next.Journal

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
      AcceptedGuardKeys: Set<string>
      RecentToolCallIds: string list }

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
