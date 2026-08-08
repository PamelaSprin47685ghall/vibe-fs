namespace Wanxiangshu.Orchestrator

open System
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Process

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

    let private failure stdout stderr =
        if String.IsNullOrWhiteSpace stderr then stdout else stderr

    let private verifyClean runner repo =
        task {
            let! code, stdout, stderr = runner (command repo [ "status"; "--porcelain" ])

            if code <> 0 then
                return Error(failure stdout stderr)
            elif String.IsNullOrWhiteSpace stdout then
                return Ok()
            else
                return Error "target worktree is dirty; refusing ff-only merge"
        }

    let private verifyHead runner repo (expected: CommitHash) =
        task {
            let! code, stdout, stderr = runner (command repo [ "rev-parse"; "HEAD" ])

            if code <> 0 then
                return Error(failure stdout stderr)
            elif stdout.Trim() = CommitHash.value expected then
                return Ok expected
            else
                return
                    Error(
                        sprintf
                            "ff-only merge did not advance HEAD to candidate %s (got %s)"
                            (CommitHash.value expected)
                            (stdout.Trim())
                    )
        }

    /// ORCH-005 CAS: the ref moved between the head read and the update.
    ///
    /// Recognised from git's own lock diagnostics. A moved target is not a failure
    /// to report upward — ORCH-005 answers it by rebasing and re-reviewing, and the
    /// old post-rebase witness is discarded.
    let private isRefMoved (message: string) =
        [ "cannot lock ref"; "update-ref"; "expected" ]
        |> List.exists (fun marker -> message.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)

    let private revParse runner repo (spec: string) (missing: string) =
        task {
            let! code, stdout, stderr = runner (command repo [ "rev-parse"; spec ])

            if code <> 0 then
                return
                    Error(
                        if String.IsNullOrWhiteSpace stderr then
                            missing
                        else
                            stderr.Trim()
                    )
            elif String.IsNullOrWhiteSpace stdout then
                return Error missing
            else
                return Ok(CommitHash.create (stdout.Trim()))
        }

    let private targetSpec (target: TargetRef) =
        sprintf "refs/heads/%s" (TargetRef.value target)

    /// ff-only publish inside the short Integration Gate (ORCH-005).
    ///
    /// `expectedHead` is mandatory. Every publish is preceded by a head read inside
    /// the gate, so there is no legitimate "publish without an expectation" — and
    /// making it optional is what allowed a lost update.
    let private ffMerge
        (runner: Command -> Task<int * string * string>)
        repoPath
        (worktree: WorktreePath)
        (target: TargetRef)
        (expectedHead: CommitHash)
        : Task<Result<CommitHash, string>> =
        task {
            match! revParse runner (WorktreePath.value worktree) "HEAD" "candidate HEAD is empty" with
            | Error error -> return Error error
            | Ok candidate ->
                // ORCH-008: the publish target is frozen at fork time. If the repo has
                // since moved to another branch, refuse rather than publish to whichever
                // branch happens to be checked out.
                let! branchCode, branchOut, _ = runner (command repoPath [ "symbolic-ref"; "--short"; "HEAD" ])

                if branchCode <> 0 || branchOut.Trim() <> TargetRef.value target then
                    return
                        Error(
                            sprintf
                                "publish branch mismatch: target repo is on '%s' but publish is frozen to '%s'"
                                (if branchCode = 0 then
                                     branchOut.Trim()
                                 else
                                     "<detached HEAD>")
                                (TargetRef.value target)
                        )
                else
                    match! revParse runner repoPath (targetSpec target) "target branch not found" with
                    | Error error -> return Error error
                    | Ok currentHead when currentHead <> expectedHead ->
                        return Error OrchestratorConstants.targetRefMovedError
                    | Ok currentHead ->
                        let! ancestorCode, _, ancestorError =
                            runner (
                                command
                                    repoPath
                                    [ "merge-base"
                                      "--is-ancestor"
                                      CommitHash.value currentHead
                                      CommitHash.value candidate ]
                            )

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
                                    runner (command repoPath [ "merge"; "--ff-only"; CommitHash.value candidate ])

                                if mergeCode = 0 then
                                    return! verifyHead runner repoPath candidate
                                else
                                    let message = failure mergeOut mergeError

                                    if isRefMoved message then
                                        return Error OrchestratorConstants.targetRefMovedError
                                    else
                                        return Error message
        }

    let createWithRepo repoPath (runner: Command -> Task<int * string * string>) : GitPort =
        { IsDirty = WorktreeCommands.isDirty runner
          CreateWorktree = WorktreeCommands.create runner repoPath

          // ORCH-008: freeze by symbolic-ref. A detached HEAD has no branch to
          // publish to, so this fails closed instead of resolving HEAD to a commit
          // and treating that as the target.
          FreezeTargetBranch =
            fun () ->
                task {
                    let! code, stdout, stderr = runner (command repoPath [ "symbolic-ref"; "--short"; "HEAD" ])

                    if code <> 0 || String.IsNullOrWhiteSpace stdout then
                        return
                            Error(
                                if String.IsNullOrWhiteSpace stderr then
                                    "cannot freeze target branch: repository HEAD is detached"
                                else
                                    stderr.Trim()
                            )
                    else
                        return Ok(TargetRef.create (stdout.Trim()))
                }

          Rebase =
            fun worktree target ->
                task {
                    let dir = WorktreePath.value worktree
                    // ORCH-003: continue only when rebase-merge/rebase-apply exists.
                    // A bare REBASE_HEAD ref can be stale (no rebase in progress).
                    let! mergeCode, mergePath, _ = runner (command dir [ "rev-parse"; "--git-path"; "rebase-merge" ])

                    let! applyCode, applyPath, _ = runner (command dir [ "rev-parse"; "--git-path"; "rebase-apply" ])

                    let inProgress =
                        let exists code path =
                            code = 0
                            && not (String.IsNullOrWhiteSpace path)
                            && System.IO.Directory.Exists(path.Trim())

                        exists mergeCode mergePath || exists applyCode applyPath

                    if inProgress then
                        // Stage any Manager/Coder resolution before continue (ORCH-003).
                        // ResumeManager's finalizeWorktree should already have staged, but a
                        // missed finalize leaves unmerged paths; add here is idempotent.
                        let! addCode, _, addErr = runner (command dir [ "add"; "-A" ])

                        if addCode <> 0 then
                            return Error(failure "" addErr)
                        else
                            let! code, stdout, stderr =
                                runner (command dir [ "-c"; "core.editor=true"; "rebase"; "--continue" ])

                            return if code = 0 then Ok() else Error(failure stdout stderr)
                    else
                        // Clear stale REBASE_HEAD so the fresh rebase is not confused.
                        let! _ = runner (command dir [ "update-ref"; "-d"; "REBASE_HEAD" ])
                        let! code, stdout, stderr = runner (command dir [ "rebase"; TargetRef.value target ])
                        return if code = 0 then Ok() else Error(failure stdout stderr)
                }

          ConflictedFiles =
            fun worktree ->
                task {
                    let! code, stdout, stderr =
                        runner (command (WorktreePath.value worktree) [ "diff"; "--name-only"; "--diff-filter=U" ])

                    if code <> 0 then
                        return Error(failure stdout stderr)
                    else
                        return
                            stdout.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
                            |> Array.toList
                            |> Ok
                }

          FfMerge = ffMerge runner repoPath
          RemoveWorktree = WorktreeCommands.remove runner

          HasRebaseHead =
            fun worktree ->
                task {
                    let dir = WorktreePath.value worktree

                    let! mergeCode, mergePath, _ = runner (command dir [ "rev-parse"; "--git-path"; "rebase-merge" ])

                    let! applyCode, applyPath, _ = runner (command dir [ "rev-parse"; "--git-path"; "rebase-apply" ])

                    let exists code path =
                        code = 0
                        && not (String.IsNullOrWhiteSpace path)
                        && System.IO.Directory.Exists(path.Trim())

                    return exists mergeCode mergePath || exists applyCode applyPath
                }

          ListWorktrees = WorktreeCommands.list runner repoPath
          ListManagerBranches = WorktreeCommands.listBranches runner repoPath
          DeleteBranch = WorktreeCommands.deleteBranch runner repoPath
          ReadHead = fun path -> revParse runner (WorktreePath.value path) "HEAD" "HEAD is empty"

          GetTargetHead =
            fun target ->
                revParse
                    runner
                    repoPath
                    (targetSpec target)
                    (sprintf "target branch not found: %s" (TargetRef.value target)) }

    let createWithRunner runner = createWithRepo "." runner
