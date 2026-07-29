namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Orchestrator
open Wanxiangshu.Next.Session

/// Durable ManagerJobProjection lifecycle helpers for OrchestratorHost: review-barrier
/// emission plus lazy journal recovery and conflict-resumption prompt building
/// for persisted ManagerJobProjections.
module OrchestratorManagerJob =
    /// Open a review barrier so the phase requires two FRESH PERFECT verdicts on
    /// the current tree (REVIEW-008).
    ///
    /// SHOCK-UNMIGRATED[REVIEW-008]: `ReviewBarrierStarted` now carries
    /// `ReviewerSessionId`, `BarrierId` and `GitTreeHash`, and the fold keys
    /// `ReviewGuardProjection` by the reviewer session. This call site has none of
    /// the three: it runs BEFORE `runReviewerOnce` forks the reviewer, so no
    /// reviewer session exists yet, and it receives an opaque `barrierKey` string
    /// instead of a `ReviewBarrierId` and tree.
    ///
    /// Not an implementation difficulty — an ordering contradiction. A barrier is
    /// opened for a (job, tree) before any reviewer exists, so it cannot be keyed
    /// by a reviewer session at emission time. Two coherent resolutions exist and
    /// both change code this package does not own:
    ///
    ///   1. Emit the barrier from the reviewer fork path, once the child session
    ///      id exists. A fresh reviewer session per barrier also makes REVIEW-008's
    ///      "fresh dual PERFECT" automatic, since its guard starts empty.
    ///   2. Key `ReviewGuardProjection` by the review owner instead, reverting the
    ///      per-reviewer keying that removed a full-scan parent lookup.
    ///
    /// `reverify` belongs to the Orchestrator package, so the choice is made there.
    let emitReviewBarrier
        (_journal: AgentJournal option)
        (_reviewOwnerSessionId: SessionId)
        (_barrierKey: string)
        : Task<Result<unit, string>> =
        failwith
            "SHOCK-UNMIGRATED[REVIEW-008]: a review barrier cannot be keyed by a reviewer session that does not exist yet"

    /// Build the recovery prompt for a manager job: conflict-resumption prompt
    /// when REBASE_HEAD exists and no candidate, otherwise the original prompt.
    let recoveryPrompt (gitPort: GitPort) (job: ManagerJobProjection) : Task<string> =
        task {
            let! hasRb = gitPort.HasRebaseHead job.WorktreePath

            if not job.CandidateCommit.IsSome && hasRb then
                let! conflicted = gitPort.ConflictedFiles job.WorktreePath

                match conflicted with
                | Error err -> return sprintf "[RECOVERY BLOCKED] unable to read rebase conflicts: %s" err
                | Ok files -> return OrchestratorPrompts.buildConflictResumePrompt job.Prompt files
            else
                return job.Prompt
        }

    /// Reconcile and recover durable ManagerJobProjections from the journal into a freshly
    /// built Orchestrator. Runs only during on-demand engine initialization
    /// (lazy engine load): it persists no Task/handle/phase and performs no
    /// boot-time scan — recovery is the idempotent publish-chain re-run.
    let recoverJobs
        (journal: AgentJournal)
        (gitPort: GitPort)
        (orchestratorId: SessionId)
        (worktrees: Dictionary<string, string>)
        (registerChildDirectory: SessionId -> string -> unit)
        (registerReviewerTree: string -> GitTreePort -> unit)
        (engine: Orchestrator)
        : Task<unit> =
        task {
            let snapshot = AgentJournal.snapshot journal
            let jobs = snapshot.AgentProjections.Orchestrator.ManagerJobs

            for KeyValue(managerId, job) in jobs do
                let id = ManagerId.value managerId
                worktrees.[id] <- job.WorktreePath
                worktrees.[sprintf "%s-reviewer" id] <- job.WorktreePath

            OrchestratorSessionDirectories.registerRestored
                snapshot
                orchestratorId
                worktrees
                registerChildDirectory
                registerReviewerTree

            for KeyValue(managerId, job) in jobs do
                let id = ManagerId.value managerId
                let! prompt = recoveryPrompt gitPort job
                engine.RecoverManagerJob(id, job.WorktreePath, prompt, job.CandidateCommit.IsSome)
        }
