namespace Wanxiangshu.Mission.Work

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.Persistence.Journal

/// The capabilities one `deliver` call needs from the runtime that owns it.
///
/// The product owns the delivery flow — the admission, the exact witness
/// write and the settlement order. The runtime supplies the facts (who the
/// caller is, which fence this call is) and the effects that only the runtime
/// can perform (freezing a tenure, arming the interrupt, continuing the
/// manager loop). Nothing here is a product decision: every value is a fact
/// the runtime already proved.
type DeliveryRuntime =
    {
        Role: Role option
        ToolCallId: ToolCallId option
        ProviderRun: ProviderRunIdentity option
        PhysicalUserMessageId: string option
        WorkspaceDirectory: string option
        Journal: AgentJournal option
        Snapshot: ISessionSnapshotPort option
        /// Whether the session still holds an active incumbency.
        HasActiveIncumbency: SessionId -> Task<bool>
        /// The physical user message the session currently sits on.
        CurrentPhysicalUserMessage: SessionId -> string option
        /// Freeze the tenure so no other call may retire it.
        FreezeRetirement: SessionId -> string -> bool
        /// Release the tenure freeze.
        UnfreezeRetirement: SessionId -> unit
        /// The resources that still block the retirement.
        RetirementBlockers: SessionId -> string list
        /// Record that the delivery of this attempt is established.
        RegisterEstablishedDelivery: SessionId -> ProviderRunIdentity -> ToolCallId -> PhysicalUserMessageId -> unit
        /// Hand the work over to the successor this retirement created.
        ContinueManagerLoop: SessionId -> string -> Task<Result<unit, string>>
    }

