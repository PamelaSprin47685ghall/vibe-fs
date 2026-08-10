namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.Session
open Wanxiangshu.Orchestrator

/// Host wiring for the Orchestrator: forks Managers and reviewers under one
/// runtime, and supplies `ManagerPort` to the pure publish program.
type OrchestratorHost(deps: OrchestratorHostDeps, orchestratorId: SessionId) =
    let worktrees = Dictionary<string, string>()
    let joinGate = obj ()
    // DSL-MUTABLE: single-flight — join-in-flight latch under joinGate
    let mutable joinInFlight = false

    let gitPort = GitOperations.createWithRepo deps.RepoPath OrchestratorGit.run

    let onChildCreated (agentId: string) (role: Role) (childId: SessionId) =
        if role = Role.Reviewer then
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
                    // ORCH-006 defence: the worktree is removed at publish. A
                    // residual manager-family prompt must not keep pointing at the
                    // deleted path (ARCH-004 seal break); fall back to the root
                    // workspace once the worktree is gone.
                    | true, path when System.IO.Directory.Exists path -> Some path
                    | _ -> None),
            ?sessionSnapshot = deps.SessionSnapshot,
            onRunStarted = deps.OnRunStarted,
            parentWorkRecordFor = deps.ParentWorkRecordFor,
            childWorkRecordFor = deps.ChildWorkRecordFor
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
        | AgentFailed payload -> Error payload.Message
        | AgentAbandoned(_, reason) -> Error reason

    /// Fork a child and hand back the Host session it created.
    ///
    /// The session comes from the runtime's own child map, not from the fork result:
    /// only the Host can issue a session id, and ORCH-006 requires the real one.
    let forkChild (agentId: string) (role: Role) (agent: string) (worktree: WorktreePath) (prompt: string) =
        task {
            worktrees.[agentId] <- WorktreePath.value worktree

            match! runtime.Fork(agentId, role, agent, prompt, None) with
            | Error error -> return Error error
            | Ok _ ->
                match runtime.TryChildSession agentId with
                | Some childId -> return Ok childId
                | None -> return Error(sprintf "Fork of '%s' produced no child session" agentId)
        }

    // Await one HostPendingRun.Source for this agent. Prefer Host pending over
    // ForkRuntime.AwaitAgent: same agentId resume would otherwise re-observe the
    // already-settled ChildRun.Completion and skip the new work unit.
    let awaitPendingSource (agentId: string) (source: Task<AgentCompletionOutcome>) =
        task {
            let! completedFirst =
                Wanxiangshu.Process.PtyTiming.raceExit (source :> Task) ExecutorSummarize.AwaitAgentTimeoutMs

            if not completedFirst then
                return Error(sprintf "await agent timed out: %s" agentId)
            else
                let! outcome = source

                match outcome with
                | AgentCompleted _ -> return Ok()
                | AgentFailed payload -> return Error payload.Message
                | AgentAbandoned(_, reason) -> return Error reason
        }

    // After Resume/Fork: wait for an unfinished Host pending Source, await it,
    // then return. If the manager finishes before we observe pending (fast path),
    // treat as done. Never finalize before this returns — early Ok on missing
    // pending raced installRun and staged an unresolved conflict (ORCH-003).
    let awaitCurrentPendingRun (agentId: string) =
        task {
            let deadline =
                DateTimeOffset.UtcNow.AddMilliseconds(float ExecutorSummarize.AwaitAgentTimeoutMs)
            // Brief window for sendToExistingChild to installRun after Fork returns.
            let appearDeadline = DateTimeOffset.UtcNow.AddMilliseconds(2000.0)

            let trySource () =
                lock runtime.Gate (fun () ->
                    match runtime.PendingRuns.TryGetValue agentId with
                    | true, run when not run.Finished -> Some run.Source.Task
                    | _ -> None)

            let rec waitAppear () =
                task {
                    match trySource () with
                    | Some source -> return Some source
                    | None when DateTimeOffset.UtcNow >= appearDeadline -> return None
                    | None ->
                        do! Wanxiangshu.Process.PtyTiming.timerTask 10
                        return! waitAppear ()
                }

            match! waitAppear () with
            | Some source -> return! awaitPendingSource agentId source
            | None ->
                // No pending within appear window: either still installing (spin)
                // or already finished. Poll until deadline for a late pending.
                let rec waitLate () =
                    task {
                        match trySource () with
                        | Some source -> return! awaitPendingSource agentId source
                        | None when DateTimeOffset.UtcNow >= deadline -> return Ok()
                        | None ->
                            do! Wanxiangshu.Process.PtyTiming.timerTask 25
                            return! waitLate ()
                    }

                return! waitLate ()
        }

    let awaitChild (agentId: string) =
        task {
            match
                lock runtime.Gate (fun () ->
                    match runtime.PendingRuns.TryGetValue agentId with
                    | true, run when not run.Finished -> Some run.Source.Task
                    | _ -> None)
            with
            | Some source -> return! awaitPendingSource agentId source
            | None ->
                match! runtime.AwaitAgent(agentId, ?timeoutMs = Some ExecutorSummarize.AwaitAgentTimeoutMs) with
                | Error error -> return Error error
                | Ok run -> return outcomeOf run
        }

    // ── ManagerPort ─────────────────────────────────────────────────────────

    let startManager (start: ManagerStart) : Task<Result<SessionId, string>> =
        forkChild (managerAgentId start.JobId) Role.Manager start.ManagerAgent start.Worktree start.Prompt

    /// Await the Manager, then stage its work into a candidate commit.
    ///
    /// `finalizeWorktree` runs only on a completed Manager: a failed or aborted run
    /// has nothing to commit, and committing anyway would produce a candidate the
    /// Manager never claimed was done.
    let awaitManager (jobId: ManagerJobId) : Task<Result<unit, string>> =
        task {
            let agentId = managerAgentId jobId

            let descriptor =
                DiagnosticWait.create
                    "manager-job-completion"
                    (CausalOwner.create "OrchestratorJob" [ "job", ManagerJobId.value jobId ])
                    [ "job", ManagerJobId.value jobId; "manager_agent", agentId ]
                    (WorkflowProducer(CausalOwner.create "ManagerWorkflow" [ "agent", agentId ]))
                    [ WaitEscape.DeadlineAt(
                          DateTimeOffset.UtcNow.AddMilliseconds(float ExecutorSummarize.AwaitAgentTimeoutMs)
                      )
                      WaitEscape.ProcessLifetime ]
                    "OrchestratorHost.awaitManager"

            match! CausalAwait.awaitTask CausalWaitHub.observer descriptor (awaitChild agentId) with
            | Error error -> return Error error
            | Ok() ->
                match worktrees.TryGetValue agentId with
                | true, path -> return! OrchestratorGit.finalizeWorktree OrchestratorGit.run agentId path
                | false, _ -> return Error(sprintf "No worktree registered for manager job '%s'" agentId)
        }

    /// Drop a leftover Host pending for this agent so Resume can install a fresh
    /// work unit. A stuck unfinished pending (e.g. dual-suicide race) would make
    /// sendToExistingChild take the busy-nudge path and never re-installRun, leaving
    /// awaitCurrentPendingRun waiting on a Source that will not observe conflict
    /// resolution (ORCH-003 measured: conflict-resume on wire, REBASE_HEAD unmerged).
    let clearStaleHostRun (agentId: string) =
        lock runtime.Gate (fun () ->
            match runtime.PendingRuns.TryGetValue agentId with
            | true, run when not run.Finished ->
                run.Finished <- true
                run.Subscription |> Option.iter (fun s -> s.Dispose())

                run.Source.TrySetResult(
                    AgentCompletion.failed
                        agentId
                        ("run-" + agentId)
                        (Some Role.Manager)
                        None
                        "SUPERSEDED"
                        "superseded by ResumeManager"
                )
                |> ignore

                runtime.PendingRuns.Remove agentId |> ignore
            | true, _ -> runtime.PendingRuns.Remove agentId |> ignore
            | false, _ -> ())

    /// ORCH-003/ORCH-007: hand work back to the SAME Manager in the SAME worktree.
    ///
    /// Fork the conflict-resume prompt, then wait until the worktree has no
    /// unmerged paths / conflict markers (Coder resolution on disk), then
    /// finalizeWorktree. Do not await Host pending terminal alone: after
    /// LifeCompleted the Manager turn often stays on IdleEncouragement and never
    /// NotifyTerminal (measured: conflict-resume.2 + coder resolved, REBASE_HEAD
    /// stuck, no manager HandleCompleted for the resume unit).
    let resumeManager (jobId: ManagerJobId) (worktree: WorktreePath) (prompt: string) =
        task {
            match jobRecord jobId with
            | None -> return Error(sprintf "No durable job record for '%s'" (ManagerJobId.value jobId))
            | Some record ->
                let agentId = managerAgentId jobId
                let path = WorktreePath.value worktree
                worktrees.[agentId] <- path
                clearStaleHostRun agentId

                match runtime.Children.TryGetValue agentId with
                | true, _ -> ()
                | false, _ -> runtime.AdoptChild(agentId, record.ManagerSessionId)

                match! runtime.Fork(agentId, Role.Manager, record.ManagerAgent, prompt, None, firstPrompt = false) with
                | Error error -> return Error error
                | Ok _ ->
                    // Keep Host pending progressing in the background.
                    awaitCurrentPendingRun agentId |> ignore

                    let resolutionDeadline =
                        DateTimeOffset.UtcNow.AddMilliseconds(float ExecutorSummarize.AwaitAgentTimeoutMs)

                    // Gate on disk content only. Unmerged index entries clear only
                    // after `git add`; that belongs to finalizeWorktree. Waiting for
                    // empty `--diff-filter=U` before add is a deadlock (Coder can
                    // rewrite the file while paths stay AA until staged).
                    let rec waitResolved () =
                        task {
                            let! grepCode, grepOut, _ =
                                OrchestratorGit.run (
                                    OrchestratorGit.command
                                        path
                                        [ "grep"; "-I"; "-n"; "-E"; "^<<<<<<< |^>>>>>>> "; "--"; "." ]
                                )

                            // git grep: 0 = markers present, 1 = clean, >1 = error
                            if grepCode = 1 then
                                return Ok()
                            elif grepCode > 1 then
                                return Error "conflict-marker scan failed"
                            elif DateTimeOffset.UtcNow >= resolutionDeadline then
                                return
                                    Error(
                                        sprintf
                                            "conflict resolution timed out (markers still present):\n%s"
                                            (if String.IsNullOrWhiteSpace grepOut then
                                                 "(no grep body)"
                                             else
                                                 grepOut.Trim())
                                    )
                            else
                                do! Wanxiangshu.Process.PtyTiming.timerTask 50
                                return! waitResolved ()
                        }

                    match! waitResolved () with
                    | Error error -> return Error error
                    | Ok() -> return! OrchestratorGit.finalizeWorktree OrchestratorGit.run agentId path
        }

    /// ORCH-006: abort the manager and every reviewer child session for a job
    /// before the worktree is released. This is a non-failing cleanup step.
    let terminateChildren (jobId: ManagerJobId) : Task<unit> =
        task {
            let managerId = managerAgentId jobId
            let reviewerPrefix = OrchestratorManagerJob.reviewerAgentId jobId + "-"

            let entries =
                lock runtime.Gate (fun () ->
                    runtime.Children
                    |> Seq.choose (fun kv ->
                        let id = kv.Key

                        if id = managerId || id.StartsWith(reviewerPrefix) then
                            Some(kv.Value, id)
                        else
                            None)
                    |> Seq.toList)

            let sessions, ids = List.unzip entries
            let! _ = HostForkChildDispatch.teardownChildren runtime.Sessions sessions

            lock runtime.Gate (fun () ->
                for id in ids do
                    runtime.Children.Remove id |> ignore)
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
                forkChild reviewerAgentId Role.Reviewer OrchestratorHostReview.DeepReviewerAgent path prompt)
            (fun _ -> awaitChild reviewerAgentId)
            jobId
            managerSessionId
            worktree
            barrierId

    let managerPort: ManagerPort =
        { StartManager = startManager
          AwaitManager = awaitManager
          Reverify = reverify
          ResumeManager = resumeManager
          TerminateChildren = terminateChildren }

    // ── engine ──────────────────────────────────────────────────────────────

    // DSL-MUTABLE: resource — memoized orchestrator engine instance
    let mutable engineInstance: Orchestrator option = None
    let engineGate = obj ()
    // DSL-MUTABLE: single-flight — engine create task under engineGate
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
                    let sweepDescriptor =
                        DiagnosticWait.create
                            "orchestrator-engine-sweep"
                            (CausalOwner.create "OrchestratorWorkflow" [ "session", SessionId.value orchestratorId ])
                            [ "lock", sweepLockPath; "target", TargetRef.value target ]
                            (ExternalProducer("integration-gate", [ "lock", sweepLockPath ]))
                            [ WaitEscape.ProcessLifetime ]
                            "OrchestratorHost.initializeEngine.sweepLocked"

                    match!
                        CausalAwait.awaitTask
                            CausalWaitHub.observer
                            sweepDescriptor
                            (OrchestratorSweep.sweepLocked sweepLockPath gitPort activeJobs)
                    with
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
            let descriptor =
                DiagnosticWait.create
                    "fork-manager-job"
                    (CausalOwner.create "OrchestratorWorkflow" [ "session", SessionId.value orchestratorId ])
                    [ "job", ManagerJobId.value jobId; "manager_agent", managerAgent ]
                    (ExternalProducer("orchestrator-engine", [ "job", ManagerJobId.value jobId ]))
                    [ WaitEscape.ProcessLifetime; WaitEscape.SessionLifetime ]
                    "OrchestratorHost.ForkManagerJob"

            let pending =
                task {
                    match! engine () with
                    | Error reason -> return Error reason
                    | Ok engine ->
                        match! engine.ForkManager(jobId, managerAgent, prompt) with
                        | Error verdict -> return Error(sprintf "%A" verdict)
                        | Ok handle -> return Ok(WorktreePath.value handle.WorktreePath)
                }

            return! CausalAwait.awaitTask CausalWaitHub.observer descriptor pending
        }

    /// GLORY-068: `fork-manager(existing_job_id, prompt)` — continue the SAME
    /// Manager job (same worktree, same session) with an appended requirement.
    member _.ContinueManagerJob(jobId: ManagerJobId, prompt: string) : Task<Result<string, string>> =
        task {
            match! engine () with
            | Error reason -> return Error reason
            | Ok engine ->
                match! engine.ContinueManager(jobId, prompt) with
                | Error error -> return Error error
                | Ok path -> return Ok(WorktreePath.value path)
        }

    /// Compatibility single-result join (stringified Empty/verdict). Prefer JoinPublishedAvailable.
    member _.JoinPublished() : Task<string> =
        task {
            match! engine () with
            | Error reason -> return sprintf "Orchestrator init failed: %s" reason
            | Ok engine ->
                let! verdict = engine.JoinPublished()
                return sprintf "%A" verdict
        }

    /// EXEC-019: FIFO batch + local interrupt (JoinTool renders wire).
    member _.JoinPublishedAvailable
        (maxCount: int, interrupt: Task<JoinInterruptReason>)
        : Task<Result<JoinWaitOutcome<OrchestratorVerdict>, string>> =
        let acquired =
            lock joinGate (fun () ->
                if joinInFlight then
                    false
                else
                    joinInFlight <- true
                    true)

        if not acquired then
            Task.FromResult(Error "JOIN_IN_PROGRESS: another join call is already waiting for this session")
        else
            task {
                try
                    match! engine () with
                    | Error reason -> return Error reason
                    | Ok engine ->
                        let! outcome = engine.JoinPublishedBatch(maxCount, interrupt)
                        return Ok outcome
                finally
                    lock joinGate (fun () -> joinInFlight <- false)
            }

    member _.Cancel() = runtime.Cancel()
