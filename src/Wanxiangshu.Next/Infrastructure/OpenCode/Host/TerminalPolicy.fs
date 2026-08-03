namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

/// Pure terminal admission rules; no Host transport or mutable registry.
module TerminalPolicy =

    let sessionDead (journal: AgentJournal option) (sessionId: SessionId) =
        match journal with
        | Some j -> j.IsPoisoned
        | None -> false

    let roleName (role: Wanxiangshu.Next.Session.AgentRole option) =
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
                    | Some run, _ -> run.CanonicalRole = Role.Manager
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
