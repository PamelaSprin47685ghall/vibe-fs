namespace Wanxiangshu.Git

open System
open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Change
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process

module private WorktreeDisposal =
#if FABLE_COMPILER
    [<Emit("$0")>]
    let asValueTask (operation: Task) : ValueTask = jsNative
#else
    let asValueTask (operation: Task) = ValueTask(operation)
#endif

/// Owned manager worktree. Release is idempotent; DisposeAsync performs the
/// same physical cleanup when a program exits before publish.
///
/// `Identity` is the stable name (the `manager/<job>` branch) and `Path` is where
/// it currently lives. ORCH-006 keeps both for exactly this reason: recovery
/// locates by identity, diagnostics show the path, and a moved worktree must not
/// orphan its job.
type WorktreeResource
    private (path: WorktreePath, identity: WorktreeIdentity, git: GitPort, releaseOnDisposeInitially: bool) =
    // DSL-MUTABLE: resource — shared physical release receipt, including failure.
    let mutable release: Task<Result<unit, string>> option = None
    // DSL-MUTABLE: resource — durable-mark flag for worktree dispose policy
    let mutable releaseOnDispose = releaseOnDisposeInitially

    let deleteAfterRemove result =
        task {
            match result with
            | Error error -> return Error("worktree=" + error)
            | Ok() ->
                let! branch = git.DeleteBranch identity
                return Result.mapError (fun error -> "branch=" + error) branch
        }

    let releaseOwned () =
        task {
            try
                let! removed = git.RemoveWorktree path
                return! deleteAfterRemove removed
            with error ->
                return Error error.Message
        }

    let settleRelease (receipt: TaskCompletionSource<Result<unit, string>>) =
        task {
            let! result = releaseOwned ()
            receipt.SetResult result
        }

    member _.Path = path
    member _.Identity = identity
    member _.MarkDurable() = releaseOnDispose <- false

    member _.Release() =
        match release with
        | Some operation -> operation
        | None ->
            let receipt = TaskCompletionSource<Result<unit, string>>()
            release <- Some receipt.Task
            settleRelease receipt |> ignore
            receipt.Task

    member private this.DisposeRelease() =
        WorktreeDisposal.asValueTask (
            task {
                try
                    let! _ = this.Release()
                    ()
                with _ ->
                    ()
            }
        )

    interface IAsyncDisposable with
        member this.DisposeAsync() =
            if releaseOnDispose then
                this.DisposeRelease()
            else
                WorktreeDisposal.asValueTask (AsyncSupport.completedTask ())

    static member Create(git: GitPort, jobId: ManagerJobId, path: WorktreePath) =
        task {
            match! git.CreateWorktree jobId path with
            | Error error -> return Error error
            | Ok identity -> return Ok(WorktreeResource(path, identity, git, true))
        }

    /// Re-adopt an existing worktree during recovery (ORCH-007: never create a new
    /// one). The identity comes from the durable job record, not from re-deriving it
    /// from the id — a job whose branch was renamed must still be found.
    static member Adopt(git: GitPort, identity: WorktreeIdentity, path: WorktreePath) =
        WorktreeResource(path, identity, git, false)

