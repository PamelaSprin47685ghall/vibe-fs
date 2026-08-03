namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.Process
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
        (managerGuardNudges: HashSet<string>)
        (sessionParents: Dictionary<string, string>)
        (disposeExecutorRuntime: string -> unit)
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
        // from the child's AgentRole invented tier Fast, so a `deep-coder` child
        // acquired a root naming `fast-coder` and FALLBACK-002's A/B pair was wrong
        // for the whole Logical Run.
        //
        // A session known only through the in-memory `sessionParents` map has no
        // durable record and therefore no defensible agent name. It is skipped rather
        // than registered from a guess: the turn still completes through its
        // reconciled AgentRole, and the missing authority stays visibly missing.
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

        let completeReviewerOrAssistant (forceConfirmedReviewer: bool) =
            let wasAborted, sessionWide = cleanAbortAndAccumulate ()

            let sessionWideText =
                if not (String.IsNullOrWhiteSpace sessionWide) then
                    sessionWide
                elif forceConfirmedReviewer then
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
                match turn.AgentRole with
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
            turn.AgentRole = Some AgentRole.Reviewer
            && ReviewerGuardState.isConfirmedReviewer journal sessionKey

        match turn.Outcome with
        | TurnUnknown -> AsyncSupport.completedTask ()
        | TurnInProgress when reviewerAlreadyConfirmed ->
            // A second PERFECT is frequently a tool-only provider step. Once the
            // witness is Confirmed, finish the physical reviewer run so
            // OrchestratorHost.reverify and Manager `join` observe completion.
            completeReviewerOrAssistant true |> ignore
            AsyncSupport.completedTask ()
        | TurnInProgress ->
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
                match turn.AgentRole with
                | Some AgentRole.Manager ->
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
            elif CompletedTurnClassifier.needsZeroWidthContinuation turn.AgentRole turn.Outcome turn.Parts then
                sendRepair sessionPort eventPort journal turn "\u200B" "zero-width"
            else
                AsyncSupport.completedTask ()
        | TurnNeedsContinuation _ when reviewerAlreadyConfirmed ->
            completeReviewerOrAssistant true |> ignore
            AsyncSupport.completedTask ()
        | TurnNeedsContinuation _ ->
            // Absorb text and reasoning into the XTrace even though this turn is
            // not completable, then ask for the missing report. Still not fallback.
            // (The XTrace parts are captured at the transform boundary.)
            sendRepair sessionPort eventPort journal turn RuntimeNudge.missingFinalReport "missing-final-report"
        | TurnAborted reason ->
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
                Pty.abortParent sessionKey
                sessionPort.AbortChildren turn.SessionId |> ignore

                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Aborted reason)
                |> ignore

                AsyncSupport.completedTask ()
        | TurnFailed error -> continueAfterOrdinaryFailure sessionPort eventPort journal turn error
        | TurnCompleted ->
            // REVIEW-003/007 synchronize before completion: a reviewer awaiting
            // PERFECT confirmation and a manager whose current tree lacks a witness
            // are still running. Reporting Completed here lets join/AwaitManager return
            // before confirmation, racing publish and worktree release.
            let managerGuard =
                match turn.AgentRole with
                | Some AgentRole.Manager when TerminalPolicy.isTopLevelManager sessionParents journal sessionKey ->
                    Some(HostReviewGuard.missingTree journal gitTreePort sessionKey)
                | _ -> None

            let completionDeferred =
                match managerGuard with
                | Some(HostReviewGuard.ReviewGuardMissing _)
                | Some(HostReviewGuard.ReviewGuardUnavailable _) -> true
                | _ ->
                    turn.AgentRole = Some AgentRole.Reviewer
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
            else
                match turn.AgentRole with
                // REVIEW-003: a first PERFECT must enter its causal confirmation
                // round-trip before the generic missing-verdict branch.
                // `verdictSessions` is terminal bookkeeping only and must never
                // suppress the pending confirmation transition.
                | Some AgentRole.Reviewer when ReviewerGuardState.pendingConfirmation journal sessionKey ->
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
                | Some AgentRole.Reviewer when
                    not (verdictSessions.Remove sessionKey)
                    && not (ReviewerGuardState.submitted journal sessionKey)
                    ->
                    task {
                        let! _ =
                            HostReviewGuard.nudgeReviewer sessionPort journal nudgeSent turn.SessionId turn.ProviderRun

                        ()
                    }
                    :> Task
                | Some AgentRole.Manager when TerminalPolicy.isTopLevelManager sessionParents journal sessionKey ->
                    match managerGuard with
                    | Some(HostReviewGuard.ReviewGuardMissing treeHash) ->
                        task {
                            // Same deferred-completion fail closed as the reviewer branch.
                            let! outcome =
                                HostReviewGuard.nudgeManager
                                    sessionPort
                                    journal
                                    managerGuardNudges
                                    turn.SessionId
                                    treeHash

                            match outcome with
                            | HostReviewGuard.GuardNudgeOutcome.NoLongerRequired ->
                                completeReviewerOrAssistant false |> ignore
                            | HostReviewGuard.GuardNudgeOutcome.Failed reason ->
                                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed reason)
                                |> ignore
                            | _ -> ()
                        }
                        :> Task
                    | Some(HostReviewGuard.ReviewGuardUnavailable reason) ->
                        // ORCH-008 / REVIEW-007 fail closed: an unavailable guard must not
                        // let a Manager finish unreviewed. Reported as a terminal failure
                        // rather than raised, because raising here escapes into whichever
                        // Host callback happens to be on the stack.
                        eventPort.NotifyTerminal
                            turn.SessionId
                            (TerminalOutcome.Failed(sprintf "Review guard unavailable: %s" reason))
                        |> ignore

                        AsyncSupport.completedTask ()
                    | _ -> AsyncSupport.completedTask ()
                | _ -> AsyncSupport.completedTask ()
