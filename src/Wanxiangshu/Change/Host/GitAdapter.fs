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

    let private readText (spool: Spool.StreamingSpool option) =
        match spool with
        | None -> ""
        | Some value ->
            let chunks = ResizeArray<byte[]>()
            Spool.readChunksSync value.Path chunks.Add
            chunks |> Seq.toArray |> Array.concat |> ProcessOutput.decodeText

    let private streamOf (slot: Spool.StreamingSpool option ref) =
        match slot.Value with
        | Some value -> value
        | None ->
            let value = Spool.startStreamingSpool ()
            slot.Value <- Some value
            value

    let private readExitText stdout stderr (exit: TextProcessExit) =
        try
            Ok
                { exit with
                    Stdout = readText stdout
                    Stderr = readText stderr }
        with error ->
            Error(
                ProcessResult.Observed
                    { End = EndEvidence.Confirmed(ProgramEnd.Exited exit.ExitCode)
                      StopRequested = None
                      Output = None
                      Fault = Some error.Message }
            )

    let private readOutput stdout stderr result =
        ProcessResult.requireText result |> Result.bind (readExitText stdout stderr)

    let run (cmd: Command) : Task<Result<TextProcessExit, ProcessResult>> =
        task {
            // DSL-MUTABLE: resource — separate output streams owned by this invocation.
            let stdout = ref None
            let stderr = ref None
            let closed = ref false

            let append (slot: Spool.StreamingSpool option ref) (bytes: byte[]) =
                if not closed.Value && not (isNull bytes) && bytes.Length > 0 then
                    Spool.appendStreamingSpool (streamOf slot) bytes

            let host command context _ _ cancellation =
                NodeProcessHost.spawn command context (append stdout) (append stderr) cancellation

            let context =
                { WorkingDirectory = cmd.WorkingDirectory
                  HardLimit = ProcessEstimate.DefaultHardLimit }

            try
                let! result = ProcessRunner.runWithHost host cmd estimate context CancellationToken.None
                closed.Value <- true

                return readOutput stdout.Value stderr.Value result
            finally
                closed.Value <- true
                stdout.Value |> Option.iter (fun spool -> Spool.delete spool.Path)
                stderr.Value |> Option.iter (fun spool -> Spool.delete spool.Path)
        }

    let command (dir: string) (args: string list) : Command =
        { FileName = GitSubject.Executable
          Arguments = args
          WorkingDirectory = Some dir
          Environment = None
          Stdin = None }

    let private execute (runner: TextProcessRunner) cmd =
        task {
            let! result = runner cmd
            return Result.mapError ProcessResult.describe result
        }

    /// True when an in-progress rebase is present (rebase-merge / rebase-apply,
    /// never a stale REBASE_HEAD; ORCH-003). One reader: WorktreeCommands.
    let hasRebaseHead (runner: TextProcessRunner) (worktree: string) =
        WorktreeCommands.rebaseInProgress runner worktree

    /// After a manager terminal: stage everything, then either continue an
    /// in-flight rebase or create the candidate commit.
    ///
    /// Stage first (`git add -A`), then refuse leftover unmerged paths / conflict
    /// markers. Checking unmerged *before* add rejected a resolved file that was
    /// still unstaged (ORCH-003 conflict resume: Coder rewrote the file; Manager
    /// joined; finalize must stage then continue).
    let private continueRebase (runner: TextProcessRunner) (worktree: string) : Task<Result<unit, string>> =
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

            do!
                if deleted.ExitCode = 0 then
                    Ok()
                else
                    Error(sprintf "git stale-ref cleanup failed: %s" deleted.Stderr)

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
            let! result =
                execute runner (command worktree [ "grep"; "-I"; "-n"; "-E"; "^<<<<<<< |^>>>>>>> "; "--"; "." ])

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
