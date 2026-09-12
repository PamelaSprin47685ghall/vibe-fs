namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

    /// Pure terminal admission rules; no Host transport or mutable registry.
module TerminalPolicy =

    let sessionDead (port: TerminalPolicyPort option) (sessionId: SessionId) =
        match port with
        | Some p -> p.IsPoisoned()
        | None -> false

    /// ORCH-003: the Manager guard applies when no registered OR durable parent
    /// claims this session (unlinked + unregistered => top-level), or when the
    /// durable role says Manager but the flow-level parent is not an Orchestrator.
    /// `sessionParents` is the flow-level in-memory parent registry (host-owned).
    ///
    /// Durable-side evidence the caller supplies through `TerminalPolicyPort`:
    /// linked child lookup `IsLinkedChild` and session canonical role
    /// `TryCanonicalRole` — see `AgentJournalPortAdapter.forTerminalPolicy`.


    /// `isTopLevelManager` reads three orthogonal facts: the durable-family linked-child
    /// predicate, the canonical role, and the host's flow-level parent map. The truth
    /// table coerces the role-and-parent pair into a single switch — the domain fact
    /// "weri one of: registered under orchestrator" is the only thing being tested.
    ///
    /// ORCH-003: the Manager guard applies when no registered OR durable parent
    /// claims this session (unlinked + unregistered => top-level), or when the
    /// durable role says Manager but the flow-level parent is not an Orchestrator.
    /// Manager children of Orchestrator remain linked to the family root, so parent
    /// linkage alone must not suppress the guard.
    let isTopLevelManager
        (sessionParents: Dictionary<string, string>)
        (port: TerminalPolicyPort option)
        (sessionKey: string)
        : bool =
        let canonicalRole =
            port |> Option.bind (fun p -> p.TryCanonicalRole(SessionId.create sessionKey))

        let parent_registered = sessionParents.ContainsKey sessionKey

        let parentIsOrchestrator =
            match sessionParents.TryGetValue sessionKey, port with
            | (true, parentId), Some p -> p.TryCanonicalRole(SessionId.create parentId) = Some Role.Orchestrator
            | _ -> false

        match canonicalRole, parent_registered, port with
        | Some Role.Manager, _, _ -> not parentIsOrchestrator
        | Some _, _, _ -> false
        | None, true, _ -> false
        | None, false, Some p -> not (p.IsLinkedChild(SessionId.create sessionKey))
        | None, false, None -> true

    let private hasListableHandles (port: TerminalPolicyPort option) (sessionId: SessionId) =
        match port with
        | None -> false
        | Some p -> p.HasListableHandles sessionId

    let private hasActiveOrchestratorJobs (port: TerminalPolicyPort option) =
        match port with
        | None -> false
        | Some p -> p.HasActiveOrchestratorJobs()

    /// EXEC-016: join-capable role still owns unconsumed background work.
    ///
    /// Pure projection predicate + optional live-PTY probe. Executor private
    /// runtimes never participate (EXEC-014).
    let outstandingBackground
        (port: TerminalPolicyPort option)
        (hasLivePty: string -> bool)
        (role: Role option)
        (sessionId: SessionId)
        : bool =
        match role with
        | Some Role.Manager -> hasListableHandles port sessionId
        | Some Role.DevOps -> hasListableHandles port sessionId || hasLivePty (SessionId.value sessionId)
        | Some Role.Orchestrator -> hasActiveOrchestratorJobs port
        | _ -> false
