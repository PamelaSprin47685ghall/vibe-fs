namespace Wanxiangshu.Interaction.Repair

open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.Journal

/// Idle-derived interaction repair (missing-final-report / incomplete interaction).
/// HOST-004 admission: no idle permit, or a permit that no longer holds at send
/// time → zero physical prompt, zero claim, zero terminal.
module InteractionRepairWorkflow =

    /// Generic interaction nudges are gate reminders, gated on a fresh idle permit
    /// (HOST-004). The same terminal occasion is idempotent; a fresh terminal while
    /// the interaction gate remains unsatisfied earns another reminder.
    ///
    /// The task is awaited rather than discarded. `|> ignore` on the task also
    /// discarded the claim/abandon bookkeeping inside it, so a failed repair left
    /// a Claimed fact with nothing after it and no terminal for the caller.
    ///
    /// `Superseded` (stale permit) is not a failure: nothing was claimed, nothing
    /// was sent — the system is doing something fresher.
    let private sendRepair
        (quiescence: ISessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (prompt: string)
        (repairKind: string)
        : Task =
        task {
            let! outcome =
                HostSessionNudge.trySendIdleGateRepair
                    quiescence
                    permit
                    sessionPort
                    rootWorkspace
                    turn.SessionId
                    prompt
                    turn.Directory
                    journal
                    repairKind
                    turn.ProviderRun

            match outcome with
            | HostSessionNudge.IdleContinuationOutcome.Sent _
            | HostSessionNudge.IdleContinuationOutcome.AdmissionRejected _
            | HostSessionNudge.IdleContinuationOutcome.AlreadyAdmitted
            | HostSessionNudge.IdleContinuationOutcome.Retired -> ()
            | HostSessionNudge.IdleContinuationOutcome.NotSent error ->
                Diagnostic.emit
                    "interaction-gate-nudge-not-sent"
                    [ "session_id", SessionId.value turn.SessionId; "result", error ]
            | HostSessionNudge.IdleContinuationOutcome.Failed error ->
                // Journal/authority/transport failures are Wanxiangshu invariant
                // failures, not model behavior. In production fatal kills the
                // process; the terminal signal keeps node:test fail-closed too.
                Diagnostic.fatal
                    "interaction-repair-infrastructure-failed"
                    [ "session_id", SessionId.value turn.SessionId; "result", error ]

                eventPort.NotifyTerminal
                    turn.SessionId
                    (TerminalOutcome.Failed(
                        TerminalStop.forAuthority turn.AuthorityRootUserMessageId ("WANXIANGSHU_FATAL: " + error)
                    ))
                |> ignore
        }
        :> Task

    /// HOST-004: idle-derived repair sends funnel through one admission point.
    let private trySendIdleRepair
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (prompt: string)
        (repairKind: string)
        : Task =
        match context.Quiescence with
        | None -> AsyncSupport.completedTask ()
        | Some permit ->
            sendRepair quiescence permit sessionPort rootWorkspace eventPort journal context.Turn prompt repairKind

    let private continuationKindOf (journal: AgentJournal option) (turn: ReconciledTurn) =
        journal
        |> Option.bind (fun durable ->
            AgentProjection.tryFind turn.SessionId (AgentJournal.snapshot durable).AgentProjections)
        |> Option.bind (fun session -> session.PromptAuthority)
        |> Option.bind (fun authority -> Map.tryFind turn.PhysicalUserMessageId authority.AcceptedContinuationIds)

    let private isInteractionRepairAttempt journal turn =
        continuationKindOf journal turn = Some PromptAuthority.ContinuationKind.InteractionRepair

    let private repairDefect
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (prompt: string)
        (repairKind: string)
        : Task =
        let turn = context.Turn

        match
            CompletedTurnClassifier.decideRepairDefect
                (isInteractionRepairAttempt journal turn)
                turn.Observation
                turn.Outcome
        with
        | CompletedTurnClassifier.RepairDefectDecision.RequestRepair ->
            trySendIdleRepair quiescence context sessionPort rootWorkspace eventPort journal prompt repairKind
        | CompletedTurnClassifier.RepairDefectDecision.AwaitRepairTerminal
        | CompletedTurnClassifier.RepairDefectDecision.NoRepair -> AsyncSupport.completedTask ()

    let private notifyBloggerProtocolFailure
        (eventPort: IEventObservationPort)
        (turn: ReconciledTurn)
        (reason: string)
        =
        Diagnostic.fatal
            "blogger-protocol-repair-failed"
            [ "session_id", SessionId.value turn.SessionId; "result", reason ]

        eventPort.NotifyTerminal
            turn.SessionId
            (TerminalOutcome.Failed(TerminalStop.forAuthority turn.AuthorityRootUserMessageId reason))
        |> ignore

    let private exhaustBloggerProtocol
        (host: IBloggerRuntimeHost)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal)
        (context: ReconciledTurnContext)
        (reason: string)
        : Task =
        task {
            let turn = context.Turn
            let key = SessionId.value turn.SessionId
            let live = host.TryPeekCurrentRequest key

            match live with
            | Some request ->
                do!
                    BloggerAbandon.openRequest
                        journal
                        (BloggerRequestContext.mainSessionId request)
                        turn.SessionId
                        (Some request)
                        reason

                BloggerRuntimeHost.requireReleaseCurrentRequest host key request
            | None -> ()

            notifyBloggerProtocolFailure eventPort turn reason
        }
        :> Task

    type private BloggerAabbFailureDecision =
        | SendAabb
        | ExhaustProtocol
        | FailProtocol of string

    let private decideBloggerAabbFailure
        (guaranteedFirstAabb: bool)
        (outcome: Result<ConfirmedFailureOutcome, string>)
        : BloggerAabbFailureDecision =
        match outcome with
        | Error error -> FailProtocol error
        | Ok ConfirmedFailureOutcome.NoActiveRun -> FailProtocol "blogger AABB has no active logical run"
        // The nudge failure has already earned one protocol AABB attempt.
        // A generic fallback boundary reached by this same failure may not
        // retroactively steal that first send.
        | Ok ConfirmedFailureOutcome.RecoveryExhausted when guaranteedFirstAabb -> SendAabb
        | Ok ConfirmedFailureOutcome.RecoveryExhausted -> ExhaustProtocol
        | Ok ConfirmedFailureOutcome.AlreadyRecorded ->
            // A racing observer may have advanced this exact terminal before
            // the request-scoped AABB claim became visible. The claim itself
            // dedupes the physical send.
            SendAabb
        | Ok(ConfirmedFailureOutcome.RecoveryAdvanced _) -> SendAabb

    let private sendBloggerAabbAfterPermitConsumed
        (host: IBloggerRuntimeHost)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal)
        (context: ReconciledTurnContext)
        (requestId: BloggerRequestId)
        (requestKind: ProviderRequestKind)
        (guaranteedFirstAabb: bool)
        (reason: string)
        : Task =
        task {
            let turn = context.Turn

            let sendAabb () =
                task {
                    match!
                        HostSessionNudge.trySendInteractionRepair
                            sessionPort
                            rootWorkspace
                            turn.SessionId
                            EnforcerRepair.RepairInstruction
                            turn.Directory
                            (Some journal)
                            requestId
                            turn.ProviderRun
                            BloggerRecoveryProbe.BloggerAabbRepairKind
                    with
                    | InteractionRepairSendOutcome.Sent _
                    | InteractionRepairSendOutcome.AlreadyAdmitted
                    | InteractionRepairSendOutcome.Retired -> ()
                    | InteractionRepairSendOutcome.Failed error ->
                        notifyBloggerProtocolFailure eventPort turn ("blogger AABB send failed: " + error)
                }

            let! confirmedFailure =
                ProviderRecoveryWorkflow.admitPolicyAuthorizedFailure
                    journal
                    turn
                    ExecutionFailure.ProviderTransient
                    requestKind
                    reason

            match decideBloggerAabbFailure guaranteedFirstAabb confirmedFailure with
            | SendAabb -> do! sendAabb ()
            | ExhaustProtocol ->
                do! exhaustBloggerProtocol host eventPort journal context "blogger protocol repair exhausted"
            | FailProtocol error -> notifyBloggerProtocolFailure eventPort turn error
        }
        :> Task

    let private admitPermit
        (quiescence: ISessionQuiescenceGate)
        (permit: QuiescencePermit)
        : Result<QuiescencePermit, QuiescencePermitFailure> =
        quiescence.TryConsume permit |> Result.map (fun () -> permit)

    let private consumeThenSendBloggerAabb
        (host: IBloggerRuntimeHost)
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal)
        (requestId: BloggerRequestId)
        (requestKind: ProviderRequestKind)
        (guaranteedFirstAabb: bool)
        (reason: string)
        : Task =
        match context.Quiescence |> Option.map (admitPermit quiescence) with
        | Some(Ok _) ->
            sendBloggerAabbAfterPermitConsumed
                host
                sessionPort
                rootWorkspace
                eventPort
                journal
                context
                requestId
                requestKind
                guaranteedFirstAabb
                reason
        | Some(Error _)
        | None -> AsyncSupport.completedTask ()

    let private sendBloggerNudge
        (host: IBloggerRuntimeHost)
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal)
        (requestId: BloggerRequestId)
        (requestKind: ProviderRequestKind)
        : Task =
        task {
            match context.Quiescence with
            | None -> ()
            | Some permit ->
                match!
                    HostSessionNudge.trySendIdleInteractionRepair
                        quiescence
                        permit
                        sessionPort
                        rootWorkspace
                        context.Turn.SessionId
                        EnforcerRepair.RepairInstruction
                        context.Turn.Directory
                        (Some journal)
                        requestId
                        context.Turn.ProviderRun
                        BloggerRecoveryProbe.BloggerMissingToolRepairKind
                with
                | HostSessionNudge.IdleContinuationOutcome.Sent _
                | HostSessionNudge.IdleContinuationOutcome.AdmissionRejected _
                | HostSessionNudge.IdleContinuationOutcome.AlreadyAdmitted
                | HostSessionNudge.IdleContinuationOutcome.Retired -> ()
                | HostSessionNudge.IdleContinuationOutcome.NotSent error
                | HostSessionNudge.IdleContinuationOutcome.Failed error ->
                    do!
                        sendBloggerAabbAfterPermitConsumed
                            host
                            sessionPort
                            rootWorkspace
                            eventPort
                            journal
                            context
                            requestId
                            requestKind
                            true
                            ("blogger nudge failed: " + error)
        }
        :> Task

    let private isInteractionRepairContinuation (durable: AgentJournal) (turn: ReconciledTurn) =
        continuationKindOf (Some durable) turn = Some PromptAuthority.ContinuationKind.InteractionRepair

    let private bloggerProviderRequestKind (request: BloggerRequestContext) =
        match request with
        | BloggerRequestContext.Main _ -> ProviderRequestKind.BloggerMain
        | BloggerRequestContext.Squash _ -> ProviderRequestKind.BloggerSquash

    let private repairRequestKind (durable: AgentJournal) (turn: ReconciledTurn) (request: BloggerRequestContext) =
        if isInteractionRepairContinuation durable turn then
            ProviderRequestKind.InteractionRepair
        else
            bloggerProviderRequestKind request

    let private repairOwnedBloggerProtocol
        (host: IBloggerRuntimeHost)
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (request: BloggerRequestContext)
        : Task =
        let requestId = BloggerRequestContext.requestId request

        let requestKind = repairRequestKind durable context.Turn request

        match
            BloggerRecoveryProbe.repairStateForInvalidTerminal
                durable
                context.Turn.SessionId
                requestId
                context.Turn.ProviderRun
        with
        | BloggerRecoveryProbe.InvalidTerminalRepairState.NoRecovery ->
            sendBloggerNudge host quiescence context sessionPort rootWorkspace eventPort durable requestId requestKind
        | BloggerRecoveryProbe.InvalidTerminalRepairState.InteractionNudgeIssued issuedRun when
            issuedRun = context.Turn.ProviderRun
            ->
            AsyncSupport.completedTask ()
        | BloggerRecoveryProbe.InvalidTerminalRepairState.InteractionNudgeIssued _ ->
            consumeThenSendBloggerAabb
                host
                quiescence
                context
                sessionPort
                rootWorkspace
                eventPort
                durable
                requestId
                requestKind
                true
                "blogger missing chronicle after interaction nudge"
        | BloggerRecoveryProbe.InvalidTerminalRepairState.AabbRepairIssued issuedRun when
            issuedRun = context.Turn.ProviderRun
            ->
            AsyncSupport.completedTask ()
        | BloggerRecoveryProbe.InvalidTerminalRepairState.AabbRepairIssued _ ->
            consumeThenSendBloggerAabb
                host
                quiescence
                context
                sessionPort
                rootWorkspace
                eventPort
                durable
                requestId
                requestKind
                false
                "blogger invalid terminal after AABB"

    /// Blogger has a stricter terminal protocol than ordinary agents: prose-only
    /// completion is not a closing report. Idle is the only guaranteed wake after
    /// a zero-tool terminal, so it owns the missing-chronicle nudge → AABB state
    /// machine instead of the generic MissingClosingReport continuation.
    /// Historical/unowned idle is observation only: without the exact live
    /// BloggerRequest there is no protocol budget to spend.
    let repairBloggerProtocol
        (host: IBloggerRuntimeHost)
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        : Task =
        let liveRequest = host.TryPeekCurrentRequest(SessionId.value context.Turn.SessionId)

        match journal, liveRequest with
        | None, _ ->
            notifyBloggerProtocolFailure eventPort context.Turn "blogger protocol repair requires an AgentJournal"
            AsyncSupport.completedTask ()
        | Some _, None ->
            Diagnostic.emit
                "blogger-protocol-repair-unowned-idle"
                [ "session_id", SessionId.value context.Turn.SessionId
                  "result", "no live BloggerRequest; idle cannot spend protocol repair budget" ]

            AsyncSupport.completedTask ()
        | Some durable, Some request ->
            match
                BloggerRecoveryProbe.terminalRequestOwnershipForPhysicalMessage
                    durable
                    context.Turn.SessionId
                    request
                    context.Turn.PhysicalUserMessageId
            with
            | BloggerTerminalRequestOwnership.Superseded ->
                Diagnostic.emit
                    "blogger-protocol-repair-superseded"
                    [ "session_id", SessionId.value context.Turn.SessionId
                      "result", "terminal belongs to an older Blogger request" ]

                AsyncSupport.completedTask ()
            | BloggerTerminalRequestOwnership.Current
            | BloggerTerminalRequestOwnership.Unproven ->
                repairOwnedBloggerProtocol host quiescence context sessionPort rootWorkspace eventPort durable request

    /// CTX-010 recovery continue owns the physical run until its own terminal is
    /// published. Missing-final-report / interaction-repair on that run hijacks the
    /// recovery slot: the interleaved idle reads finish=None (Unknown) or a
    /// provisional NeedsContinuation while the probe response is still on the wire,
    /// and a fresh SessionIdle of the *same* provider attempt mints a valid
    /// quiescence permit (BeginProviderAttempt already ran for the probe itself).
    /// Stale-permit gating cannot suppress that race — the permit is not stale.
    ///
    /// The durable fact is the authority ledger: this PhysicalUserMessageId was
    /// accepted as `ProviderRetryAttempt`. That is the recovery continue's identity,
    /// not a runtime whitelist and not a substitute for HOST-004 on ordinary mains.
    let private isRecoveryContinue (journal: AgentJournal option) (turn: ReconciledTurn) : bool =
        continuationKindOf journal turn = Some PromptAuthority.ContinuationKind.ProviderRetryAttempt

    let private isFissionReplaced (journal: AgentJournal option) (sessionId: SessionId) : bool =
        FissionRuntime.isSilentInterrupt sessionId
        || (journal
            |> Option.exists (fun durable ->
                FissionProjection.tryActiveForOwner sessionId (AgentJournal.snapshot durable).AgentProjections.Fission
                |> Option.isSome))

    /// GLORY-070 / HOST-004 rev.4: a stable idle that never produced a final
    /// report is reminded once per exact terminal occasion, and only when the
    /// pass carried idle evidence. If the reminder itself reaches another invalid
    /// terminal, that fresh occasion may remind again until the closing-report
    /// gate is satisfied. ProviderRetryAttempt
    /// continues own the recovery slot — suppress missing-final-report so the
    /// probe's own terminal can promote.
    let repairMissingFinalReport
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        : Task =
        if
            isFissionReplaced journal context.Turn.SessionId
            || isRecoveryContinue journal context.Turn
        then
            AsyncSupport.completedTask ()
        else
            repairDefect
                quiescence
                context
                sessionPort
                rootWorkspace
                eventPort
                journal
                (ProviderProse.documentFor context.Turn.SessionId RuntimeNudge.MissingClosingReport Map.empty)
                "missing-final-report"

    /// Incomplete in-progress interaction: classify then idle-repair, unless a
    /// ProviderRetryAttempt continue owns the recovery slot.
    let repairIncompleteInteraction
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        : Task =
        let turn = context.Turn

        if isFissionReplaced journal turn.SessionId || isRecoveryContinue journal turn then
            AsyncSupport.completedTask ()
        elif CompletedTurnClassifier.needsInteractionRepair turn.Role (box turn.Outcome) turn.Parts then
            repairDefect
                quiescence
                context
                sessionPort
                rootWorkspace
                eventPort
                journal
                (ProviderProse.documentFor turn.SessionId RuntimeNudge.InteractionContinue Map.empty)
                "interaction-repair"
        else
            AsyncSupport.completedTask ()
