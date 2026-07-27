namespace Wanxiangshu.Next.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Orchestrator

module OrchestratorSweep =
    let sweepStaleArtifacts (git: GitPort) (activeJobs: Map<ManagerId, ManagerJob>) : Task<unit> =
        task {
            let activeIds =
                activeJobs
                |> Map.toList
                |> List.map (fun (id, _) -> ManagerId.value id)
                |> Set.ofList

            // Remove linked worktrees before deleting their branches. Git refuses
            // to delete a branch that is still checked out, so the order is part
            // of the sweep's correctness contract.
            match! git.ListWorktrees() with
            | Ok entries ->
                for path, branchRef in entries do
                    match branchRef with
                    | Some reference when reference.StartsWith("refs/heads/manager/") ->
                        let id = reference.Substring("refs/heads/manager/".Length)

                        if not (Set.contains id activeIds) then
                            let! _ = git.RemoveWorktree path
                            ()
                    | _ -> ()
            | Error _ -> ()

            match! git.ListManagerBranches() with
            | Ok branches ->
                for branch in branches do
                    let slash = branch.IndexOf('/')

                    if slash >= 0 && not (Set.contains (branch.Substring(slash + 1)) activeIds) then
                        let! _ = git.DeleteBranch branch
                        ()
            | Error _ -> ()
        }

    let sweepLocked (lockPath: string) (git: GitPort) (activeJobs: Map<ManagerId, ManagerJob>) : Task<unit> =
        task {
            let! release = PublishLock.acquire lockPath

            let! outcome =
                task {
                    try
                        do! sweepStaleArtifacts git activeJobs
                        return Choice1Of2()
                    with ex ->
                        return Choice2Of2 ex
                }

            do! PublishLock.release release

            match outcome with
            | Choice1Of2() -> return ()
            | Choice2Of2 _ -> return ()
        }
