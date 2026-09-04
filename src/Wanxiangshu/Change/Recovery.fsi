namespace Wanxiangshu.Change

open System.Collections.Generic
open System.Threading.Tasks
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
    val recoverJobs:
        journal: AgentJournal ->
        orchestratorId: SessionId ->
        worktrees: Dictionary<string, string> ->
        registerChildDirectory: (SessionId -> string -> unit) ->
        engine: Orchestrator ->
            Task<unit>
