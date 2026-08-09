namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.Session
open Wanxiangshu.Host

/// The one production path that turns a reconciled turn into side effects
/// (NotifyTerminal, dispose runtime, nudges, fallback advance).
module TurnCompletionProgram =

    /// FALLBACK-008: one repair per unusable terminal, gated on a fresh idle
    /// permit (HOST-004).
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
                HostSessionNudge.trySendIdleInteractionRepair
                    quiescence
                    permit
                    sessionPort
                    turn.SessionId
                    prompt
                    turn.Directory
                    journal
                    turn.ProviderRun
                    repairKind

            match outcome with
            | HostSessionNudge.IdleContinuationOutcome.Sent _ -> ()
            | HostSessionNudge.IdleContinuationOutcome.Superseded -> ()
            | HostSessionNudge.IdleContinuationOutcome.Failed _ ->
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "MISSING_FINAL_REPORT")
                |> ignore
        }
        :> Task

    /// HOST-004: the three idle-derived repair sends (missing-final-report ×2,
    /// interaction-repair) all funnel through one admission point. No idle
    /// permit in the context, or a permit that no longer holds at send time →
    /// zero physical prompt, zero claim, zero terminal.
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

    /// CTX-006 hasMaterial for X: BlogEntryCommitted coverage on the main session.
    let private sessionHasCoverage (durable: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot durable).AgentProjections
        |> Option.bind (fun state -> state.Blog)
        |> Option.map BlogProjection.hasCoverage
        |> Option.defaultValue false

    /// True when a Companion Blogger is linked — only then is waiting for coverage
    /// meaningful. Sessions without a blogger never grow coverage; waiting would
    /// only burn the A′ budget on a clock.
    let private expectsCoverage (durable: AgentJournal) (sessionId: SessionId) =
        SessionAssociationProjection.tryBloggerOf
            sessionId
            (AgentJournal.snapshot durable).AgentProjections.Associations
        |> Option.isSome

    /// Bound: after a confirmed failure, the next A′/B′ ProviderRetry is the only
    /// recovery slot that may probe (CTX-006/010). Blog frames often land a few
    /// tens of ms after the failed main turn's companion request — racing the
    /// continue send made hasMaterial=false, so AttemptPlanner skipped the probe
    /// and ClearRecovery burned the armed slot (measured: FallbackCursorAdvanced
    /// + ProviderRetryAttempt with PrefixRebaseCommitted=0).
    ///
    /// Wait on journal folds until coverage exists or the budget expires. Fail
    /// open: timeout still sends the ordinary main (CTX-011 no-candidate path).
    let private awaitCoverageBeforeRetry
        (sliceTimer: int -> Task<unit>)
        (durable: AgentJournal)
        (sessionId: SessionId)
        : Task =
        task {
            if not (expectsCoverage durable sessionId) || sessionHasCoverage durable sessionId then
                return ()
            else
                let budgetMs = 2000
                let sliceMs = 25
                let deadline = DateTimeOffset.UtcNow.AddMilliseconds(float budgetMs)

                let rec loop () =
                    task {
                        if sessionHasCoverage durable sessionId then
                            return ()
                        elif DateTimeOffset.UtcNow >= deadline then
                            return ()
                        else
                            let fromRev = AgentJournal.revision durable
                            // Fable Task has no WhenAny export; race journal wake vs slice.
                            do!
                                emitJsExpr
                                    (AgentJournal.awaitChangeFrom fromRev durable, sliceTimer sliceMs)
                                    "Promise.race([$0, $1]).then(function () { return undefined; })"
                                : Task

                            return! loop ()
                    }

                return! loop ()
        }

    /// FALLBACK-003 + FALLBACK-004: a settled failed turn.
    ///
    /// The reconciled snapshot is what proves the attempt failed (HOST-004), so
    /// this is where the cursor advances — not in the Host retry event handler,
    /// which only wakes. `FallbackController` is the single writer.
    ///
    /// FALLBACK-004 then decides whether a continuation follows: only when the
    /// budget still permits one. The continuation itself produces no second
    /// advance, which is why nothing here writes again.
    let private continueAfterProviderFailure
        (sliceTimer: int -> Task<unit>)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (error: string)
        (continuationPrompt: string)
        : Task =
        task {
            let fail reason =
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed reason)
                |> ignore

            match journal with
            | None -> fail error
            | Some durable ->
                match
                    FallbackController.recordConfirmedFailure
                        durable
                        AgentPairCursor.DefaultAutoRecoveryBudget
                        turn.SessionId
                        turn.ProviderRun
                        error
                with
                | Error reason -> fail reason
                | Ok outcome when not (FallbackController.mayContinue outcome) ->
                    // FALLBACK-005: budget spent, or no proven authority. Either way
                    // no further automatic physical request may be issued.
                    fail error
                | Ok _ ->
                    // CTX-006: give the linked Blogger a chance to commit coverage
                    // before the armed A′/B′ continue is planned (XWire.applyTransform).
                    do! awaitCoverageBeforeRetry sliceTimer durable turn.SessionId

                    let! continuation =
                        HostSessionNudge.sendContinuationResult
                            sessionPort
                            turn.SessionId
                            continuationPrompt
                            PromptAuthority.ProviderRetryAttempt
                            turn.Directory
                            journal
                            PromptDispatcher.AwaitMode.Detached
                            None

                    match continuation with
                    | Ok _ ->
                        if error = "loop-kill" then
                            Diagnostic.emit
                                "loop-kill"
                                [ "session_id", SessionId.value turn.SessionId; "result", "continue-sent" ]
                    | Error _ -> fail error
        }
        :> Task


    /// LOOP-006: an abort we armed is provider failure for AABB purposes.
    let private continueAfterLoopKill
        (sliceTimer: int -> Task<unit>)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        : Task =
        continueAfterProviderFailure
            sliceTimer
            sessionPort
            eventPort
            journal
            turn
            "loop-kill"
            RuntimeNudge.loopContinue

    let private continueAfterOrdinaryFailure
        (sliceTimer: int -> Task<unit>)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (error: string)
        : Task =
        continueAfterProviderFailure
            sliceTimer
            sessionPort
            eventPort
            journal
            turn
            error
            RuntimeNudge.providerRetry


    /// Physical cleanup and durable child-authority registration before routing.
    let prepareTurn (journal: AgentJournal option) (disposeExecutorRuntime: string -> unit) (turn: ReconciledTurn) =
        let sessionKey = SessionId.value turn.SessionId
        disposeExecutorRuntime sessionKey

        TerminalPolicy.tryLinkedChild journal sessionKey
        |> Option.iter (fun record ->
            HostSessionNudge.ensureAgentOwnerAuthority
                journal
                turn.SessionId
                turn.PhysicalUserMessageId
                record.TargetAgent
            |> ignore)

    /// Generic physical completion shared by bounded-context workflows.
    let completeAgent
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (abortedSessions: HashSet<string>)
        (turn: ReconciledTurn)
        =
        let sessionKey = SessionId.value turn.SessionId
        let wasAborted = abortedSessions.Contains sessionKey
        abortedSessions.Remove sessionKey |> ignore
        let sessionWideText = CompletedTurnClassifier.partsSessionText turn.Parts

        let terminalValid =
            match turn.Role with
            | None ->
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "completed with no resolved role")
                |> ignore

                false
            | Some role ->
                let runResult: AgentRunResult =
                    { SessionId = turn.SessionId
                      AuthorityRootUserMessageId = turn.AuthorityRootUserMessageId
                      ProviderRun = turn.ProviderRun
                      Role = AgentRoleIdentity.toRole role
                      Directory = turn.Directory
                      TerminalText = sessionWideText
                      TurnFormalText = CompletedTurnClassifier.partsText turn.Parts }

                if runResult.IsValid then
                    XTraceCapture.captureTerminal journal turn

                    eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Completed runResult)
                    |> ignore

                    true
                else
                    eventPort.NotifyTerminal
                        turn.SessionId
                        (TerminalOutcome.Failed "completed with empty terminal output")
                    |> ignore

                    false

        wasAborted, terminalValid

    /// Generic terminal plumbing for ordinary turns.
    /// `sliceTimer` and `abortParent` are injected by Host composition (Process is not Application).
    let applyWithContinuation
        (sliceTimer: int -> Task<unit>)
        (abortParent: string -> unit)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (abortedSessions: HashSet<string>)
        (loopSensor: LoopSensor option)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        : Task =
        let turn = context.Turn
        let sessionKey = SessionId.value turn.SessionId

        let completeAgent () =
            completeAgent eventPort journal abortedSessions turn

        match turn.Observation with
        | Some ReconcileProgram.TurnUnknown ->
            // GLORY-070 / HOST-004 rev.3: a stable idle that never produced a
            // final report is repaired exactly once (the reconcile maps dedupe
            // the turn token), and only when the pass carried idle evidence.
            // ProviderRetryAttempt continues own the recovery slot — suppress
            // missing-final-report so the probe's own terminal can promote.
            if isRecoveryContinue journal turn then
                AsyncSupport.completedTask ()
            else
                trySendIdleRepair
                    quiescence
                    context
                    sessionPort
                    eventPort
                    journal
                    RuntimeNudge.missingFinalReport
                    "missing-final-report"
        | None ->
            match turn.Outcome with
            | ReconcileProgram.TurnInProgress ->
                if isRecoveryContinue journal turn then
                    AsyncSupport.completedTask ()
                elif CompletedTurnClassifier.needsInteractionRepair turn.Role (box turn.Outcome) turn.Parts then
                    trySendIdleRepair
                        quiescence
                        context
                        sessionPort
                        eventPort
                        journal
                        RuntimeNudge.interactionRepairContinue
                        "interaction-repair"
                else
                    AsyncSupport.completedTask ()
            | ReconcileProgram.TurnNeedsContinuation _ ->
                // Absorb text and reasoning into the XTrace even though this turn is
                // not completable, then ask for the missing report. Still not fallback.
                // (The XTrace parts are captured at the transform boundary.)
                //
                // Same admission contract as SnapshotObservation.TurnUnknown, plus
                // recovery-continue ownership: do not hijack a ProviderRetryAttempt
                // with repair.
                if isRecoveryContinue journal turn then
                    AsyncSupport.completedTask ()
                else
                    trySendIdleRepair
                        quiescence
                        context
                        sessionPort
                        eventPort
                        journal
                        RuntimeNudge.missingFinalReport
                        "missing-final-report"
            | ReconcileProgram.TurnAborted reason ->
                // LOOP-006: our own kill is bridged into the provider-failure AABB path.
                // User / cleanup aborts still report Aborted and do not advance the cursor.
                let loopKill =
                    match loopSensor with
                    | Some sensor when sensor.IsArmed turn.SessionId ->
                        sensor.ClearArmed turn.SessionId
                        true
                    | _ -> false

                if loopKill then
                    continueAfterLoopKill sliceTimer sessionPort eventPort journal turn
                else
                    abortedSessions.Add sessionKey |> ignore
                    abortParent sessionKey
                    sessionPort.AbortChildren turn.SessionId |> ignore

                    eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Aborted reason)
                    |> ignore

                    AsyncSupport.completedTask ()
            | ReconcileProgram.TurnFailed error ->
                continueAfterOrdinaryFailure sliceTimer sessionPort eventPort journal turn error
            | ReconcileProgram.TurnCompleted ->
                let joinOutstanding =
                    TerminalPolicy.outstandingBackground journal hasLivePty turn.Role turn.SessionId

                let wasAborted, terminalValid =
                    if joinOutstanding then
                        let aborted = abortedSessions.Contains sessionKey

                        abortedSessions.Remove sessionKey |> ignore
                        aborted, false
                    else
                        completeAgent ()

                if terminalValid then
                    AgentJournal.recordDerivedFallbackSuccess journal turn.SessionId

                if wasAborted || TerminalPolicy.sessionDead journal turn.SessionId then
                    AsyncSupport.completedTask ()
                elif joinOutstanding then
                    task {
                        match! HostJoinGuard.nudge sessionPort journal joinGuardNudges turn.SessionId turn.Directory with
                        | HostJoinGuard.JoinGuardNudgeOutcome.Failed reason ->
                            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed reason)
                            |> ignore
                        | _ -> ()
                    }
                    :> Task
                else
                    AsyncSupport.completedTask ()
