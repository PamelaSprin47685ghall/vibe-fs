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
open Wanxiangshu.Interaction.Attention
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
        let NoRetirementProjection = "tool/deliver/finish-not-recorded"

        [<Literal>]
        let BindingUnavailable = "tool/deliver/binding-unavailable"

        [<Literal>]
        let FinishFailed = "tool/deliver/finish-failed"

    let private text (path: string) =
        ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) path Map.empty

    let private pendingDeferred (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.pendingAttentionWorkPairs sessionId (AgentJournal.snapshot journal).AgentProjections

    /// The one lifecycle work this session owns. Lifecycle storage is the
    /// wanxiangli journal, independent of the workspace EventStore.
    let private lifecycleWorkOf
        (directory: string)
        (sessionId: string)
        : Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option =
        let commonDir = RuntimePath.stateRoot directory

        if String.IsNullOrWhiteSpace commonDir then
            None
        else
            Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected commonDir sessionId

    let private deterministicId prefix payload create =
        HostDigest.sha256Hex (prefix + "\n" + payload)
        |> fun digest -> create (prefix + ":" + digest)

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

    let private retiredResult () =
        ToolHostCodec.tomlObjectWithInstructions [ text Path.Retired ] [ "finished", ToolHostCodec.TBool true ]

    let private requireSome error =
        function
        | Some value -> Ok value
        | None -> Error error

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
          Directory: string
          Journal: AgentJournal }

    type private PreparedRetirement =
        { Bound: BoundRetirement
          SessionId: SessionId
          Incumbent: string
          Reviewed: bool }


    let private bindInvocation (scope: ToolRuntimeScope) (context: HostToolContext) =
        match
            scope.RoleFor context,
            context.ToolCallId,
            context.ProviderRunId,
            scope.CurrentPhysicalUserMessage context.SessionId,
            scope.WorkspaceDirectory,
            scope.Journal
        with
        | Some Role.Manager, Some toolCallId, Some providerRun, Some _, Some directory, Some journal when
            not (String.IsNullOrWhiteSpace context.SessionId)
            ->
            Ok
                { ToolCallId = toolCallId
                  ProviderRun = providerRun
                  Directory = directory
                  Journal = journal }
        | Some Role.Manager, _, _, _, _, _ -> Error(text Path.BindingUnavailable)
        | Some _, _, _, _, _, _ -> Error(text Path.NonManagerRole)
        | None, _, _, _, _, _ -> Error(text Path.NoAuthority)

    let private prepareTarget (context: HostToolContext) (bound: BoundRetirement) : Result<PreparedRetirement, string> =
        let located =
            lifecycleWorkOf bound.Directory context.SessionId
            |> Option.bind (fun work -> work.ActiveId |> Option.map (fun active -> work, active))

        match located with
        | None -> Error(text Path.BindingUnavailable)
        | Some(work, active) ->
            Ok
                { Bound = bound
                  SessionId = SessionId.create context.SessionId
                  Incumbent = active
                  Reviewed = work.Reviewed }

    let private confirmationResult (prepared: PreparedRetirement) pending =
        let deferred =
            pending
            |> List.mapi (fun index (_, itemText) -> string index, ToolHostCodec.TString itemText)

        let optionalFields =
            [ if not (List.isEmpty deferred) then
                  yield "deferred", ToolHostCodec.TTable deferred ]

        ToolHostCodec.tomlObjectWithInstructions
            [ text Path.Confirmation ]
            ([ "finished", ToolHostCodec.TBool false
               "confirmation_required", ToolHostCodec.TBool true ]
             @ optionalFields)

    /// The confirmed path of the two-stage deliver. The committed confirmation
    /// fence names the run and call that confirmed; this call either replays
    /// that exact confirmation to its recorded state, or is the different call
    /// that closes out.
    let private runConfirmed
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (prepared: PreparedRetirement)
        pending
        toolCallId
        providerRunId
        confirmFirst
        =
        let confirmedReplay =
            scope.WorkspaceDirectory
            |> Option.bind (fun directory -> lifecycleWorkOf directory context.SessionId)
            |> Option.bind (fun work -> work.Confirmation)
            |> Option.exists (fun fence -> fence.ProviderRunId = providerRunId && fence.ToolCallId = toolCallId)

        if confirmedReplay then
            scope.UnfreezeRetirement context.SessionId
            Task.FromResult(Ok(confirmationResult prepared pending))
        else
            confirmFirst ()

    let private enqueueRelayedSuccessor (scope: ToolRuntimeScope) (prepared: PreparedRetirement) (kind: string) =
        task {
            if kind = "Relayed" then
                let! continued = scope.ContinueManagerLoop(prepared.SessionId, prepared.Bound.Directory)

                continued
                |> Result.defaultWith (fun error -> raise (InvalidOperationException error))
        }

    let private deliverFrozenCursor
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (prepared: PreparedRetirement)
        (pending: (string * string) list)
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
            || receipt.Status = "Observed"

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

                    do! JournalSurface.appendAuthorityLogicalRunClosed journalDir context.SessionId retirementId

                    let! interrupted = scope.Sessions.InterruptAttempt prepared.SessionId

                    interrupted
                    |> Result.defaultWith (fun reason ->
                        invalidOp ("Manager delivery was recorded, but its attempt interrupt failed: " + reason))

                return
                    Ok(
                        if receipt.Kind = "Confirmed" then
                            confirmationResult prepared pending
                        else
                            text Path.Retired
                    )
            }
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
        (pending: (string * string) list)
        (providerRunId: string)
        (toolCallId: string)
        (journalDir: string)
        : Task<Result<string, string>> =
        task {
            let! observedHead = observedRetirementCursor scope context prepared.Bound.Journal

            match observedHead with
            | None -> return Error "retirement cursor was not observed"
            | Some cursor ->
                return! deliverFrozenCursor scope context prepared pending providerRunId toolCallId journalDir cursor
        }

    let private runFrozenDeliver
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (prepared: PreparedRetirement)
        (pending: (string * string) list)
        (providerRunId: string)
        (toolCallId: string)
        (journalDir: string)
        : Task<Result<string, string>> =
        task {
            match Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected journalDir context.SessionId with
            | None -> return Error "lifecycle work is missing"
            | Some _ -> return! observeFrozenCursor scope context prepared pending providerRunId toolCallId journalDir
        }

    let private runFrozen (scope: ToolRuntimeScope) (context: HostToolContext) (prepared: PreparedRetirement) =
        let hasAssessment = prepared.Reviewed
        let toolCallId = ToolCallId.value prepared.Bound.ToolCallId
        let providerRunId = ProviderRunIdentity.value prepared.Bound.ProviderRun

        let pending = pendingDeferred prepared.Bound.Journal prepared.SessionId

        let confirmFirst () =
            task {
                let directory = scope.WorkspaceDirectory
                let commonDir = directory |> Option.map RuntimePath.stateRoot

                match commonDir with
                | None -> return Error "lifecycle journal directory is unavailable"
                | Some journalDir when String.IsNullOrWhiteSpace journalDir ->
                    return Error "lifecycle journal directory is unavailable"
                | Some journalDir ->
                    return! runFrozenDeliver scope context prepared pending providerRunId toolCallId journalDir
            }

        if not hasAssessment then
            scope.UnfreezeRetirement context.SessionId
            Task.FromResult(Ok(assessmentRequiredResult ()))
        else
            runConfirmed scope context prepared pending toolCallId providerRunId confirmFirst

    let private unfreezeUnlessRetired (scope: ToolRuntimeScope) (context: HostToolContext) =
        let facts = scope.ManagerCapabilityFactsFor context.SessionId

        if facts.HasActiveIncumbency then
            scope.UnfreezeRetirement context.SessionId

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
        (context: HostToolContext)
        (target: PreparedRetirement)
        : Result<PreparedRetirement, string> =
        try
            lifecycleWorkOf target.Bound.Directory (SessionId.value target.SessionId)
            |> Option.map (refreshedTarget target)
            |> Option.defaultValue (Error(text Path.BindingUnavailable))
        with _ ->
            Error(text Path.FinishFailed)

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
                let refreshed = refreshAfterFreeze context target |> refreshedOrRaise scope context
                let! outcome = runFrozen scope context refreshed
                return finishOutcome scope context outcome
            with ex ->
                unfreezeUnlessRetired scope context
                return raise ex
        }

    let private execute (scope: ToolRuntimeScope) (_: HostToolArguments) (context: HostToolContext) =
        match bindInvocation scope context |> Result.bind (prepareTarget context) with
        | Ok target -> executePrepared scope context target
        | Error error -> task { return raise (InvalidOperationException error) }

    let description () = text Path.Description

    let admission =
        ToolAdmission.OfficeRole(fun _ role -> OfficeCapability.isAllowed role ToolPermission.Finality)

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "deliver"
          Description = text Path.Description
          Arguments = []
          Admission = admission
          Execute = execute scope }
