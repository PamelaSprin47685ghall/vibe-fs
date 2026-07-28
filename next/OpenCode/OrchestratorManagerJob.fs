namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Orchestrator
open Wanxiangshu.Next.Session

/// Durable ManagerJob lifecycle helpers for OrchestratorHost: review-barrier
/// emission plus lazy journal recovery and conflict-resumption prompt building
/// for persisted ManagerJobs.
module OrchestratorManagerJob =
    /// Emit a ReviewBarrierStarted fact that resets the review guard so the
    /// phase requires two FRESH PERFECT verdicts on the current tree.
    let emitReviewBarrier
        (journal: AgentJournal option)
        (reviewOwnerSessionId: SessionId)
        (barrierKey: string)
        : Task<Result<unit, string>> =
        task {
            match journal with
            | Some j ->
                // reviewOwnerSessionId must be the durable session that receives
                // ReviewVerdictRecorded for this barrier (the reviewer's parent
                // in sessionParents / linkage). For OrchestratorHost reverify
                // that is the Orchestrator session; for Manager-owned reviewers
                // it is the Manager session.
                let fact =
                    AgentFact.ReviewBarrierStarted
                        {| ManagerSessionId = reviewOwnerSessionId
                           BarrierKey = barrierKey |}

                match AgentJournal.appendAgent (StreamId.Session reviewOwnerSessionId) None fact j with
                | Ok _ -> return Ok()
                | Error failure -> return Error(sprintf "%A" failure.Failure)
            | None -> return Ok()
        }

    /// Build the recovery prompt for a manager job: conflict-resumption prompt
    /// when REBASE_HEAD exists and no candidate, otherwise the original prompt.
    let recoveryPrompt (gitPort: GitPort) (job: ManagerJob) : Task<string> =
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

    /// Reconcile and recover durable ManagerJobs from the journal into a freshly
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
