namespace Wanxiangshu.Change

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Persistence.Journal

/// What one ManagerJob's publication attempt resolved to.
///
/// Typed ids throughout. These used to be bare `string` manager ids, which made
/// `ManagerJobId`, the Manager's Host `SessionId`, and the reviewer's agent id
/// (built as `<managerId>-reviewer`) all the same type — so a function taking two
/// of them accepted them in either order.
type OrchestratorVerdict =
    | Published of jobId: ManagerJobId * head: CommitHash
    | RejectedDirty of reason: string
    | NeedsReview of jobId: ManagerJobId * reviewDetails: string
    | IntegrationFailed of jobId: ManagerJobId * errorDetails: string
    | Empty

type OrchestratorHandle =
    { JobId: ManagerJobId
      WorktreePath: WorktreePath }

/// Typed Git verbs.
///
/// `repoPath` is baked in at construction, so no verb takes it: passing it
/// per-call let a caller address a different repository than the one the port was
/// built for, and ORCH-008's frozen target branch belongs to exactly one repo.
type GitPort =
    {
        IsDirty: WorktreePath -> Task<bool>

        /// Creates the worktree and returns its stable identity (ORCH-006).
        /// Recovery locates a worktree by identity; the path is diagnostic and may
        /// move.
        CreateWorktree: ManagerJobId -> WorktreePath -> Task<Result<WorktreeIdentity, string>>

        /// ORCH-008: freeze the target branch by `symbolic-ref` at fork time.
        ///
        /// A separate verb from `GetTargetHead` because they answer different
        /// questions — which ref, versus where that ref points. Reading HEAD when
        /// the ref cannot be resolved is the fallback ORCH-008 forbids, and a single
        /// combined verb makes that fallback one line away.
        FreezeTargetBranch: unit -> Task<Result<TargetRef, string>>

        Rebase: WorktreePath -> TargetRef -> Task<Result<unit, string>>

        /// ff-only publish with a mandatory CAS expectation (ORCH-005).
        ///
        /// `expectedHead` is not optional: every publish happens inside the short
        /// gate against a head that was just read. An optional expectation made
        /// "publish without checking" expressible, and that is the lost-update the
        /// gate exists to prevent.
        FfMerge: WorktreePath -> TargetRef -> CommitHash -> Task<Result<CommitHash, string>>

        ConflictedFiles: WorktreePath -> Task<Result<string list, string>>
        RemoveWorktree: WorktreePath -> Task<Result<unit, string>>
        HasRebaseHead: WorktreePath -> Task<bool>
        ListWorktrees: unit -> Task<Result<(WorktreePath * WorktreeIdentity option) list, string>>
        ListManagerBranches: unit -> Task<Result<WorktreeIdentity list, string>>
        DeleteBranch: WorktreeIdentity -> Task<Result<unit, string>>
        ReadHead: WorktreePath -> Task<Result<CommitHash, string>>
        GetTargetHead: TargetRef -> Task<Result<CommitHash, string>>
    }

/// Everything a Manager fork needs, as one value.
///
/// A record, not four positional arguments: `ManagerAgent` and `Prompt` are both
/// `string` and adjacent, which is exactly where positional arguments get swapped —
/// and a swapped pair would fork an agent named after the task text.
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
      InvalidateCertificate: ManagerJobId -> reason: string -> Task<Result<unit, string>>
      RequestSuccessor: ManagerJobId -> WorktreePath -> reason: string -> Task<Result<IncumbencyId, string>>
      CaptureSnapshot: ManagerJobId -> Task<Result<WorkspaceSnapshotId, string>>
      PrepareCandidate: ManagerJobId -> Task<Result<CommitHash, string>>
      TerminateRoadResources: ManagerJobId -> Task<unit> }

type OrchestratorJournalPort =
    { AppendFact: StreamId -> AgentFact -> Task<Result<ProjectionSet, string>>
      Snapshot: unit -> ProjectionSet }

module OrchestratorJournalPort =
    let fromAgentJournal (journal: AgentJournal) : OrchestratorJournalPort =
        { AppendFact =
            fun stream fact ->
                task {
                    match! AgentJournal.appendAgent stream None fact journal with
                    | Ok projection -> return Ok projection
                    | Error failure -> return Error(JournalAppendFailure.describe failure)
                }
          Snapshot = fun () -> AgentJournal.snapshot journal }

type PublishGateLease = { Release: unit -> Task<unit> }

type OrchestratorProgramDeps =
    { Git: GitPort
      Relay: RelayPort
      AppendFact: StreamId -> AgentFact -> Task<Result<unit, string>>
      Snapshot: unit -> ProjectionSet
      AcquirePublishGate: unit -> Task<PublishGateLease> }

module OrchestratorConstants =
    /// `FfMerge` reports this when the target advanced between the head read and
    /// the ref update. ORCH-005 turns it into "rebase and review again", never into
    /// a retry that reuses the old post-rebase witness.
    [<Literal>]
    let targetRefMovedError = "target ref moved"
