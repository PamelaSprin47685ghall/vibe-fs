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
    // DSL-MUTABLE: resource — one-shot worktree release latch
    let mutable released = false
    // DSL-MUTABLE: resource — durable-mark flag for worktree dispose policy
    let mutable releaseOnDispose = releaseOnDisposeInitially

    member _.Path = path
    member _.Identity = identity
    member _.MarkDurable() = releaseOnDispose <- false

    member private _.ReleaseOutcome worktree branchResult =
        match worktree, branchResult with
        | Ok(), Ok() ->
            released <- true
            Ok()
        | Error left, Error right -> Error(sprintf "worktree=%s; branch=%s" left right)
        | Error error, _ -> Error("worktree=" + error)
        | _, Error error -> Error("branch=" + error)

    member this.Release() =
        task {
            if released then
                return Ok()
            else
                let! worktree = git.RemoveWorktree path
                let! branchResult = git.DeleteBranch identity
                return this.ReleaseOutcome worktree branchResult
        }

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
          Stdin = None
          PtyOptions = None }

    let private outcome code stdout stderr =
        if code = 0 then
            Ok()
        else
            Error(if String.IsNullOrWhiteSpace stderr then stdout else stderr)

    let private failure stdout stderr =
        if String.IsNullOrWhiteSpace stderr then stdout else stderr

    let isDirty (runner: Command -> Task<int * string * string>) (path: WorktreePath) =
        task {
            let! code, stdout, stderr = runner (command (Some(WorktreePath.value path)) [ "status"; "--porcelain" ])

            if code <> 0 then
                return Error(failure stdout stderr)
            else
                return Ok(not (String.IsNullOrWhiteSpace stdout))
        }

    let create (runner: Command -> Task<int * string * string>) repo (jobId: ManagerJobId) (path: WorktreePath) =
        task {
            let identity = identityOf jobId

            let! code, stdout, stderr =
                runner (
                    command
                        (Some repo)
                        [ "worktree"
                          "add"
                          WorktreePath.value path
                          "-b"
                          WorktreeIdentity.value identity ]
                )

            // Returns the identity rather than unit: it is what recovery locates the
            // worktree by, and a caller that had to re-derive it would be a second
            // copy of `identityOf`.
            return outcome code stdout stderr |> Result.map (fun () -> identity)
        }

    let remove (runner: Command -> Task<int * string * string>) (path: WorktreePath) =
        task {
            let! code, stdout, stderr =
                runner (command None [ "worktree"; "remove"; "--force"; WorktreePath.value path ])

            return outcome code stdout stderr
        }

    let list (runner: Command -> Task<int * string * string>) repo () =
        task {
            let! code, stdout, stderr = runner (command (Some repo) [ "worktree"; "list"; "--porcelain" ])

            if code <> 0 then
                return Error(failure stdout stderr)
            else
                return
                    GitPorcelain.worktrees stdout
                    |> List.map (fun (path, branch) ->
                        WorktreePath.create path, branch |> Option.map WorktreeIdentity.create)
                    |> Ok
        }

    let listBranches (runner: Command -> Task<int * string * string>) repo () =
        task {
            let! code, stdout, stderr = runner (command (Some repo) [ "branch"; "--list"; "manager/*" ])

            if code <> 0 then
                return Error(failure stdout stderr)
            else
                // `git branch` marks a branch checked out in ANOTHER worktree with
                // `+` (an owned job's branch arrives as `+ manager/<job>`); the
                // marker is stripped by the porcelain law so `isStale` sees the name.
                return
                    GitPorcelain.branches stdout
                    |> List.map WorktreeIdentity.create
                    |> Ok
        }

    let deleteBranch (runner: Command -> Task<int * string * string>) repo (identity: WorktreeIdentity) =
        task {
            let! code, stdout, stderr = runner (command (Some repo) [ "branch"; "-D"; WorktreeIdentity.value identity ])

            return outcome code stdout stderr
        }

    /// True when an in-progress rebase is present: rebase-merge / rebase-apply
    /// exists. A bare REBASE_HEAD can survive a failed continue and is not
    /// evidence (ORCH-003 conflict resume). Which probe output counts is the
    /// porcelain law; only the directory check is host I/O.
    let rebaseInProgress (runner: Command -> Task<int * string * string>) (dir: string) : Task<bool> =
        task {
            let! mergeCode, mergePath, _ = runner (command (Some dir) [ "rev-parse"; "--git-path"; "rebase-merge" ])
            let! applyCode, applyPath, _ = runner (command (Some dir) [ "rev-parse"; "--git-path"; "rebase-apply" ])

            return
                GitPorcelain.rebaseStatePaths (mergeCode, mergePath) (applyCode, applyPath)
                |> List.exists System.IO.Directory.Exists
        }
