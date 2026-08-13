namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Domain.MagicTodoAfter
open Wanxiangshu.Domain.MagicTodoFacts
open Wanxiangshu.Domain.MagicTodoProcessReview
open Wanxiangshu.Finality
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Resources
open Wanxiangshu.Review
open Wanxiangshu.Process
open Wanxiangshu.Session

/// HOST-021 / TODO-006/008 / REVIEW-013..017: Host-owned dedicated process reviewer.
/// ensureReview is idempotent and may reenter from after / restart / next todowrite / suicide.
module DedicatedTodoReviewerRuntime =

    let private writeKey (writeId: TodoWriteId) = TodoWriteId.value writeId

    let private agentIdOf (dedicatedId: DedicatedReviewerId) =
        "todo-process-reviewer:" + DedicatedReviewerId.value dedicatedId

    let private directoryOf (sessionId: SessionId) =
        match SharedState.SessionDirectories.TryGetValue(SessionId.value sessionId) with
        | true, path -> Some path
        | false, _ -> SharedState.RootWorkspace

    let private reviewerHead (journal: AgentJournal) (reviewerSessionId: SessionId) =
        AgentProjection.tryFind reviewerSessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.XTrace)
        |> Option.map XTraceProjection.head
        |> Option.defaultValue 0L

    /// Same slice as ProviderRecoveryWorkflow.awaitRecoveryMaterial (Delay 2000).
    /// Assignment delivery is a local Journal fold, not reviewer runtime.
    let AssignmentHeadDeadlineMs = 2000

    let private timerPort: ITimerPort = PtyTiming.nodeTimerPort ()

    let private waitHeadAdvanced
        (journal: AgentJournal)
        (reviewerSessionId: SessionId)
        (fromHead: int64)
        : Task<Result<XTraceCursor, string>> =
        task {
            let tryRead () =
                let head = reviewerHead journal reviewerSessionId

                if head > fromHead then Some { Sequence = head } else None

            match tryRead () with
            | Some cursor -> return Ok cursor
            | None ->
                let sessionKey = SessionId.value reviewerSessionId

                let descriptor =
                    DiagnosticWait.create
                        "todo-reviewer-assignment-head"
                        (CausalOwner.create "DedicatedTodoReviewerRuntime" [ "session", sessionKey ])
                        [ "session", sessionKey ]
                        (ExternalProducer("AgentJournal", [ "kind", "assignment-head" ]))
                        [ WaitEscape.ProcessLifetime ]
                        "DedicatedTodoReviewerRuntime.waitHeadAdvanced"

                let deadline = timerPort.Delay AssignmentHeadDeadlineMs

                let awaitSignal () =
                    task {
                        let! _ = AgentJournal.awaitChangeFrom (AgentJournal.revision journal) journal
                        return ()
                    }

                match!
                    CausalAwait.untilSignalOrDeadline CausalWaitHub.observer descriptor deadline tryRead awaitSignal
                with
                | Ok cursor -> return Ok cursor
                | Error DiagnosticWaitExit.WaitTimedOut ->
                    return Error "reviewer XTrace head did not advance after assignment send (REVIEW-018)"
                | Error exit -> return Error("reviewer XTrace head wait failed: " + string exit)
        }

    let private readObligations
        (journal: AgentJournal)
        (blobRef: BlobRef)
        (expected: BlobDigest)
        : Result<ObligationList, string> =
        match journal.Writer.BlobWriter.Read blobRef with
        | Error reason -> Error reason
        | Ok body when HostDigest.sha256Hex body <> BlobDigest.value expected -> Error "obligation blob digest mismatch"
        | Ok body -> MagicTodoObligationCodec.tryDecode body

    let private openingRaw (journal: AgentJournal) (life: LifeProjection) =
        match journal.Writer.BlobWriter.Read life.OpeningTextRef with
        | Ok body when HostDigest.sha256Hex body = BlobDigest.value life.OpeningTextDigest -> body
        | _ -> ""

    let private managerCheckpointLwr
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (life: LifeProjection)
        (reviewFrontier: XTraceCursor)
        =
        let snapshot = AgentJournal.snapshot journal

        let xTrace =
            AgentProjection.tryFind managerSessionId snapshot.AgentProjections
            |> Option.bind (fun session -> session.XTrace)
            |> Option.defaultValue XTraceProjection.empty

        let start =
            ManagerOpeningFloor.workRecordStart
                life
                (MagicTodoProjection.tryLife life.LifeId snapshot.AgentProjections.MagicTodo)
                xTrace
            |> Option.defaultValue life.OpeningCursor

        let range =
            { MagicTodoLwr.BoundedRange.StartInclusive = start
              MagicTodoLwr.BoundedRange.EndExclusive = reviewFrontier }

        LifecycleWorkRecordProjection.lifecycleWorkRecordBounded (Some journal) managerSessionId range
        |> Option.defaultValue ""

    let private treeHash (gitTree: GitTreePort option) (reviewId: TodoReviewId) =
        let fallback =
            GitTreeHash.create (HostDigest.sha256Hex (TodoReviewId.value reviewId))

        match gitTree with
        | None -> fallback
        | Some port ->
            try
                let value = port.GetTreeHash().Trim()

                if String.IsNullOrWhiteSpace value then
                    fallback
                else
                    GitTreeHash.create value
            with _ex ->
                fallback

    let private reviewerAgentName (journal: AgentJournal) (managerSessionId: SessionId) =
        PromptAuthorityLedger.activeProfile managerSessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.map (fun profile -> profile.SelectedTier)
        |> Option.defaultValue AgentTier.Deep
        |> fun tier -> ManagedAgent.nameOf tier Role.Reviewer

    let private forkRuntime
        (sessions: ISessionHostPort)
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        =
        HostForkRuntime(
            managerSessionId,
            sessions,
            ?journal = Some journal,
            onChildCreated =
                (fun _ _ childId ->
                    SharedState.SessionParents.[SessionId.value childId] <- SessionId.value managerSessionId),
            onChildCreatedDir =
                (fun _ childId directory ->
                    directory
                    |> Option.iter (fun path -> SharedState.SessionDirectories.[SessionId.value childId] <- path)),
            directoryFor = (fun _ -> directoryOf managerSessionId),
            ?sessionSnapshot = snapshot,
            managerOpensReviewBarrier = false,
            ownership = HandleOwnership.HostOwnedHidden
        )

    let private currentLife (journal: AgentJournal) (managerSessionId: SessionId) =
        AgentProjection.tryFind managerSessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.ManagerLife)
        |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)

    let private appendAssigned
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (prepared: TodoWritePrepared)
        (enlisted: DedicatedTodoReviewerEnlisted)
        (reviewWorkStart: XTraceCursor)
        =
        let assigned =
            MagicTodoAfter.planEnsureReview HostDigest.sha256Hex prepared enlisted reviewWorkStart

        AgentJournal.appendMagicTodo
            (StreamId.Session managerSessionId)
            None
            (MagicTodoFact.TodoProcessReviewAssigned assigned)
            journal
        |> Result.mapError JournalAppendFailure.describe
        |> Result.map ignore

    let ensureReview
        (sessions: ISessionHostPort)
        (snapshot: ISessionSnapshotPort option)
        (gitTree: GitTreePort option)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        : Task<Result<unit, string>> =
        task {
            let projection = AgentJournal.snapshot journal

            match MagicTodoProjection.tryLife lifeId projection.AgentProjections.MagicTodo with
            | None -> return Error "Magic Todo life is missing"
            | Some life ->
                match Map.tryFind (writeKey writeId) life.Checkpoints with
                | None -> return Error "TodoWrite checkpoint is missing"
                | Some { Concluded = Some _ } -> return Ok()
                | Some checkpoint when not checkpoint.Accepted -> return Error "TodoWrite is not Accepted"
                | Some checkpoint ->
                    match currentLife journal managerSessionId with
                    | None -> return Error "open Manager Life is missing"
                    | Some managerLife when managerLife.LifeId <> lifeId ->
                        return Error "Manager Life does not match TodoWrite"
                    | Some managerLife ->
                        let dedicatedId = MagicTodo.dedicatedReviewerId HostDigest.sha256Hex lifeId
                        let handleId = agentIdOf dedicatedId
                        let runtime = forkRuntime sessions snapshot journal managerSessionId
                        let agentName = reviewerAgentName journal managerSessionId

                        let! enlistedResult =
                            task {
                                match life.Dedicated with
                                | Some dedicated ->
                                    runtime.AdoptChild(handleId, dedicated.ReviewerSessionId)

                                    return
                                        Ok
                                            { ManagerLifeId = lifeId
                                              DedicatedReviewerId = dedicated.DedicatedReviewerId
                                              ReviewerSessionId = dedicated.ReviewerSessionId }
                                | None ->
                                    match!
                                        runtime.Fork(
                                            handleId,
                                            Role.Reviewer,
                                            agentName,
                                            ProviderProse.render
                                                (ProviderProse.languageOf managerSessionId)
                                                MagicTodoSurface.Path.ProcessReviewerPreamble
                                                Map.empty,
                                            None,
                                            firstPrompt = true,
                                            ownership = HandleOwnership.HostOwnedHidden,
                                            deferSend = true
                                        )
                                    with
                                    | Error reason -> return Error reason
                                    | Ok _ ->
                                        match runtime.TryChildSession handleId with
                                        | None -> return Error "dedicated process reviewer session was not created"
                                        | Some childId ->
                                            let enlisted =
                                                { ManagerLifeId = lifeId
                                                  DedicatedReviewerId = dedicatedId
                                                  ReviewerSessionId = childId }

                                            match
                                                AgentJournal.appendMagicTodo
                                                    (StreamId.Session managerSessionId)
                                                    None
                                                    (MagicTodoFact.DedicatedTodoReviewerEnlisted enlisted)
                                                    journal
                                            with
                                            | Error failure -> return Error(JournalAppendFailure.describe failure)
                                            | Ok _ -> return Ok enlisted
                            }

                        match enlistedResult with
                        | Error reason -> return Error reason
                        | Ok enlisted ->
                            match checkpoint.Assignment with
                            | Some _ ->
                                match TodoProcessReviewProgram.tryConclude journal lifeId writeId with
                                | TodoProcessReviewProgram.ConcludeOutcome.Failed reason -> return Error reason
                                | _ -> return Ok()
                            | None ->
                                let prepared: TodoWritePrepared =
                                    { ManagerSessionId = managerSessionId
                                      ManagerLifeId = lifeId
                                      TodoWriteId = writeId
                                      ToolCallId = checkpoint.ToolCallId
                                      ToolPartOrdinal = checkpoint.ToolPartOrdinal
                                      BaseTodoRef = checkpoint.BaseTodoRef
                                      BaseTodoDigest = checkpoint.BaseTodoDigest
                                      ProposedTodoRef = checkpoint.ProposedTodoRef
                                      ProposedTodoDigest = checkpoint.ProposedTodoDigest
                                      ProviderInputDigest = checkpoint.ProviderInputDigest
                                      ReviewFrontier = checkpoint.ReviewFrontier
                                      SemanticVersion = checkpoint.SemanticVersion }

                                let reviewId = MagicTodo.todoReviewId HostDigest.sha256Hex lifeId writeId

                                match
                                    ReviewBarrier.openBarrier
                                        (Some journal)
                                        managerSessionId
                                        enlisted.ReviewerSessionId
                                        (ReviewBarrierId.create (TodoReviewId.value reviewId))
                                        (treeHash gitTree reviewId)
                                with
                                | Error reason -> return Error reason
                                | Ok() ->
                                    match
                                        readObligations journal checkpoint.BaseTodoRef checkpoint.BaseTodoDigest,
                                        readObligations journal checkpoint.ProposedTodoRef checkpoint.ProposedTodoDigest
                                    with
                                    | Error reason, _
                                    | _, Error reason -> return Error reason
                                    | Ok oldItems, Ok proposed ->
                                        let request: ProcessReviewRequest =
                                            { TodoReviewId = reviewId
                                              TodoWriteId = writeId
                                              ManagerLifeId = lifeId
                                              OpeningRaw = openingRaw journal managerLife
                                              ManagerCheckpointLwr =
                                                managerCheckpointLwr
                                                    journal
                                                    managerSessionId
                                                    managerLife
                                                    checkpoint.ReviewFrontier
                                              OldTodo = oldItems
                                              ProposedTodo = proposed }

                                        let preamble =
                                            ProviderProse.render
                                                (ProviderProse.languageOf managerSessionId)
                                                MagicTodoSurface.Path.ProcessReviewerPreamble
                                                Map.empty

                                        let assignmentText =
                                            MagicTodoProcessReview.renderAssignmentUserMessage preamble request

                                        let beforeHead = reviewerHead journal enlisted.ReviewerSessionId

                                        let hasActiveProfile =
                                            PromptAuthorityLedger.activeProfile
                                                enlisted.ReviewerSessionId
                                                (AgentJournal.snapshot journal).AgentProjections
                                            |> Option.isSome

                                        let isFirstAcceptedWrite =
                                            match life.AcceptedOrder with
                                            | [ only ] when only = writeId -> true
                                            | _ -> false

                                        let delivery =
                                            MagicTodoAfter.assignmentDelivery hasActiveProfile isFirstAcceptedWrite

                                        let assignmentDirectory = directoryOf enlisted.ReviewerSessionId

                                        let! sent =
                                            match delivery with
                                            | MagicTodoAfter.AssignmentDelivery.OwnerRoot ->
                                                runtime.DiscardDeferredFirstPrompt handleId

                                                task {
                                                    match!
                                                        HostForkAgentOwner.sendFirstPrompt
                                                            sessions
                                                            (Some journal)
                                                            enlisted.ReviewerSessionId
                                                            agentName
                                                            assignmentDirectory
                                                            assignmentText
                                                    with
                                                    | Ok _ -> return Ok()
                                                    | Error reason -> return Error reason
                                                }
                                            | MagicTodoAfter.AssignmentDelivery.Continuation ->
                                                task {
                                                    match!
                                                        HostSessionNudge.sendContinuation
                                                            sessions
                                                            enlisted.ReviewerSessionId
                                                            assignmentText
                                                            PromptAuthority.ContinuationKind.ReviewerGuard
                                                            assignmentDirectory
                                                            (Some journal)
                                                    with
                                                    | Ok _ -> return Ok()
                                                    | Error reason -> return Error reason
                                                }
                                            | MagicTodoAfter.AssignmentDelivery.AwaitHead -> Task.FromResult(Ok())

                                        match sent with
                                        | Error reason -> return Error reason
                                        | Ok() ->
                                            let! reviewWorkStart =
                                                match delivery with
                                                | MagicTodoAfter.AssignmentDelivery.AwaitHead when beforeHead > 0L ->
                                                    Task.FromResult(Ok { Sequence = beforeHead })
                                                | _ -> waitHeadAdvanced journal enlisted.ReviewerSessionId beforeHead

                                            match reviewWorkStart with
                                            | Error reason -> return Error reason
                                            | Ok reviewWorkStart ->
                                                match
                                                    appendAssigned
                                                        journal
                                                        managerSessionId
                                                        prepared
                                                        enlisted
                                                        reviewWorkStart
                                                with
                                                | Error reason -> return Error reason
                                                | Ok() ->
                                                    match
                                                        TodoProcessReviewProgram.tryConclude journal lifeId writeId
                                                    with
                                                    | TodoProcessReviewProgram.ConcludeOutcome.Failed reason ->
                                                        return Error reason
                                                    | _ -> return Ok()
        }

    let awaitConsumableReview
        (sessions: ISessionHostPort)
        (snapshot: ISessionSnapshotPort option)
        (gitTree: GitTreePort option)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        : Task<Result<unit, string>> =
        task {
            match! ensureReview sessions snapshot gitTree journal managerSessionId lifeId writeId with
            | Error reason -> return Error reason
            | Ok() ->
                match TodoProcessReviewProgram.tryConclude journal lifeId writeId with
                | TodoProcessReviewProgram.ConcludeOutcome.Concluded -> return Ok()
                | TodoProcessReviewProgram.ConcludeOutcome.Failed reason -> return Error reason
                | TodoProcessReviewProgram.ConcludeOutcome.Pending _ ->
                    match! TodoProcessReviewProgram.awaitConsumableReview journal lifeId writeId with
                    | Ok() -> return Ok()
                    | Error reason -> return Error reason
        }

    let port
        (sessions: ISessionHostPort)
        (snapshot: ISessionSnapshotPort option)
        (gitTree: GitTreePort option)
        : ProcessReviewPort =
        { EnsureReview = ensureReview sessions snapshot gitTree
          AwaitConsumableReview = awaitConsumableReview sessions snapshot gitTree }
