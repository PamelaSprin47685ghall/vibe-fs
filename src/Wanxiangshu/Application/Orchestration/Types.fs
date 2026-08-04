namespace Wanxiangshu.Orchestrator

open System.Threading.Tasks
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal

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
type ManagerStart =
    { JobId: ManagerJobId
      ManagerAgent: string
      Worktree: WorktreePath
      Prompt: string }

/// Manager and reviewer execution, as the Host layer provides it.
///
/// `StartManager` and `AwaitManager` are separate because ORCH-006 requires
/// `ManagerJobCreated` to carry the Manager's `SessionId`, which only exists once
/// the fork has happened. A single combined call could only write that fact after
/// the Manager had already finished — a crash in between would leave a live
/// Manager with no durable job.
type ManagerPort =
    {
        StartManager: ManagerStart -> Task<Result<SessionId, string>>
        AwaitManager: ManagerJobId -> Task<Result<unit, string>>

        /// One review barrier: fork a reviewer, open the barrier, and wait for a
        /// confirmed dual PERFECT on the current tree. REVIEW-008 requires a fresh
        /// barrier per round, so the id is supplied by the caller rather than derived
        /// from the tree.
        Reverify: ManagerJobId -> SessionId -> WorktreePath -> ReviewBarrierId -> Task<Result<unit, string>>

        /// Hand a rebase conflict back to the SAME Manager in the SAME worktree
        /// (ORCH-003/ORCH-007).
        ResumeManager: ManagerJobId -> WorktreePath -> string -> Task<Result<unit, string>>

        /// ORCH-006: abort the Manager's Host session and every reviewer child
        /// session before the worktree is released.  This prevents residual guard
        /// nudges and continuations from building a system prompt against the
        /// deleted worktree after `Published` has been appended.
        TerminateChildren: ManagerJobId -> Task<unit>
    }

type OrchestratorJournalPort =
    { AppendFact: StreamId -> AgentFact -> Result<ProjectionSet, string>
      Snapshot: unit -> ProjectionSet }

module OrchestratorJournalPort =
    let fromAgentJournal (journal: AgentJournal) : OrchestratorJournalPort =
        { AppendFact =
            fun stream fact ->
                match AgentJournal.appendAgent stream None fact journal with
                | Ok projection -> Ok projection
                | Error failure -> Error(JournalAppendFailure.describe failure)
          Snapshot = fun () -> AgentJournal.snapshot journal }

type OrchestratorProgramDeps =
    { Git: GitPort
      Manager: ManagerPort
      AppendFact: StreamId -> AgentFact -> Result<unit, string>
      Snapshot: unit -> ProjectionSet
      GatePath: string }

module OrchestratorConstants =
    /// `FfMerge` reports this when the target advanced between the head read and
    /// the ref update. ORCH-005 turns it into "rebase and review again", never into
    /// a retry that reuses the old post-rebase witness.
    [<Literal>]
    let targetRefMovedError = "target ref moved"
