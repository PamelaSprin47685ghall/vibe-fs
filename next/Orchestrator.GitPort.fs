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
