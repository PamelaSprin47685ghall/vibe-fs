namespace Wanxiangshu.Next.Orchestrator

open System
open System.IO
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

module private PublishLock =
    [<Import("default", "proper-lockfile")>]
    let private lockfile: obj = jsNative

    // proper-lockfile's lock(file, options) takes 2 positional JS args, so call it
    // via Emit: a tupled F# unbox would pass a single array argument (stringifying
    // to "path,[object Object]"), and a curried unbox would call lock(file)(options).
    // lock() returns a release function; unlock by invoking it directly.
    [<Emit("$0($1, $2)")>]
    let private lockAsync (fn: obj) (path: string) (opts: obj) : Task<obj> = jsNative

    /// Cross-process publication lock path: lives in the temp dir (never inside the
    /// repo) so git status stays clean. No ".lock" suffix: proper-lockfile appends it.
    let lockPath (repoPath: string) (branch: string) =
        let norm = repoPath.Replace('/', '_').Replace('\\', '_')
        let b = branch.Replace('/', '_').Replace('\\', '_')
        Path.Combine(Path.GetTempPath(), sprintf "wanxiangshu-publish-%s-%s" norm b)

    /// Acquire a cross-process publication lock on the target ref. The in-object
    /// publishChain only serializes within one Orchestrator instance; this lock
    /// serializes publication across sessions, runtimes, and processes on the same
    /// repo. proper-lockfile v4 has no .sync API, so this is async and released in a
    /// `finally`. Keep the happy path: a single instance behaves exactly as before.
    /// realpath:false lets the lock target be a synthetic temp path that need not exist.
    let acquire (path: string) : Task<obj> =
        lockAsync lockfile path (createObj [ "retries", box 5; "realpath", box false ])

    /// Release by invoking the release function returned by lock.
    let release (releaseFn: obj) : Task<unit> =
        let fn: unit -> Task<unit> = unbox releaseFn
        fn ()

type Orchestrator
    (
        git: GitPort,
        manager: ManagerPort,
        repoPath: string,
        targetBranch: string,
        ?journal: OrchestratorJournalPort,
        ?authority: GitAuthorityPort
    ) =
    let lockObj = obj ()
    let mutable publishChain: Task = Task.FromResult(()) :> Task
    let mailbox = System.Collections.Generic.Queue<ManagerCompletion>()
    let journalPort = journal
    let authorityPort = authority
    let prompts = System.Collections.Generic.Dictionary<string, string>()

    let appendFact stream fact =
        match journalPort with
        | None -> Ok()
        | Some port ->
            match port.AppendFact stream fact with
            | Ok _ -> Ok()
            | Error err -> Error err

    let reverifyTwice managerId worktreePath =
        task {
            match! manager.Reverify managerId worktreePath with
            | Error err -> return Error err
            | Ok() -> return! manager.Reverify managerId worktreePath
        }

    let readHead worktreePath fallback =
        task {
            match authorityPort with
            | None -> return Ok fallback
            | Some port ->
                match! port.GetHead worktreePath with
                | Ok head -> return Ok head
                | Error err -> return Error err
        }

    let reconcileTarget () =
        task {
            match authorityPort with
            | None -> return Ok()
            | Some port ->
                match! port.GetTargetHead repoPath targetBranch with
                | Ok _ -> return Ok()
                | Error err -> return Error err
        }

    let waiters =
        System.Collections.Generic.Queue<TaskCompletionSource<ManagerCompletion>>()

    let runSerial (fn: unit -> Task<'T>) : Task<'T> =
        task {
            let tcs = TaskCompletionSource<'T>()

            lock lockObj (fun () ->
                let prev = publishChain

                publishChain <-
                    task {
                        try
                            do! prev
                        with _ ->
                            ()

                        try
                            let! res = fn ()
                            tcs.SetResult(res)
                        with ex ->
                            tcs.SetException(ex)
                    }
                    :> Task)

            return! tcs.Task
        }

    let runSerialLocked (lockPath: string) (fn: unit -> Task<'T>) : Task<'T> =
        // In-process serialization via the publishChain; the cross-process lock is
        // acquired INSIDE the serialized region so an in-process contender never
        // sees the lock held (no ~1s proper-lockfile retry), while two processes
        // still serialize on the file lock.
        runSerial (fun () ->
            task {
                let! release = PublishLock.acquire lockPath

                try
                    return! fn ()
                finally
                    PublishLock.release release |> ignore
            })

    member this.ForkManager
        (managerId: string, prompt: string, ?worktreePath: string)
        : Task<Result<OrchestratorHandle, OrchestratorVerdict>> =
        task {
            let path =
                defaultArg worktreePath (IO.Path.Combine(IO.Path.GetTempPath(), sprintf "wanxiangshu-%s" managerId))

            let! isDirty = git.IsDirty repoPath

            if isDirty then
                return Error(OrchestratorVerdict.RejectedDirty "Worktree is dirty")
            else
                match! git.CreateWorktree repoPath managerId path with
                | Error err ->
                    return
                        Error(
                            OrchestratorVerdict.IntegrationFailed(
                                managerId,
                                sprintf "Failed to create worktree: %s" err
                            )
                        )
                | Ok() ->
                    prompts.[managerId] <- prompt

                    let handle =
                        { ManagerId = managerId
                          WorktreePath = path }

                    match
                        appendFact
                            StreamId.Workspace
                            (AgentFact.OrchestratorManagerJobCreated
                                {| ManagerId = managerId
                                   WorktreePath = path
                                   Branch = sprintf "manager/%s" managerId
                                   Prompt = prompt |})
                    with
                    | Error err ->
                        let _ = git.RemoveWorktree path

                        return
                            Error(
                                OrchestratorVerdict.IntegrationFailed(
                                    managerId,
                                    sprintf "Failed to persist manager job: %s" err
                                )
                            )
                    | Ok() ->
                        let _ =
                            task {
                                let! res = manager.RunManager managerId path prompt
                                let completion = { Handle = handle; Result = res }

                                lock lockObj (fun () ->
                                    if waiters.Count > 0 then
                                        waiters.Dequeue().SetResult(completion)
                                    else
                                        mailbox.Enqueue(completion))
                            }

                        return Ok handle
        }

    member this.JoinPublished() : Task<OrchestratorVerdict> =
        task {
            let completion =
                lock lockObj (fun () ->
                    if mailbox.Count > 0 then
                        Task.FromResult(mailbox.Dequeue())
                    else
                        let waiter = TaskCompletionSource<ManagerCompletion>()
                        waiters.Enqueue(waiter)
                        waiter.Task)

            let! completion = completion

            match completion.Result with
            | Error err ->
                return
                    OrchestratorVerdict.IntegrationFailed(
                        completion.Handle.ManagerId,
                        sprintf "Manager run failed: %s" err
                    )
            | Ok() ->
                let deps: PublishChain.Deps =
                    { Git = git
                      Manager = manager
                      AppendFact = appendFact
                      ReverifyTwice = reverifyTwice
                      ReadHead = readHead
                      ReconcileTarget = reconcileTarget
                      TargetBranch = targetBranch
                      Prompts = prompts }

                return!
                    runSerialLocked (PublishLock.lockPath repoPath targetBranch) (fun () ->
                        PublishChain.run deps completion)
        }
