namespace Wanxiangshu.Next.Orchestrator

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Process

[<RequireQualifiedAccess>]
type OrchestratorVerdict =
    | Published of managerId: string * headCommit: string
    | RejectedDirty of reason: string
    | NeedsReview of managerId: string * reviewDetails: string
    | IntegrationFailed of managerId: string * errorDetails: string

[<RequireQualifiedAccess>]
type ManagerStatus =
    | Forked
    | Running
    | ReadyToPublish
    | Published of commit: string
    | RejectedDirty of reason: string
    | NeedsReview of details: string
    | Failed of error: string

type ManagerInfo =
    { Id: string
      WorktreePath: string
      Status: ManagerStatus }

type GitPort =
    { IsDirty: string -> Task<bool>
      CreateWorktree: string -> string -> string -> Task<Result<unit, string>>
      Rebase: string -> string -> Task<Result<unit, string>>
      FfMerge: string -> string -> Task<Result<string, string>>
      RemoveWorktree: string -> Task<Result<unit, string>> }

type ManagerPort =
    { RunManager: string -> string -> Task<Result<unit, string>>
      Reverify: string -> string -> Task<Result<unit, string>> }

type Orchestrator(git: GitPort, manager: ManagerPort, repoPath: string, targetBranch: string) =
    let publishLock = new SemaphoreSlim(1, 1)
    let managers = ConcurrentDictionary<string, ManagerInfo>()

    member this.ForkManager(managerId: string, ?worktreePath: string) : Task<Result<ManagerInfo, OrchestratorVerdict>> =
        task {
            let path =
                defaultArg worktreePath (IO.Path.Combine(repoPath, sprintf ".worktrees/%s" managerId))

            let! isDirty = git.IsDirty repoPath

            if isDirty then
                let verdict = OrchestratorVerdict.RejectedDirty "Worktree is dirty"

                let info =
                    { Id = managerId
                      WorktreePath = path
                      Status = ManagerStatus.RejectedDirty "Worktree is dirty" }

                managers.[managerId] <- info
                return Error verdict
            else
                match! git.CreateWorktree repoPath managerId path with
                | Error err ->
                    let verdict =
                        OrchestratorVerdict.IntegrationFailed(managerId, sprintf "Failed to create worktree: %s" err)

                    let info =
                        { Id = managerId
                          WorktreePath = path
                          Status = ManagerStatus.Failed err }

                    managers.[managerId] <- info
                    return Error verdict
                | Ok() ->
                    let info =
                        { Id = managerId
                          WorktreePath = path
                          Status = ManagerStatus.Running }

                    managers.[managerId] <- info

                    match! manager.RunManager managerId path with
                    | Error err ->
                        let failedInfo =
                            { info with
                                Status = ManagerStatus.Failed err }

                        managers.[managerId] <- failedInfo

                        let verdict =
                            OrchestratorVerdict.IntegrationFailed(managerId, sprintf "Manager run failed: %s" err)

                        return Error verdict
                    | Ok() ->
                        let readyInfo =
                            { info with
                                Status = ManagerStatus.ReadyToPublish }

                        managers.[managerId] <- readyInfo
                        return Ok readyInfo
        }

    member this.JoinPublished(managerId: string) : Task<OrchestratorVerdict> =
        task {
            match managers.TryGetValue(managerId) with
            | false, _ ->
                return OrchestratorVerdict.IntegrationFailed(managerId, sprintf "Manager '%s' not found" managerId)
            | true, info ->
                let! _ = publishLock.WaitAsync()

                try
                    match! git.Rebase info.WorktreePath targetBranch with
                    | Error err ->
                        let verdict =
                            OrchestratorVerdict.IntegrationFailed(managerId, sprintf "Rebase failed: %s" err)

                        managers.[managerId] <-
                            { info with
                                Status = ManagerStatus.Failed("Rebase failed: " + err) }

                        return verdict
                    | Ok() ->
                        match! manager.Reverify managerId info.WorktreePath with
                        | Error err ->
                            let verdict = OrchestratorVerdict.NeedsReview(managerId, err)

                            managers.[managerId] <-
                                { info with
                                    Status = ManagerStatus.NeedsReview err }

                            return verdict
                        | Ok() ->
                            match! git.FfMerge info.WorktreePath targetBranch with
                            | Error err ->
                                let verdict =
                                    OrchestratorVerdict.IntegrationFailed(managerId, sprintf "FF merge failed: %s" err)

                                managers.[managerId] <-
                                    { info with
                                        Status = ManagerStatus.Failed("Merge failed: " + err) }

                                return verdict
                            | Ok commitHash ->
                                let! _ = git.RemoveWorktree info.WorktreePath
                                let verdict = OrchestratorVerdict.Published(managerId, commitHash)

                                managers.[managerId] <-
                                    { info with
                                        Status = ManagerStatus.Published commitHash }

                                return verdict
                finally
                    publishLock.Release() |> ignore
        }

    member this.List() : ManagerInfo list = managers.Values |> Seq.toList

