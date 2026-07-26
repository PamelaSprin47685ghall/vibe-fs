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

            let! res = Runner.execute cmd estimate ctx CancellationToken.None

            match res with
            | Ok(RunnerOutcome.Completed(code, stdout, stderr, _)) -> return (code, stdout, stderr)
            | Ok(RunnerOutcome.Spooled(code, _, _, _)) -> return (code, "", "output spooled")
            | Ok(RunnerOutcome.OutputExceeded(bytes, _)) ->
                return (1, "", sprintf "git output exceeded (%d bytes)" bytes)
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

    /// After a manager terminal: stage everything, then either continue an
    /// in-flight rebase or create the candidate commit.
    let finalizeWorktree
        (runner: Command -> Task<int * string * string>)
        (managerId: string)
        (worktree: string)
        : Task<Result<unit, string>> =
        task {
            let! addCode, _, addErr = runner (command worktree [ "add"; "-A" ])

            if addCode <> 0 then
                return Error(sprintf "git add failed: %s" addErr)
            else
                let! headCode, _, _ = runner (command worktree [ "rev-parse"; "--verify"; "REBASE_HEAD" ])

                if headCode = 0 then
                    let! contCode, _, contErr =
                        runner (command worktree [ "-c"; "core.editor=true"; "rebase"; "--continue" ])

                    if contCode = 0 then
                        return Ok()
                    else
                        return Error(sprintf "git rebase --continue failed: %s" contErr)
                else
                    let! commitCode, commitOut, commitErr =
                        runner (command worktree [ "commit"; "-m"; sprintf "candidate: %s" managerId ])

                    if commitCode = 0 then
                        return Ok()
                    elif (commitOut + commitErr).Contains("nothing to commit") then
                        return Ok()
                    else
                        return Error(sprintf "git commit failed: %s" commitErr)
        }
