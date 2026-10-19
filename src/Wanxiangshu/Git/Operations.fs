namespace Wanxiangshu.Git

open System
open System.Threading.Tasks
open Wanxiangshu.Change
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process

/// Typed Git verbs. Process command construction and output classification are
/// contained here; workflow sequencing stays in OrchestratorProgram.
module GitOperations =

    let private execute (runner: TextProcessRunner) cmd =
        task {
            let! result = runner cmd
            return Result.mapError ProcessResult.describe result
        }

    let private command repo args =
        { FileName = "git"
          Arguments = args
          WorkingDirectory = Some repo
          Environment = None
          Stdin = None }

    let private failure stdout stderr =
        if String.IsNullOrWhiteSpace stderr then stdout else stderr

    let private commandOutcome (result: TextProcessExit) =
        if result.ExitCode = 0 then
            Ok()
        else
            Error(failure result.Stdout result.Stderr)

    let private absoluteGitPath (directory: string) (path: string) =
        if path.StartsWith "/" then
            path
        else
            System.IO.Path.Combine(directory, path)

    let private stderrOr (missing: string) (stderr: string) =
        if String.IsNullOrWhiteSpace stderr then
            missing
        else
            stderr.Trim()

    let private verifyClean runner repo =
        taskResult {
            let! result = execute runner (command repo [ "status"; "--porcelain" ])

            if result.ExitCode <> 0 then
                return! Error(failure result.Stdout result.Stderr)
            elif String.IsNullOrWhiteSpace result.Stdout then
                return ()
            else
                return! Error "target worktree is dirty; refusing ff-only merge"
        }

    let private verifyHead runner repo (expected: CommitHash) =
        taskResult {
            let! result = execute runner (command repo [ "rev-parse"; "HEAD" ])

            if result.ExitCode <> 0 then
                return! Error(failure result.Stdout result.Stderr)
            elif result.Stdout.Trim() = CommitHash.value expected then
                return expected
            else
                return!
                    Error(
                        sprintf
                            "ff-only merge did not advance HEAD to candidate %s (got %s)"
                            (CommitHash.value expected)
                            (result.Stdout.Trim())
                    )
        }

    /// ORCH-005 CAS: the ref moved between the head read and the update.
    ///
    /// Recognised from git's own lock diagnostics. A moved target is not a failure
    /// to report upward — ORCH-005 answers it by rebasing and re-reviewing, and the
    /// old post-rebase witness is discarded.
    let private isRefMoved (message: string) =
        [ "cannot lock ref"; "is at "; "but expected " ]
        |> List.forall (fun marker -> message.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)

    let private revParse runner repo (spec: string) (missing: string) =
        taskResult {
            let! result = execute runner (command repo [ "rev-parse"; spec ])

            if result.ExitCode <> 0 then
                return! Error(stderrOr missing result.Stderr)
            elif String.IsNullOrWhiteSpace result.Stdout then
                return! Error missing
            else
                return CommitHash.create (result.Stdout.Trim())
        }

    let private targetSpec (target: TargetRef) =
        sprintf "refs/heads/%s" (TargetRef.value target)

    let private checkedOutBranch (branchCode: int) (branchOut: string) =
        if branchCode = 0 then
            branchOut.Trim()
        else
            "<detached HEAD>"

    let private requirePublishBranch
        (runner: TextProcessRunner)
        repoPath
        (target: TargetRef)
        : Task<Result<unit, string>> =
        taskResult {
            let! result = execute runner (command repoPath [ "symbolic-ref"; "--quiet"; "--short"; "HEAD" ])

            if result.ExitCode > 1 then
                return! Error(stderrOr "cannot read publish branch" result.Stderr)
            elif result.ExitCode <> 0 || result.Stdout.Trim() <> TargetRef.value target then
                return!
                    Error(
                        sprintf
                            "publish branch mismatch: target repo is on '%s' but publish is frozen to '%s'"
                            (checkedOutBranch result.ExitCode result.Stdout)
                            (TargetRef.value target)
                    )
            else
                return ()
        }

    let private assertExpectedHead (currentHead: CommitHash) (expectedHead: CommitHash) =
        if currentHead <> expectedHead then
            Error OrchestratorConstants.targetRefMovedError
        else
            Ok()

    let private requireAncestor
        (runner: TextProcessRunner)
        repoPath
        (currentHead: CommitHash)
        (candidate: CommitHash)
        : Task<Result<unit, string>> =
        taskResult {
            let! result =
                execute
                    runner
                    (command
                        repoPath
                        [ "merge-base"
                          "--is-ancestor"
                          CommitHash.value currentHead
                          CommitHash.value candidate ])

            if result.ExitCode <> 0 then
                return! Error(stderrOr "candidate is not a fast-forward of the target branch" result.Stderr)
            else
                return ()
        }

    let private mergeFailure message =
        if isRefMoved message then
            OrchestratorConstants.targetRefMovedError
        else
            message

    let private mergeFf
        (runner: TextProcessRunner)
        repoPath
        (candidate: CommitHash)
        : Task<Result<CommitHash, string>> =
        taskResult {
            let! result = execute runner (command repoPath [ "merge"; "--ff-only"; CommitHash.value candidate ])

            if result.ExitCode = 0 then
                return! verifyHead runner repoPath candidate
            else
                let message = failure result.Stdout result.Stderr
                let classified = mergeFailure message
                return! Error classified
        }

    /// ff-only publish inside the short Integration Gate (ORCH-005).
    ///
    /// `expectedHead` is mandatory. Every publish is preceded by a head read inside
    /// the gate, so there is no legitimate "publish without an expectation" — and
    /// making it optional is what allowed a lost update.
    let private ffMerge
        (runner: TextProcessRunner)
        repoPath
        (worktree: WorktreePath)
        (target: TargetRef)
        (expectedHead: CommitHash)
        (pinnedCandidate: CommitHash)
        : Task<Result<CommitHash, string>> =
        taskResult {
            let! candidate = revParse runner (WorktreePath.value worktree) "HEAD" "candidate HEAD is empty"
            // R12: the physical FF is bound to the durable pin. The worktree
            // candidate must equal the pin; the merge itself runs on the pin
            // so a worktree move after the read cannot redirect the publish.
            do!
                if candidate <> pinnedCandidate then
                    Error(
                        sprintf
                            "worktree HEAD %s does not match pinned candidate %s"
                            (CommitHash.value candidate)
                            (CommitHash.value pinnedCandidate)
                    )
                else
                    Ok()
            // ORCH-008: the publish target is frozen at fork time. If the repo has
            // since moved to another branch, refuse rather than publish to whichever
            // branch happens to be checked out.
            do! requirePublishBranch runner repoPath target
            let! currentHead = revParse runner repoPath (targetSpec target) "target branch not found"
            do! assertExpectedHead currentHead expectedHead
            do! requireAncestor runner repoPath currentHead pinnedCandidate
            do! verifyClean runner repoPath
            return! mergeFf runner repoPath pinnedCandidate
        }

    let continueRebase (runner: TextProcessRunner) dir =
        taskResult {
            // Stage any Manager/Coder resolution before continue (ORCH-003).
            // Predecessor's finalizeWorktree should already have staged, but a
            // missed finalize leaves unmerged paths; add here is idempotent.
            let! staged = execute runner (command dir [ "add"; "-A" ])

            if staged.ExitCode <> 0 then
                return! Error(failure staged.Stdout staged.Stderr)
            else
                let! result = execute runner (command dir [ "-c"; "core.editor=true"; "rebase"; "--continue" ])

                return! commandOutcome result
        }

    let stageAll (runner: TextProcessRunner) dir =
        taskResult {
            let! result = execute runner (command dir [ "add"; "-A" ])

            return!
                if result.ExitCode = 0 then
                    Ok()
                else
                    Error(failure result.Stdout result.Stderr)
        }

    let candidateCommit (runner: TextProcessRunner) dir (msg: string) =
        taskResult {
            let! deleted = execute runner (command dir [ "update-ref"; "-d"; "REBASE_HEAD" ])

            do!
                if deleted.ExitCode = 0 then
                    Ok()
                else
                    Error(failure deleted.Stdout deleted.Stderr)

            let! result = execute runner (command dir [ "commit"; "-m"; msg ])

            return!
                if result.ExitCode = 0 then
                    Ok()
                else
                    Error(failure result.Stdout result.Stderr)
        }

    let private freshRebase (runner: TextProcessRunner) dir (target: TargetRef) =
        taskResult {
            // Clear stale REBASE_HEAD so the fresh rebase is not confused.
            let! deleted = execute runner (command dir [ "update-ref"; "-d"; "REBASE_HEAD" ])

            do!
                if deleted.ExitCode = 0 then
                    Ok()
                else
                    Error(failure deleted.Stdout deleted.Stderr)

            let! result = execute runner (command dir [ "rebase"; TargetRef.value target ])

            return!
                if result.ExitCode = 0 then
                    Ok()
                else
                    Error(failure result.Stdout result.Stderr)
        }

    let private rebaseInProgress (runner: TextProcessRunner) dir =
        taskResult {
            // ORCH-003: continue only when rebase-merge/rebase-apply exists.
            // A bare REBASE_HEAD ref can be stale (no rebase in progress).
            let! merge = execute runner (command dir [ "rev-parse"; "--git-path"; "rebase-merge" ])

            do!
                if merge.ExitCode = 0 then
                    Ok()
                else
                    Error(failure merge.Stdout merge.Stderr)

            do!
                if String.IsNullOrWhiteSpace merge.Stdout then
                    Error "git rebase-state query returned no path"
                else
                    Ok()

            let! apply = execute runner (command dir [ "rev-parse"; "--git-path"; "rebase-apply" ])

            do!
                if apply.ExitCode = 0 then
                    Ok()
                else
                    Error(failure apply.Stdout apply.Stderr)

            do!
                if String.IsNullOrWhiteSpace apply.Stdout then
                    Error "git rebase-state query returned no path"
                else
                    Ok()

            let exists (path: string) =
                if String.IsNullOrWhiteSpace path then
                    false
                else
                    let path = path.Trim()
                    let absolute = absoluteGitPath dir path
                    System.IO.Directory.Exists absolute

            return exists merge.Stdout || exists apply.Stdout
        }

    let createWithRepo repoPath (runner: TextProcessRunner) : GitPort =
        { IsDirty = WorktreeCommands.isDirty runner
          CreateWorktree = WorktreeCommands.create runner repoPath

          // ORCH-008: freeze by symbolic-ref. A detached HEAD has no branch to
          // publish to, so this fails closed instead of resolving HEAD to a commit
          // and treating that as the target.
          FreezeTargetBranch =
            fun () ->
                taskResult {
                    let! result = execute runner (command repoPath [ "symbolic-ref"; "--quiet"; "--short"; "HEAD" ])

                    if result.ExitCode > 1 then
                        return!
                            Error(
                                stderrOr
                                    (sprintf "cannot freeze target branch: git exited %d" result.ExitCode)
                                    result.Stderr
                            )
                    elif result.ExitCode <> 0 || String.IsNullOrWhiteSpace result.Stdout then
                        return! Error(stderrOr "cannot freeze target branch: repository HEAD is detached" result.Stderr)
                    else
                        return TargetRef.create (result.Stdout.Trim())
                }

          Rebase =
            fun worktree target ->
                taskResult {
                    let dir = WorktreePath.value worktree
                    let! inProgress = rebaseInProgress runner dir

                    if inProgress then
                        return! continueRebase runner dir
                    else
                        return! freshRebase runner dir target
                }

          ConflictedFiles =
            fun worktree ->
                taskResult {
                    let! result =
                        execute
                            runner
                            (command (WorktreePath.value worktree) [ "diff"; "--name-only"; "--diff-filter=U" ])

                    if result.ExitCode <> 0 then
                        return! Error(failure result.Stdout result.Stderr)
                    else
                        return
                            result.Stdout.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
                            |> Array.toList
                }

          FfMerge = ffMerge runner repoPath
          RemoveWorktree = WorktreeCommands.remove runner repoPath

          HasRebaseHead =
            fun worktree ->
                task {
                    let dir = WorktreePath.value worktree
                    return! rebaseInProgress runner dir
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