/// Process-backed worktree verbs used by GitOperations.
///
/// `repo` is bound once by `GitOperations.createWithRepo`; no verb takes it as a
/// per-call argument, so a caller cannot address a repository the port was not
/// built for.
module WorktreeCommands =

    let private execute (runner: TextProcessRunner) cmd =
        task {
            let! result = runner cmd
            return Result.mapError ProcessResult.describe result
        }

    /// The branch a job's worktree lives on IS its stable identity (ORCH-006).
    ///
    /// Spelled once. It used to appear in `WorktreeResource.Create`, `Adopt` and
    /// `create` — three copies of one naming rule, and `Adopt` re-derived it during
    /// recovery instead of reading the durable record.
    let identityOf (jobId: ManagerJobId) =
        WorktreeIdentity.create (GitPorcelain.managerBranchOf (ManagerJobId.value jobId))

    let private command cwd args =
        { FileName = GitSubject.Executable
          Arguments = args
          WorkingDirectory = cwd
          Environment = None
          Stdin = None }

    let private outcome code stdout stderr =
        if code = 0 then
            Ok()
        else
            Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)

    let private failure stdout stderr =
        if String.IsNullOrWhiteSpace stderr then stdout else stderr

    let isDirty (runner: TextProcessRunner) (path: WorktreePath) =
        taskResult {
            let! result = execute runner (command (Some(WorktreePath.value path)) [ "status"; "--porcelain" ])

            if result.ExitCode <> 0 then
                return! Error(failure result.Stdout result.Stderr)
            else
                return not (String.IsNullOrWhiteSpace result.Stdout)
        }

    let create (runner: TextProcessRunner) repo (jobId: ManagerJobId) (path: WorktreePath) =
        taskResult {
            let identity = identityOf jobId

            let! result =
                execute
                    runner
                    (command
                        (Some repo)
                        [ "worktree"
                          "add"
                          WorktreePath.value path
                          "-b"
                          WorktreeIdentity.value identity ])

            // Returns the identity rather than unit: it is what recovery locates the
            // worktree by, and a caller that had to re-derive it would be a second
            // copy of `identityOf`.
            do! outcome result.ExitCode result.Stdout result.Stderr
            return identity
        }

    let remove (runner: TextProcessRunner) repo (path: WorktreePath) =
        taskResult {
            let! result =
                execute runner (command (Some repo) [ "worktree"; "remove"; "--force"; WorktreePath.value path ])

            return! outcome result.ExitCode result.Stdout result.Stderr
        }

    let list (runner: TextProcessRunner) repo () =
        taskResult {
            let! result = execute runner (command (Some repo) [ "worktree"; "list"; "--porcelain" ])

            if result.ExitCode <> 0 then
                return! Error(failure result.Stdout result.Stderr)
            else
                return
                    GitPorcelain.worktrees result.Stdout
                    |> List.map (fun (path, branch) ->
                        WorktreePath.create path, branch |> Option.map WorktreeIdentity.create)
        }

    let listBranches (runner: TextProcessRunner) repo () =
        taskResult {
            let! result = execute runner (command (Some repo) [ "branch"; "--list"; "manager/*" ])

            if result.ExitCode <> 0 then
                return! Error(failure result.Stdout result.Stderr)
            else
                return GitPorcelain.branches result.Stdout |> List.map WorktreeIdentity.create
        }

    let deleteBranch (runner: TextProcessRunner) repo (identity: WorktreeIdentity) =
        taskResult {
            let! result = execute runner (command (Some repo) [ "branch"; "-D"; WorktreeIdentity.value identity ])

            return! outcome result.ExitCode result.Stdout result.Stderr
        }

    [<Import("isAbsolute", "node:path")>]
    let private isAbsolute (path: string) : bool = jsNative

    let private absoluteGitPath directory (value: string) =
        let path = value.Trim()

        if isAbsolute path then
            path
        else
            System.IO.Path.Combine(directory, path)

    /// Read actual rebase directories; a stale REBASE_HEAD is not evidence.
    let rebaseInProgress (runner: TextProcessRunner) (dir: string) : Task<Result<bool, string>> =
        let pathFor name =
            taskResult {
                let! result = execute runner (command (Some dir) [ "rev-parse"; "--git-path"; name ])

                if result.ExitCode <> 0 then
                    return! Error(failure result.Stdout result.Stderr)
                elif String.IsNullOrWhiteSpace result.Stdout then
                    return! Error "git rebase-state query returned no path"
                else
                    return absoluteGitPath dir result.Stdout
            }

        taskResult {
            let! mergePath = pathFor "rebase-merge"
            let! applyPath = pathFor "rebase-apply"

            return
                GitPorcelain.rebaseStatePaths (0, mergePath) (0, applyPath)
                |> List.exists System.IO.Directory.Exists
        }
