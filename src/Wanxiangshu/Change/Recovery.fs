namespace Wanxiangshu.Change

open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Change
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Mission.Review
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

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
