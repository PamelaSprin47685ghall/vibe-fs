namespace Wanxiangshu.Change.Host

open System.Collections.Generic
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation.Identity

module OrchestratorSessionDirectories =
    let private tryWorktreePath (worktrees: Dictionary<string, string>) (agentHandle: AgentHandleId) =
        match worktrees.TryGetValue(AgentHandleId.value agentHandle) with
        | true, path -> Some path
        | false, _ -> None

    let private registerLinkedChild
        (worktrees: Dictionary<string, string>)
        (register: SessionId -> string -> unit)
        (record: HandleRecord)
        =
        match HandleId.tryAgent record.Handle |> Option.bind (tryWorktreePath worktrees) with
        | None -> ()
        | Some path -> register record.ChildSessionId path

    let private registerHandles
        (worktrees: Dictionary<string, string>)
        (register: SessionId -> string -> unit)
        (handles: AgentLinkageProjection)
        =
        for record in HandleProjection.linkedChildren handles do
            // `worktrees` is keyed by the runtime agent id, which for an agent
            // child IS the handle's inner id. PTY and ManagerJob handles have
            // no agent id and no worktree entry, so they are skipped rather
            // than rendered into a lookup key.
            registerLinkedChild worktrees register record

    let registerRestored
        (snapshot: ProjectionSet)
        (orchestratorId: SessionId)
        (worktrees: Dictionary<string, string>)
        (register: SessionId -> string -> unit)
        =
        Map.tryFind orchestratorId snapshot.AgentProjections.Sessions
        |> Option.bind (fun session -> session.Handles)
        |> Option.iter (registerHandles worktrees register)
