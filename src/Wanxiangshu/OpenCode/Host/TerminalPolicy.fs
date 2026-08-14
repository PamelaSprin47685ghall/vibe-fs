namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Change
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
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

/// Pure terminal admission rules; no Host transport or mutable registry.
module TerminalPolicy =

    let sessionDead (journal: AgentJournal option) (sessionId: SessionId) =
        match journal with
        | Some j -> j.IsPoisoned
        | None -> false

    let roleName (role: Wanxiangshu.Foundation.Role option) =
        role |> Option.map (fun value -> value.ToString().ToLowerInvariant())

    /// The durable handle record for a child session, across all parents.
    ///
    /// PERSIST-008: one keyed lookup in the fold-maintained
    /// `HandleByChildSession` index. The previous version scanned every session's
    /// handle map for a `ChildSessionId` match — the scan PERSIST-008 forbids.
    ///
    /// Returns the record rather than a bool because callers need `TargetAgent`:
    /// PROMPT-008 forbids rebuilding a managed agent name from a role, so the only
    /// legitimate source is what the fork recorded.
    ///
    /// Retired handles are included. EXEC-009 makes the tombstone permanent, so a
    /// child that already finished is still a child — answering otherwise is how a
    /// completed child gets treated as a fresh top-level session.
    let tryLinkedChild (journal: AgentJournal option) (sessionKey: string) =
        match journal with
        | None -> None
        | Some j ->
            let childSessionId = SessionId.create sessionKey
            Map.tryFind childSessionId (AgentJournal.snapshot j).AgentProjections.HandleByChildSession

    let isLinkedChild (journal: AgentJournal option) (sessionKey: string) =
        (tryLinkedChild journal sessionKey).IsSome

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
            |> Option.bind (fun authority ->
                match authority.ActiveLogicalRun, authority.LastAuthorityProfile with
                | Some run, _ -> Some run.CanonicalRole
                | None, Some profile -> Some profile.CanonicalRole
                | None, None -> None)
            |> Option.exists (fun role -> role = Role.Orchestrator)
        | false, _ -> false

    /// Fork child whose handle is CompletedAwaitingJoin or Retired: Blogger must
    /// not Start/Offer. Human root (no handle) → false.
    let mainSealedForBlogger (journal: AgentJournal option) (mainSessionId: SessionId) : bool =
        match journal with
        | None -> false
        | Some j -> AgentProjection.mainSealedForBlogger mainSessionId (AgentJournal.snapshot j).AgentProjections

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
        | Some j ->
            let projection = AgentJournal.snapshot j

            match Map.tryFind (SessionId.create sessionKey) projection.AgentProjections.Sessions with
            | Some session ->
                match session.PromptAuthority with
                | Some authority ->
                    match authority.ActiveLogicalRun, authority.LastAuthorityProfile with
                    | Some run, _ ->
                        // ORCH-003: a Manager forked by an Orchestrator (AgentOwnerRoot)
                        // is a job worker, not a top-level Manager — its review is the
                        // Orchestrator's barrier (ORCH-006), never the top-level guard.
                        // `CanonicalRole` alone is not enough: the forked Manager
                        // carries the Manager role on purpose, and parent linkage
                        // alone is not enough either (the HumanRoot's forked Manager
                        // is linked too and must keep its guard). The discriminator
                        // is the parent's own role: an Orchestrator parent means the
                        // guard does not apply (measured: the guard deferred the
                        // completion forever and the barrier review never started).
                        run.CanonicalRole = Role.Manager
                        && not (parentedByOrchestrator projection.AgentProjections sessionParents sessionKey)
                    | None, Some profile -> profile.CanonicalRole = Role.Manager
                    | None, None ->
                        not (sessionParents.ContainsKey sessionKey)
                        && not (isLinkedChild journal sessionKey)
                | None ->
                    not (sessionParents.ContainsKey sessionKey)
                    && not (isLinkedChild journal sessionKey)
            | None ->
                not (sessionParents.ContainsKey sessionKey)
                && not (isLinkedChild journal sessionKey)

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
        | Some Role.Manager ->
            match journal with
            | None -> false
            | Some durable ->
                AgentJournal.handleProjection durable sessionId
                |> HandleProjection.listable
                |> List.isEmpty
                |> not
        | Some Role.DevOps ->
            let durableOutstanding =
                match journal with
                | None -> false
                | Some durable ->
                    AgentJournal.handleProjection durable sessionId
                    |> HandleProjection.listable
                    |> List.isEmpty
                    |> not

            durableOutstanding || hasLivePty (SessionId.value sessionId)
        | Some Role.Orchestrator ->
            match journal with
            | None -> false
            | Some durable ->
                OrchestratorProjection.activeJobs (AgentJournal.snapshot durable).AgentProjections.Orchestrator
                |> List.isEmpty
                |> not
        | _ -> false
