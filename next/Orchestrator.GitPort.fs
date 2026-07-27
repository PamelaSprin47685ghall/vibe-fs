namespace Wanxiangshu.Next.Orchestrator

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Process

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
          ConflictedFiles =
            fun worktreePath ->
                task {
                    let cmd =
                        { FileName = "git"
                          Arguments = [ "diff"; "--name-only"; "--diff-filter=U" ]
                          WorkingDirectory = Some worktreePath
                          Environment = None
                          Stdin = None
                          Deadline = None
                          PtyOptions = None }

                    let! (code, stdout, stderr) = runner cmd

                    if code = 0 then
                        let files =
                            stdout.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
                            |> Array.toList

                        return Ok files
                    else
                        return Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)
                }
          FfMerge =
            fun worktreePath targetBranch expectedTargetHead ->
                task {
                    let revCmd =
                        { FileName = "git"
                          Arguments = [ "rev-parse"; "HEAD" ]
                          WorkingDirectory = Some worktreePath
                          Environment = None
                          Stdin = None
                          Deadline = None
                          PtyOptions = None }

                    let! revCode, revStdout, revStderr = runner revCmd

                    match revCode with
                    | code when code <> 0 ->
                        return
                            Error(
                                if String.IsNullOrWhiteSpace revStderr then
                                    revStdout
                                else
                                    revStderr
                            )
                    | _ ->
                        let commitHash = revStdout.Trim()

                        let! bCode, bStdout, bStderr =
                            runner
                                { FileName = "git"
                                  Arguments = [ "symbolic-ref"; "--short"; "HEAD" ]
                                  WorkingDirectory = Some repoPath
                                  Environment = None
                                  Stdin = None
                                  Deadline = None
                                  PtyOptions = None }

                        if bCode <> 0 || bStdout.Trim() <> targetBranch then
                            return
                                Error(
                                    sprintf
                                        "publish branch mismatch: target repo is on '%s' but publish is frozen to '%s'"
                                        (if bCode = 0 then bStdout.Trim() else "<detached HEAD>")
                                        targetBranch
                                )
                        else
                            let! hCode, hStdout, hStderr =
                                runner
                                    { FileName = "git"
                                      Arguments = [ "rev-parse"; sprintf "refs/heads/%s" targetBranch ]
                                      WorkingDirectory = Some repoPath
                                      Environment = None
                                      Stdin = None
                                      Deadline = None
                                      PtyOptions = None }

                            if hCode <> 0 then
                                return
                                    Error(
                                        if String.IsNullOrWhiteSpace hStderr then
                                            hStdout
                                        else
                                            hStderr
                                    )
                            else
                                let currentHead = hStdout.Trim()

                                match expectedTargetHead with
                                | Some expected when expected <> currentHead ->
                                    return Error OrchestratorConstants.targetRefMovedError
                                | _ ->
                                    let! aCode, _, aStderr =
                                        runner
                                            { FileName = "git"
                                              Arguments = [ "merge-base"; "--is-ancestor"; currentHead; commitHash ]
                                              WorkingDirectory = Some repoPath
                                              Environment = None
                                              Stdin = None
                                              Deadline = None
                                              PtyOptions = None }

                                    if aCode <> 0 then
                                        return
                                            Error(
                                                if String.IsNullOrWhiteSpace aStderr then
                                                    "candidate is not a fast-forward of the target branch"
                                                else
                                                    aStderr.Trim()
                                            )
                                    else
                                        let! cleanResult = GitPortHelpers.checkClean runner repoPath

                                        match cleanResult with
                                        | Error err -> return Error err
                                        | Ok() ->
                                            let! mCode, mStdout, mStderr =
                                                runner
                                                    { FileName = "git"
                                                      Arguments = [ "merge"; "--ff-only"; commitHash ]
                                                      WorkingDirectory = Some repoPath
                                                      Environment = None
                                                      Stdin = None
                                                      Deadline = None
                                                      PtyOptions = None }

                                            if mCode = 0 then
                                                return! GitPortHelpers.verifyHead runner repoPath commitHash
                                            else
                                                let message =
                                                    if String.IsNullOrWhiteSpace mStderr then
                                                        mStdout
                                                    else
                                                        mStderr

                                                if
                                                    expectedTargetHead.IsSome
                                                    && (message.IndexOf(
                                                            "cannot lock ref",
                                                            StringComparison.OrdinalIgnoreCase
                                                        )
                                                        >= 0
                                                        || message.IndexOf(
                                                            "update-ref",
                                                            StringComparison.OrdinalIgnoreCase
                                                           )
                                                           >= 0
                                                        || message.IndexOf(
                                                            "expected",
                                                            StringComparison.OrdinalIgnoreCase
                                                           )
                                                           >= 0)
                                                then
                                                    return Error OrchestratorConstants.targetRefMovedError
                                                else
                                                    return Error message
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
                }
          HasRebaseHead =
            fun worktreePath ->
                task {
                    let cmd =
                        { FileName = "git"
                          Arguments = [ "rev-parse"; "--verify"; "REBASE_HEAD" ]
                          WorkingDirectory = Some worktreePath
                          Environment = None
                          Stdin = None
                          Deadline = None
                          PtyOptions = None }

                    let! (code, _, _) = runner cmd
                    return code = 0
                }
          ListWorktrees =
            fun () ->
                task {
                    let cmd =
                        { FileName = "git"
                          Arguments = [ "worktree"; "list"; "--porcelain" ]
                          WorkingDirectory = Some repoPath
                          Environment = None
                          Stdin = None
                          Deadline = None
                          PtyOptions = None }

                    let! (code, stdout, stderr) = runner cmd

                    if code = 0 then
                        let lines =
                            stdout.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
                            |> Array.toList

                        return Ok(GitPortHelpers.parseWorktreeList lines)
                    else
                        return Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)
                }
          ListManagerBranches =
            fun () ->
                task {
                    let cmd =
                        { FileName = "git"
                          Arguments = [ "branch"; "--list"; "manager/*" ]
                          WorkingDirectory = Some repoPath
                          Environment = None
                          Stdin = None
                          Deadline = None
                          PtyOptions = None }

                    let! (code, stdout, stderr) = runner cmd

                    if code = 0 then
                        let branches =
                            stdout.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
                            |> Array.map (fun s -> s.Trim().TrimStart('*').Trim())
                            |> Array.filter (fun s -> not (System.String.IsNullOrWhiteSpace s))
                            |> Array.toList

                        return Ok branches
                    else
                        return Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)
                }
          DeleteBranch =
            fun branch ->
                task {
                    let cmd =
                        { FileName = "git"
                          Arguments = [ "branch"; "-D"; branch ]
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
                } }

    let createWithRunner (runner: Command -> Task<int * string * string>) : GitPort = createWithRepo "." runner
