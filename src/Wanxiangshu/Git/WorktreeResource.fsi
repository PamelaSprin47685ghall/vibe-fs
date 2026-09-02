namespace Wanxiangshu.Git

open System
open System.Threading.Tasks
open Wanxiangshu.Change
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process

/// Owned manager worktree. Release is idempotent; DisposeAsync performs the
/// same physical cleanup when a program exits before publish.
///
/// `Identity` is the stable name (the `manager/<job>` branch) and `Path` is where
/// it currently lives. ORCH-006 keeps both for exactly this reason: recovery
/// locates by identity, diagnostics show the path, and a moved worktree must not
/// orphan its job.
[<Class>]
type WorktreeResource =
    interface IAsyncDisposable
    member Path: WorktreePath
    member Identity: WorktreeIdentity
    member MarkDurable: unit -> unit
    member Release: unit -> Task<Result<unit, string>>

    static member Create:
        git: GitPort * jobId: ManagerJobId * path: WorktreePath -> Task<Result<WorktreeResource, string>>

    static member Adopt: git: GitPort * identity: WorktreeIdentity * path: WorktreePath -> WorktreeResource

/// Process-backed worktree verbs used by GitOperations.
///
/// `repo` is bound once by `GitOperations.createWithRepo`; no verb takes it as a
/// per-call argument, so a caller cannot address a repository the port was not
/// built for.
module WorktreeCommands =
    /// The branch a job's worktree lives on IS its stable identity (ORCH-006).
    val identityOf: jobId: ManagerJobId -> WorktreeIdentity
    val isDirty: runner: (Command -> Task<int * string * string>) -> path: WorktreePath -> Task<bool>

    val create:
        runner: (Command -> Task<int * string * string>) ->
        repo: string ->
        jobId: ManagerJobId ->
        path: WorktreePath ->
            Task<Result<WorktreeIdentity, string>>

    val remove: runner: (Command -> Task<int * string * string>) -> path: WorktreePath -> Task<Result<unit, string>>

    val list:
        runner: (Command -> Task<int * string * string>) ->
        repo: string ->
        unit ->
            Task<Result<(WorktreePath * WorktreeIdentity option) list, string>>

    val listBranches:
        runner: (Command -> Task<int * string * string>) ->
        repo: string ->
        unit ->
            Task<Result<WorktreeIdentity list, string>>

    val deleteBranch:
        runner: (Command -> Task<int * string * string>) ->
        repo: string ->
        identity: WorktreeIdentity ->
            Task<Result<unit, string>>
