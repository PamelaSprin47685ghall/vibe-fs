namespace Wanxiangshu.Next.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Orchestrator

module OrchestratorSweep =
    let private removeWorktrees git paths =
        let rec loop remaining =
            task {
                match remaining with
                | [] -> return Ok()
                | path :: tail ->
                    match! git.RemoveWorktree path with
                    | Ok() -> return! loop tail
                    | Error error -> return Error(sprintf "stale worktree cleanup failed for %s: %s" path error)
            }

        loop paths

    let private deleteBranches git branches =
        let rec loop remaining =
            task {
                match remaining with
                | [] -> return Ok()
                | branch :: tail ->
                    match! git.DeleteBranch branch with
                    | Ok() -> return! loop tail
                    | Error error -> return Error(sprintf "stale manager branch cleanup failed for %s: %s" branch error)
            }

        loop branches

    let sweepStaleArtifacts (git: GitPort) (activeJobs: Map<ManagerId, ManagerJob>) : Task<Result<unit, string>> =
        task {
            let activeIds =
                activeJobs
                |> Map.toList
                |> List.map (fun (id, _) -> ManagerId.value id)
                |> Set.ofList

            match! git.ListWorktrees() with
            | Error error -> return Error(sprintf "cannot list worktrees for cleanup: %s" error)
            | Ok entries ->
                let staleWorktrees =
                    entries
                    |> List.choose (fun (path, branchRef) ->
                        match branchRef with
                        | Some reference when reference.StartsWith("refs/heads/manager/") ->
                            let id = reference.Substring("refs/heads/manager/".Length)
                            if Set.contains id activeIds then None else Some path
                        | _ -> None)

                match! removeWorktrees git staleWorktrees with
                | Error error -> return Error error
                | Ok() ->
                    match! git.ListManagerBranches() with
                    | Error error -> return Error(sprintf "cannot list manager branches for cleanup: %s" error)
                    | Ok branches ->
                        let staleBranches =
                            branches
                            |> List.filter (fun branch ->
                                let slash = branch.IndexOf('/')
                                slash >= 0 && not (Set.contains (branch.Substring(slash + 1)) activeIds))

                        return! deleteBranches git staleBranches
        }

    let sweepLocked
        (lockPath: string)
        (git: GitPort)
        (activeJobs: Map<ManagerId, ManagerJob>)
        : Task<Result<unit, string>> =
        task {
            let! release = PublishLock.acquire lockPath

            let! outcome =
                task {
                    try
                        return! sweepStaleArtifacts git activeJobs
                    with ex ->
                        return Error ex.Message
                }

            do! PublishLock.release release
            return outcome
        }
