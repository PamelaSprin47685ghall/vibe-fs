namespace Wanxiangshu.Next.Orchestrator

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Process

/// Typed Git verbs. Process command construction and output classification are
/// contained here; workflow sequencing stays in OrchestratorProgram.
module GitOperations =

    let private command repo args =
        { FileName = "git"
          Arguments = args
          WorkingDirectory = Some repo
          Environment = None
          Stdin = None
          Deadline = None
          PtyOptions = None }

    let private error stdout stderr = if String.IsNullOrWhiteSpace stderr then stdout else stderr

    let private verifyClean runner repo =
        task {
            let! code, stdout, stderr = runner (command repo [ "status"; "--porcelain" ])

            if code <> 0 then return Error(error stdout stderr)
            elif String.IsNullOrWhiteSpace stdout then return Ok()
            else return Error "target worktree is dirty; refusing ff-only merge"
        }

    let private verifyHead runner repo expected =
        task {
            let! code, stdout, stderr = runner (command repo [ "rev-parse"; "HEAD" ])

            if code <> 0 then return Error(error stdout stderr)
            elif stdout.Trim() = expected then return Ok expected
            else
                return
                    Error(
                        sprintf "ff-only merge did not advance HEAD to candidate %s (got %s)" expected (stdout.Trim())
                    )
        }

    let createWithRepo repoPath (runner: Command -> Task<int * string * string>) : GitPort =
        { IsDirty = WorktreeCommands.isDirty runner
          CreateWorktree = WorktreeCommands.create runner
          Rebase =
            fun worktree target ->
                task {
                    let! code, stdout, stderr = runner (command worktree [ "rebase"; target ])
                    return if code = 0 then Ok() else Error(error stdout stderr)
                }
          ConflictedFiles =
            fun worktree ->
                task {
                    let! code, stdout, stderr =
                        runner (command worktree [ "diff"; "--name-only"; "--diff-filter=U" ])

                    if code <> 0 then
                        return Error(error stdout stderr)
                    else
                        return
                            stdout.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
                            |> Array.toList
                            |> Ok
                }
          FfMerge =
            fun worktree targetBranch expectedTargetHead ->
                task {
                    let! candidateCode, candidateOut, candidateError =
                        runner (command worktree [ "rev-parse"; "HEAD" ])

                    if candidateCode <> 0 then
                        return Error(error candidateOut candidateError)
                    else
                        let candidate = candidateOut.Trim()
                        let! branchCode, branchOut, _ =
                            runner (command repoPath [ "symbolic-ref"; "--short"; "HEAD" ])

                        if branchCode <> 0 || branchOut.Trim() <> targetBranch then
                            return
                                Error(
                                    sprintf
                                        "publish branch mismatch: target repo is on '%s' but publish is frozen to '%s'"
                                        (if branchCode = 0 then branchOut.Trim() else "<detached HEAD>")
                                        targetBranch
                                )
                        else
                            let! headCode, headOut, headError =
                                runner (command repoPath [ "rev-parse"; sprintf "refs/heads/%s" targetBranch ])

                            if headCode <> 0 then
                                return Error(error headOut headError)
                            else
                                let currentHead = headOut.Trim()

                                match expectedTargetHead with
                                | Some expected when expected <> currentHead ->
                                    return Error OrchestratorConstants.targetRefMovedError
                                | _ ->
                                    let! ancestorCode, _, ancestorError =
                                        runner
                                            (command
                                                repoPath
                                                [ "merge-base"; "--is-ancestor"; currentHead; candidate ])

                                    if ancestorCode <> 0 then
                                        return
                                            Error(
                                                if String.IsNullOrWhiteSpace ancestorError then
                                                    "candidate is not a fast-forward of the target branch"
                                                else
                                                    ancestorError.Trim()
                                            )
                                    else
                                        match! verifyClean runner repoPath with
                                        | Error cleanError -> return Error cleanError
                                        | Ok() ->
                                            let! mergeCode, mergeOut, mergeError =
                                                runner (command repoPath [ "merge"; "--ff-only"; candidate ])

                                            if mergeCode = 0 then
                                                return! verifyHead runner repoPath candidate
                                            else
                                                let message = error mergeOut mergeError

                                                if
                                                    expectedTargetHead.IsSome
                                                    && [ "cannot lock ref"; "update-ref"; "expected" ]
                                                       |> List.exists (fun marker ->
                                                           message.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                                                then
                                                    return Error OrchestratorConstants.targetRefMovedError
                                                else
                                                    return Error message
                }
          RemoveWorktree = WorktreeCommands.remove runner
          HasRebaseHead =
            fun worktree ->
                task {
                    let! code, _, _ = runner (command worktree [ "rev-parse"; "--verify"; "REBASE_HEAD" ])
                    return code = 0
                }
          ListWorktrees = WorktreeCommands.list runner repoPath
          ListManagerBranches = WorktreeCommands.listBranches runner repoPath
          DeleteBranch = WorktreeCommands.deleteBranch runner repoPath
          ReadHead =
            fun path ->
                task {
                    let! code, stdout, stderr = runner (command path [ "rev-parse"; "HEAD" ])

                    if code <> 0 then
                        return Error(error stdout stderr)
                    elif String.IsNullOrWhiteSpace stdout then
                        return Error "HEAD is empty"
                    else
                        return Ok(stdout.Trim())
                }
          GetTargetHead =
            fun branch ->
                task {
                    let! code, stdout, stderr =
                        runner (command repoPath [ "rev-parse"; sprintf "refs/heads/%s" branch ])

                    if code <> 0 then
                        return
                            Error(
                                if String.IsNullOrWhiteSpace stderr then
                                    sprintf "target branch not found: %s" branch
                                else
                                    stderr.Trim()
                            )
                    elif String.IsNullOrWhiteSpace stdout then
                        return Error(sprintf "target branch not found: %s" branch)
                    else
                        return Ok(stdout.Trim())
                } }

    let createWithRunner runner = createWithRepo "." runner
