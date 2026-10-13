namespace Wanxiangshu.Mission.Lifecycle

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

[<RequireQualifiedAccess>]
type CommitOutcome =
    | AppendDurable
    | AppendNotAttempted
    | CommitUnknown

[<RequireQualifiedAccess>]
type KernelDecision =
    | Refuse of string
    | Write of LifecycleEvent list * string
    | Done of string

type HostFence =
    { ProviderRunId: string option
      ToolCallId: string option }

type KnownSession =
    { ExecSessionId: string
      WorkId: string option }

[<RequireQualifiedAccess>]
type SessionLookup =
    | Unknown
    | None
    | One of KnownSession
    | Many

type DeliverDraft =
    { Missing: bool
      Empty: bool
      Digest: string
      Path: string }

[<RequireQualifiedAccess>]
module LifecycleKernel =

    let classifyCommit (result: Result<unit, AppendError>) : Result<CommitOutcome, string> =
        match result with
        | Ok() -> Ok CommitOutcome.AppendDurable
        | Error(AppendError.AppendNotAttempted _) -> Ok CommitOutcome.AppendNotAttempted
        | Error(AppendError.CommitUnknown _) -> Ok CommitOutcome.CommitUnknown
        | Error error -> Error(AppendError.describe error)

    let private envelope (workId: string) (event: LifecycleEvent) (parents: EventId list) =
        EventEnvelope.normalize
            { EventId = EventId.create (Guid.NewGuid().ToString("N"))
              StreamId = EventStreamId.create ("lifecycle/" + workId)
              EventType = LifecycleCodec.eventTypeOf event
              Parents = parents
              Payload = LifecycleCodec.encodePayload event
              PayloadRefs = []
              Payloads = Map.empty }

    let commit (store: IEventStore) (workId: string) (events: LifecycleEvent list) : Task<Result<CommitOutcome, string>> =
        if List.isEmpty events then
            Task.FromResult(Error "append requires at least one envelope")
        else
            task {
                let streamId = EventStreamId.create ("lifecycle/" + workId)
                let parents = store.TryHead streamId |> Option.toList
                let envelopes = events |> List.map (fun event -> envelope workId event parents)
                let! appended = store.Append envelopes
                return classifyCommit (appended |> Result.map ignore)
            }

    /// The unified lifecycle work already folded into a held store. A missing
    /// current or a missing work is None; the caller keeps the store for commit.
    let tryWorkOfStore (store: IEventStore) (workId: string) : LifecycleWork option =
        store.TryCurrent "Lifecycle"
        |> Option.bind (fun current ->
            let works = unbox<Map<string, LifecycleWork>> current
            Map.tryFind workId works)

    /// Every unified lifecycle work already folded into a held store.
    let tryWorksOfStore (store: IEventStore) : Map<string, LifecycleWork> option =
        store.TryCurrent "Lifecycle"
        |> Option.map (fun current -> unbox<Map<string, LifecycleWork>> current)

    let private opened (workId: string) (root: string) (sequence: WorkSequence) (sha256: string -> string) (head: int64) =
        let authority = LifecycleIdentity.authorityInitial sha256 workId
        let incumbencyId = LifecycleIdentity.firstIncumbencyId sha256 workId
        let first = WorkSequence.elements sequence |> List.head
        [ LifecycleEvent.WorkOpened(workId, root, sequence, authority)
          LifecycleEvent.IncumbencyOpened(workId, incumbencyId, first, head) ]

    let openWork
        (sha256: string -> string)
        (kind: OpeningKind)
        (firstPromptLine: string option)
        (workId: string)
        (root: string)
        (alreadyOpened: bool)
        (observedHead: int64 option)
        : KernelDecision =
        let marked = firstPromptLine = Some LifecycleIdentity.spawnMark
        if alreadyOpened then
            KernelDecision.Refuse "second WorkOpened"
        elif kind = OpeningKind.SpawnedExecution && not marked then
            KernelDecision.Refuse "spawn mark missing"
        elif marked && kind <> OpeningKind.SpawnedExecution then
            KernelDecision.Refuse "spawn mark is not a manager opening"
        elif String.IsNullOrEmpty workId then
            KernelDecision.Refuse "workId is empty"
        else
            let sequence =
                match kind with
                | OpeningKind.UserPlan -> Some WorkSequence.Plan
                | OpeningKind.ManagerDirect
                | OpeningKind.SpawnedExecution -> Some WorkSequence.Execution

            match sequence, observedHead with
            | None, _ -> KernelDecision.Refuse "opening kind is not a sequence"
            | Some seq, _ when (WorkSequence.elements seq |> List.length) <> 1 && (WorkSequence.elements seq |> List.length) <> 3 ->
                KernelDecision.Refuse "sequence length is neither 1 nor 3"
            | Some _, None -> KernelDecision.Refuse "opening cursor was not observed"
            | Some seq, Some head -> KernelDecision.Write(opened workId root seq sha256 head, "opened")

    let bindDevOps (work: LifecycleWork) (trigger: BindTrigger) (devopsId: string) (target: string option) : KernelDecision =
        let allowed =
            match work.Sequence, trigger with
            | WorkSequence.Execution, BindTrigger.Resume
            | WorkSequence.Execution, BindTrigger.Horizon
            | WorkSequence.Plan, BindTrigger.Ask -> true
            | _ -> false

        if not allowed then
            KernelDecision.Refuse "bind trigger does not match the sequence"
        elif work.DevOps.IsSome then
            KernelDecision.Done "already bound"
        else
            KernelDecision.Write([ LifecycleEvent.DevOpsBound(work.WorkId, devopsId, target) ], "bound")

    let review (work: LifecycleWork) (incumbencyId: string) (outcome: ReviewOutcome) : KernelDecision =
        match LifecycleFold.projection work, work.Active with
        | Error error, _ -> KernelDecision.Refuse error
        | _, None -> KernelDecision.Refuse "no active incumbency"
        | _, Some active when active.Id <> incumbencyId -> KernelDecision.Refuse "review incumbency mismatch"
        | _, Some active when active.Review.IsSome -> KernelDecision.Refuse "incumbency already reviewed"
        | Ok(stage, _), Some active ->
            KernelDecision.Write([ LifecycleEvent.Reviewed(active.Id, stage, outcome) ], "reviewed")

    let private fenceHit (work: LifecycleWork) (fence: HostFence) =
        match fence.ProviderRunId, fence.ToolCallId with
        | Some providerRunId, Some toolCallId ->
            work.Retirements
            |> List.tryFind (fun record ->
                match record.ProjectionCut with
                | Some cut -> cut.ProviderRunId = providerRunId && cut.ToolCallId = toolCallId
                | None -> false)
        | _ -> None

    let private oneshotHit (work: LifecycleWork) (fence: HostFence) =
        match fence.ProviderRunId, fence.ToolCallId with
        | Some providerRunId, Some toolCallId ->
            work.Retirements
            |> List.tryFind (fun record ->
                record.ProjectionCut.IsNone
                && record.ProviderRunId = Some providerRunId
                && record.ToolCallId = Some toolCallId)
        | _ -> None

    let replayOneshot
        (sha256: string -> string)
        (work: LifecycleWork)
        (fence: HostFence)
        (record: RetirementRecord)
        (sessions: SessionLookup)
        =
        match fence.ProviderRunId, fence.ToolCallId, record.Outcome with
        | Some providerRunId, Some toolCallId, outcome ->
            let retirementId =
                LifecycleIdentity.oneshotRetirementId
                    sha256
                    work.WorkId
                    providerRunId
                    toolCallId
                    (RetirementOutcome.render outcome)
                    work.Authority

            if retirementId <> record.RetirementId || record.ExpectedAdvance <> work.Authority then
                KernelDecision.Refuse "RetirementReplayConflict"
            elif outcome = RetirementOutcome.Delivered then
                match sessions, record.ExecSessionId, record.Path with
                | SessionLookup.One known, Some stored, Some path
                    when known.ExecSessionId = stored && known.WorkId = Some work.WorkId && path <> "" ->
                    KernelDecision.Done "idempotent"
                | _ -> KernelDecision.Refuse "RetirementReplayConflict"
            else
                KernelDecision.Done "idempotent"
        | _ -> KernelDecision.Refuse "RetirementReplayConflict"

    let private successor (sha256: string -> string) (work: LifecycleWork) (retirementId: string) (cursor: int64) =
        match LifecycleFold.projection work with
        | Error error -> Error error
        | Ok(stage, _) ->
            Ok(
                LifecycleEvent.IncumbencyOpened(
                    work.WorkId,
                    LifecycleIdentity.successorIncumbencyId sha256 retirementId,
                    stage,
                    cursor
                )
            )

    let private oneshotRetired
        (sha256: string -> string)
        (work: LifecycleWork)
        (active: ActiveIncumbency)
        (fence: HostFence)
        (outcome: RetirementOutcome)
        (cursor: int64)
        (execSessionId: string option)
        (digest: string option)
        (path: string option)
        =
        match fence.ProviderRunId, fence.ToolCallId with
        | Some providerRunId, Some toolCallId when work.Authority <> "" ->
            let retirementId =
                LifecycleIdentity.oneshotRetirementId
                    sha256
                    work.WorkId
                    providerRunId
                    toolCallId
                    (RetirementOutcome.render outcome)
                    work.Authority

            let retired =
                LifecycleEvent.IncumbencyRetiredOneshot
                    { RetirementId = retirementId
                      IncumbencyId = active.Id
                      Outcome = outcome
                      ProviderRunId = providerRunId
                      ToolCallId = toolCallId
                      ExpectedAdvance = work.Authority
                      RetirementCursor = cursor
                      ExecSessionId = execSessionId
                      Digest = digest
                      Path = path }

            if outcome = RetirementOutcome.Relayed then
                successor sha256 work retirementId cursor
                |> Result.map (fun opened -> [ retired; opened ])
            else
                Ok [ retired ]
        | _ -> Error "retirement fence or expected advance is missing"

    let deliverOneshot
        (sha256: string -> string)
        (work: LifecycleWork)
        (fence: HostFence)
        (observedHead: int64 option)
        (blockerDigest: string option)
        (sessions: SessionLookup)
        (draft: DeliverDraft option)
        (startedSessionId: string option)
        : KernelDecision =
        if work.Sequence <> WorkSequence.Plan then
            KernelDecision.Refuse "oneshot deliver requires S1 S2 S3"
        elif work.Authority = "" then
            KernelDecision.Refuse "authority count is missing"
        else
            match oneshotHit work fence with
            | Some record -> replayOneshot sha256 work fence record sessions
            | None when work.Active.IsNone -> KernelDecision.Refuse "no active incumbency"
            | None ->
                match work.Active, observedHead with
                | Some active, None -> KernelDecision.Refuse "retirement cursor was not observed"
                | Some active, Some cursor ->
                    match blockerDigest with
                    | Some digest ->
                        match sessions with
                        | SessionLookup.Unknown -> KernelDecision.Refuse "execution session lookup failed"
                        | SessionLookup.Many -> KernelDecision.Refuse "more than one execution session"
                        | SessionLookup.One _ -> KernelDecision.Refuse "execution session already exists"
                        | SessionLookup.None ->
                            KernelDecision.Write(
                                [ LifecycleEvent.RetirementBlocked(active.Id, digest) ],
                                "blocked"
                            )
                    | None ->
                        match LifecycleFold.projection work with
                        | Error error -> KernelDecision.Refuse error
                        | Ok(_, true) ->
                            match draft with
                            | None
                            | Some { Missing = true }
                            | Some { Empty = true } -> KernelDecision.Refuse "draft is missing or empty"
                            | Some loaded ->
                                match sessions, startedSessionId with
                                | SessionLookup.Unknown, _ -> KernelDecision.Refuse "execution session lookup failed"
                                | SessionLookup.Many, _ -> KernelDecision.Refuse "more than one execution session"
                                | SessionLookup.One known, _ when known.WorkId <> Some work.WorkId ->
                                    KernelDecision.Refuse "execution session belongs to another work"
                                | SessionLookup.One known, _ when known.WorkId.IsNone ->
                                    KernelDecision.Refuse "execution session has no work id"
                                | SessionLookup.None, None -> KernelDecision.Refuse "execution session was not started"
                                | SessionLookup.None, Some execSessionId
                                | SessionLookup.One { ExecSessionId = execSessionId }, _ ->
                                    match
                                        oneshotRetired
                                            sha256
                                            work
                                            active
                                            fence
                                            RetirementOutcome.Delivered
                                            cursor
                                            (Some execSessionId)
                                            (Some loaded.Digest)
                                            (Some loaded.Path)
                                    with
                                    | Error error -> KernelDecision.Refuse error
                                    | Ok events -> KernelDecision.Write(events, "delivered")
                        | Ok(_, false) ->
                            match
                                oneshotRetired
                                    sha256
                                    work
                                    active
                                    fence
                                    RetirementOutcome.Relayed
                                    cursor
                                    None
                                    None
                                    None
                            with
                            | Error error -> KernelDecision.Refuse error
                            | Ok events -> KernelDecision.Write(events, "relayed")
                | None, _ -> KernelDecision.Refuse "no active incumbency"

    let deliverExecution
        (sha256: string -> string)
        (work: LifecycleWork)
        (fence: HostFence)
        (observedHead: int64 option)
        (blockerDigest: string option)
        : KernelDecision =
        if work.Sequence <> WorkSequence.Execution then
            KernelDecision.Refuse "execution deliver requires S4"
        elif work.Authority = "" then
            KernelDecision.Refuse "authority count is missing"
        else
            match fenceHit work fence with
            | Some record ->
                match fence.ProviderRunId, fence.ToolCallId, record.Outcome with
                | Some providerRunId, Some toolCallId, outcome ->
                    let retirementId =
                        LifecycleIdentity.executionRetirementId
                            sha256
                            work.WorkId
                            providerRunId
                            toolCallId
                            (RetirementOutcome.render outcome)
                            work.Authority

                    if retirementId = record.RetirementId && record.ExpectedAdvance = work.Authority then
                        KernelDecision.Done "idempotent"
                    else
                        KernelDecision.Refuse "RetirementReplayConflict"
                | _ -> KernelDecision.Refuse "RetirementReplayConflict"
            | None when work.Active.IsNone -> KernelDecision.Refuse "no active incumbency"
            | None ->
                match work.Active with
                | None -> KernelDecision.Refuse "no active incumbency"
                | Some active when active.Review.IsNone && active.Confirmed.IsSome ->
                    KernelDecision.Refuse "confirmation precedes review"
                | Some active when active.Review.IsNone -> KernelDecision.Done "assessment required"
                | Some active when active.Confirmed.IsNone ->
                    match fence.ProviderRunId, fence.ToolCallId with
                    | Some providerRunId, Some toolCallId ->
                        KernelDecision.Write(
                            [ LifecycleEvent.ConfirmationCommitted(active.Id, providerRunId, toolCallId) ],
                            "confirmed"
                        )
                    | _ -> KernelDecision.Refuse "confirmation fence is missing"
                | Some active ->
                    match active.Confirmed, fence.ProviderRunId, fence.ToolCallId, observedHead with
                    | Some(confirmedRun, confirmedCall), Some providerRunId, Some toolCallId, _ when
                        toolCallId = confirmedCall && providerRunId = confirmedRun
                        ->
                        KernelDecision.Done "idempotent confirmation"
                    | Some(confirmedRun, confirmedCall), Some providerRunId, Some toolCallId, _ when
                        toolCallId = confirmedCall && providerRunId <> confirmedRun
                        ->
                        KernelDecision.Refuse "RetirementConfirmationReplayConflict"
                    | Some(_, confirmedCall), Some _, Some toolCallId, None when toolCallId <> confirmedCall ->
                        KernelDecision.Refuse "retirement cursor was not observed"
                    | Some(_, confirmedCall), Some providerRunId, Some toolCallId, Some cursor when
                        toolCallId <> confirmedCall
                        ->
                        match blockerDigest with
                        | Some digest ->
                            KernelDecision.Write(
                                [ LifecycleEvent.RetirementBlocked(active.Id, digest) ],
                                "blocked"
                            )
                        | None ->
                            match active.Review with
                            | None -> KernelDecision.Refuse "review is missing"
                            | Some review ->
                                let outcome =
                                    match review.Outcome with
                                    | ReviewOutcome.Promoted -> RetirementOutcome.Delivered
                                    | ReviewOutcome.Stayed -> RetirementOutcome.Relayed

                                let retirementId =
                                    LifecycleIdentity.executionRetirementId
                                        sha256
                                        work.WorkId
                                        providerRunId
                                        toolCallId
                                        (RetirementOutcome.render outcome)
                                        work.Authority

                                let retired =
                                    LifecycleEvent.IncumbencyRetiredExecution
                                        { RetirementId = retirementId
                                          IncumbencyId = active.Id
                                          Outcome = outcome
                                          ProjectionCut =
                                            { ProviderRunId = providerRunId
                                              ToolCallId = toolCallId }
                                          ExpectedAdvance = work.Authority }

                                if outcome = RetirementOutcome.Delivered then
                                    KernelDecision.Write([ retired ], "delivered")
                                else
                                    match successor sha256 work retirementId cursor with
                                    | Error error -> KernelDecision.Refuse error
                                    | Ok opened -> KernelDecision.Write([ retired; opened ], "relayed")
                    | _ -> KernelDecision.Refuse "second fence is incomplete"

    let reopen (work: LifecycleWork) (incumbencyId: string) : KernelDecision =
        if work.Sequence = WorkSequence.Execution then
            KernelDecision.Refuse "S4 cannot reopen"
        elif work.Active.IsSome then
            KernelDecision.Refuse "active tenure blocks reopen"
        else
            match work.DeliveredRecord with
            | Some record when record.Outcome = RetirementOutcome.Delivered && record.IncumbencyId = incumbencyId && record.Stage.IsSome ->
                KernelDecision.Write([ LifecycleEvent.Reopened incumbencyId ], "reopened")
            | Some record when record.Outcome = RetirementOutcome.Relayed ->
                KernelDecision.Refuse "relayed retirement cannot reopen"
            | Some _ -> KernelDecision.Refuse "reopen authorization cannot be read"
            | None -> KernelDecision.Refuse "no delivered retirement"

    let advance (work: LifecycleWork) (expected: string) (next: string) : KernelDecision =
        match work.Active with
        | None -> KernelDecision.Refuse "no active incumbency"
        | Some active when active.Review.IsNone -> KernelDecision.Refuse "advance without review"
        | Some _ when expected <> work.Authority -> KernelDecision.Refuse "advance expected mismatch"
        | Some _ when next = "" || next = expected -> KernelDecision.Refuse "advance does not move"
        | Some _ when List.contains next work.AdvanceHistory -> KernelDecision.Refuse "advance value already used"
        | Some active ->
            KernelDecision.Write([ LifecycleEvent.AdvanceCommitted(active.Id, expected, next) ], "advanced")

    let refuseDetachedOpening (_work: LifecycleWork) : KernelDecision =
        KernelDecision.Refuse "no legal incumbency opening"

    let spawnPrompt (draft: string) =
        LifecycleIdentity.spawnMark + "\n" + draft

    let readRecovery (text: string) (draftExists: bool) : Result<RecoveryPosition * RecoveryNext, ReadFault> =
        ignore draftExists
        match LifecycleCodec.readDocument text with
        | Error(ReadFault.PairIncomplete reason) ->
            Ok(RecoveryPosition.PairIncomplete, RecoveryNext.StopPairIncomplete)
        | Error fault -> Error fault
        | Ok events ->
            match LifecycleFold.applyAll events with
            | Error _ -> Ok(RecoveryPosition.Rejected, RecoveryNext.StopRejected)
            | Ok work -> LifecycleFold.recover work |> Result.mapError ReadFault.Rejected
