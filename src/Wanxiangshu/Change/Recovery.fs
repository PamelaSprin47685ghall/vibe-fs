namespace Wanxiangshu.Change

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Change.Host
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// Lazy journal recovery for persisted ManagerJobs.
///
/// RECOVERY-FAMILY: caller must hold FamilyRecoveryPermit for the orchestrator
/// session before starting publication programs. This module only registers
/// worktree directories and hands each active job to RecoverManagerJob.
///
/// No recovery prompt is built here. ORCH-006 does not persist the Manager's prompt,
/// and ORCH-007 decides the resume action from the last durable fact.
module OrchestratorManagerJob =
    let recoverJobs
        (journal: AgentJournal)
        (orchestratorId: SessionId)
        (worktrees: Dictionary<string, string>)
        (registerChildDirectory: SessionId -> string -> unit)
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

            OrchestratorSessionDirectories.registerRestored
                snapshot
                orchestratorId
                worktrees
                registerChildDirectory

            for job in active do
                engine.RecoverManagerJob job
        }
