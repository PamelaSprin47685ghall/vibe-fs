namespace Wanxiangshu.Mission.Work.OpenCode

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Git
open Wanxiangshu.Host
open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.Mission.Work
open Wanxiangshu.OpenCode
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Context.Trace
open Wanxiangshu.Persistence.Journal

module DeliverTool =
    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/deliver/description"

        [<Literal>]
        let Blocked = "tool/deliver/blocked"

        [<Literal>]
        let AssessmentRequired = "tool/deliver/assessment-required"

        [<Literal>]
        let Retired = "tool/deliver/finished"

        [<Literal>]
        let Confirmation = "tool/deliver/confirmation"

        [<Literal>]
        let NonManagerRole = "tool/deliver/non-manager-role"

        [<Literal>]
        let NoAuthority = "tool/deliver/no-authority"

        [<Literal>]
        let BindingUnavailable = "tool/deliver/binding-unavailable"

        [<Literal>]
        let FinishFailed = "tool/deliver/finish-failed"

    let private text (path: string) =
        ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) path Map.empty

    /// The retirement result tells the model whether this call handed the work
    /// over to a successor or ended it. The instruction alone cannot tell the
    /// two apart, so the recorded outcome travels with it.
    let private retirementResult (outcome: string) =
        ToolHostCodec.tomlObjectWithInstructions
            [ text Path.Retired ]
            [ "outcome", ToolHostCodec.TString outcome ]

    /// The first call of the two-stage sequence records the confirmation and
    /// does not finish the work; a second deliver ends it.
    let private confirmationResult () =
        ToolHostCodec.tomlObjectWithInstructions
            [ text Path.Confirmation ]
            [ "finished", ToolHostCodec.TBool false
              "confirmation_required", ToolHostCodec.TBool true ]

    /// The one lifecycle work this session owns. Lifecycle storage is the
    /// wanxiangli journal, independent of the workspace EventStore.
    let private lifecycleWorkOf
        (directory: string)
        (sessionId: string)
        : Task<Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option> =
        task {
            let! commonDir = RuntimePath.stateRoot directory

            if String.IsNullOrWhiteSpace commonDir then
                return None
            else
                return Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected commonDir sessionId
        }

    /// Algorithm E1.1: the fence (ProviderRunId, ToolCallId) is looked up on the
    /// retirements the fold already committed, before the entry reads an active
    /// tenure or a review position. A retried call must return the state that
    /// call recorded: a delivered work has no active tenure left, and a relayed
    /// one leaves a successor that is not yet reviewed. The recorded outcome
    /// name is already the closed result vocabulary, so the expected advance the
    /// entry read is the one field left to compare; a different one is the same
    /// refusal the second call gets. This path writes nothing.
    let recordedFenceReplay
        (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork)
        (providerRunId: string)
        (toolCallId: string)
        : Result<string, string> option =
        let sameFence (retirement: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.HostRetirement) =
            retirement.ProjectionCut
            && retirement.Fence.ProviderRunId = providerRunId
            && retirement.Fence.ToolCallId = toolCallId

        let recordedState (retirement: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.HostRetirement) =
            if retirement.Expected = work.Authority then
                Ok(retirementResult retirement.Outcome)
            else
                Error "RetirementReplayConflict"

        work.Retirements |> List.tryFind sameFence |> Option.map recordedState

    let private blockedResult blockers =
        ToolHostCodec.tomlObjectWithInstructions
            [ text Path.Blocked ]
            [ "finished", ToolHostCodec.TBool false
              "blocker_count", ToolHostCodec.TInt(List.length blockers)
              "blockers",
              ToolHostCodec.TTable(
                  blockers
                  |> List.mapi (fun index blocker -> string index, ToolHostCodec.TString blocker)
              ) ]

    let private assessmentRequiredResult () =
        ToolHostCodec.tomlObjectWithInstructions
            [ text Path.AssessmentRequired ]
            [ "finished", ToolHostCodec.TBool false
              "assessment_required", ToolHostCodec.TBool true ]

    /// The second deliver writes the retirement cursor from the XTrace head
    /// observed on the Host snapshot. A head already on the journal is used
    /// as-is; otherwise the snapshot messages are captured through the real
    /// XTrace path and the receipt's current head is read. The capture is
    /// append-only and idempotent, so a message already observed adds nothing.
    /// A missing snapshot is not a guessed cursor: None leaves the kernel
    /// refusal unchanged.
    let private captureObservedCursor
        (journal: AgentJournal)
        (sessionId: SessionId)
        (snapshotPort: ISessionSnapshotPort)
        : Task<int64 option> =
        task {
            let! messagesResult = snapshotPort.GetMessages sessionId

            match messagesResult with
            | Error _ -> return None
            | Ok messages ->
                let! captured = XTraceCapture.captureSessionMessagesWithReceipt (Some journal) sessionId messages

                return
                    captured
                    |> Result.toOption
                    |> Option.map (fun receipt -> XTraceCursor.sequence receipt.CurrentHead)
        }

    let private observedRetirementCursor (scope: ToolRuntimeScope) (context: HostToolContext) (journal: AgentJournal) =
        let headOf projections =
            AgentProjection.tryFind (SessionId.create context.SessionId) projections
            |> Option.bind (fun session -> session.XTrace)
            |> Option.map (XTraceProjection.headCursor >> XTraceCursor.sequence)

        let current = headOf (AgentJournal.snapshot journal).AgentProjections

        match current, scope.Snapshot with
        | Some cursor, _ -> Task.FromResult(Some cursor)
        | None, None -> Task.FromResult None
        | None, Some snapshotPort -> captureObservedCursor journal (SessionId.create context.SessionId) snapshotPort

    type private BoundRetirement =
        { ToolCallId: ToolCallId
          ProviderRun: ProviderRunIdentity
          PhysicalUserMessageId: PhysicalUserMessageId
          Directory: string
          Journal: AgentJournal }

    type private PreparedRetirement =
        { Bound: BoundRetirement
          SessionId: SessionId
          Incumbent: string
          Reviewed: bool }

    /// Algorithm E1.1 and E2: the entry either replays a fence the fold already
    /// settled, or binds the active tenure. The replay decision belongs to the
    /// fold, so no earlier layer answers a retried call with a refusal for the
    /// tenure the retirement removed.
    type private DeliverEntry =
        | Replayed of string
        | Prepared of PreparedRetirement

    let private bindInvocation (scope: ToolRuntimeScope) (context: HostToolContext) =
        match
            scope.RoleFor context,
            context.ToolCallId,
            context.ProviderRunId,
            scope.CurrentPhysicalUserMessage context.SessionId,
            scope.WorkspaceDirectory,
            scope.Journal
        with
        | Some Role.Manager, Some toolCallId, Some providerRun, Some physical, Some directory, Some journal when
            not (String.IsNullOrWhiteSpace context.SessionId)
            ->
            Ok
                { ToolCallId = toolCallId
                  ProviderRun = providerRun
                  PhysicalUserMessageId = PhysicalUserMessageId.create physical
                  Directory = directory
                  Journal = journal }
        | Some Role.Manager, _, _, _, _, _ -> Error(text Path.BindingUnavailable)
        | Some _, _, _, _, _, _ -> Error(text Path.NonManagerRole)
        | None, _, _, _, _, _ -> Error(text Path.NoAuthority)

    /// Algorithm E1: the caller enters the two-stage sequence only for the
    let private enterDeliver (context: HostToolContext) (bound: BoundRetirement) : Task<Result<DeliverEntry, string>> =
        task {
            let fenceReplay (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork) =
                recordedFenceReplay
                    work
                    (ProviderRunIdentity.value bound.ProviderRun)
                    (ToolCallId.value bound.ToolCallId)
                |> Option.map (Result.map Replayed)

            /// A confirmation this call already recorded is a durable fact.
            /// The tenure it confirmed may since have been relayed or
            /// delivered, so the confirmation is replayed from the recorded
            /// fence alone: a later state of the work never turns a recorded
            /// confirmation into a refusal. The committed confirmation fence
            /// names the run and call that confirmed; this call replays that
            /// exact confirmation to its recorded state.
            let confirmationReplay (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork) =
                work.Confirmation
                |> Option.filter (fun fence ->
                    fence.ProviderRunId = ProviderRunIdentity.value bound.ProviderRun
                    && fence.ToolCallId = ToolCallId.value bound.ToolCallId)
                |> Option.map (fun _ -> Ok(Replayed(confirmationResult ())))

            let activeEntry (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork) =
                work.ActiveId
                |> Option.map (fun active ->
                    Ok(
                        Prepared
                            { Bound = bound
                              SessionId = SessionId.create context.SessionId
                              Incumbent = active
                              Reviewed = work.Reviewed }
                    ))
                |> Option.defaultValue (Error(text Path.BindingUnavailable))

            let openedEntry (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork) =
                match fenceReplay work with
                | Some replay -> replay
                | None ->
                    match confirmationReplay work with
                    | Some replay -> replay
                    | None -> activeEntry work

            match! lifecycleWorkOf bound.Directory context.SessionId with
            | Some work when work.Sequence = [ "S4" ] -> return openedEntry work
            | _ -> return Error(text Path.BindingUnavailable)
        }

    let private enqueueRelayedSuccessor (scope: ToolRuntimeScope) (prepared: PreparedRetirement) (kind: string) =
        task {
            if kind = "Relayed" then
                let! continued = scope.ContinueManagerLoop(prepared.SessionId, prepared.Bound.Directory)

                continued
                |> Result.defaultWith (fun error -> raise (InvalidOperationException error))
        }

    /// The authority closure is written after the retirement is already
    /// committed. A failure to write it is captured instead of thrown, so the
    /// caller can still arm the interrupt for this exact attempt and only
    /// then surface the failure.
    let private tryAppendAuthorityClosure
        (journalDir: string)
        (sessionId: string)
        (retirementId: string)
        : Task<Result<unit, exn>> =
        task {
            try
                do! JournalSurface.appendAuthorityLogicalRunClosed journalDir sessionId retirementId

                return Ok()
            with ex ->
                return Error ex
        }

    /// The authority closure is written after the retirement is already
    /// committed, so a failure to write it must not skip the stop of this
    /// attempt, and it must not undo the committed retirement. The closure
    /// failure is captured where it happens, the attempt is armed for
    /// interruption next, and only then is the failure surfaced.
    let private closeAndArmDelivered
        (scope: ToolRuntimeScope)
        (prepared: PreparedRetirement)
        (journalDir: string)
        (retirementId: string)
        : Task<unit> =
        task {
            let! closureOutcome =
                tryAppendAuthorityClosure journalDir (SessionId.value prepared.SessionId) retirementId

            // 条款 3/4/6：交付已成立是提交面事实，登记精确 attempt 围栏；
            // 物理中断不在工具内部发起——停止由 transform 钩子的交付裁决执行。
            // 中断未确认时，已提交的交付不得呈现为失败。
            //
            // 登记前复核当前物理输入：交付之后到达的新输入不得被本次交付的
            // 停止瞄准（对齐 ask 路径「落地前有新输入 → 只记诊断、不视为失败」）。
            let boundPhysical = prepared.Bound.PhysicalUserMessageId

            let currentPhysical =
                scope.CurrentPhysicalUserMessage (SessionId.value prepared.SessionId)
                |> Option.map PhysicalUserMessageId.create

            if currentPhysical = Some boundPhysical then
                scope.RegisterEstablishedDelivery(
                    prepared.SessionId,
                    prepared.Bound.ProviderRun,
                    prepared.Bound.ToolCallId,
                    ?physicalUserMessageId = Some boundPhysical
                )
            else
                Diagnostic.emit
                    "delivery-interrupt-skipped-new-input"
                    [ "session_id", SessionId.value prepared.SessionId
                      "physical_user_message_id", PhysicalUserMessageId.value boundPhysical ]

            closureOutcome
            |> Result.defaultWith (fun ex -> raise ex)
        }

    let private deliverFrozenCursor
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (prepared: PreparedRetirement)
        (providerRunId: string)
        (toolCallId: string)
        (journalDir: string)
        (cursor: int64)
        : Task<Result<string, string>> =
        let blockers = scope.RetirementBlockersFor context.SessionId
        let requestId = providerRunId + ":" + toolCallId

        let command =
            Wanxiangshu.Mission.Lifecycle.DurableWorkHost.command
                "Deliver"
                [ "incumbencyId", box prepared.Incumbent
                  "providerRunId", box providerRunId
                  "toolCallId", box toolCallId
                  "cursor", box (float cursor)
                  "blockers", box (List.toArray blockers)
                  "nextOwnerId", box context.SessionId ]

        let receipt =
            Wanxiangshu.Mission.Lifecycle.DurableWorkHost.execute journalDir context.SessionId requestId command

        let durableOutcome =
            receipt.Status = "Committed"
            || receipt.Status = "Replayed"

        if
            receipt.Status = "Unknown"
            || receipt.Status = "NotWritten"
            || receipt.Status = "Conflict"
        then
            Task.FromResult(
                Error(
                    if receipt.Reason = "" then
                        receipt.Status
                    else
                        receipt.Reason
                )
            )
        elif receipt.Kind = "ReviewRequired" then
            Task.FromResult(Error "assessment required")
        elif durableOutcome then
            task {
                do! enqueueRelayedSuccessor scope prepared receipt.Kind
                scope.UnfreezeRetirement context.SessionId

                if receipt.Kind = "Delivered" then
                    let retirementId =
                        Wanxiangshu.Mission.Lifecycle.DurableWorkHost.resultText receipt "retirementId"

                    do! closeAndArmDelivered scope prepared journalDir retirementId

                return
                    Ok(
                        if receipt.Kind = "Confirmed" then
                            confirmationResult ()
                        elif receipt.Kind = "Blocked" then
                            // Algorithm E8: a committed blocker keeps the tenure
                            // active. The call reports what to close and may be
                            // repeated once the resources have settled.
                            blockedResult blockers
                        else
                            retirementResult receipt.Kind
                    )
            }
        else if receipt.Status = "Observed" then
            // Observed 是 Work 的无新事实观察（requirements/work/WHAT.md 015）：
            // 本次调用没有落地任何事实，因此不派发后继、不解冻、不登记交付成立。
            // 裸状态名对模型没有意义，这里给一句稳定的可读说明。
            Task.FromResult(Error(text Path.FinishFailed))
        else
            Task.FromResult(
                Error(
                    if receipt.Reason = "" then
                        receipt.Status
                    else
                        receipt.Reason
                )
            )

    let private observeFrozenCursor
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (prepared: PreparedRetirement)
        (providerRunId: string)
        (toolCallId: string)
        (journalDir: string)
        : Task<Result<string, string>> =
        task {
            let! observedHead = observedRetirementCursor scope context prepared.Bound.Journal

            match observedHead with
            | None -> return Error "retirement cursor was not observed"
            | Some cursor ->
                return! deliverFrozenCursor scope context prepared providerRunId toolCallId journalDir cursor
        }

    let private runFrozenDeliver
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (prepared: PreparedRetirement)
        (providerRunId: string)
        (toolCallId: string)
        (journalDir: string)
        : Task<Result<string, string>> =
        task {
            match Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected journalDir context.SessionId with
            | None -> return Error "lifecycle work is missing"
            | Some _ -> return! observeFrozenCursor scope context prepared providerRunId toolCallId journalDir
        }

    let private runFrozen (scope: ToolRuntimeScope) (context: HostToolContext) (prepared: PreparedRetirement) =
        let hasAssessment = prepared.Reviewed
        let toolCallId = ToolCallId.value prepared.Bound.ToolCallId
        let providerRunId = ProviderRunIdentity.value prepared.Bound.ProviderRun

        let confirmFirst () =
            task {
                let directory = scope.WorkspaceDirectory

                let! commonDir =
                    match directory with
                    | None -> Task.FromResult None
                    | Some d ->
                        task {
                            let! cr = RuntimePath.stateRoot d
                            return Some cr
                        }

                match commonDir with
                | None -> return Error "lifecycle journal directory is unavailable"
                | Some journalDir when String.IsNullOrWhiteSpace journalDir ->
                    return Error "lifecycle journal directory is unavailable"
                | Some journalDir ->
                    return! runFrozenDeliver scope context prepared providerRunId toolCallId journalDir
            }

        if not hasAssessment then
            scope.UnfreezeRetirement context.SessionId
            Task.FromResult(Ok(assessmentRequiredResult ()))
        else
            confirmFirst ()

    let private unfreezeUnlessRetired (scope: ToolRuntimeScope) (context: HostToolContext) =
        task {
            let! facts = scope.ManagerCapabilityFactsFor context.SessionId

            if facts.HasActiveIncumbency then
                scope.UnfreezeRetirement context.SessionId
        }

    let private finishOutcome (scope: ToolRuntimeScope) (context: HostToolContext) outcome =
        match outcome with
        | Ok value -> value
        | Error error ->
            scope.UnfreezeRetirement context.SessionId
            raise (InvalidOperationException error)

    let private refreshedTarget
        (target: PreparedRetirement)
        (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork)
        =
        match work.ActiveId, work.Reviewed with
        | Some id, reviewed when id = target.Incumbent ->
            Ok
                { Bound = target.Bound
                  SessionId = target.SessionId
                  Incumbent = target.Incumbent
                  Reviewed = reviewed }
        | _ -> Error(text Path.BindingUnavailable)

    let private refreshAfterFreeze
        (target: PreparedRetirement)
        : Task<Result<PreparedRetirement, string>> =
        task {
            try
                let! workOpt = lifecycleWorkOf target.Bound.Directory (SessionId.value target.SessionId)

                return
                    workOpt
                    |> Option.map (refreshedTarget target)
                    |> Option.defaultValue (Error(text Path.BindingUnavailable))
            with _ ->
                return Error(text Path.FinishFailed)
        }

    let private refreshedOrRaise
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (refreshResult: Result<PreparedRetirement, string>)
        =
        match refreshResult with
        | Ok refreshed -> refreshed
        | Error error ->
            scope.UnfreezeRetirement context.SessionId
            raise (InvalidOperationException error)

    let private executePrepared (scope: ToolRuntimeScope) (context: HostToolContext) (target: PreparedRetirement) =
        task {
            try
                scope.TryFreezeRetirement(context.SessionId, target.Incumbent) |> ignore

                let! refreshed =
                    task {
                        let! r = refreshAfterFreeze target
                        return refreshedOrRaise scope context r
                    }

                let! outcome = runFrozen scope context refreshed
                return finishOutcome scope context outcome
            with ex ->
                do! unfreezeUnlessRetired scope context
                return raise ex
        }

    let private execute (scope: ToolRuntimeScope) (_: HostToolArguments) (context: HostToolContext) =
        task {
            let boundRes = bindInvocation scope context

            let! entered =
                match boundRes with
                | Error err -> Task.FromResult(Error err)
                | Ok bound -> enterDeliver context bound

            match entered with
            | Ok(Replayed result) -> return result
            | Ok(Prepared target) -> return! executePrepared scope context target
            | Error error -> return raise (InvalidOperationException error)
        }

    let description () = text Path.Description

    let admission =
        ToolAdmission.OfficeRole(fun _ role -> OfficeCapability.isAllowed role ToolPermission.Finality)

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "deliver"
          Description = text Path.Description
          Arguments = []
          Admission = admission
          Execute = execute scope }
