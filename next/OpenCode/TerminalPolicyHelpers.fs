namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

/// Pure helpers shared by TerminalPolicies.
module TerminalPolicyHelpers =

    let sessionDead (journal: AgentJournal option) (sessionId: SessionId) =
        match journal with
        | Some j -> j.IsPoisoned
        | None -> false

    let roleName (role: Wanxiangshu.Next.Session.AgentRole option) =
        role |> Option.map (fun value -> value.ToString().ToLowerInvariant())

    /// True when this session is a linked child of some parent in the durable
    /// journal projection. Used when the in-memory sessionParents map is empty
    /// (worktree plugin instance) so Orchestrator managers never receive the
    /// top-level ReviewGuard nudge.
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

    let isTopLevelManager
        (sessionParents: Dictionary<string, string>)
        (journal: AgentJournal option)
        (sessionKey: string)
        =
        not (sessionParents.ContainsKey sessionKey)
        && not (isLinkedChild journal sessionKey)
