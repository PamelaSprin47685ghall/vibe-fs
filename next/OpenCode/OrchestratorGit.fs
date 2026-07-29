namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Process

/// Git command plumbing for the production Orchestrator host.
module OrchestratorGit =

    let private estimate =
        { EstimatedRuntime = RuntimeSeconds 30.0
          EstimatedOutput = OutputBytes 65536L
          EstimatedMemory = EstimatedMemory.Medium }

    let run (cmd: Command) : Task<int * string * string> =
        task {
            let ctx =
                { WorkingDirectory = cmd.WorkingDirectory
                  DefaultTimeout = None }

            let! res = ProcessRunner.run cmd estimate ctx CancellationToken.None

            match res with
            | Ok(ProcessOutcome.Completed(code, stdout, stderr, _)) -> return (code, stdout, stderr)
            | Ok(ProcessOutcome.Spooled(code, _, _, _)) -> return (code, "", "output spooled")
            | Error err -> return (1, "", sprintf "%A" err)
        }

    let command (dir: string) (args: string list) : Command =
        { FileName = "git"
          Arguments = args
          WorkingDirectory = Some dir
          Environment = None
          Stdin = None
          Deadline = None
          PtyOptions = None }

    let detectBranch
        (runner: Command -> Task<int * string * string>)
        (repoPath: string)
        : Task<Result<string, string>> =
        task {
            let! code, stdout, stderr = runner (command repoPath [ "symbolic-ref"; "--short"; "HEAD" ])

            if code = 0 && not (String.IsNullOrWhiteSpace stdout) then
                return Ok(stdout.Trim())
            else
                // Fail closed: a detached HEAD or missing branch must not publish
                // to a guessed branch name.
                let reason =
                    if String.IsNullOrWhiteSpace stderr then
                        "could not determine current branch (detached HEAD or no branch)"
                    else
                        stderr.Trim()

                return Error reason
        }

    /// True when an in-progress rebase is present (REBASE_HEAD exists). Shared by
    /// the publish chain skip-check and finalizeWorktree so both agree on rebase
    /// state.
    let hasRebaseHead (runner: Command -> Task<int * string * string>) (worktree: string) : Task<bool> =
        task {
            let! code, _, _ = runner (command worktree [ "rev-parse"; "--verify"; "REBASE_HEAD" ])
            return code = 0
        }

    /// After a manager terminal: stage everything, then either continue an
    /// in-flight rebase or create the candidate commit.
    let finalizeWorktree
        (runner: Command -> Task<int * string * string>)
        (managerId: string)
        (worktree: string)
        : Task<Result<unit, string>> =
        task {
            let! addCode, _, addErr = runner (command worktree [ "add"; "-A" ])

            match addCode with
            | code when code <> 0 -> return Error(sprintf "git add failed: %s" addErr)
            | _ ->
                let! hasRb = hasRebaseHead runner worktree

                match hasRb with
                | true ->
                    let! contCode, _, contErr =
                        runner (command worktree [ "-c"; "core.editor=true"; "rebase"; "--continue" ])

                    match contCode with
                    | 0 -> return Ok()
                    | _ -> return Error(sprintf "git rebase --continue failed: %s" contErr)
                | false ->
                    let! commitCode, commitOut, commitErr =
                        runner (command worktree [ "commit"; "-m"; sprintf "candidate: %s" managerId ])

                    match commitCode with
                    | 0 -> return Ok()
                    | _ when (commitOut + commitErr).Contains("nothing to commit") -> return Ok()
                    | _ -> return Error(sprintf "git commit failed: %s" commitErr)
        }
