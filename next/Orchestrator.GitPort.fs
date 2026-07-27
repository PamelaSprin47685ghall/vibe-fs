namespace Wanxiangshu.Next.Orchestrator

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Process

module ProcessGitPort =

    let createWithRepo (repoPath: string) (runner: Command -> Task<int * string * string>) : GitPort =
        { IsDirty = GitPortWorktree.isDirty runner
          CreateWorktree = GitPortWorktree.createWorktree runner
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
          RemoveWorktree = GitPortWorktree.removeWorktree runner
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
          ListWorktrees = GitPortWorktree.listWorktrees runner repoPath
          ListManagerBranches = GitPortWorktree.listManagerBranches runner repoPath
          DeleteBranch = GitPortWorktree.deleteBranch runner repoPath }

    let createWithRunner (runner: Command -> Task<int * string * string>) : GitPort = createWithRepo "." runner
