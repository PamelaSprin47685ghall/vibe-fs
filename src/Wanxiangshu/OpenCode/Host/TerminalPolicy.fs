namespace Wanxiangshu.OpenCode

open Wanxiangshu.Change
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Pure terminal admission rules; no Host transport or mutable registry.
module TerminalPolicy =

    let sessionDead (journal: AgentJournal option) (sessionId: SessionId) =
        match journal with
        | Some j -> j.IsPoisoned
        | None -> false

    let private hasListableHandles (journal: AgentJournal option) (sessionId: SessionId) =
        match journal with
        | None -> false
        | Some durable ->
            AgentJournal.handleProjection durable sessionId
            |> HandleProjection.listable
            |> List.isEmpty
            |> not

    let private hasActiveOrchestratorJobs (journal: AgentJournal option) =
        match journal with
        | None -> false
        | Some durable ->
            OrchestratorProjection.activeJobs (AgentJournal.snapshot durable).AgentProjections.Orchestrator
            |> List.isEmpty
            |> not

    /// EXEC-016: join-capable role still owns unconsumed background work.
    ///
    /// Pure projection predicate + optional live-PTY probe. Executor private
    /// runtimes never participate (EXEC-014).
    let outstandingBackground
        (journal: AgentJournal option)
        (hasLivePty: string -> bool)
        (role: Role option)
        (sessionId: SessionId)
        : bool =
        match role with
        | Some Role.Manager -> hasListableHandles journal sessionId
        | Some Role.DevOps -> hasListableHandles journal sessionId || hasLivePty (SessionId.value sessionId)
        | Some Role.Orchestrator -> hasActiveOrchestratorJobs journal
        | _ -> false
