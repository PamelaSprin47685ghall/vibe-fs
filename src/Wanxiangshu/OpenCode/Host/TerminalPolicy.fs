namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Change
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority

/// Pure terminal admission rules; no Host transport or mutable registry.
module TerminalPolicy =

    let sessionDead (journal: AgentJournal option) (sessionId: SessionId) =
        match journal with
        | Some j -> j.IsPoisoned
        | None -> false

    let private canonicalRoleOf (authority: PromptAuthority.PromptAuthorityProjection) =
        match authority.ActiveLogicalRun, authority.LastAuthorityProfile with
        | Some run, _ -> Some run.CanonicalRole
        | None, Some profile -> Some profile.CanonicalRole
        | None, None -> None

    /// ORCH-003: true when the session's registered parent is an Orchestrator
    /// session (its own canonical role is Orchestrator). Only that parent
    /// suppresses the top-level Manager guard; a HumanRoot-forked Manager stays
    /// top-level and keeps its guard.
    let private parentedByOrchestrator
        (projection: AgentProjectionSet)
        (sessionParents: Dictionary<string, string>)
        (sessionKey: string)
        =
        match sessionParents.TryGetValue sessionKey with
        | true, parentId ->
            Map.tryFind (SessionId.create parentId) projection.Sessions
            |> Option.bind (fun parent -> parent.PromptAuthority)
            |> Option.bind canonicalRoleOf
            |> Option.exists (fun role -> role = Role.Orchestrator)
        | false, _ -> false

    let private isLinkedChild (journal: AgentJournal option) (sessionKey: string) =
        match journal with
        | None -> false
        | Some durable ->
            Map.containsKey
                (SessionId.create sessionKey)
                (AgentJournal.snapshot durable).AgentProjections.HandleByChildSession

    let private unlinkedTopLevel
        (sessionParents: Dictionary<string, string>)
        (journal: AgentJournal option)
        (sessionKey: string)
        =
        not (sessionParents.ContainsKey sessionKey)
        && not (isLinkedChild journal sessionKey)

    let private managerOwnsTopLevel
        (sessionParents: Dictionary<string, string>)
        (journal: AgentJournal option)
        (sessionKey: string)
        (projection: AgentProjectionSet)
        (authority: PromptAuthority.PromptAuthorityProjection)
        =
        match authority.ActiveLogicalRun, authority.LastAuthorityProfile with
        | Some run, _ ->
            run.CanonicalRole = Role.Manager
            && not (parentedByOrchestrator projection sessionParents sessionKey)
        | None, Some profile -> profile.CanonicalRole = Role.Manager
        | None, None -> unlinkedTopLevel sessionParents journal sessionKey

    let private sessionIsTopLevelManager
        (sessionParents: Dictionary<string, string>)
        (journal: AgentJournal option)
        (sessionKey: string)
        (projection: ProjectionSet)
        =
        match Map.tryFind (SessionId.create sessionKey) projection.AgentProjections.Sessions with
        | Some session ->
            session.PromptAuthority
            |> Option.map (managerOwnsTopLevel sessionParents journal sessionKey projection.AgentProjections)
            |> Option.defaultValue (unlinkedTopLevel sessionParents journal sessionKey)
        | None -> unlinkedTopLevel sessionParents journal sessionKey

    /// Manager Guard applies to any manager that still owns the review loop for its
    /// worktree. Manager children of Orchestrator remain linked to the family root,
    /// so parent linkage alone must not suppress the guard.
    let isTopLevelManager
        (sessionParents: Dictionary<string, string>)
        (journal: AgentJournal option)
        (sessionKey: string)
        =
        match journal with
        | None -> not (sessionParents.ContainsKey sessionKey)
        | Some j -> sessionIsTopLevelManager sessionParents journal sessionKey (AgentJournal.snapshot j)

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
