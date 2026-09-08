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
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

/// Host wiring for one Change manager session. One physical Manager session can host many
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
            CompletionMailboxRuntime.create,
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

    // ── RelayPort ───────────────────────────────────────────────────────────

    let createManagerSession (start: ManagerStart) : Task<Result<SessionId, string>> =
        forkChild
            (managerAgentId start.JobId)
            Role.Manager
            start.ManagerAgent
            start.Worktree
            start.RootRequest
            true
            start.ExpectedToolCalls

    let activateManager (jobId: ManagerJobId) : Task<Result<unit, string>> =
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

    let loopSignalOfRetirement (road: RoadView) (retirement: RetirementSummary) =
        match retirement.Outcome, road.Certificate with
        | RetirementOutcome.Continue, _ -> ManagerLoopSignal.Continue
        | RetirementOutcome.Accepted certificateId, Some certificate when
            certificate.Id = certificateId && certificate.Valid
            ->
            ManagerLoopSignal.Candidate certificate
        | RetirementOutcome.Accepted certificateId, Some certificate when
            certificate.Id = certificateId && not certificate.Valid
            ->
            ManagerLoopSignal.Continue
        | RetirementOutcome.Accepted certificateId, _ ->
            ManagerLoopSignal.ExceptionalTerminal(
                sprintf
                    "Accepted retirement %s without matching valid certificate %s"
                    (RetirementId.value retirement.Id)
                    (QualityCertificateId.value certificateId)
            )

    let loopSignalOfRoad (road: RoadView) =
        if road.ActiveIncumbency.IsSome then
            None
        else
            road.LatestRetirement |> Option.map (loopSignalOfRetirement road)

    let signalOfProjection projection record =
        relayView projection record |> Option.bind loopSignalOfRoad

    let requireJournalAndJob (journal: AgentJournal option) (jobId: ManagerJobId) (journalError: string) =
        match journal, requireJobRecord jobId with
        | None, _ -> Error journalError
        | _, Error error -> Error error
        | Some journal, Ok record -> Ok(journal, record)

    let rec awaitLoopSignalFromJournal
        (jobId: ManagerJobId)
        (journal: AgentJournal)
        (record: ManagerJobProjection)
        : Task<Result<ManagerLoopSignal, string>> =
        task {
            let projection, revision = AgentJournal.snapshotWithRevision journal

            match signalOfProjection projection record with
            | Some signal -> return Ok signal
            | None -> return! awaitDispatchedWait jobId journal record projection revision
        }

    and awaitDispatchedWait jobId journal record _projection revision =
        task {
            // ManagerWorkflow owns the sole physical send for the
            // `ManagerLoopGate.gateKind retirement.Id` occasion; Change only awaits
            // the durable loop signal admitted through the PromptAuthority gate.
            match! deps.ContinueManagerLoop record.ManagerSessionId (WorktreePath.value record.WorktreePath) with
            | Error error -> return Error error
            | Ok() ->
                let! _ = AgentJournal.awaitChangeFrom revision journal
                return! awaitLoopSignal jobId
        }

    and awaitLoopSignal (jobId: ManagerJobId) : Task<Result<ManagerLoopSignal, string>> =
        match requireJournalAndJob deps.Journal jobId "Manager session requires a durable journal" with
        | Error error -> Task.FromResult(Error error)
        | Ok(journal, record) -> awaitLoopSignalFromJournal jobId journal record

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
        deps.CaptureWorktreeSnapshot worktreePath

    let captureSnapshot (jobId: ManagerJobId) : Task<Result<WorkspaceSnapshotId, string>> =
        requireJobRecord jobId
        |> Result.bind (fun record -> tryCaptureSnapshot record.WorktreePath)
        |> Task.FromResult

    let requireOpenRoad (journal: AgentJournal) (record: ManagerJobProjection) =
        let projection = AgentJournal.snapshot journal

        relayView projection record |> Result.requireSome "Manager session is not open"

    let requireCommittedRetirement (road: RoadView) =
        road.LatestRetirement
        |> Result.requireSome "Manager loop continuation requires a committed retirement"

    let activateNextIfAbsent
        (journal: AgentJournal)
        (record: ManagerJobProjection)
        (road: RoadView)
        (retirement: RetirementSummary)
        : Task<Result<IncumbencyId, string>> =
        match road.ActiveIncumbency with
        | Some active -> Task.FromResult(Ok active)
        | None ->
            taskResult {
                let! snapshot = tryCaptureSnapshot record.WorktreePath |> Task.FromResult

                let opening =
                    IncumbencyOpening.next
                        HostDigest.sha256Hex
                        (roadIdOf record)
                        retirement.Id
                        road.AuthorityRevision
                        snapshot

                let! _ = appendRelayResult journal record opening.Transaction
                return opening.IncumbencyId
            }

    let continueLoop (jobId: ManagerJobId) : Task<Result<IncumbencyId, string>> =
        taskResult {
            let! journal, record =
                requireJournalAndJob deps.Journal jobId "Manager loop continuation requires a durable journal"

            let! road = requireOpenRoad journal record
            let! retirement = requireCommittedRetirement road

            let! incumbent =
                match retirement.Outcome, road.Certificate with
                | RetirementOutcome.Continue, _ -> activateNextIfAbsent journal record road retirement
                | RetirementOutcome.Accepted certificateId, Some certificate when
                    certificate.Id = certificateId && not certificate.Valid
                    ->
                    activateNextIfAbsent journal record road retirement
                | RetirementOutcome.Accepted _, _ ->
                    Task.FromResult(Error "Manager loop continuation requires an invalidated Accepted certificate")

            return incumbent
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
        { CreateManagerSession = createManagerSession
          ActivateManager = activateManager
          AwaitLoopSignal = awaitLoopSignal
          InvalidateCertificate = invalidateCertificate
          ContinueLoop = continueLoop
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
                    deps.WaitObserver
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
                    deps.WaitObserver,
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
        match road.ActiveIncumbency with
        | None -> Error "Manager session has no active incumbent"
        | Some active when List.contains active road.RetiredIncumbencies ->
            Error(sprintf "Relay incumbency %s is already retired" (IncumbencyId.value active))
        | Some _ when road.AcceptedAssessmentTransport.IsNone ->
            Error "Relay incumbency cannot take new charge before assessment is recorded"
        | Some _ when road.Certificate |> Option.exists (fun cert -> cert.Valid) ->
            Error "Relay incumbency cannot take new charge after perfect assessment yields valid certificate"
        | Some active -> Ok active

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
                deps.SendGateContinuation
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
                          Wanxiangshu.Mission.Relay.PhysicalUserMessageId.create (
                              Wanxiangshu.Foundation.Identity.PhysicalUserMessageId.value physicalAuthorityMessage
                          ),
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

        CausalAwait.awaitTask deps.WaitObserver descriptor pending

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
