namespace Wanxiangshu.Next.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity

module OrchestratorReviewState =
    let read
        (journal: AgentJournal option)
        (orchestratorId: SessionId)
        (worktree: string)
        : Task<Result<bool, string>> =
        task {
            match journal with
            | None -> return Error "Orchestrator review requires a journal"
            | Some journal ->
                let tree = (GitTree.create worktree).GetTreeHash()
                let snapshot = AgentJournal.snapshot journal

                match Map.tryFind orchestratorId snapshot.AgentProjections.Sessions with
                | Some session ->
                    match session.ReviewGuard with
                    | Some guard when guard.LastGitTreeHash = Some(GitTreeHash.create tree) && guard.IsConfirmed ->
                        return Ok true
                    | Some guard when
                        guard.LastGitTreeHash = Some(GitTreeHash.create tree)
                        && guard.ConsecutivePerfects >= 1
                        ->
                        return Ok false
                    | Some guard when guard.LastGitTreeHash = Some(GitTreeHash.create tree) ->
                        return Error "Reviewer requested revision"
                    | _ -> return Ok false
                | None -> return Ok false
        }