/// The product's delivery flow: the admission, the exact witness write and
/// the settlement order.
///
/// The adapter decodes the call, builds the `DeliveryRuntime` from the facts it
/// proved, and presents the text this module returns. The durable write itself
/// goes through the product fact persistence capability (`DurableWorkHost`).
[<RequireQualifiedAccess>]
module DeliveryFlow =

    /// The one lifecycle work this session owns. Lifecycle storage is the
    /// wanxiangli journal, independent of the workspace EventStore.
    let private lifecycleWorkOf (directory: string) (sessionId: string) : Task<DurableWorkHost.InspectedWork option> =
        task {
            let! commonDir = RuntimePath.stateRoot directory

            if String.IsNullOrWhiteSpace commonDir then
                return None
            else
                return DurableWorkHost.inspected commonDir sessionId
        }

    /// Algorithm E1: the caller enters the two-stage sequence only for the
    /// S4 work. Any other sequence is a refusal.
    let private enterDeliver
        (runtime: DeliveryRuntime)
        (fence: DeliverPolicy.Fence)
        : Task<Result<DeliverPolicy.Entry, string>> =
        task {
            let! work = lifecycleWorkOf fence.Directory (SessionId.value fence.SessionId)

            match work with
            | Some inspected when inspected.Sequence = [ "S4" ] -> return DeliverPolicy.enter inspected fence
            | _ -> return Error(DeliverPolicy.refusalText DeliverPolicy.BindingUnavailable)
        }

    /// The retirement cursor is read from the XTrace head observed on the Host
    /// snapshot. A head already on the journal is used as-is; otherwise the
    /// snapshot messages are captured through the real XTrace path and the
    /// receipt's current head is read. The capture is append-only and
    /// idempotent, so a message already observed adds nothing. A missing
    /// snapshot is not a guessed cursor: None leaves the kernel refusal
    /// unchanged.
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

    let private observedRetirementCursor
        (runtime: DeliveryRuntime)
        (sessionId: SessionId)
        (journal: AgentJournal)
        : Task<int64 option> =
        task {
            let headOf projections =
                AgentProjection.tryFind sessionId projections
                |> Option.bind (fun session -> session.XTrace)
                |> Option.map (XTraceProjection.headCursor >> XTraceCursor.sequence)

            let current = headOf (AgentJournal.snapshot journal).AgentProjections

            match current, runtime.Snapshot with
            | Some cursor, _ -> return Some cursor
            | None, None -> return None
            | None, Some snapshotPort -> return! captureObservedCursor journal sessionId snapshotPort
        }

    /// The authority closure is written after the retirement is already
    /// committed, so a failure to write it must not skip the stop of this
    /// attempt, and it must not undo the committed retirement. The closure
    /// failure is captured where it happens, the attempt is armed for
    /// interruption next, and only then is the failure surfaced.
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

    /// The delivery is already committed as a submit-surface fact, so the exact
    /// attempt fence is registered; the physical interrupt is not issued here —
    /// the stop is adjudicated by the transform hook. An unconfirmed interrupt
    /// must not present a committed delivery as a failure.
    ///
    /// The current physical input is re-read before registering: a new input
    /// that arrived after the delivery must not become this delivery's aim.
    let private closeAndArmDelivered
        (runtime: DeliveryRuntime)
        (prepared: DeliverPolicy.Prepared)
        (journalDir: string)
        (retirementId: string)
        : Task<unit> =
        task {
            let! closureOutcome =
                tryAppendAuthorityClosure journalDir (SessionId.value prepared.Fence.SessionId) retirementId

            let boundPhysical = prepared.Fence.PhysicalUserMessageId

            let currentPhysical =
                runtime.CurrentPhysicalUserMessage prepared.Fence.SessionId
                |> Option.map PhysicalUserMessageId.create

            if currentPhysical = Some boundPhysical then
                runtime.RegisterEstablishedDelivery
                    prepared.Fence.SessionId
                    prepared.Fence.ProviderRun
                    prepared.Fence.ToolCallId
                    boundPhysical
            else
                Diagnostic.emit
                    "delivery-interrupt-skipped-new-input"
                    [ "session_id", SessionId.value prepared.Fence.SessionId
                      "physical_user_message_id", PhysicalUserMessageId.value boundPhysical ]

            closureOutcome |> Result.defaultWith (fun ex -> raise ex)
        }

    let private enqueueRelayedSuccessor (runtime: DeliveryRuntime) (prepared: DeliverPolicy.Prepared) (kind: string) =
        task {
            if kind = "Relayed" then
                let! continued = runtime.ContinueManagerLoop prepared.Fence.SessionId prepared.Fence.Directory

                ignore (
                    continued
                    |> Result.defaultWith (fun error -> raise (InvalidOperationException error))
                )

            ()
        }

    /// The exact witness write: the retirement command the product issues and
    /// the settlement that follows it.
    let private deliverFrozenCursor
        (runtime: DeliveryRuntime)
        (prepared: DeliverPolicy.Prepared)
        (journalDir: string)
        (cursor: int64)
        : Task<Result<string, string>> =
        task {
            let blockers = runtime.RetirementBlockers prepared.Fence.SessionId
            let providerRunId = ProviderRunIdentity.value prepared.Fence.ProviderRun
            let toolCallId = ToolCallId.value prepared.Fence.ToolCallId
            let requestId = providerRunId + ":" + toolCallId

            // S4 single delivery: when review outcome is "Stayed", the manager
            // hands the work to a successor tenure; otherwise (promoted or no
            // review), it delivers and completes the work in one step.
            let isRelay = prepared.ReviewOutcome = Some "Stayed"

            let nextOwnerFields =
                if isRelay then
                    [ "nextOwnerId", box (SessionId.value prepared.Fence.SessionId) ]
                else
                    []

            let command =
                DurableWorkHost.command
                    "Deliver"
                    ([ "incumbencyId", box prepared.Incumbent
                       "providerRunId", box providerRunId
                       "toolCallId", box toolCallId
                       "cursor", box (float cursor)
                       "blockers", box (List.toArray blockers) ]
                     @ nextOwnerFields)

            let receipt =
                DurableWorkHost.execute journalDir (SessionId.value prepared.Fence.SessionId) requestId command

            let durableOutcome = receipt.Status = "Committed" || receipt.Status = "Replayed"

            if
                receipt.Status = "Unknown"
                || receipt.Status = "NotWritten"
                || receipt.Status = "Conflict"
            then
                return
                    Error(
                        if receipt.Reason = "" then
                            receipt.Status
                        else
                            receipt.Reason
                    )
            elif durableOutcome then
                do! enqueueRelayedSuccessor runtime prepared receipt.Kind
                runtime.UnfreezeRetirement prepared.Fence.SessionId

                if receipt.Kind = "Delivered" then
                    let retirementId = DurableWorkHost.resultText receipt "retirementId"

                    do! closeAndArmDelivered runtime prepared journalDir retirementId

                return
                    Ok(
                        if receipt.Kind = "Blocked" then
                            DeliverPolicy.blockedResult blockers
                        else
                            DeliverPolicy.retirementResult receipt.Kind
                    )
            elif receipt.Status = "Observed" then
                // Observed is the no-new-fact observation of Work: this call
                // landed nothing, so it dispatches no successor, unfreezes
                // nothing and records no delivery. A bare status name means
                // nothing to the model, so it gets one stable readable line.
                return Error(DeliverPolicy.refusalText DeliverPolicy.FinishFailed)
            else
                return
                    Error(
                        if receipt.Reason = "" then
                            receipt.Status
                        else
                            receipt.Reason
                    )
        }

    let private observeFrozenCursor
        (runtime: DeliveryRuntime)
        (prepared: DeliverPolicy.Prepared)
        (journal: AgentJournal)
        (journalDir: string)
        : Task<Result<string, string>> =
        task {
            let! observedHead = observedRetirementCursor runtime prepared.Fence.SessionId journal

            match observedHead with
            | None -> return Error "retirement cursor was not observed"
            | Some cursor -> return! deliverFrozenCursor runtime prepared journalDir cursor
        }

    let private runFrozenDeliver
        (runtime: DeliveryRuntime)
        (prepared: DeliverPolicy.Prepared)
        (journal: AgentJournal)
        (journalDir: string)
        : Task<Result<string, string>> =
        task {
            match DurableWorkHost.inspected journalDir (SessionId.value prepared.Fence.SessionId) with
            | None -> return Error "lifecycle work is missing"
            | Some _ -> return! observeFrozenCursor runtime prepared journal journalDir
        }

    let private runFrozen
        (runtime: DeliveryRuntime)
        (prepared: DeliverPolicy.Prepared)
        (journal: AgentJournal)
        : Task<Result<string, string>> =
        task {
            // N-01: One genuine delivery intent completes in one call without
            // an assessment precondition or a two-step confirmation ritual.
            let! journalDir =
                match runtime.WorkspaceDirectory with
                | None -> Task.FromResult None
                | Some directory ->
                    task {
                        let! stateRoot = RuntimePath.stateRoot directory
                        return Some stateRoot
                    }

            match journalDir with
            | None -> return Error "lifecycle journal directory is unavailable"
            | Some dir when String.IsNullOrWhiteSpace dir -> return Error "lifecycle journal directory is unavailable"
            | Some dir -> return! runFrozenDeliver runtime prepared journal dir
        }

    /// The refreshed target: the same incumbency, still the one this call may
    /// retire, with the assessment state the freeze observed.
    let private refreshedTarget
        (target: DeliverPolicy.Prepared)
        (work: DurableWorkHost.InspectedWork)
        : Result<DeliverPolicy.Prepared, string> =
        match work.ActiveId, work.Reviewed with
        | Some id, reviewed when id = target.Incumbent ->
            Ok
                { Fence = target.Fence
                  Incumbent = target.Incumbent
                  Reviewed = reviewed
                  ReviewOutcome = work.ReviewOutcome }
        | _ -> Error(DeliverPolicy.refusalText DeliverPolicy.BindingUnavailable)

    let private refreshAfterFreeze (target: DeliverPolicy.Prepared) : Task<Result<DeliverPolicy.Prepared, string>> =
        task {
            try
                let! workOpt = lifecycleWorkOf target.Fence.Directory (SessionId.value target.Fence.SessionId)

                return
                    workOpt
                    |> Option.map (refreshedTarget target)
                    |> Option.defaultValue (Error(DeliverPolicy.refusalText DeliverPolicy.BindingUnavailable))
            with _ ->
                return Error(DeliverPolicy.refusalText DeliverPolicy.FinishFailed)
        }

    let private unfreezeUnlessRetired (runtime: DeliveryRuntime) (sessionId: SessionId) =
        task {
            let! hasIncumbency = runtime.HasActiveIncumbency sessionId

            if hasIncumbency then
                runtime.UnfreezeRetirement sessionId
        }

    let private finishOutcome (runtime: DeliveryRuntime) (sessionId: SessionId) (outcome: Result<string, string>) =
        match outcome with
        | Ok value -> value
        | Error error ->
            runtime.UnfreezeRetirement sessionId
            raise (InvalidOperationException error)

    /// The whole delivery flow for one call.
    let private runPrepared
        (runtime: DeliveryRuntime)
        (sessionId: SessionId)
        (prepared: DeliverPolicy.Prepared)
        (journal: AgentJournal)
        : Task<string> =
        task {
            try
                runtime.FreezeRetirement sessionId prepared.Incumbent |> ignore

                let! refreshed = refreshAfterFreeze prepared

                let prepared =
                    refreshed
                    |> Result.defaultWith (fun error ->
                        runtime.UnfreezeRetirement sessionId
                        raise (InvalidOperationException error))

                let! outcome = runFrozen runtime prepared journal

                return finishOutcome runtime sessionId outcome
            with ex ->
                do! unfreezeUnlessRetired runtime sessionId
                return raise ex
        }

    let execute (runtime: DeliveryRuntime) (sessionId: SessionId) : Task<string> =
        task {
            let! result =
                task {
                    let fenceResult =
                        match
                            runtime.Role,
                            runtime.ToolCallId,
                            runtime.ProviderRun,
                            runtime.PhysicalUserMessageId,
                            runtime.WorkspaceDirectory,
                            runtime.Journal
                        with
                        | Some Role.Manager,
                          Some toolCallId,
                          Some providerRun,
                          Some physical,
                          Some directory,
                          Some journal when not (String.IsNullOrWhiteSpace physical) ->
                            Ok(
                                { SessionId = sessionId
                                  ToolCallId = toolCallId
                                  ProviderRun = providerRun
                                  PhysicalUserMessageId = PhysicalUserMessageId.create physical
                                  Directory = directory }
                                : DeliverPolicy.Fence
                            )
                        | Some Role.Manager, _, _, _, _, _ ->
                            Error(DeliverPolicy.refusalText DeliverPolicy.BindingUnavailable)
                        | Some _, _, _, _, _, _ -> Error(DeliverPolicy.refusalText DeliverPolicy.NonManagerRole)
                        | None, _, _, _, _, _ -> Error(DeliverPolicy.refusalText DeliverPolicy.NoAuthority)

                    let! entered =
                        match fenceResult with
                        | Error error -> Task.FromResult(Error error)
                        | Ok fence -> enterDeliver runtime fence

                    match entered with
                    | Error error -> return Error error
                    | Ok(DeliverPolicy.Entry.Replayed result) -> return Ok result
                    | Ok(DeliverPolicy.Entry.Prepared prepared) ->
                        let journal = runtime.Journal.Value

                        let! r = runPrepared runtime sessionId prepared journal
                        return Ok r
                }

            return
                match result with
                | Ok value -> value
                | Error error -> raise (InvalidOperationException error)
        }
