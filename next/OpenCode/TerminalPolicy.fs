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

    let isLinkedChild (journal: AgentJournal option) (sessionKey: string) =
        match journal with
        | None -> false
        | Some j ->
            let child = ChildId.create sessionKey

            (AgentJournal.snapshot j).AgentProjections.Sessions
            |> Map.exists (fun _ session ->
                match session.Linkage with
                | Some linkage -> Map.containsKey child linkage.LinkedChildren
                | None -> false)

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
