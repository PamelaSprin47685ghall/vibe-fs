namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Orchestrator

/// Host wiring for the Orchestrator: forks Managers and reviewers under one
/// runtime, and supplies `ManagerPort` to the pure publish program.
type OrchestratorHost(deps: OrchestratorHostDeps, orchestratorId: SessionId) =
    let worktrees = Dictionary<string, string>()

    let gitPort = GitOperations.createWithRepo deps.RepoPath OrchestratorGit.run

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
            directoryFor =
                (fun agentId ->
                    match worktrees.TryGetValue agentId with
                    | true, path -> Some path
                    | false, _ -> None),
            ?sessionSnapshot = deps.SessionSnapshot,
            onRunStarted = deps.OnRunStarted,
            parentWorkRecordFor = deps.ParentWorkRecordFor,
            childWorkRecordFor = deps.ChildWorkRecordFor,
            publishToMailbox = false
        )

    let managerAgentId (jobId: ManagerJobId) = ManagerJobId.value jobId

    /// The durable job record. ORCH-003: the Manager's managed agent name lives here
    /// and nowhere else, so a resumed `deep-manager` job never degrades to
    /// `fast-manager` (PROMPT-008 forbids rebuilding it from the role).
    let jobRecord (jobId: ManagerJobId) =
        deps.Journal
        |> Option.bind (fun journal ->
            OrchestratorProjection.tryFind jobId (AgentJournal.snapshot journal).AgentProjections.Orchestrator)

    let outcomeOf (run: RunCompletion) =
        match run.Outcome with
        | AgentCompleted _ -> Ok()
        | AgentFailed payload
        | AgentAborted payload -> Error payload.Message

    /// Fork a child and hand back the Host session it created.
    ///
    /// The session comes from the runtime's own child map, not from the fork result:
    /// only the Host can issue a session id, and ORCH-006 requires the real one.
    let forkChild (agentId: string) (role: AgentRole) (agent: string) (worktree: WorktreePath) (prompt: string) =
        task {
            worktrees.[agentId] <- WorktreePath.value worktree

            match! runtime.Fork(agentId, role, agent, prompt) with
            | Error error -> return Error error
            | Ok _ ->
                match runtime.TryChildSession agentId with
                | Some childId -> return Ok childId
                | None -> return Error(sprintf "Fork of '%s' produced no child session" agentId)
        }

    let awaitChild (agentId: string) =
        task {
            match! runtime.AwaitAgent agentId with
            | Error error -> return Error error
            | Ok run -> return outcomeOf run
        }

    // ── ManagerPort ─────────────────────────────────────────────────────────

    let startManager (start: ManagerStart) : Task<Result<SessionId, string>> =
        forkChild (managerAgentId start.JobId) AgentRole.Manager start.ManagerAgent start.Worktree start.Prompt

    /// Await the Manager, then stage its work into a candidate commit.
    ///
    /// `finalizeWorktree` runs only on a completed Manager: a failed or aborted run
    /// has nothing to commit, and committing anyway would produce a candidate the
    /// Manager never claimed was done.
    let awaitManager (jobId: ManagerJobId) : Task<Result<unit, string>> =
        task {
            let agentId = managerAgentId jobId

            match! awaitChild agentId with
            | Error error -> return Error error
            | Ok() ->
                match worktrees.TryGetValue agentId with
                | true, path -> return! OrchestratorGit.finalizeWorktree OrchestratorGit.run agentId path
                | false, _ -> return Error(sprintf "No worktree registered for manager job '%s'" agentId)
        }

    /// ORCH-003/ORCH-007: hand work back to the SAME Manager in the SAME worktree.
    ///
    /// `Fork` on an existing agent nudges it (EXEC-002) rather than creating a second
    /// child, so this is a continuation of that Manager's Logical Run.
    let resumeManager (jobId: ManagerJobId) (worktree: WorktreePath) (prompt: string) =
        task {
            match jobRecord jobId with
            | None -> return Error(sprintf "No durable job record for '%s'" (ManagerJobId.value jobId))
            | Some record ->
                let agentId = managerAgentId jobId
                worktrees.[agentId] <- WorktreePath.value worktree

                match! runtime.Fork(agentId, AgentRole.Manager, record.ManagerAgent, prompt, firstPrompt = false) with
                | Error error -> return Error error
                | Ok _ -> return! awaitManager jobId
        }

    /// One review barrier. A fresh reviewer agent id per barrier, so REVIEW-008's
    /// "fresh dual PERFECT" is structural: a new session's guard starts empty.
    let reverify
        (jobId: ManagerJobId)
        (managerSessionId: SessionId)
        (worktree: WorktreePath)
        (barrierId: ReviewBarrierId)
        =
        let reviewerAgentId =
            sprintf "%s-%s" (OrchestratorManagerJob.reviewerAgentId jobId) (ReviewBarrierId.value barrierId)

        OrchestratorHostReview.reverify
            deps.Journal
            (fun _ path prompt ->
                forkChild reviewerAgentId AgentRole.Reviewer OrchestratorHostReview.DeepReviewerAgent path prompt)
            (fun _ -> awaitChild reviewerAgentId)
            (fun _ prompt ->
                task {
                    match!
                        runtime.Fork(
                            reviewerAgentId,
                            AgentRole.Reviewer,
                            OrchestratorHostReview.DeepReviewerAgent,
                            prompt,
                            firstPrompt = false
                        )
                    with
                    | Error error -> return Error error
                    | Ok _ -> return! awaitChild reviewerAgentId
                })
            jobId
            managerSessionId
            worktree
            barrierId

    let managerPort: ManagerPort =
        { StartManager = startManager
          AwaitManager = awaitManager
          Reverify = reverify
          ResumeManager = resumeManager }

    // ── engine ──────────────────────────────────────────────────────────────

    let mutable engineInstance: Orchestrator option = None
    let engineGate = obj ()
    let mutable engineTask: Task<Result<Orchestrator, string>> option = None

    /// ORCH-008: freeze the publish target by `symbolic-ref` once, at engine start.
    ///
    /// A configured branch is still resolved through the same verb rather than trusted
    /// as a string, so a configured name that does not exist fails here instead of at
    /// publish time.
    let frozenTarget () =
        task {
            match! gitPort.FreezeTargetBranch() with
            | Ok target when String.IsNullOrWhiteSpace deps.TargetBranch -> return Ok target
            | Ok _ -> return Ok(TargetRef.create deps.TargetBranch)
            | Error error -> return Error error
        }

    let initializeEngine () : Task<Result<Orchestrator, string>> =
        task {
            match engineInstance with
            | Some value -> return Ok value
            | None ->
                match! frozenTarget () with
                | Error reason -> return Error reason
                | Ok target ->
                    // Canonicalize the repo path via git common-dir so symlinked
                    // spellings share one cross-process publish lock.
                    let lockRepoPath = RuntimePath.gitCommonDir deps.RepoPath
                    let sweepLockPath = IntegrationGate.lockPath lockRepoPath (TargetRef.value target)

                    let activeJobs =
                        deps.Journal
                        |> Option.map (fun journal ->
                            OrchestratorProjection.activeJobs
                                (AgentJournal.snapshot journal).AgentProjections.Orchestrator)
                        |> Option.defaultValue []

                    // Sweep orphaned manager artifacts before resuming jobs, so a
                    // resumed job never adopts a worktree the sweep is about to remove.
                    match! OrchestratorSweep.sweepLocked sweepLockPath gitPort activeJobs with
                    | Error error -> return Error(sprintf "orchestrator cleanup failed: %s" error)
                    | Ok() ->
                        let value =
                            Orchestrator(
                                gitPort,
                                managerPort,
                                deps.RepoPath,
                                target,
                                ?journal = (deps.Journal |> Option.map OrchestratorJournalPort.fromAgentJournal),
                                ?lockRepoPath = Some lockRepoPath
                            )

                        match deps.Journal with
                        | Some journal ->
                            do!
                                OrchestratorManagerJob.recoverJobs
                                    journal
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

    member _.ForkManagerJob(jobId: ManagerJobId, managerAgent: string, prompt: string) : Task<Result<string, string>> =
        task {
            match! engine () with
            | Error reason -> return Error reason
            | Ok engine ->
                match! engine.ForkManager(jobId, managerAgent, prompt) with
                | Error verdict -> return Error(sprintf "%A" verdict)
                | Ok handle -> return Ok(WorktreePath.value handle.WorktreePath)
        }

    member _.JoinPublished() : Task<string> =
        task {
            match! engine () with
            | Error reason -> return sprintf "Orchestrator init failed: %s" reason
            | Ok engine ->
                let! verdict = engine.JoinPublished()
                return sprintf "%A" verdict
        }

    member _.Cancel() = runtime.Cancel()
