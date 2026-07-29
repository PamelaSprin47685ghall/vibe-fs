namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

/// A single continuous program that sequences terminal-policy decisions with
/// ports.  It is the only production path that drives side effects
/// (NotifyTerminal, dispose runtime, nudges).
module TurnCompletionProgram =

    let private repairDirectory (turn: ReconciledTurn) =
        if String.IsNullOrWhiteSpace turn.Directory then
            None
        else
            Some turn.Directory

    let private sendRepair
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (continuationAccepted: SessionId -> MessageId -> unit)
        (turn: ReconciledTurn)
        (prompt: string)
        (repairKind: string)
        =
        let sent =
            HostSessionNudge.trySendInteractionRepair
                sessionPort
                turn.SessionId
                prompt
                (repairDirectory turn)
                journal
                turn.AssistantMessageId
                repairKind
                (Some(fun messageId -> continuationAccepted turn.SessionId messageId))

        if not sent then
            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "MISSING_FINAL_REPORT")
            |> ignore

    let private continueAfterProviderFailure
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (continuationAccepted: SessionId -> MessageId -> unit)
        (turn: ReconciledTurn)
        (error: string)
        =
        task {
            let! continuation =
                HostSessionNudge.sendContinuationResult
                    sessionPort
                    turn.SessionId
                    "Continue after provider failure."
                    PromptAuthority.ProviderRetryAttempt
                    (repairDirectory turn)
                    journal
                    (Some(fun messageId -> continuationAccepted turn.SessionId messageId))

            match continuation with
            | Ok _ -> ()
            | Error _ -> eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed error) |> ignore
        }
        |> ignore

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
        (continuationAccepted: SessionId -> MessageId -> unit)
        (_fallbackFailures: HashSet<string>)
        (turn: ReconciledTurn)
        =
        let sessionKey = SessionId.value turn.SessionId
        disposeExecutorRuntime sessionKey

        match turn.AgentRole with
        | Some agent when
            TerminalPolicy.isLinkedChild journal sessionKey
            || sessionParents.ContainsKey sessionKey
            ->
            HostSessionNudge.ensureAgentOwnerAuthority
                journal
                turn.SessionId
                turn.RootUserMessageId
                (AgentRoleIdentity.defaultFastManagedName agent)
        | _ -> ()

        let completeReviewerOrAssistant (forceConfirmedReviewer: bool) =
            let wasAborted = abortedSessions.Contains sessionKey
            abortedSessions.Remove sessionKey |> ignore

            let roleStr =
                turn.AgentRole
                |> Option.map (fun r -> r.ToString().ToLowerInvariant())
                |> Option.defaultValue "coder"

            // Session-wide A: append this turn's text + reasoning/thinking, then
            // expose the full Session accumulation. Empty intermediate turns do
            // not wipe prior A.
            let finalA = TerminalSessionA.accumulateTurn eventPort turn
            let formalText = CompletedTurnClassifier.partsText turn.Parts

            let finalText =
                if not (String.IsNullOrWhiteSpace finalA) then
                    finalA
                elif forceConfirmedReviewer then
                    // Dual-PERFECT confirmation often ends on a tool-only frame.
                    // The durable witness is already Confirmed; expose a minimal
                    // A so AwaitAgent can resolve without a prose stop frame.
                    "Review confirmed."
                else
                    finalA

            let runResult: AgentRunResult =
                { SessionId = turn.SessionId
                  RootUserMessageId = turn.RootUserMessageId
                  AssistantMessageId = turn.AssistantMessageId
                  Role = roleStr
                  Directory = turn.Directory
                  FinalText = finalText
                  FormalText = formalText }

            // A first PERFECT is not a child completion. Keep the reviewer's
            // physical handle live until the confirmation prompt causes a
            // distinct run that records the second PERFECT; otherwise Manager
            // `join()` can return before the ReviewWitness is confirmed.
            let awaitingReviewerConfirmation =
                turn.AgentRole = Some AgentRole.Reviewer
                && ReviewerGuardState.pendingConfirmation sessionParents journal sessionKey
                && not forceConfirmedReviewer

            // This path runs only after an idle signal reconciles a terminal
            // reviewer turn. A first PERFECT remains pending; only the reviewer
            // that supplied the confirmed double-PERFECT clears prior inputs.
            match
                turn.AgentRole,
                ReviewerGuardState.confirmedOwner sessionParents journal sessionKey (MessageId.value turn.UserMessageId),
                journal
            with
            | Some AgentRole.Reviewer, Some reviewOwner, Some j ->
                match AgentJournal.recordReviewConfirmedIdle j reviewOwner turn.SessionId turn.AssistantMessageId with
                | Ok() -> ()
                | Error err ->
                    raise (InvalidOperationException(sprintf "Failed to checkpoint confirmed reviewer idle: %s" err))
            | Some AgentRole.Reviewer, None, Some j when forceConfirmedReviewer ->
                match ReviewerGuardState.reviewOwner sessionParents journal sessionKey with
                | Some reviewOwner ->
                    match
                        AgentJournal.recordReviewConfirmedIdle j reviewOwner turn.SessionId turn.AssistantMessageId
                    with
                    | Ok() -> ()
                    | Error err ->
                        raise (
                            InvalidOperationException(sprintf "Failed to checkpoint confirmed reviewer idle: %s" err)
                        )
                | None -> ()
            | _ -> ()

            if not awaitingReviewerConfirmation then
                // Gate on session-wide A (text + reasoning). An empty intermediate
                // turn does not wipe prior A; only a Session with no A fails empty.
                if String.IsNullOrWhiteSpace runResult.FinalText then
                    eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "completed with empty final text")
                    |> ignore
                else
                    // Completion fact path only: ReconciledTurn → AgentRunResult → TerminalOutcome.
                    // FinalText is full Session A (incl. reasoning), not last-turn only.
                    eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Completed runResult)
                    |> ignore

            wasAborted

        let reviewerAlreadyConfirmed =
            turn.AgentRole = Some AgentRole.Reviewer
            && ReviewerGuardState.isConfirmedReviewer sessionParents journal sessionKey

        match turn.Outcome with
        | TurnUnknown -> ()
        | TurnInProgress when reviewerAlreadyConfirmed ->
            // Second PERFECT is frequently a tool-only provider step. Once the
            // durable witness is Confirmed, finish the physical reviewer run so
            // OrchestratorHost.reverify / Manager join can observe completion.
            completeReviewerOrAssistant true |> ignore
        | TurnInProgress ->
            // Host finished a provider step with tool-calls only. Idle means the
            // step is settled; interaction repair continues the Logical Run.
            // This is never durable provider fallback.
            if CompletedTurnClassifier.needsZeroWidthContinuation turn.AgentRole turn.Outcome turn.Parts then
                sendRepair sessionPort eventPort journal continuationAccepted turn "\u200B" "zero-width"
        | TurnNeedsContinuation _ when reviewerAlreadyConfirmed -> completeReviewerOrAssistant true |> ignore
        | TurnNeedsContinuation _ ->
            // Absorb text+reasoning into session-wide A even when this turn is
            // not yet completable (empty formal text / contains-XML formal text).
            TerminalSessionA.accumulateTurn eventPort turn |> ignore

            // No final natural-language report yet or length limit. Do NOT complete
            // the run. Interaction repair continues the same Logical Run; this is
            // never a durable fallback advance.
            let repairPrompt =
                "Your tool work is complete, but no final task report was produced. "
                + "Return a concise final report containing:\n"
                + "- result\n- evidence\n- files changed\n- tests run\n- remaining risks or blockers\n"
                + "Do not call another tool unless necessary."

            sendRepair sessionPort eventPort journal continuationAccepted turn repairPrompt "missing-final-report"
        | TurnAborted reason ->
            abortedSessions.Add sessionKey |> ignore
            Pty.abortParent sessionKey
            sessionPort.AbortChildren turn.SessionId |> ignore

            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Aborted reason)
            |> ignore
        | TurnFailed error ->
            // A settled failed turn continues the same Logical Run. The next
            // typed provider-retry signal, if emitted by the Host, is the only
            // durable fallback cursor advance; this continuation itself cannot
            // reset or mutate its A/A/B/B attempt profile.
            continueAfterProviderFailure sessionPort eventPort journal continuationAccepted turn error
        | TurnCompleted ->
            let wasAborted = completeReviewerOrAssistant reviewerAlreadyConfirmed

            if wasAborted then
                ()
            elif not (TerminalPolicy.sessionDead journal turn.SessionId) then
                match turn.AgentRole with
                // A first PERFECT must always enter its causal confirmation
                // round-trip before the generic "missing verdict" branch.
                // `verdictSessions` is only a terminal bookkeeping hint; it
                // must never suppress the pending confirmation transition.
                | Some AgentRole.Reviewer when ReviewerGuardState.pendingConfirmation sessionParents journal sessionKey ->
                    verdictSessions.Remove sessionKey |> ignore

                    HostReviewGuard.requestPerfectConfirmation
                        sessionPort
                        journal
                        nudgeSent
                        sessionKey
                        (MessageId.value turn.AssistantMessageId)
                        continuationAccepted
                | Some AgentRole.Reviewer when
                    not (verdictSessions.Remove sessionKey)
                    && not (ReviewerGuardState.submitted sessionParents journal sessionKey)
                    ->
                    HostReviewGuard.nudgeReviewer
                        sessionPort
                        journal
                        nudgeSent
                        sessionKey
                        (MessageId.value turn.AssistantMessageId)
                        continuationAccepted
                | Some AgentRole.Manager when TerminalPolicy.isTopLevelManager sessionParents journal sessionKey ->
                    match HostReviewGuard.missingTree journal gitTreePort sessionKey with
                    | HostReviewGuard.ReviewGuardMissing treeHash ->
                        HostReviewGuard.nudgeManager
                            sessionPort
                            journal
                            managerGuardNudges
                            sessionKey
                            (MessageId.value turn.AssistantMessageId)
                            treeHash
                            continuationAccepted
                    | HostReviewGuard.ReviewGuardConfirmed -> ()
                    | HostReviewGuard.ReviewGuardUnavailable reason ->
                        raise (InvalidOperationException(sprintf "Review guard unavailable: %s" reason))
                | _ -> ()

    /// Apply without a continuation-accepted callback (tests / simple callers).
    let apply
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
        (turn: ReconciledTurn)
        =
        applyWithContinuation
            sessionPort
            eventPort
            journal
            gitTreePort
            verdictSessions
            nudgeSent
            managerGuardNudges
            sessionParents
            disposeExecutorRuntime
            abortedSessions
            (fun _ _ -> ())
            (HashSet<string>())
            turn
