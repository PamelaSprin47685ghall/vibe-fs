namespace Wanxiangshu.Next.Orchestrator

open System
open System.IO
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

type Orchestrator
    (
        git: GitPort,
        manager: ManagerPort,
        repoPath: string,
        targetBranch: string,
        ?journal: OrchestratorJournalPort,
        ?authority: GitAuthorityPort,
        ?lockRepoPath: string
    ) =
    let lockObj = obj ()
    let mutable publishChain: Task = Task.FromResult(()) :> Task
    let mailbox = System.Collections.Generic.Queue<ManagerCompletion>()
    let recoveredPublished = System.Collections.Generic.Queue<OrchestratorVerdict>()
    let journalPort = journal
    let authorityPort = authority
    let lockRepoPath = defaultArg lockRepoPath repoPath
    let prompts = System.Collections.Generic.Dictionary<string, string>()

    let appendFact stream fact =
        match journalPort with
        | None -> Ok()
        | Some port ->
            match port.AppendFact stream fact with
            | Ok _ -> Ok()
            | Error err -> Error err

    let reverifyTwice managerId worktreePath barrierKey =
        task {
            // A single Reverify call performs the full double-PERFECT check
            // (reviewer, state, nudge, state). The barrier key is forwarded so
            // the host emits ReviewBarrierStarted before the first reviewer
            // call, resetting the guard to require two FRESH verdicts for this
            // phase. Calling Reverify twice would either waste a third reviewer
            // call (old bug) or short-circuit immediately (barrier already
            // confirmed) — either way, one call is the correct contract.
            return! manager.Reverify managerId worktreePath barrierKey
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

    let getTargetHead () =
        task {
            match authorityPort with
            | None -> return Ok ""
            | Some port ->
                match! port.GetTargetHead repoPath targetBranch with
                | Ok head -> return Ok head
                | Error err -> return Error err
        }

    let snapshotProjection () =
        match journalPort with
        | Some port -> port.Snapshot()
        | None -> Fold.empty

    let waiters =
        System.Collections.Generic.Queue<TaskCompletionSource<ManagerCompletion>>()

    let enqueueCompletion completion =
        lock lockObj (fun () ->
            if waiters.Count > 0 then
                waiters.Dequeue().SetResult(completion)
            else
                mailbox.Enqueue(completion))

    let startManager handle prompt =
        task {
            let! result = manager.RunManager handle.ManagerId handle.WorktreePath prompt
            enqueueCompletion { Handle = handle; Result = result }
        }
        |> ignore

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
                let! release =
                    task {
                        try
                            return! PublishLock.acquire lockPath
                        with ex ->
                            // Convert lock acquisition failure (contention budget
                            // exhausted or fs error) into a named domain error.
                            return
                                raise (
                                    InvalidOperationException(
                                        sprintf "publish lock acquire failed for %s: %s" lockPath ex.Message
                                    )
                                )
                    }

                let! outcome =
                    task {
                        try
                            let! value = fn ()
                            return Choice1Of2 value
                        with ex ->
                            return Choice2Of2 ex
                    }

                do! PublishLock.release release

                match outcome with
                | Choice1Of2 value -> return value
                | Choice2Of2 ex -> return raise ex
            })

    let forkManagerCore
        (managerId: string)
        (prompt: string)
        (worktreePath: string option)
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
                        startManager handle prompt
                        return Ok handle
        }

    member this.ForkManager
        (managerId: string, prompt: string, ?worktreePath: string)
        : Task<Result<OrchestratorHandle, OrchestratorVerdict>> =
        runSerialLocked (PublishLock.lockPath lockRepoPath targetBranch) (fun () ->
            forkManagerCore managerId prompt worktreePath)

    member _.RecoverPublished(managerId: string, commitHash: string) : unit =
        lock lockObj (fun () -> recoveredPublished.Enqueue(OrchestratorVerdict.Published(managerId, commitHash)))

    member _.RecoverManagerJob(managerId: string, worktreePath: string, prompt: string, managerCompleted: bool) : unit =
        prompts.[managerId] <- prompt

        let handle =
            { ManagerId = managerId
              WorktreePath = worktreePath }

        if managerCompleted then
            enqueueCompletion { Handle = handle; Result = Ok() }
        else
            startManager handle prompt

    member this.JoinPublished() : Task<OrchestratorVerdict> =
        task {
            let terminal =
                lock lockObj (fun () ->
                    if recoveredPublished.Count > 0 then
                        Some(recoveredPublished.Dequeue())
                    else
                        None)

            match terminal with
            | Some verdict -> return verdict
            | None ->
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
                          GetTargetHead = getTargetHead
                          TargetBranch = targetBranch
                          Prompts = prompts
                          Snapshot = snapshotProjection }

                    return!
                        runSerialLocked (PublishLock.lockPath lockRepoPath targetBranch) (fun () ->
                            PublishChain.run deps completion)
        }
