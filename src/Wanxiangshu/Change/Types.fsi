namespace Wanxiangshu.Change

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Persistence.Journal

type OrchestratorVerdict =
    | Published of jobId: ManagerJobId * head: CommitHash
    | PublishedPendingCleanup of jobId: ManagerJobId * head: CommitHash * cleanupError: string
    | Cancelled of jobId: ManagerJobId
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
      FfMerge: WorktreePath -> TargetRef -> CommitHash -> CommitHash -> Task<Result<CommitHash, string>>
      ConflictedFiles: WorktreePath -> Task<Result<string list, string>>
      RemoveWorktree: WorktreePath -> Task<Result<unit, string>>
      HasRebaseHead: WorktreePath -> Task<bool>
      ListWorktrees: unit -> Task<Result<(WorktreePath * WorktreeIdentity option) list, string>>
      ListManagerBranches: unit -> Task<Result<WorktreeIdentity list, string>>
      DeleteBranch: WorktreeIdentity -> Task<Result<unit, string>>
      ReadHead: WorktreePath -> Task<Result<CommitHash, string>>
      GetTargetHead: TargetRef -> Task<Result<CommitHash, string>> }

type ManagerStart =
    { JobId: ManagerJobId
      ManagerAgent: string
      Worktree: WorktreePath
      RootRequest: string
      ExpectedToolCalls: int option }

[<RequireQualifiedAccess>]
type ManagerLoopSignal =
    | Continue
    | Candidate of QualityCertificate
    | ExceptionalTerminal of string

type RelayPort =
    { CreateManagerSession: ManagerStart -> Task<Result<SessionId, string>>
      ActivateManager: ManagerJobId -> Task<Result<unit, string>>
      AwaitLoopSignal: ManagerJobId -> Task<Result<ManagerLoopSignal, string>>
      InvalidateCertificate: ManagerJobId -> string -> Task<Result<unit, string>>
      ContinueLoop: ManagerJobId -> Task<Result<IncumbencyId, string>>
      CaptureSnapshot: ManagerJobId -> Task<Result<WorkspaceSnapshotId, string>>
      PrepareCandidate: ManagerJobId -> Task<Result<CommitHash, string>>
      TerminateRoadResources: ManagerJobId -> Task<unit> }

type OrchestratorJournalPort =
    { AppendFact: StreamId -> AgentFact -> Task<Result<ProjectionSet, string>>
      Snapshot: unit -> ProjectionSet }

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
