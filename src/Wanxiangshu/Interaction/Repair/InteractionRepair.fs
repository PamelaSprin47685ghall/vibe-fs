namespace Wanxiangshu.Interaction.Repair

open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Host
open Wanxiangshu.Resources
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// Idle-derived interaction repair (missing-final-report / incomplete interaction).
/// HOST-004 admission: no idle permit, or a permit that no longer holds at send
/// time → zero physical prompt, zero claim, zero terminal.
module InteractionRepairWorkflow =

    /// Generic repair is bounded by LogicalRun + repair family, gated on a fresh
    /// idle permit (HOST-004). A repair response is still part of the same logical
    /// run, so a new ProviderRunIdentity must not mint the same nudge again.
    ///
    /// The task is awaited rather than discarded. `|> ignore` on the task also
    /// discarded the claim/abandon bookkeeping inside it, so a failed repair left
    /// a Claimed fact with nothing after it and no terminal for the caller.
    ///
    /// `Superseded` (stale permit) is not a failure: nothing was claimed, nothing
    /// was sent — the system is doing something fresher.
    let private sendRepair
        (quiescence: SessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (prompt: string)
        (repairKind: string)
        : Task =
        task {
            let! outcome =
                HostSessionNudge.trySendIdleRepairFamily
                    quiescence
                    permit
                    sessionPort
                    turn.SessionId
                    prompt
                    turn.Directory
                    journal
                    repairKind

            match outcome with
            | HostSessionNudge.IdleRepairFamilyOutcome.Sent _
            | HostSessionNudge.IdleRepairFamilyOutcome.Superseded
            | HostSessionNudge.IdleRepairFamilyOutcome.Retired -> ()
            | HostSessionNudge.IdleRepairFamilyOutcome.BudgetExhausted ->
                // The one bounded repair already ran and this LogicalRun is still
                // unusable. This is now a proved recovery exhaustion, not another
                // invitation to synthesize user input.
                eventPort.NotifyTerminal
                    turn.SessionId
                    (TerminalOutcome.Failed(
                        TerminalStop.forAuthority turn.AuthorityRootUserMessageId "INTERACTION_REPAIR_EXHAUSTED"
                    ))
                |> ignore
            | HostSessionNudge.IdleRepairFamilyOutcome.Failed error ->
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
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (prompt: string)
        (repairKind: string)
        : Task =
        match context.Quiescence with
        | None -> AsyncSupport.completedTask ()
        | Some permit -> sendRepair quiescence permit sessionPort eventPort journal context.Turn prompt repairKind

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

    let private sendBloggerAabbAfterPermitConsumed
        (host: IBloggerRuntimeHost)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal)
        (context: ReconciledTurnContext)
        (requestId: BloggerRequestId)
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
                            turn.SessionId
                            EnforcerRepair.RepairInstruction
                            turn.Directory
                            (Some journal)
                            requestId
                            turn.ProviderRun
                            BloggerRecoveryProbe.BloggerAabbRepairKind
                    with
                    | Ok _ -> ()
                    | Error error when error.IndexOf("already claimed", System.StringComparison.OrdinalIgnoreCase) >= 0 ->
                        ()
                    | Error error -> notifyBloggerProtocolFailure eventPort turn ("blogger AABB send failed: " + error)
                }

            let fallbackStillOpen () =
                FallbackEvidence.mayContinue
                    AgentPairCursor.DefaultAutoRecoveryBudget
                    turn.SessionId
                    (AgentJournal.snapshot journal)

            match!
                FallbackLedger.recordConfirmedFailure
                    journal
                    AgentPairCursor.DefaultAutoRecoveryBudget
                    turn.SessionId
                    turn.ProviderRun
                    reason
            with
            | Error error -> notifyBloggerProtocolFailure eventPort turn error
            | Ok ConfirmedFailureOutcome.NoActiveRun ->
                notifyBloggerProtocolFailure eventPort turn "blogger AABB has no active logical run"
            | Ok ConfirmedFailureOutcome.RecoveryExhausted when guaranteedFirstAabb ->
                // The nudge failure has already earned one protocol AABB attempt.
                // A generic fallback boundary reached by this same failure may not
                // retroactively steal that first send.
                do! sendAabb ()
            | Ok ConfirmedFailureOutcome.RecoveryExhausted ->
                do! exhaustBloggerProtocol host eventPort journal context "blogger protocol repair exhausted"
            | Ok ConfirmedFailureOutcome.AlreadyRecorded when guaranteedFirstAabb -> do! sendAabb ()
            | Ok ConfirmedFailureOutcome.AlreadyRecorded when fallbackStillOpen () ->
                // A racing observer may have advanced this exact terminal before
                // the request-scoped AABB claim became visible. The claim itself
                // dedupes the physical send.
                do! sendAabb ()
            | Ok ConfirmedFailureOutcome.AlreadyRecorded ->
                do! exhaustBloggerProtocol host eventPort journal context "blogger protocol repair exhausted"
            | Ok(ConfirmedFailureOutcome.RecoveryAdvanced _) -> do! sendAabb ()
        }
        :> Task

    let private consumeThenSendBloggerAabb
        (host: IBloggerRuntimeHost)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal)
        (requestId: BloggerRequestId)
        (guaranteedFirstAabb: bool)
        (reason: string)
        : Task =
        match context.Quiescence with
        | Some permit when quiescence.TryConsume permit ->
            sendBloggerAabbAfterPermitConsumed
                host
                sessionPort
                eventPort
                journal
                context
                requestId
                guaranteedFirstAabb
                reason
        | _ -> AsyncSupport.completedTask ()

    let private sendBloggerNudge
        (host: IBloggerRuntimeHost)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal)
        (requestId: BloggerRequestId)
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
                        context.Turn.SessionId
                        EnforcerRepair.RepairInstruction
                        context.Turn.Directory
                        (Some journal)
                        requestId
                        context.Turn.ProviderRun
                        BloggerRecoveryProbe.BloggerMissingToolRepairKind
                with
                | HostSessionNudge.IdleContinuationOutcome.Sent _
                | HostSessionNudge.IdleContinuationOutcome.Superseded -> ()
                | HostSessionNudge.IdleContinuationOutcome.Failed error ->
                    do!
                        sendBloggerAabbAfterPermitConsumed
                            host
                            sessionPort
                            eventPort
                            journal
                            context
                            requestId
                            true
                            ("blogger nudge failed: " + error)
        }
        :> Task

    let private repairOwnedBloggerProtocol
        (host: IBloggerRuntimeHost)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (durable: AgentJournal)
        (request: BloggerRequestContext)
        : Task =
        let requestId = BloggerRequestContext.requestId request

        match
            BloggerRecoveryProbe.repairStateForInvalidTerminal
                durable
                context.Turn.SessionId
                requestId
                context.Turn.ProviderRun
        with
        | BloggerRecoveryProbe.InvalidTerminalRepairState.NoRecovery ->
            sendBloggerNudge host quiescence context sessionPort eventPort durable requestId
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
                eventPort
                durable
                requestId
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
                eventPort
                durable
                requestId
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
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
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
            repairOwnedBloggerProtocol host quiescence context sessionPort eventPort durable request

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
        match journal with
        | None -> false
        | Some durable ->
            AgentProjection.tryFind turn.SessionId (AgentJournal.snapshot durable).AgentProjections
            |> Option.bind (fun session -> session.PromptAuthority)
            |> Option.exists (fun authority ->
                authority.AcceptedContinuationIds
                |> Map.tryFind turn.PhysicalUserMessageId
                |> Option.exists (fun kind -> kind = PromptAuthority.ContinuationKind.ProviderRetryAttempt))

    let private isFissionReplaced (journal: AgentJournal option) (sessionId: SessionId) : bool =
        FissionRuntime.isSilentInterrupt sessionId
        || (journal
            |> Option.exists (fun durable ->
                FissionProjection.tryActiveForOwner sessionId (AgentJournal.snapshot durable).AgentProjections.Fission
                |> Option.isSome))

    /// GLORY-070 / HOST-004 rev.3: a stable idle that never produced a final
    /// report is repaired exactly once (reconcile maps dedupe the turn token),
    /// and only when the pass carried idle evidence. ProviderRetryAttempt
    /// continues own the recovery slot — suppress missing-final-report so the
    /// probe's own terminal can promote.
    let repairMissingFinalReport
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        : Task =
        if
            isFissionReplaced journal context.Turn.SessionId
            || isRecoveryContinue journal context.Turn
        then
            AsyncSupport.completedTask ()
        else
            trySendIdleRepair
                quiescence
                context
                sessionPort
                eventPort
                journal
                (ProviderProse.documentFor context.Turn.SessionId RuntimeNudge.MissingClosingReport Map.empty)
                "missing-final-report"

    /// Incomplete in-progress interaction: classify then idle-repair, unless a
    /// ProviderRetryAttempt continue owns the recovery slot.
    let repairIncompleteInteraction
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        : Task =
        let turn = context.Turn

        if isFissionReplaced journal turn.SessionId || isRecoveryContinue journal turn then
            AsyncSupport.completedTask ()
        elif CompletedTurnClassifier.needsInteractionRepair turn.Role (box turn.Outcome) turn.Parts then
            trySendIdleRepair
                quiescence
                context
                sessionPort
                eventPort
                journal
                (ProviderProse.documentFor turn.SessionId RuntimeNudge.InteractionContinue Map.empty)
                "interaction-repair"
        else
            AsyncSupport.completedTask ()
