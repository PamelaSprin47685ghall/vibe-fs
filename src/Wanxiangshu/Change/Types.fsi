namespace Wanxiangshu.Change

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Persistence.Journal

type OrchestratorVerdict =
    | Published of jobId: ManagerJobId * head: CommitHash
    | RejectedDirty of reason: string
    | IntegrationFailed of jobId: ManagerJobId * errorDetails: string
    | Empty

type OrchestratorHandle =
    { JobId: ManagerJobId
      WorktreePath: WorktreePath }

type GitPort =
    { IsDirty: WorktreePath -> Task<bool>
      CreateWorktree: ManagerJobId -> WorktreePath -> Task<Result<WorktreeIdentity, string>>
      FreezeTargetBranch: unit -> Task<Result<TargetRef, string>>
      Rebase: WorktreePath -> TargetRef -> Task<Result<unit, string>>
      FfMerge: WorktreePath -> TargetRef -> CommitHash -> Task<Result<CommitHash, string>>
      ConflictedFiles: WorktreePath -> Task<Result<string list, string>>
      RemoveWorktree: WorktreePath -> Task<Result<unit, string>>
      HasRebaseHead: WorktreePath -> Task<bool>
      ListWorktrees: unit -> Task<Result<(WorktreePath * WorktreeIdentity option) list, string>>
      ListManagerBranches: unit -> Task<Result<WorktreeIdentity list, string>>
      DeleteBranch: WorktreeIdentity -> Task<Result<unit, string>>
      ReadHead: WorktreePath -> Task<Result<CommitHash, string>>
      GetTargetHead: TargetRef -> Task<Result<CommitHash, string>> }

type RoadStart =
    { JobId: ManagerJobId
      ManagerAgent: string
      Worktree: WorktreePath
      RootRequest: string
      ExpectedToolCalls: int option }

[<RequireQualifiedAccess>]
type RoadSignal =
    | IncumbencyRetired of RetirementSummary
    | QualityCandidateAccepted of RetirementSummary * QualityCertificate
    | ExceptionalTerminal of string

type RelayPort =
    { OpenRoad: RoadStart -> Task<Result<SessionId, string>>
      ActivateRoad: ManagerJobId -> Task<Result<unit, string>>
      AwaitRoadSignal: ManagerJobId -> Task<Result<RoadSignal, string>>
      InvalidateCertificate: ManagerJobId -> string -> Task<Result<unit, string>>
      RequestSuccessor: ManagerJobId -> WorktreePath -> string -> Task<Result<IncumbencyId, string>>
      CaptureSnapshot: ManagerJobId -> Task<Result<WorkspaceSnapshotId, string>>
      PrepareCandidate: ManagerJobId -> Task<Result<CommitHash, string>>
      TerminateRoadResources: ManagerJobId -> Task<unit> }

type OrchestratorJournalPort =
    { AppendFact: StreamId -> AgentFact -> Task<Result<ProjectionSet, string>>
      Snapshot: unit -> ProjectionSet }

module OrchestratorJournalPort =
    val fromAgentJournal: journal: AgentJournal -> OrchestratorJournalPort

type PublishGateLease = { Release: unit -> Task<unit> }

type OrchestratorProgramDeps =
    { Git: GitPort
      Relay: RelayPort
      AppendFact: StreamId -> AgentFact -> Task<Result<unit, string>>
      Snapshot: unit -> ProjectionSet
      AcquirePublishGate: unit -> Task<PublishGateLease> }

module OrchestratorConstants =
    [<Literal>]
    val targetRefMovedError: string = "target ref moved"