module OrchestratorEngine =
    let create git manager repoPath targetBranch =
        Orchestrator(git, manager, repoPath, targetBranch)

    let forkManager (orch: Orchestrator) managerId (worktreePath: string option) =
        orch.ForkManager(managerId, ?worktreePath = worktreePath)

    let joinPublished (orch: Orchestrator) managerId = orch.JoinPublished(managerId)

    let list (orch: Orchestrator) = orch.List()

module ProcessGitPort =
    let createWithRunner (runner: Command -> Task<int * string * string>) : GitPort =
        { IsDirty =
            fun repoPath ->
                task {
                    let cmd =
                        { FileName = "git"
                          Arguments = [ "status"; "--porcelain" ]
                          WorkingDirectory = Some repoPath
                          Environment = None
                          Stdin = None
                          Deadline = None
                          PtyOptions = None }

                    let! (code, stdout, _) = runner cmd
                    return code = 0 && not (String.IsNullOrWhiteSpace stdout)
                }
          CreateWorktree =
            fun repoPath managerId targetPath ->
                task {
                    let cmd =
                        { FileName = "git"
                          Arguments = [ "worktree"; "add"; targetPath; "-b"; sprintf "manager/%s" managerId ]
                          WorkingDirectory = Some repoPath
                          Environment = None
                          Stdin = None
                          Deadline = None
                          PtyOptions = None }

                    let! (code, stdout, stderr) = runner cmd

                    if code = 0 then
                        return Ok()
                    else
                        return Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)
                }
          Rebase =
            fun worktreePath targetBranch ->
                task {
                    let cmd =
                        { FileName = "git"
                          Arguments = [ "rebase"; targetBranch ]
                          WorkingDirectory = Some worktreePath
                          Environment = None
                          Stdin = None
                          Deadline = None
                          PtyOptions = None }

                    let! (code, stdout, stderr) = runner cmd

                    if code = 0 then
                        return Ok()
                    else
                        return Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)
                }
          FfMerge =
            fun worktreePath targetBranch ->
                task {
                    let cmd =
                        { FileName = "git"
                          Arguments = [ "merge"; "--ff-only"; "HEAD" ]
                          WorkingDirectory = Some targetBranch
                          Environment = None
                          Stdin = None
                          Deadline = None
                          PtyOptions = None }

                    let! (code, stdout, stderr) = runner cmd

                    if code = 0 then
                        let revCmd =
                            { FileName = "git"
                              Arguments = [ "rev-parse"; "HEAD" ]
                              WorkingDirectory = Some worktreePath
                              Environment = None
                              Stdin = None
                              Deadline = None
                              PtyOptions = None }

                        let! (_, revStdout, _) = runner revCmd
                        return Ok(revStdout.Trim())
                    else
                        return Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)
                }
          RemoveWorktree =
            fun worktreePath ->
                task {
                    let cmd =
                        { FileName = "git"
                          Arguments = [ "worktree"; "remove"; "--force"; worktreePath ]
                          WorkingDirectory = None
                          Environment = None
                          Stdin = None
                          Deadline = None
                          PtyOptions = None }

                    let! (code, stdout, stderr) = runner cmd

                    if code = 0 then
                        return Ok()
                    else
                        return Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)
                } }
