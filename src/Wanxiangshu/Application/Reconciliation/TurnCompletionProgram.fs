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

/// The one production path that turns a reconciled turn into side effects
/// (NotifyTerminal, dispose runtime, nudges, fallback advance).
module TurnCompletionProgram =

    /// FALLBACK-008: one repair per unusable terminal.
    ///
    /// The task is awaited rather than discarded. `|> ignore` on the task also
    /// discarded the claim/abandon bookkeeping inside it, so a failed repair left
    /// a Claimed fact with nothing after it and no terminal for the caller.
    let private sendRepair
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (prompt: string)
        (repairKind: string)
        : Task =
        task {
            let! sent =
                HostSessionNudge.trySendInteractionRepair
                    sessionPort
                    turn.SessionId
                    prompt
                    turn.Directory
                    journal
                    turn.ProviderRun
                    repairKind

            match sent with
            | Ok _ -> ()
            | Error _ ->
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "MISSING_FINAL_REPORT")
                |> ignore
        }
        :> Task

    /// CTX-012: is this run the recovery probe — a physical message accepted as
    /// a ProviderRetryAttempt continuation?
    ///
    /// The probe's terminal is often still on the wire when an interleaved
    /// reconcile pass reads the run as Unknown (finish=None), and the bounded
    /// rereads are synchronous, so RepairMissingFinalReport fires before the
    /// response lands. Repairing hijacks the probe run and the TurnCompleted
    /// promote never fires (measured: FallbackCursorAdvanced + ProviderRetryAttempt
    /// with PrefixRebaseCommitted=0 in x-c/x-d). The probe turn's own idle
    /// re-reconciles it as TurnCompleted.
    let private isRecoveryProbeRun (journal: AgentJournal option) (turn: ReconciledTurn) : bool =
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
    let private awaitCoverageBeforeRetry (durable: AgentJournal) (sessionId: SessionId) : Task =
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
                                    (AgentJournal.awaitChangeFrom fromRev durable,
                                     Wanxiangshu.Process.PtyTiming.timerTask sliceMs)
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
                    do! awaitCoverageBeforeRetry durable turn.SessionId

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
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        : Task =
        continueAfterProviderFailure sessionPort eventPort journal turn "loop-kill" RuntimeNudge.loopContinue

    let private continueAfterOrdinaryFailure
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (error: string)
        : Task =
        continueAfterProviderFailure sessionPort eventPort journal turn error RuntimeNudge.providerRetry


    /// Apply the full terminal completion program for a reconciled turn.
    let applyWithContinuation
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (gitTreePort: GitTreePort option)
        (verdictSessions: HashSet<string>)
        (nudgeSent: HashSet<string>)
        (joinGuardNudges: HashSet<string>)
        (sessionParents: Dictionary<string, string>)
        (disposeExecutorRuntime: string -> unit)
        (hasLivePty: string -> bool)
        (abortedSessions: HashSet<string>)
        (loopSensor: LoopSensor option)
        (turn: ReconciledTurn)
        : Task =
        let sessionKey = SessionId.value turn.SessionId
        disposeExecutorRuntime sessionKey

        // EXEC-009 + PROMPT-008: a reconciled linked child has a host-proven physical
        // user message even when the Host omitted agent metadata from `chat.message`,
        // so its AgentOwner Root can be registered here. The managed agent name comes
        // from the durable `HandleLinked.TargetAgent` and nowhere else — rebuilding it
        // from the child's Role invented tier Fast, so a `deep-coder` child
        // acquired a root naming `fast-coder` and FALLBACK-002's A/B pair was wrong
        // for the whole Logical Run.
        //
        // A session known only through the in-memory `sessionParents` map has no
        // durable record and therefore no defensible agent name. It is skipped rather
        // than registered from a guess: the turn still completes through its
        // reconciled Role, and the missing authority stays visibly missing.
        TerminalPolicy.tryLinkedChild journal sessionKey
        |> Option.iter (fun record ->
            HostSessionNudge.ensureAgentOwnerAuthority
                journal
                turn.SessionId
                turn.PhysicalUserMessageId
                record.TargetAgent
            |> ignore)

        let cleanAbortAndAccumulate () =
            let wasAborted = abortedSessions.Contains sessionKey
            abortedSessions.Remove sessionKey |> ignore

            // COMPANION-003: the terminal text is this turn's formal text + host-
            // visible reasoning — the XTrace terminal segment, not a parallel
            // accumulation channel (HOST-005). XTrace parts themselves are
            // captured at the transform boundary (`XTraceCapture.captureProjection`).
            let sessionWide = CompletedTurnClassifier.partsSessionText turn.Parts

            wasAborted, sessionWide

        let completeReviewerOrAssistant (confirmedReviewerEmptyTextFallback: bool) =
            let wasAborted, sessionWide = cleanAbortAndAccumulate ()

            let sessionWideText =
                if not (String.IsNullOrWhiteSpace sessionWide) then
                    sessionWide
                elif confirmedReviewerEmptyTextFallback then
                    // A confirmed double-PERFECT often ends on a tool-only frame.
                    // The witness is already Confirmed, so expose a minimal A rather
                    // than failing a review that actually succeeded.
                    "Review confirmed."
                else
                    sessionWide

            // REVIEW-006: nothing is written here. Confirmation is a fact
            // ReviewController already journalled from the seal evidence, so the
            // completion path only reports the run. The previous code wrote its own
            // confirmation fact keyed by the confirmation prompt's physical message
            // id, which is REVIEW-003's forbidden physical-message match wearing a
            // different name.
            // PROMPT-008: the Role comes from the reconciled turn, and there is no
            // default. Defaulting to Coder — as the previous `"coder"` string did —
            // reports a completion under a role nobody selected.
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

                    // EXEC-006: `IsValid` is the single place that decides whether a
                    // completed run carries terminal output. Re-testing the text here
                    // would be a second copy of that rule.
                    if runResult.IsValid then
                        // COMPANION-003: the terminal output becomes the XTrace's
                        // final segment. Idempotent (PERSIST-010).
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

        let reviewerAlreadyConfirmed =
            turn.Role = Some Role.Reviewer
            && ReviewerGuardState.isConfirmedReviewer journal sessionKey

        match turn.Outcome with
        | ReconcileProgram.TurnUnknown ->
            // GLORY-070: a stable idle that never produced a final report is
            // repaired exactly once (the reconcile maps dedupe the turn token);
            // it must never silently stop.
            //
            // Exception: the recovery probe run. Its terminal is still on the
            // wire when an interleaved pass reads it as Unknown; repairing it
            // hijacks the probe and the TurnCompleted promote never fires. The
            // probe turn's own idle re-reconciles it as TurnCompleted.
            if isRecoveryProbeRun journal turn then
                AsyncSupport.completedTask ()
            else
                sendRepair sessionPort eventPort journal turn RuntimeNudge.missingFinalReport "missing-final-report"
        | ReconcileProgram.TurnInProgress when reviewerAlreadyConfirmed ->
            // A second PERFECT is frequently a tool-only provider step. Once the
            // witness is Confirmed, finish the physical reviewer run so
            // OrchestratorHost.reverify and Manager `join` observe completion.
            completeReviewerOrAssistant true |> ignore
            AsyncSupport.completedTask ()
        | ReconcileProgram.TurnInProgress ->
            // A Manager whose job the Orchestrator has already taken over must not
            // keep its tool loop running. The worktree is released once the job
            // lands (ORCH-006), and a further provider request from the manager
            // family would load Host instructions from the deleted directory,
            // truncating the system prompt and breaking the ARCH-004 seal
            // (measured: seal-undeclared in orchestrator-publish under
            // concurrency — the guard-round join raced the release, which happens
            // when the Orchestrator's own review finishes while the Manager is
            // still mid-guard-loop). Once the job has left ManagerStarted the
            // Orchestrator's barrier owns the review; the guard round's residual
            // tool loop has no work left and must not continue.
            let managerJobHandedOff =
                match turn.Role with
                | Some Role.Manager ->
                    match journal with
                    | Some durable ->
                        let snapshot = AgentJournal.snapshot durable

                        OrchestratorProjection.tryFindByManagerSession
                            turn.SessionId
                            snapshot.AgentProjections.Orchestrator
                        |> Option.exists (fun job ->
                            match job.Progress with
                            | JobProgress.ManagerStarted
                            | JobProgress.ConflictPending _ -> false
                            | JobProgress.CandidateReady _
                            | JobProgress.RebasedCandidateReady _
                            | JobProgress.PublishClaimed _
                            | JobProgress.Published _
                            | JobProgress.Failed _
                            | JobProgress.Abandoned -> true)
                    | None -> false
                | _ -> false

            if managerJobHandedOff then
                completeReviewerOrAssistant false |> ignore
                AsyncSupport.completedTask ()
            elif CompletedTurnClassifier.needsInteractionRepair turn.Role turn.Outcome turn.Parts then
                sendRepair
                    sessionPort
                    eventPort
                    journal
                    turn
                    RuntimeNudge.interactionRepairContinue
                    "interaction-repair"
            else
                AsyncSupport.completedTask ()
        | ReconcileProgram.TurnNeedsContinuation _ when reviewerAlreadyConfirmed ->
            completeReviewerOrAssistant true |> ignore
            AsyncSupport.completedTask ()
        | ReconcileProgram.TurnNeedsContinuation _ ->
            // Absorb text and reasoning into the XTrace even though this turn is
            // not completable, then ask for the missing report. Still not fallback.
            // (The XTrace parts are captured at the transform boundary.)
            //
            // Same recovery-probe exception as TurnUnknown: the probe's terminal
            // may still be on the wire; repairing would hijack the promote.
            if isRecoveryProbeRun journal turn then
                AsyncSupport.completedTask ()
            else
                sendRepair sessionPort eventPort journal turn RuntimeNudge.missingFinalReport "missing-final-report"
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
                continueAfterLoopKill sessionPort eventPort journal turn
            else
                abortedSessions.Add sessionKey |> ignore
                Wanxiangshu.Process.Pty.abortParent sessionKey
                sessionPort.AbortChildren turn.SessionId |> ignore

                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Aborted reason)
                |> ignore

                AsyncSupport.completedTask ()
        | ReconcileProgram.TurnFailed error -> continueAfterOrdinaryFailure sessionPort eventPort journal turn error
        | ReconcileProgram.TurnCompleted ->
            // EXEC-016 first: join-capable roles with outstanding background work
            // must join before any completion decision.
            let joinOutstanding =
                TerminalPolicy.outstandingBackground journal hasLivePty turn.Role turn.SessionId

            // GLORY-018: a legal planning terminal does not complete the Manager.
            // The Host sends exactly one ManagerWorkActivation continuation
            // instead; the completion stays deferred until the Life is activated.
            let managerPlanning =
                match turn.Role with
                | Some Role.Manager -> ManagerLifecycleGate.shouldActivate journal turn
                | _ -> false

            // GLORY-041: a suicide was accepted — the Manager's completion is
            // parked until the Finality workflow lands. Neither this turn's prose
            // nor any later text may become the terminal: last_words is the only
            // candidate (GLORY-061).
            let finalityOutstanding =
                match turn.Role with
                | Some Role.Manager ->
                    match journal with
                    | Some durable ->
                        AgentProjection.tryFind turn.SessionId (AgentJournal.snapshot durable).AgentProjections
                        |> Option.bind (fun session -> session.ManagerLife)
                        |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
                        |> Option.exists (fun life ->
                            match life.ActiveFinality with
                            | Some request -> ManagerLifecycleProjection.isOpen request
                            | None -> false)
                    | None -> false
                | _ -> false

            // ORCH-006: once the Orchestrator has taken the job over, the Manager
            // has no work left and its run may complete (the Orchestrator's
            // barrier owns the review). ConflictPending is still Orchestrator-
            // owned work: ResumeManager is driving a conflict-resolution turn
            // that MUST NotifyTerminal so finalizeWorktree can stage+continue
            // (measured: ConflictPending → idle-only → REBASE_HEAD stuck).
            let managerJobHandedOff =
                match turn.Role with
                | Some Role.Manager ->
                    match journal with
                    | Some durable ->
                        let snapshot = AgentJournal.snapshot durable

                        OrchestratorProjection.tryFindByManagerSession
                            turn.SessionId
                            snapshot.AgentProjections.Orchestrator
                        |> Option.exists (fun job ->
                            match job.Progress with
                            | JobProgress.ManagerStarted -> false
                            | JobProgress.ConflictPending _
                            | JobProgress.CandidateReady _
                            | JobProgress.RebasedCandidateReady _
                            | JobProgress.PublishClaimed _
                            | JobProgress.Published _
                            | JobProgress.Failed _
                            | JobProgress.Abandoned -> true)
                    | None -> false
                | _ -> false

            // GLORY-070: an active Manager (any Life state except completed)
            // keeps working until suicide. Its completion is deferred and the
            // ordinary idle earns GLORY-029's encouragement — never a review guard.
            let managerShouldContinue =
                match turn.Role with
                | Some Role.Manager -> not managerJobHandedOff
                | _ -> false

            let completionDeferred =
                // Orchestrator-owned job states must NotifyTerminal so ResumeManager
                // can finalizeWorktree (ConflictPending rebase continue). Do not defer
                // on joinOutstanding here — a race where join is still retiring would
                // leave Host pending open forever (measured: conflict-resume.2 on wire,
                // coder resolved, REBASE_HEAD unmerged, no manager HandleCompleted).
                if managerJobHandedOff then
                    false
                elif joinOutstanding then
                    true
                elif finalityOutstanding then
                    true
                elif managerPlanning then
                    true
                elif managerShouldContinue then
                    true
                else
                    turn.Role = Some Role.Reviewer
                    && not reviewerAlreadyConfirmed
                    && ReviewerGuardState.pendingConfirmation journal sessionKey

            let wasAborted, terminalValid =
                if completionDeferred then
                    let aborted, _ = cleanAbortAndAccumulate ()
                    aborted, false
                else
                    completeReviewerOrAssistant reviewerAlreadyConfirmed

            if terminalValid then
                AgentJournal.recordDerivedFallbackSuccess journal turn.SessionId

            if wasAborted || TerminalPolicy.sessionDead journal turn.SessionId then
                AsyncSupport.completedTask ()
            elif joinOutstanding then
                task {
                    let! outcome = HostJoinGuard.nudge sessionPort journal joinGuardNudges turn.SessionId turn.Directory

                    match outcome with
                    | HostJoinGuard.JoinGuardNudgeOutcome.Failed reason ->
                        eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed reason)
                        |> ignore
                    | _ -> ()
                }
                :> Task
            elif finalityOutstanding then
                // GLORY-041: the Finality workflow owns the Manager's ending now;
                // no nudge, no completion, no prose-as-terminal.
                AsyncSupport.completedTask ()
            else
                match turn.Role with
                // REVIEW-003: a first PERFECT must enter its causal confirmation
                // round-trip before the generic missing-verdict branch.
                // `verdictSessions` is terminal bookkeeping only and must never
                // suppress the pending confirmation transition.
                | Some Role.Reviewer when ReviewerGuardState.pendingConfirmation journal sessionKey ->
                    verdictSessions.Remove sessionKey |> ignore

                    task {
                        // Completion is deferred on this turn; with no confirmation
                        // continuation in flight the run would wait forever — fail closed.
                        let! outcome =
                            HostReviewGuard.requestPerfectConfirmation
                                sessionPort
                                journal
                                nudgeSent
                                turn.SessionId
                                turn.ProviderRun

                        match outcome with
                        | HostReviewGuard.GuardNudgeOutcome.Failed reason ->
                            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed reason)
                            |> ignore
                        | _ -> ()
                    }
                    :> Task
                | Some Role.Reviewer when
                    not (verdictSessions.Remove sessionKey)
                    && not (ReviewerGuardState.submitted journal sessionKey)
                    ->
                    task {
                        let! _ =
                            HostReviewGuard.nudgeReviewer sessionPort journal nudgeSent turn.SessionId turn.ProviderRun

                        ()
                    }
                    :> Task
                | Some Role.Manager when managerPlanning ->
                    // GLORY-018/020: send the Activation continuation exactly once
                    // (deduped by the pending-claim gate inside shouldActivate).
                    // The Manager stays unfinished; the next turns are Labor.
                    task {
                        let! outcome =
                            HostSessionNudge.sendContinuationResult
                                sessionPort
                                turn.SessionId
                                ManagerLifecyclePrompt.WorkActivation
                                PromptAuthority.ContinuationKind.ManagerWorkActivation
                                turn.Directory
                                journal
                                PromptDispatcher.AwaitMode.Detached
                                None

                        match outcome with
                        | Error error ->
                            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed error) |> ignore
                        | Ok _ -> ()
                    }
                    :> Task
                | Some Role.Manager when managerJobHandedOff ->
                    // ORCH-006: the job is out of the Manager's hands; this run
                    // may complete so the Orchestrator's AwaitManager returns.
                    completeReviewerOrAssistant false |> ignore
                    AsyncSupport.completedTask ()
                | Some Role.Manager ->
                    // GLORY-029/070: ordinary Labor idle. The Manager is never
                    // reviewed by a guard; it continues until suicide. Exactly one
                    // encouragement per idle terminal (dedupe: pending claim + run).
                    let encouragementKey =
                        sprintf "manager-idle:%s:%s" sessionKey (ProviderRunIdentity.value turn.ProviderRun)

                    let idleAlreadyClaimed =
                        match journal with
                        | Some durable ->
                            AgentProjection.tryFind turn.SessionId (AgentJournal.snapshot durable).AgentProjections
                            |> Option.bind (fun session -> session.PromptAuthority)
                            |> Option.exists (fun authority ->
                                authority.PendingClaims
                                |> Map.exists (fun _ claim ->
                                    match claim.Origin with
                                    | PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.ManagerIdleEncouragement ->
                                        true
                                    | _ -> false))
                        | None -> false

                    if nudgeSent.Contains encouragementKey || idleAlreadyClaimed then
                        AsyncSupport.completedTask ()
                    else
                        nudgeSent.Add encouragementKey |> ignore

                        task {
                            let! outcome =
                                HostSessionNudge.sendContinuationResult
                                    sessionPort
                                    turn.SessionId
                                    ManagerLifecyclePrompt.IdleEncouragement
                                    PromptAuthority.ContinuationKind.ManagerIdleEncouragement
                                    turn.Directory
                                    journal
                                    PromptDispatcher.AwaitMode.Detached
                                    None

                            match outcome with
                            | Error error ->
                                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed error) |> ignore
                            | Ok _ -> ()
                        }
                        :> Task
                | _ -> AsyncSupport.completedTask ()
