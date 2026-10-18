namespace Wanxiangshu.Change.Host

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Git
open Wanxiangshu.Process
open Wanxiangshu.Foundation

/// Git command plumbing for the production Orchestrator host.
module OrchestratorGit =

    let private estimate =
        { EstimatedRuntime = RuntimeSeconds 30.0
          EstimatedOutput = OutputBytes 65536L
          EstimatedMemory = EstimatedMemory.Medium }

    let createRunner (run: RunProcess) (cmd: Command) =
        task {
            let ctx =
                { WorkingDirectory = cmd.WorkingDirectory
                  HardLimit = ProcessEstimate.DefaultHardLimit }

            let! res = run cmd estimate ctx CancellationToken.None

            match res with
            | ProcessResult.Observed { Output = Some(CapturedOutput.Spooled(path, _, _)) } -> Spool.delete path
            | _ -> ()
            return ProcessResult.requireText res
        }

    let command (dir: string) (args: string list) : Command =
        { FileName = GitSubject.Executable
          Arguments = args
          WorkingDirectory = Some dir
          Environment = None
          Stdin = None }

    /// True when an in-progress rebase is present. Prefer rebase-merge / rebase-apply
    /// over a bare REBASE_HEAD ref: a stale REBASE_HEAD can survive after a failed
    /// continue and would make `rebase --continue` report "no rebase in progress"
    /// while HasRebaseHead still returns true (ORCH-003 conflict resume).
    let private execute (runner: TextProcessRunner) cmd =
        task {
            let! result = runner cmd
            return Result.mapError ProcessResult.describe result
        }

    let hasRebaseHead (runner: TextProcessRunner) (worktree: string) =
        let pathFor name =
            taskResult {
                let! result = execute runner (command worktree [ "rev-parse"; "--git-path"; name ])
                if result.ExitCode <> 0 || String.IsNullOrWhiteSpace result.Stdout then
                    return! Error(sprintf "git rebase-state query failed: %s" result.Stderr)
                else
                    let path = result.Stdout.Trim()
                    let absolute = if path.StartsWith "/" then path else System.IO.Path.Combine(worktree, path)
                    return System.IO.Directory.Exists absolute
            }
        taskResult {
            let! merge = pathFor "rebase-merge"
            let! apply = pathFor "rebase-apply"
            return merge || apply
        }

    /// After a manager terminal: stage everything, then either continue an
    /// in-flight rebase or create the candidate commit.
    ///
    /// Stage first (`git add -A`), then refuse leftover unmerged paths / conflict
    /// markers. Checking unmerged *before* add rejected a resolved file that was
    /// still unstaged (ORCH-003 conflict resume: Coder rewrote the file; Manager
    /// joined; finalize must stage then continue).
    let private continueRebase
        (runner: TextProcessRunner)
        (worktree: string)
        : Task<Result<unit, string>> =
        taskResult {
            let! result = execute runner (command worktree [ "-c"; "core.editor=true"; "rebase"; "--continue" ])

            if result.ExitCode = 0 then
                return ()
            else
                return! Error(sprintf "git rebase --continue failed: %s" result.Stderr)
        }

    // semantic-decorator-owner: change-integration
    // semantic-decorator-WHAT: change-integration-003
    // semantic-decorator-trace-relation: delete stale REBASE_HEAD exactly once before the single candidate commit attempt
    // semantic-decorator-proof: requirements/change-integration/tests/003.test.mjs::WHAT[change-integration-003] GIT_candidate_commit_deletes_stale_rebase_head_before_commit_and_surfaces_failure
    // semantic-decorator-failure-policy: require successful stale-ref deletion; surface commit stderr unless git reports nothing to commit
    // semantic-decorator-cancel-policy: cancellation is owned by the injected command runner and stops before the next command
    // semantic-decorator-deadline-policy: each command uses the runner deadline; no sequence-level deadline extension
    // semantic-decorator-invocation-bound: 2
    let private commitCandidate
        (runner: TextProcessRunner)
        (managerId: string)
        (worktree: string)
        : Task<Result<unit, string>> =
        taskResult {
            let! deleted = execute runner (command worktree [ "update-ref"; "-d"; "REBASE_HEAD" ])
            do! if deleted.ExitCode = 0 then Ok() else Error(sprintf "git stale-ref cleanup failed: %s" deleted.Stderr)

            let! result = execute runner (command worktree [ "commit"; "-m"; sprintf "candidate: %s" managerId ])

            if result.ExitCode = 0 then
                return ()
            elif (result.Stdout + result.Stderr).Contains("nothing to commit") then
                return ()
            else
                return! Error(sprintf "git commit failed: %s" result.Stderr)
        }

    let private finalizeCommitOrRebase
        (runner: TextProcessRunner)
        (managerId: string)
        (worktree: string)
        : Task<Result<unit, string>> =
        taskResult {
            let! hasRb = hasRebaseHead runner worktree

            if hasRb then
                return! continueRebase runner worktree
            else
                return! commitCandidate runner managerId worktree
        }

    let private refuseConflictMarkers
        (runner: TextProcessRunner)
        (managerId: string)
        (worktree: string)
        : Task<Result<unit, string>> =
        taskResult {
            let! result = execute runner (command worktree [ "grep"; "-I"; "-n"; "-E"; "^<<<<<<< |^>>>>>>> "; "--"; "." ])

            // git grep exit 0 = matches, 1 = no match, >1 = error
            if result.ExitCode = 0 && not (String.IsNullOrWhiteSpace result.Stdout) then
                return! Error(sprintf "conflict markers remain in worktree:\n%s" (result.Stdout.Trim()))
            elif result.ExitCode > 1 then
                return! Error "conflict-marker scan failed"
            else
                return! finalizeCommitOrRebase runner managerId worktree
        }

    let private afterGitAdd
        (runner: TextProcessRunner)
        (managerId: string)
        (worktree: string)
        : Task<Result<unit, string>> =
        taskResult {
            let! result = execute runner (command worktree [ "diff"; "--name-only"; "--diff-filter=U" ])

            if result.ExitCode <> 0 then
                return! Error(sprintf "conflict scan failed: %s" result.Stderr)
            elif not (String.IsNullOrWhiteSpace result.Stdout) then
                return! Error(sprintf "unmerged paths remain after stage: %s" (result.Stdout.Trim()))
            else
                return! refuseConflictMarkers runner managerId worktree
        }

    let finalizeWorktree
        (runner: TextProcessRunner)
        (managerId: string)
        (worktree: string)
        : Task<Result<unit, string>> =
        taskResult {
            let! result = execute runner (command worktree [ "add"; "-A" ])

            if result.ExitCode <> 0 then
                return! Error(sprintf "git add failed: %s" result.Stderr)
            else
                return! afterGitAdd runner managerId worktree
        }
