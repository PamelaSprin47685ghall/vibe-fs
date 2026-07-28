namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Orchestrator

type OrchestratorHost(deps: OrchestratorHostDeps, orchestratorId: SessionId) =
    let orchestratorKey = SessionId.value orchestratorId
    let worktrees = Dictionary<string, string>()
    let stash = Dictionary<string, RunCompletion>()

    let gitPort = ProcessGitPort.createWithRepo deps.RepoPath OrchestratorGit.run
    let authorityPort = OrchestratorAuthority.createPort ()

    let onChildCreated (agentId: string) (role: AgentRole) (childId: SessionId) =
        if role = AgentRole.Reviewer then
            match worktrees.TryGetValue agentId with
            | true, path -> deps.RegisterReviewerTree (SessionId.value childId) (GitTree.create path)
            | false, _ -> ()

        deps.OnChildCreated agentId role childId

    let runtime =
        HostForkRuntime(
            orchestratorId,
            deps.Sessions,
            ?journal = deps.Journal,
            onChildCreated = onChildCreated,
            onChildCreatedDir =
                (fun _ childId dirOpt -> dirOpt |> Option.iter (fun path -> deps.RegisterChildDirectory childId path)),
            ?modelResolver = deps.ModelConfig,
            directoryFor =
                (fun agentId ->
                    match worktrees.TryGetValue agentId with
                    | true, path -> Some path
                    | false, _ -> None),
            onRunStarted = deps.OnRunStarted
        )

    let awaitAgent (agentId: string) : Task<Result<RunCompletion, string>> =
        task {
            match stash.TryGetValue agentId with
            | true, completion ->
                stash.Remove agentId |> ignore
                return Ok completion
            | false, _ ->
                let mutable found = None

                while found.IsNone do
                    let! joined = runtime.Join()

                    match joined with
                    | Error err -> found <- Some(Error(sprintf "%A" err))
                    | Ok c when c.AgentId = agentId -> found <- Some(Ok c)
                    | Ok c -> stash.[c.AgentId] <- c

                return found.Value
        }

    let runManager (managerId: string) (worktree: string) (prompt: string) : Task<Result<unit, string>> =
        task {
            if not (worktrees.ContainsKey managerId) then
                worktrees.[managerId] <- worktree

            let! forked = runtime.Fork(managerId, AgentRole.Manager, prompt)

            match forked with
            | Error err -> return Error err
            | Ok _ ->
                let! completion = awaitAgent managerId

                match completion with
                | Error err -> return Error err
                | Ok run ->
                    match run.Outcome with
                    | AgentCompleted _ -> return! OrchestratorGit.finalizeWorktree OrchestratorGit.run managerId worktree
                    | AgentFailed payload -> return Error payload.Message
                    | AgentAborted payload -> return Error payload.Message
        }

    let runReviewerOnce (managerId: string) (worktree: string) (prompt: string) : Task<Result<unit, string>> =
        task {
            let reviewerId = sprintf "%s-reviewer" managerId
            worktrees.[reviewerId] <- worktree
            let! forked = runtime.Fork(reviewerId, AgentRole.Reviewer, prompt)

            match forked with
            | Error err -> return Error err
            | Ok _ ->
                let! completion = awaitAgent reviewerId

                match completion with
                | Error err -> return Error err
                | Ok run ->
                    match run.Outcome with
                    | AgentCompleted _ -> return Ok()
                    | AgentFailed payload -> return Error payload.Message
                    | AgentAborted payload -> return Error payload.Message
        }

    let reverify (managerId: string) (worktree: string) (barrierKey: string) : Task<Result<unit, string>> =
        OrchestratorHostReview.reverify
            deps.Journal
            orchestratorId
            runReviewerOnce
            managerId
            worktree
            barrierKey

    let managerPort: ManagerPort =
        { RunManager = runManager
          Reverify = reverify }

    let mutable detectedBranch: string option =
        if String.IsNullOrWhiteSpace deps.TargetBranch then
            None
        else
            Some deps.TargetBranch

    let orchestratorBranch () : Task<Result<string, string>> =
        task {
            match detectedBranch with
            | Some branch -> return Ok branch
            | None ->
                let! branchResult = OrchestratorGit.detectBranch OrchestratorGit.run deps.RepoPath

                match branchResult with
                | Ok branch -> detectedBranch <- Some branch
                | Error _ -> ()

                return branchResult
        }

    let mutable engineInstance: Orchestrator option = None
    let engineGate = obj ()
    let mutable engineTask: Task<Result<Orchestrator, string>> option = None

    let initializeEngine () : Task<Result<Orchestrator, string>> =
        task {
            match engineInstance with
            | Some value -> return Ok value
            | None ->
                let! branchResult = orchestratorBranch ()

                match branchResult with
                | Error reason -> return Error reason
                | Ok branch ->
                    let! reconciledPublished =
                        OrchestratorAuthority.reconcilePublishedFromAuthority
                            deps.Journal
                            authorityPort
                            deps.RepoPath
                            branch

                    // Sweep orphaned manager artifacts before resuming jobs.
                    // Best-effort: failures are skipped, engine init is never blocked.
                    let sweepLockPath =
                        PublishLock.lockPath (RuntimePath.gitCommonDir deps.RepoPath) branch

                    let! sweepResult =
                        match deps.Journal with
                        | Some journal ->
                            let jobs = (AgentJournal.snapshot journal).AgentProjections.Orchestrator.ManagerJobs
                            OrchestratorSweep.sweepLocked sweepLockPath gitPort jobs
                        | None -> Task.FromResult(Ok())

                    match sweepResult with
                    | Error error -> return Error(sprintf "orchestrator cleanup failed: %s" error)
                    | Ok() ->
                        // Canonicalize the repo path via git common-dir so symlinked
                        // spellings share one cross-process publish lock.
                        let lockRepoPath = RuntimePath.gitCommonDir deps.RepoPath

                        let value =
                            Orchestrator(
                                gitPort,
                                managerPort,
                                deps.RepoPath,
                                branch,
                                ?journal = (deps.Journal |> Option.map OrchestratorJournalPort.fromAgentJournal),
                                ?authority = Some authorityPort,
                                ?lockRepoPath = Some lockRepoPath
                            )

                        for managerId, commitHash in reconciledPublished do
                            value.RecoverPublished(managerId, commitHash)

                        match deps.Journal with
                        | Some journal ->
                            do!
                                OrchestratorManagerJob.recoverJobs
                                    journal
                                    gitPort
                                    orchestratorId
                                    worktrees
                                    deps.RegisterChildDirectory
                                    deps.RegisterReviewerTree
                                    value
                        | None -> ()

                        lock engineGate (fun () -> engineInstance <- Some value)
                        return Ok value
        }

    let engine () : Task<Result<Orchestrator, string>> =
        lock engineGate (fun () ->
            match engineInstance with
            | Some value -> Task.FromResult(Ok value)
            | None ->
                match engineTask with
                | Some task -> task
                | None ->
                    let task = initializeEngine ()
                    engineTask <- Some task
                    task)

    member _.ForkManagerJob(managerId: string, prompt: string) : Task<Result<string, string>> =
        task {
            let! engineResult = engine ()

            match engineResult with
            | Error reason -> return Error reason
            | Ok engine ->
                let! result = engine.ForkManager(managerId, prompt)

                match result with
                | Error verdict -> return Error(sprintf "%A" verdict)
                | Ok handle -> return Ok handle.WorktreePath
        }

    member _.JoinPublished() : Task<string> =
        task {
            let! engineResult = engine ()

            match engineResult with
            | Error reason -> return sprintf "Orchestrator init failed: %s" reason
            | Ok engine ->
                let! verdict = engine.JoinPublished()
                return sprintf "%A" verdict
        }

    member _.Cancel() = runtime.Cancel()
