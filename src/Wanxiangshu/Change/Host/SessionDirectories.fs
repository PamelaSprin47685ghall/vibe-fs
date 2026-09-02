namespace Wanxiangshu.Change.Host

open System.Collections.Generic
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.OpenCode

module OrchestratorSessionDirectories =
    let private tryWorktreePath (worktrees: Dictionary<string, string>) (agentHandle: AgentHandleId) =
        match worktrees.TryGetValue(AgentHandleId.value agentHandle) with
        | true, path -> Some path
        | false, _ -> None

    let private registerReviewerTreeIfNeeded
        (registerReviewerTree: string -> GitTreePort -> unit)
        (record: HandleRecord)
        (path: string)
        =
        if record.CanonicalRole = Role.Reviewer then
            registerReviewerTree (SessionId.value record.ChildSessionId) (GitTree.create path)

    let private registerLinkedChild
        (worktrees: Dictionary<string, string>)
        (register: SessionId -> string -> unit)
        (registerReviewerTree: string -> GitTreePort -> unit)
        (record: HandleRecord)
        =
        match HandleId.tryAgent record.Handle |> Option.bind (tryWorktreePath worktrees) with
        | None -> ()
        | Some path ->
            register record.ChildSessionId path
            // CanonicalRole is the durable role the fork selected.
            // The previous version consulted a separate `LinkedRoles`
            // map, which could disagree with the handle it described.
            // Typed comparison, not a case-insensitive string match:
            // the role is a `Role`, so a spelling drift is a compile
            // error rather than a reviewer tree that silently stops
            // being registered.
            registerReviewerTreeIfNeeded registerReviewerTree record path

    let private registerHandles
        (worktrees: Dictionary<string, string>)
        (register: SessionId -> string -> unit)
        (registerReviewerTree: string -> GitTreePort -> unit)
        (handles: AgentLinkageProjection)
        =
        for record in HandleProjection.linkedChildren handles do
            // `worktrees` is keyed by the runtime agent id, which for an agent
            // child IS the handle's inner id. PTY and ManagerJob handles have
            // no agent id and no worktree entry, so they are skipped rather
            // than rendered into a lookup key.
            registerLinkedChild worktrees register registerReviewerTree record

    let registerRestored
        (snapshot: ProjectionSet)
        (orchestratorId: SessionId)
        (worktrees: Dictionary<string, string>)
        (register: SessionId -> string -> unit)
        (registerReviewerTree: string -> GitTreePort -> unit)
        =
        Map.tryFind orchestratorId snapshot.AgentProjections.Sessions
        |> Option.bind (fun session -> session.Handles)
        |> Option.iter (registerHandles worktrees register registerReviewerTree)
