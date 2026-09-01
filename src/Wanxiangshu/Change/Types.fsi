namespace Wanxiangshu.Change

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

type OrchestratorVerdict =
    | Published of jobId: ManagerJobId * head: CommitHash
    | RejectedDirty of reason: string
    | NeedsReview of jobId: ManagerJobId * reviewDetails: string
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

type ManagerStart =
    { JobId: ManagerJobId
      ManagerAgent: string
      Worktree: WorktreePath
      Prompt: string
      ExpectedToolCalls: int option }

type ManagerPort =
    { StartManager: ManagerStart -> Task<Result<SessionId, string>>
      SendManagerPrompt: ManagerJobId -> Task<Result<unit, string>>
      AwaitManager: ManagerJobId -> Task<Result<unit, string>>
      Reverify: ManagerJobId -> SessionId -> WorktreePath -> ReviewBarrierId -> Task<Result<unit, string>>
      ResumeManager: ManagerJobId -> WorktreePath -> string -> Task<Result<unit, string>>
      TerminateChildren: ManagerJobId -> Task<unit> }

type OrchestratorJournalPort =
    { AppendFact: StreamId -> AgentFact -> Task<Result<ProjectionSet, string>>
      Snapshot: unit -> ProjectionSet }

module OrchestratorJournalPort =
    val fromAgentJournal: journal: AgentJournal -> OrchestratorJournalPort

type PublishGateLease = { Release: unit -> Task<unit> }

type OrchestratorProgramDeps =
    { Git: GitPort
      Manager: ManagerPort
      AppendFact: StreamId -> AgentFact -> Task<Result<unit, string>>
      Snapshot: unit -> ProjectionSet
      AcquirePublishGate: unit -> Task<PublishGateLease> }

module OrchestratorConstants =
    [<Literal>]
    val targetRefMovedError: string = "target ref moved"
