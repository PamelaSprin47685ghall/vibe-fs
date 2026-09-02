namespace Wanxiangshu.Change

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
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

    /// Register worktree directories for a job's Manager and its reviewer.
    ///
    /// The reviewer's runtime agent id is `<job>-reviewer`, matching
    /// `OrchestratorHost.runReviewerOnce`. Spelled through `reviewerAgentId` so the
    /// two cannot drift while both still compile.
    val reviewerAgentId: jobId: ManagerJobId -> string

    val recoverJobs:
        journal: AgentJournal ->
        orchestratorId: SessionId ->
        worktrees: Dictionary<string, string> ->
        registerChildDirectory: (SessionId -> string -> unit) ->
        registerReviewerTree: (string -> GitTreePort -> unit) ->
        engine: Orchestrator ->
            Task<unit>
