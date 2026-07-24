namespace Wanxiangshu.Next.Orchestrator

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Process

[<RequireQualifiedAccess>]
type OrchestratorVerdict =
    | Published of managerId: string * headCommit: string
    | RejectedDirty of reason: string
    | NeedsReview of managerId: string * reviewDetails: string
    | IntegrationFailed of managerId: string * errorDetails: string
    | Empty

type OrchestratorHandle =
    { ManagerId: string
      WorktreePath: string }

type ManagerCompletion =
    { Handle: OrchestratorHandle
      Result: Result<unit, string> }

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
    let lockObj = obj ()
    let mutable publishChain: Task = Task.FromResult(()) :> Task
    let mailbox = System.Collections.Generic.Queue<ManagerCompletion>()

    let runSerial (fn: unit -> Task<'T>) : Task<'T> =
        task {
            let tcs = TaskCompletionSource<'T>()

            lock lockObj (fun () ->
                let prev = publishChain

                publishChain <-
                    task {
                        try
                            do! prev
                        with _ ->
                            ()

                        try
                            let! res = fn ()
                            tcs.SetResult(res)
                        with ex ->
                            tcs.SetException(ex)
                    }
                    :> Task)

            return! tcs.Task
        }

    member this.ForkManager
        (managerId: string, ?worktreePath: string)
        : Task<Result<OrchestratorHandle, OrchestratorVerdict>> =
        task {
            let path =
                defaultArg worktreePath (IO.Path.Combine(repoPath, sprintf ".worktrees/%s" managerId))

            let! isDirty = git.IsDirty repoPath

            if isDirty then
                return Error(OrchestratorVerdict.RejectedDirty "Worktree is dirty")
            else
                match! git.CreateWorktree repoPath managerId path with
                | Error err ->
                    return
                        Error(
                            OrchestratorVerdict.IntegrationFailed(
                                managerId,
                                sprintf "Failed to create worktree: %s" err
                            )
                        )
                | Ok() ->
                    let handle =
                        { ManagerId = managerId
                          WorktreePath = path }

                    let _ =
                        task {
                            let! res = manager.RunManager managerId path
                            let completion = { Handle = handle; Result = res }
                            lock lockObj (fun () -> mailbox.Enqueue(completion))
                        }

                    return Ok handle
        }

    member this.JoinPublished() : Task<OrchestratorVerdict> =
        task {
            let completionOpt =
                lock lockObj (fun () -> if mailbox.Count > 0 then Some(mailbox.Dequeue()) else None)

            match completionOpt with
            | None -> return OrchestratorVerdict.Empty

            | Some completion ->
                match completion.Result with
                | Error err ->
                    return
                        OrchestratorVerdict.IntegrationFailed(
                            completion.Handle.ManagerId,
                            sprintf "Manager run failed: %s" err
                        )
                | Ok() ->
                    return!
                        runSerial (fun () ->
                            task {
                                match! git.Rebase completion.Handle.WorktreePath targetBranch with
                                | Error err ->
                                    return
                                        OrchestratorVerdict.IntegrationFailed(
                                            completion.Handle.ManagerId,
                                            sprintf "Rebase failed: %s" err
                                        )
                                | Ok() ->
                                    match!
                                        manager.Reverify completion.Handle.ManagerId completion.Handle.WorktreePath
                                    with
                                    | Error err ->
                                        return OrchestratorVerdict.NeedsReview(completion.Handle.ManagerId, err)
                                    | Ok() ->
                                        match! git.FfMerge completion.Handle.WorktreePath targetBranch with
                                        | Error err ->
                                            return
                                                OrchestratorVerdict.IntegrationFailed(
                                                    completion.Handle.ManagerId,
                                                    sprintf "FF merge failed: %s" err
                                                )
                                        | Ok commitHash ->
                                            let! _ = git.RemoveWorktree completion.Handle.WorktreePath

                                            return
                                                OrchestratorVerdict.Published(completion.Handle.ManagerId, commitHash)
                            })
        }

module OrchestratorEngine =
    let create git manager repoPath targetBranch =
        Orchestrator(git, manager, repoPath, targetBranch)

    let forkManager (orch: Orchestrator) managerId (worktreePath: string option) =
        orch.ForkManager(managerId, ?worktreePath = worktreePath)

    let joinPublished (orch: Orchestrator) = orch.JoinPublished()

module ProcessGitPort =
    let createWithRepo (repoPath: string) (runner: Command -> Task<int * string * string>) : GitPort =
        { IsDirty =
            fun targetPath ->
                task {
                    let cmd =
                        { FileName = "git"
                          Arguments = [ "status"; "--porcelain" ]
                          WorkingDirectory = Some targetPath
                          Environment = None
                          Stdin = None
                          Deadline = None
                          PtyOptions = None }

                    let! (code, stdout, _) = runner cmd
                    return code = 0 && not (String.IsNullOrWhiteSpace stdout)
                }
          CreateWorktree =
            fun targetRepoPath managerId targetPath ->
                task {
                    let cmd =
                        { FileName = "git"
                          Arguments = [ "worktree"; "add"; targetPath; "-b"; sprintf "manager/%s" managerId ]
                          WorkingDirectory = Some targetRepoPath
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
                    let revCmd =
                        { FileName = "git"
                          Arguments = [ "rev-parse"; "HEAD" ]
                          WorkingDirectory = Some worktreePath
                          Environment = None
                          Stdin = None
                          Deadline = None
                          PtyOptions = None }

                    let! (revCode, revStdout, revStderr) = runner revCmd

                    if revCode <> 0 then
                        return
                            Error(
                                if String.IsNullOrWhiteSpace revStderr then
                                    revStdout
                                else
                                    revStderr
                            )
                    else
                        let commitHash = revStdout.Trim()

                        let mergeCmd =
                            { FileName = "git"
                              Arguments = [ "merge"; "--ff-only"; commitHash ]
                              WorkingDirectory = Some repoPath
                              Environment = None
                              Stdin = None
                              Deadline = None
                              PtyOptions = None }

                        let! (code, stdout, stderr) = runner mergeCmd

                        if code = 0 then
                            return Ok commitHash
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

    let createWithRunner (runner: Command -> Task<int * string * string>) : GitPort = createWithRepo "." runner
