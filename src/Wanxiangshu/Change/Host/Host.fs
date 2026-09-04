namespace Wanxiangshu.Change.Host

open System
open System.Collections.Generic
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Change
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Git
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Mission.Relay.OpenCode
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

/// Host wiring for one Change Road. One physical Manager session can host many
/// logical Relay incumbencies; Change never creates a second audit session.
type OrchestratorHost(deps: OrchestratorHostDeps, orchestratorId: SessionId) =
    // DSL-MUTABLE: resource — manager worktree path registry
    let worktrees = Dictionary<string, string>()
    let joinGate = obj ()
    // DSL-MUTABLE: single-flight — join-in-flight latch under joinGate
    let mutable joinInFlight = false
    let authorityUpdateGate = obj ()
    let authorityUpdatesInFlight = HashSet<string>()

    let gitPort = GitOperations.createWithRepo deps.RepoPath OrchestratorGit.run

    let onChildCreated (agentId: string) (role: Role) (childId: SessionId) =
        deps.OnChildCreated agentId role childId

    let runtime =
        let childWorkRecordForRun childId range providerRun =
            LifecycleWorkRecordProjection.lifecycleWorkRecordBoundedForRun deps.Journal childId range providerRun

        HostForkRuntime(
            orchestratorId,
            deps.Sessions,
            childWorkRecordForRun,
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
    /// and nowhere else (PROMPT-008 forbids rebuilding it from the role).
    let jobRecord (jobId: ManagerJobId) =
        deps.Journal
        |> Option.bind (fun journal ->
            OrchestratorProjection.tryFind jobId (AgentJournal.snapshot journal).AgentProjections.Orchestrator)

    let outcomeResult (outcome: AgentCompletionOutcome) =
        match outcome with
        | AgentCompleted _ -> Ok()
        | AgentFailed payload -> Error payload.Message
        | AgentAbandoned(_, reason) -> Error reason

    let outcomeOf (run: RunCompletion) = outcomeResult run.Outcome

    let childSessionOrError (agentId: string) =
        match runtime.TryChildSession agentId with
        | Some childId -> Ok childId
        | None -> Error(sprintf "Fork of '%s' produced no child session" agentId)

    /// Fork a child and hand back the Host session it created.
    ///
    /// The session comes from the runtime's own child map, not from the fork result:
    /// only the Host can issue a session id, and ORCH-006 requires the real one.
    let forkChild
        (agentId: string)
        (role: Role)
        (agent: string)
        (worktree: WorktreePath)
        (prompt: string)
        (deferSend: bool)
        (expectedToolCalls: int option)
        =
        taskResult {
            worktrees.[agentId] <- WorktreePath.value worktree

            let! _fork =
                runtime.Fork(
                    agentId,
                    role,
                    agent,
                    prompt,
                    None,
                    deferSend = deferSend,
                    ?expectedToolCalls = expectedToolCalls
                )

            return! childSessionOrError agentId
        }

    // Await one HostPendingRun.Source for this agent. Prefer Host pending over
    // ForkRuntime.AwaitAgent: same agentId resume would otherwise re-observe the
    // already-settled ChildRun.Completion and skip the new work unit.
    let awaitPendingSource (agentId: string) (source: Task<AgentCompletionOutcome>) =
        task {
            let! completedFirst =
                Wanxiangshu.Process.PtyTiming.raceExit (source :> Task) Distillation.AwaitAgentTimeoutMs

            if not completedFirst then
                return Error(sprintf "await agent timed out: %s" agentId)
            else
                let! outcome = source
                return outcomeResult outcome
        }

    let awaitPendingOrJoin (agentId: string) (sourceOpt: Task<AgentCompletionOutcome> option) =
        match sourceOpt with
        | Some source -> awaitPendingSource agentId source
        | None ->
            taskResult {
                let! run = HostForkJoin.awaitAgent runtime agentId (Some Distillation.AwaitAgentTimeoutMs)
                return! outcomeOf run
            }

    let awaitChild (agentId: string) =
        let sourceOpt =
            lock runtime.Gate (fun () ->
                match runtime.PendingRuns.TryGetValue agentId with
                | true, run when not run.Finished -> Some run.Source.Task
                | _ -> None)

        awaitPendingOrJoin agentId sourceOpt

    // ── RelayPort ───────────────────────────────────────────────────────────

    let openRoad (start: RoadStart) : Task<Result<SessionId, string>> =
        forkChild
            (managerAgentId start.JobId)
            Role.Manager
            start.ManagerAgent
            start.Worktree
            start.RootRequest
            true
            start.ExpectedToolCalls

    let activateRoad (jobId: ManagerJobId) : Task<Result<unit, string>> =
        runtime.SendDeferredFirstPrompt(managerAgentId jobId)

    let requireJobRecord (jobId: ManagerJobId) =
        match jobRecord jobId with
        | Some record -> Ok record
        | None -> Error(sprintf "No durable job record for '%s'" (ManagerJobId.value jobId))

    let roadIdOf (record: ManagerJobProjection) =
        RoadId.create (SessionId.value record.ManagerSessionId)

    let relayView (projection: ProjectionSet) (record: ManagerJobProjection) =
        AgentProjection.tryFind record.ManagerSessionId projection.AgentProjections
        |> Option.bind (fun session -> session.Relay)
        |> Option.bind (fun relay -> Fold.view relay (roadIdOf record))

    let roadSignalOfRetirement (road: RoadView) (retirement: RetirementSummary) =
        match road.Certificate with
        | Some certificate when retirement.QualityCandidateAccepted && certificate.Valid ->
            RoadSignal.QualityCandidateAccepted(retirement, certificate)
        | _ -> RoadSignal.IncumbencyRetired retirement

    let roadSignalOfRoad (road: RoadView) =
        if road.ActiveIncumbency.IsSome then
            None
        else
            road.LatestRetirement |> Option.map (roadSignalOfRetirement road)

    let signalOfProjection projection record =
        relayView projection record |> Option.bind roadSignalOfRoad

    let requireJournalAndJob (journal: AgentJournal option) (jobId: ManagerJobId) (journalError: string) =
        match journal, requireJobRecord jobId with
        | None, _ -> Error journalError
        | _, Error error -> Error error
        | Some journal, Ok record -> Ok(journal, record)

    let rec awaitRoadSignalFromJournal
        (jobId: ManagerJobId)
        (journal: AgentJournal)
        (record: ManagerJobProjection)
        : Task<Result<RoadSignal, string>> =
        task {
            let projection, revision = AgentJournal.snapshotWithRevision journal

            match signalOfProjection projection record with
            | Some signal -> return Ok signal
            | None ->
                let! _ = AgentJournal.awaitChangeFrom revision journal
                return! awaitRoadSignal jobId
        }

    and awaitRoadSignal (jobId: ManagerJobId) : Task<Result<RoadSignal, string>> =
        match requireJournalAndJob deps.Journal jobId "Relay Road requires a durable journal" with
        | Error error -> Task.FromResult(Error error)
        | Ok(journal, record) -> awaitRoadSignalFromJournal jobId journal record

    let appendRelay (journal: AgentJournal) (record: ManagerJobProjection) (transaction: RelayTransaction) =
        AgentJournal.appendAgent
            (StreamId.Session record.ManagerSessionId)
            None
            (AgentFact.Relay(
                RelayFactCases.TransactionCommitted
                    {| RoadId = roadIdOf record
                       Transaction = transaction |}
            ))
            journal

    let appendRelayResult journal record transaction =
        task {
            let! result = appendRelay journal record transaction
            return result |> Result.mapError JournalAppendFailure.describe
        }

    let certificateToInvalidate (journal: AgentJournal) (record: ManagerJobProjection) =
        relayView (AgentJournal.snapshot journal) record
        |> Option.bind (fun road -> road.Certificate)
        |> Option.map (fun cert -> cert.Id)

    let buildInvalidationTransaction reason certificateIdOpt =
        match certificateIdOpt with
        | None -> Ok None
        | Some certificateId ->
            RelayTransaction.create [ RelayEvent.QualityCertificateInvalidated(certificateId, reason) ]
            |> Result.map Some

    let appendInvalidation
        (journal: AgentJournal)
        (record: ManagerJobProjection)
        (transactionOpt: RelayTransaction option)
        : Task<Result<unit, string>> =
        match transactionOpt with
        | None -> Task.FromResult(Ok())
        | Some transaction ->
            taskResult {
                let! _ = appendRelayResult journal record transaction
                return ()
            }

    let invalidateCertificate (jobId: ManagerJobId) reason : Task<Result<unit, string>> =
        taskResult {
            let! journal, record =
                requireJournalAndJob deps.Journal jobId "Certificate invalidation requires a durable journal"

            let certIdOpt = certificateToInvalidate journal record
            let! transactionOpt = buildInvalidationTransaction reason certIdOpt
            return! appendInvalidation journal record transactionOpt
        }

    let tryCaptureSnapshot (worktreePath: WorktreePath) : Result<WorkspaceSnapshotId, string> =
        try
            Ok(WorkspaceSnapshot.capture (WorktreePath.value worktreePath))
        with error ->
            Error error.Message

    let captureSnapshot (jobId: ManagerJobId) : Task<Result<WorkspaceSnapshotId, string>> =
        requireJobRecord jobId
        |> Result.bind (fun record -> tryCaptureSnapshot record.WorktreePath)
        |> Task.FromResult

    let deterministicSuccessor (retirementId: RetirementId) =
        HostDigest.sha256Hex ("successor-v1\n" + RetirementId.value retirementId)
        |> fun digest -> IncumbencyId.create ("incumbency:" + digest)

    let successorPrompt (sessionId: SessionId) =
        ProviderProse.documentFor sessionId "runtime/relay-successor" Map.empty

    let sendSuccessor (record: ManagerJobProjection) (retirement: RetirementSummary) =
        let terminalRun =
            ProviderRunIdentity.create retirement.ProjectionCut.ThroughProviderRunId

        HostSessionNudge.trySendGateContinuationPhysical
            deps.Sessions
            record.ManagerSessionId
            (successorPrompt record.ManagerSessionId)
            PromptAuthority.ContinuationKind.ManagerGuard
            (Some(WorktreePath.value record.WorktreePath))
            deps.Journal
            (RelaySuccessorGate.gateKind retirement.Id)
            terminalRun

    let requireOpenRoad (journal: AgentJournal) (record: ManagerJobProjection) =
        let projection = AgentJournal.snapshot journal

        relayView projection record |> Result.requireSome "Relay Road is not open"

    let requireCommittedRetirement (road: RoadView) =
        road.LatestRetirement
        |> Result.requireSome "Successor requires a committed predecessor retirement"

    let buildSuccessorTransaction
        (retirementId: RetirementId)
        (incumbent: IncumbencyId)
        (snapshot: WorkspaceSnapshotId)
        (authority: AuthorityRevision)
        reason
        =
        RelayTransaction.create
            [ RelayEvent.SuccessorRequested(retirementId, reason)
              RelayEvent.SuccessorActivated(retirementId, incumbent, snapshot, authority) ]

    let activateSuccessorIfAbsent
        (journal: AgentJournal)
        (record: ManagerJobProjection)
        (road: RoadView)
        (retirement: RetirementSummary)
        (incumbent: IncumbencyId)
        (worktree: WorktreePath)
        reason
        : Task<Result<unit, string>> =
        if road.ActiveIncumbency.IsSome then
            Task.FromResult(Ok())
        else
            taskResult {
                let snapshot = WorkspaceSnapshot.capture (WorktreePath.value worktree)

                let! transaction =
                    buildSuccessorTransaction retirement.Id incumbent snapshot road.AuthorityRevision reason

                let! _ = appendRelayResult journal record transaction
                return ()
            }

    let deliverActivatedSuccessor
        (record: ManagerJobProjection)
        (retirement: RetirementSummary)
        (incumbent: IncumbencyId)
        : Task<Result<IncumbencyId, string>> =
        taskResult {
            let! _ = sendSuccessor record retirement
            return incumbent
        }

    let requestSuccessor (jobId: ManagerJobId) (worktree: WorktreePath) reason : Task<Result<IncumbencyId, string>> =
        taskResult {
            let! journal, record =
                requireJournalAndJob deps.Journal jobId "Successor activation requires a durable journal"

            let! road = requireOpenRoad journal record
            let! retirement = requireCommittedRetirement road

            let incumbent =
                road.ActiveIncumbency
                |> Option.defaultValue (deterministicSuccessor retirement.Id)

            do! activateSuccessorIfAbsent journal record road retirement incumbent worktree reason
            return! deliverActivatedSuccessor record retirement incumbent
        }

    let finalizeRegisteredWorktree (agentId: string) =
        match worktrees.TryGetValue agentId with
        | true, path -> OrchestratorGit.finalizeWorktree OrchestratorGit.run agentId path
        | false, _ -> Task.FromResult(Error(sprintf "No worktree registered for manager job '%s'" agentId))

    let prepareCandidate (jobId: ManagerJobId) : Task<Result<CommitHash, string>> =
        taskResult {
            let! record = requireJobRecord jobId
            do! finalizeRegisteredWorktree (managerAgentId jobId)
            return! gitPort.ReadHead record.WorktreePath
        }

    let terminateRoadResources (jobId: ManagerJobId) : Task<unit> =
        task {
            let managerId = managerAgentId jobId

            let entry =
                lock runtime.Gate (fun () ->
                    match runtime.Children.TryGetValue managerId with
                    | true, sessionId -> Some sessionId
                    | false, _ -> None)

            match entry with
            | None -> return ()
            | Some sessionId ->
                let! _ = HostForkChildDispatch.teardownChildren runtime.Sessions [ sessionId ]
                lock runtime.Gate (fun () -> runtime.Children.Remove managerId |> ignore)
        }

    let relayPort: RelayPort =
        { OpenRoad = openRoad
          ActivateRoad = activateRoad
          AwaitRoadSignal = awaitRoadSignal
          InvalidateCertificate = invalidateCertificate
          RequestSuccessor = requestSuccessor
          CaptureSnapshot = captureSnapshot
          PrepareCandidate = prepareCandidate
          TerminateRoadResources = terminateRoadResources }

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

    let recoverJobsIfPresent (value: Orchestrator) =
        match deps.Journal with
        | Some journal ->
            OrchestratorManagerJob.recoverJobs journal orchestratorId worktrees deps.RegisterChildDirectory value
        | None -> task { return () }

    let mapSweepError (pending: Task<Result<unit, string>>) : Task<Result<unit, string>> =
        task {
            match! pending with
            | Ok() -> return Ok()
            | Error error -> return Error(sprintf "orchestrator cleanup failed: %s" error)
        }

    let createEngine (target: TargetRef) : Task<Result<Orchestrator, string>> =
        taskResult {
            // Canonicalize the repo path via git common-dir so symlinked
            // spellings share one cross-process publish lock.
            let lockRepoPath = RuntimePath.gitCommonDir deps.RepoPath
            let sweepLockPath = IntegrationGate.lockPath lockRepoPath (TargetRef.value target)

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

            do!
                CausalAwait.awaitTask
                    CausalWaitHub.observer
                    sweepDescriptor
                    (OrchestratorSweep.sweepLocked sweepLockPath gitPort (fun () ->
                        deps.Journal
                        |> Option.map (fun journal ->
                            OrchestratorProjection.activeJobs
                                (AgentJournal.snapshot journal).AgentProjections.Orchestrator)
                        |> Option.defaultValue []))
                |> mapSweepError

            let value =
                Orchestrator(
                    gitPort,
                    relayPort,
                    deps.RepoPath,
                    target,
                    ?journal = (deps.Journal |> Option.map OrchestratorJournalPort.fromAgentJournal),
                    ?lockRepoPath = Some lockRepoPath
                )

            do! recoverJobsIfPresent value |> TaskResultCE.ofTask
            lock engineGate (fun () -> engineInstance <- Some value)
            return value
        }

    let initializeEngine () : Task<Result<Orchestrator, string>> =
        match engineInstance with
        | Some value -> Task.FromResult(Ok value)
        | None ->
            taskResult {
                let! target = frozenTarget ()
                return! createEngine target
            }

    let engine () : Task<Result<Orchestrator, string>> =
        lock engineGate (fun () ->
            match engineInstance, engineTask with
            | Some value, _ -> Task.FromResult(Ok value)
            | None, Some task -> task
            | None, None ->
                let task = initializeEngine ()
                engineTask <- Some task
                task)

    let mapForkManagerError
        (pending: Task<Result<OrchestratorHandle, OrchestratorVerdict>>)
        : Task<Result<OrchestratorHandle, string>> =
        task {
            match! pending with
            | Ok handle -> return Ok handle
            | Error verdict -> return Error(sprintf "%A" verdict)
        }

    let providerBynameOrAgent (byname: string option) (managerAgent: string) =
        match byname with
        | Some value when not (String.IsNullOrWhiteSpace value) -> value.Trim()
        | _ -> managerAgent

    let replaceEstimateIfPresent (jobId: ManagerJobId) (expectedToolCalls: int option) =
        match expectedToolCalls, jobRecord jobId, deps.Journal with
        | Some expected, Some record, Some journal ->
            DelegatedToolEstimateLedger.replace journal record.ManagerSessionId expected
        | _ -> task { return () }

    let authorityRevisionFor
        (record: ManagerJobProjection)
        (callerProviderRun: ProviderRunIdentity)
        (callerToolCallId: ToolCallId)
        (prompt: string)
        =
        String.concat
            "\n"
            [ "relay-authority-revision-v1"
              RoadId.value (roadIdOf record)
              ProviderRunIdentity.value callerProviderRun
              ToolCallId.value callerToolCallId
              HostDigest.sha256Hex prompt ]
        |> HostDigest.sha256Hex
        |> fun digest -> AuthorityRevision.create ("authority-revision:" + digest)

    let tryEnterAuthorityUpdate (jobId: ManagerJobId) =
        lock authorityUpdateGate (fun () -> authorityUpdatesInFlight.Add(ManagerJobId.value jobId))

    let leaveAuthorityUpdate (jobId: ManagerJobId) =
        lock authorityUpdateGate (fun () -> authorityUpdatesInFlight.Remove(ManagerJobId.value jobId) |> ignore)

    let joinPublishedBatchOnce
        (maxCount: int)
        (interrupt: Task<JoinInterruptReason>)
        : Task<Result<JoinWaitOutcome<OrchestratorVerdict>, string>> =
        taskResult {
            try
                let! engine = engine ()
                let! outcome = engine.JoinPublishedBatch(maxCount, interrupt) |> TaskResultCE.ofTask
                return outcome
            finally
                lock joinGate (fun () -> joinInFlight <- false)
        }

    let requireActiveWorkOwnedIncumbent (road: RoadView) : Result<IncumbencyId, string> =
        match road.ActiveIncumbency, road.ActivePhase with
        | Some active, Some IncumbencyPhase.WorkOwned -> Ok active
        | Some _, Some phase -> Error(sprintf "Relay incumbency cannot take new charge in phase %A" phase)
        | _ -> Error "Relay Road has no active incumbent"

    let advanceAuthorityRevision
        (journal: AgentJournal)
        (record: ManagerJobProjection)
        (road: RoadView)
        (incumbent: IncumbencyId)
        (nextRevision: AuthorityRevision)
        (prompt: string)
        (callerProviderRun: ProviderRunIdentity)
        : Task<Result<string, string>> =
        taskResult {
            let expectedRevision = road.AuthorityRevision
            let! snapshot = captureSnapshot record.ManagerJobId
            let gateKind = "relay-authority-update:" + AuthorityRevision.value nextRevision

            let! physicalAuthorityMessage =
                HostSessionNudge.trySendGateContinuationPhysical
                    deps.Sessions
                    record.ManagerSessionId
                    prompt
                    PromptAuthority.ContinuationKind.ManagedDelegationAssignment
                    (Some(WorktreePath.value record.WorktreePath))
                    (Some journal)
                    gateKind
                    callerProviderRun

            let! transaction =
                RelayTransaction.create
                    [ RelayEvent.AuthorityRevisionAdvanced(
                          incumbent,
                          expectedRevision,
                          nextRevision,
                          physicalAuthorityMessage,
                          snapshot
                      ) ]

            let! _ = appendRelayResult journal record transaction
            return WorktreePath.value record.WorktreePath
        }

    let continueManagerJobCore
        (jobId: ManagerJobId)
        (prompt: string)
        (callerProviderRun: ProviderRunIdentity)
        (callerToolCallId: ToolCallId)
        (expectedToolCalls: int option)
        : Task<Result<string, string>> =
        taskResult {
            do! replaceEstimateIfPresent jobId expectedToolCalls |> TaskResultCE.ofTask
            let! record = requireJobRecord jobId

            let! journal =
                deps.Journal
                |> Result.requireSome "Relay authority update requires a durable journal"

            let! road = requireOpenRoad journal record

            let nextRevision =
                authorityRevisionFor record callerProviderRun callerToolCallId prompt

            if List.contains nextRevision road.AuthorityRevisions then
                return WorktreePath.value record.WorktreePath
            else
                let! incumbent = requireActiveWorkOwnedIncumbent road
                return! advanceAuthorityRevision journal record road incumbent nextRevision prompt callerProviderRun
        }

    let runAuthorityUpdate (jobId: ManagerJobId) (action: unit -> Task<Result<string, string>>) =
        task {
            try
                return! action ()
            finally
                leaveAuthorityUpdate jobId
        }

    member _.ForkManagerJob
        (jobId: ManagerJobId, managerAgent: string, prompt: string, ?byname: string, ?expectedToolCalls: int)
        : Task<Result<string, string>> =
        let providerByname = providerBynameOrAgent byname managerAgent

        let descriptor =
            DiagnosticWait.create
                "commission-manager-job"
                (CausalOwner.create "OrchestratorWorkflow" [ "session", SessionId.value orchestratorId ])
                [ "job", ManagerJobId.value jobId; "manager_agent", managerAgent ]
                (ExternalProducer("orchestrator-engine", [ "job", ManagerJobId.value jobId ]))
                [ WaitEscape.ProcessLifetime; WaitEscape.SessionLifetime ]
                "OrchestratorHost.CommissionManagerJob"

        let pending =
            taskResult {
                let! engine = engine ()

                let! handle =
                    engine.ForkManager(
                        jobId,
                        managerAgent,
                        prompt,
                        byname = providerByname,
                        ?expectedToolCalls = expectedToolCalls
                    )
                    |> mapForkManagerError

                return WorktreePath.value handle.WorktreePath
            }

        CausalAwait.awaitTask CausalWaitHub.observer descriptor pending

    /// Same-road charge update. The physical Session/worktree stay stable, but
    /// the requirement is a new durable Relay AuthorityRevision. Exact tool
    /// replay reuses the same gate occasion and therefore the same physical
    /// authority message; it never creates a second revision.
    member _.ContinueManagerJob
        (
            jobId: ManagerJobId,
            prompt: string,
            callerProviderRun: ProviderRunIdentity,
            callerToolCallId: ToolCallId,
            ?expectedToolCalls: int
        ) : Task<Result<string, string>> =
        if not (tryEnterAuthorityUpdate jobId) then
            Task.FromResult(Error "Relay authority update is already in flight for this Road")
        else
            runAuthorityUpdate jobId (fun () ->
                continueManagerJobCore jobId prompt callerProviderRun callerToolCallId expectedToolCalls)

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
            joinPublishedBatchOnce maxCount interrupt

    member _.CancelAndDrain() : Task = runtime.CancelAndDrain()

    member _.DetachAndDrain() : Task = runtime.DetachAndDrain()

    member _.Cancel() = runtime.Cancel()
