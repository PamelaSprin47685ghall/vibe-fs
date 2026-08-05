namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.Orchestrator
open Wanxiangshu.Session

/// Lazy journal recovery for persisted ManagerJobs.
///
/// RECOVERY-FAMILY: caller must hold FamilyRecoveryPermit for the orchestrator
/// session before starting publication programs. This module only registers
/// worktree directories and hands each active job to RecoverManagerJob.
///
/// No recovery prompt is built here. ORCH-006 does not persist the Manager's prompt,
/// and ORCH-007 decides the resume action from the last durable fact.
module OrchestratorManagerJob =

    /// Register worktree directories for a job's Manager and its reviewer.
    ///
    /// The reviewer's runtime agent id is `<job>-reviewer`, matching
    /// `OrchestratorHost.runReviewerOnce`. Spelled through `reviewerAgentId` so the
    /// two cannot drift while both still compile.
    let reviewerAgentId (jobId: ManagerJobId) =
        sprintf "%s-reviewer" (ManagerJobId.value jobId)

    let recoverJobs
        (journal: AgentJournal)
        (orchestratorId: SessionId)
        (worktrees: Dictionary<string, string>)
        (registerChildDirectory: SessionId -> string -> unit)
        (registerReviewerTree: string -> GitTreePort -> unit)
        (engine: Orchestrator)
        : Task<unit> =
        task {
            let snapshot = AgentJournal.snapshot journal

            // ORCH-004: only jobs still owed work. A Published or Failed job's worktree
            // is swept, not resumed, and re-registering its directory would hand a live
            // path to a job that is finished.
            let active =
                OrchestratorProjection.activeJobs snapshot.AgentProjections.Orchestrator

            for job in active do
                let path = WorktreePath.value job.WorktreePath
                worktrees.[ManagerJobId.value job.ManagerJobId] <- path
                worktrees.[reviewerAgentId job.ManagerJobId] <- path

            OrchestratorSessionDirectories.registerRestored
                snapshot
                orchestratorId
                worktrees
                registerChildDirectory
                registerReviewerTree

            for job in active do
                engine.RecoverManagerJob job
        }
