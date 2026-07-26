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
            fun worktreePath targetBranch ->
                task {
                    // Read the candidate commit from the manager worktree.
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

                        // (a) current branch of the target repo must equal the frozen branch.
                        let! (bCode, bStdout, bStderr) =
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
                            // (b) current target HEAD.
                            let! (hCode, hStdout, hStderr) =
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

                                // (c) candidate must be a fast-forward descendant of current HEAD.
                                let! (aCode, _, aStderr) =
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
                                    // Fail-closed fast-forward merge on the correct branch.
                                    let! (mCode, mStdout, mStderr) =
                                        runner
                                            { FileName = "git"
                                              Arguments = [ "merge"; "--ff-only"; commitHash ]
                                              WorkingDirectory = Some repoPath
                                              Environment = None
                                              Stdin = None
                                              Deadline = None
                                              PtyOptions = None }

                                    if mCode = 0 then
                                        return Ok commitHash
                                    else
                                        return
                                            Error(
                                                if String.IsNullOrWhiteSpace mStderr then
                                                    mStdout
                                                else
                                                    mStderr
                                            )
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
