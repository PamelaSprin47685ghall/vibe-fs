namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

module OrchestratorSessionDirectories =
    let registerRestored
        (snapshot: ProjectionSet)
        (orchestratorId: SessionId)
        (worktrees: Dictionary<string, string>)
        (register: SessionId -> string -> unit)
        (registerReviewerTree: string -> GitTreePort -> unit)
        =
        match Map.tryFind orchestratorId snapshot.AgentProjections.Sessions with
        | Some session ->
            match session.Handles with
            | Some handles ->
                for record in HandleProjection.linkedChildren handles do
                    // `worktrees` is keyed by the runtime agent id, which for an agent
                    // child IS the handle's inner id. PTY and ManagerJob handles have
                    // no agent id and no worktree entry, so they are skipped rather
                    // than rendered into a lookup key.
                    match HandleId.tryAgent record.Handle with
                    | None -> ()
                    | Some agentHandle ->
                        match worktrees.TryGetValue(AgentHandleId.value agentHandle) with
                        | true, path ->
                            register record.ChildSessionId path

                            // CanonicalRole is the durable role the fork selected.
                            // The previous version consulted a separate `LinkedRoles`
                            // map, which could disagree with the handle it described.
                            // Typed comparison, not a case-insensitive string match:
                            // the role is a `Role`, so a spelling drift is a compile
                            // error rather than a reviewer tree that silently stops
                            // being registered.
                            match record.CanonicalRole with
                            | Role.Reviewer ->
                                registerReviewerTree (SessionId.value record.ChildSessionId) (GitTree.create path)
                            | _ -> ()
                        | false, _ -> ()
            | None -> ()
        | None -> ()
