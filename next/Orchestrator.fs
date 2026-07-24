namespace Wanxiangshu.Next.Orchestrator

open System
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
        ?authority: GitAuthorityPort
    ) =
    let lockObj = obj ()
    let mutable publishChain: Task = Task.FromResult(()) :> Task
    let mailbox = System.Collections.Generic.Queue<ManagerCompletion>()
    let journalPort = journal
    let authorityPort = authority

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

    member this.ForkManager
        (managerId: string, ?worktreePath: string)
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
                    let handle =
                        { ManagerId = managerId
                          WorktreePath = path }

                    match
                        appendFact
                            StreamId.Workspace
                            (AgentFact.OrchestratorManagerJobCreated
                                {| ManagerId = managerId
                                   WorktreePath = path
                                   Branch = sprintf "manager/%s" managerId |})
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
                                let! res = manager.RunManager managerId path
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
                return!
                    runSerial (fun () ->
                        task {
                            let managerId = completion.Handle.ManagerId
                            let worktreePath = completion.Handle.WorktreePath

                            match! reconcileTarget () with
                            | Error err ->
                                return
                                    OrchestratorVerdict.IntegrationFailed(
                                        managerId,
                                        sprintf "Git reconcile failed: %s" err
                                    )
                            | Ok() ->
                                match! reverifyTwice managerId worktreePath with
                                | Error err -> return OrchestratorVerdict.NeedsReview(managerId, err)
                                | Ok() ->
                                    let candidateId = sprintf "candidate-%s" managerId
                                    let! candidateHeadResult = readHead worktreePath ""

                                    match candidateHeadResult with
                                    | Error err ->
                                        return
                                            OrchestratorVerdict.IntegrationFailed(
                                                managerId,
                                                sprintf "Git head lookup failed: %s" err
                                            )
                                    | Ok candidateHead ->
                                        match
                                            appendFact
                                                (StreamId.Workspace)
                                                (AgentFact.OrchestratorCandidateRegistered
                                                    {| ManagerId = managerId
                                                       CandidateId = candidateId
                                                       Branch = sprintf "manager/%s" managerId
                                                       CommitHash = candidateHead |})
                                        with
                                        | Error err ->
                                            return
                                                OrchestratorVerdict.IntegrationFailed(
                                                    managerId,
                                                    sprintf "Failed to persist candidate: %s" err
                                                )
                                        | Ok() ->
                                            let! rebaseResult = git.Rebase worktreePath targetBranch

                                            let! finalRebase =
                                                match rebaseResult with
                                                | Ok() -> Task.FromResult(Ok())
                                                | Error conflict ->
                                                    task {
                                                        // A conflict is a continuation of this ManagerJob, never a new manager.
                                                        match! manager.RunManager managerId worktreePath with
                                                        | Error err ->
                                                            return
                                                                Error(
                                                                    sprintf
                                                                        "Rebase conflict (%s); manager continuation failed: %s"
                                                                        conflict
                                                                        err
                                                                )
                                                        | Ok() -> return! git.Rebase worktreePath targetBranch
                                                    }

                                            match finalRebase with
                                            | Error err ->
                                                return
                                                    OrchestratorVerdict.IntegrationFailed(
                                                        managerId,
                                                        sprintf "Rebase failed: %s" err
                                                    )
                                            | Ok() ->
                                                match! reconcileTarget () with
                                                | Error err ->
                                                    return
                                                        OrchestratorVerdict.IntegrationFailed(
                                                            managerId,
                                                            sprintf "Git reconcile failed after rebase: %s" err
                                                        )
                                                | Ok() ->
                                                    match! reverifyTwice managerId worktreePath with
                                                    | Error err ->
                                                        return OrchestratorVerdict.NeedsReview(managerId, err)
                                                    | Ok() ->
                                                        // FfMerge is deliberately the only write to the target ref: the Git port
                                                        // performs `git merge --ff-only`, keeping Git authoritative on reconcile.
                                                        match! git.FfMerge worktreePath targetBranch with
                                                        | Error err ->
                                                            return
                                                                OrchestratorVerdict.IntegrationFailed(
                                                                    managerId,
                                                                    sprintf "FF merge failed: %s" err
                                                                )
                                                        | Ok commitHash ->
                                                            match
                                                                appendFact
                                                                    StreamId.Workspace
                                                                    (AgentFact.OrchestratorPublished
                                                                        {| ManagerId = managerId
                                                                           CandidateId = candidateId
                                                                           CommitHash = commitHash |})
                                                            with
                                                            | Error err ->
                                                                return
                                                                    OrchestratorVerdict.IntegrationFailed(
                                                                        managerId,
                                                                        sprintf
                                                                            "Failed to persist published fact: %s"
                                                                            err
                                                                    )
                                                            | Ok() ->
                                                                let! _ = git.RemoveWorktree worktreePath

                                                                return
                                                                    OrchestratorVerdict.Published(
                                                                        managerId,
                                                                        commitHash
                                                                    )
                        })
        }

module OrchestratorEngine =
    let create git manager repoPath targetBranch =
        Orchestrator(git, manager, repoPath, targetBranch)

    let forkManager (orch: Orchestrator) managerId (worktreePath: string option) =
        orch.ForkManager(managerId, ?worktreePath = worktreePath)

    let joinPublished (orch: Orchestrator) = orch.JoinPublished()
