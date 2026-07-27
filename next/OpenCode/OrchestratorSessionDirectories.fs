namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

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
            match session.Linkage with
            | Some linkage ->
                for KeyValue(childId, agentId) in linkage.LinkedChildren do
                    match worktrees.TryGetValue agentId with
                    | true, path ->
                        let sessionId = SessionId.create (ChildId.value childId)
                        register sessionId path

                        match Map.tryFind childId linkage.LinkedRoles with
                        | Some role when role.Equals("reviewer", System.StringComparison.OrdinalIgnoreCase) ->
                            registerReviewerTree (ChildId.value childId) (GitTree.create path)
                        | _ -> ()
                    | false, _ -> ()
            | None -> ()
        | None -> ()
