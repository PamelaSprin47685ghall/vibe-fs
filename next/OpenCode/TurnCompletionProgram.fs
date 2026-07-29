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

    let private roleName (role: AgentRole option) =
        role |> Option.map (fun value -> value.ToString().ToLowerInvariant())

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
        (fallbackFailures: HashSet<string>)
        (turn: ReconciledTurn)
        =
        let sessionKey = SessionId.value turn.SessionId
        disposeExecutorRuntime sessionKey

        match turn.AgentRole with
        | Some agent when
            TerminalPolicyHelpers.isLinkedChild journal sessionKey
            || sessionParents.ContainsKey sessionKey
            ->
            HostSessionNudge.ensureAgentOwnerAuthority
                journal
                turn.SessionId
                turn.RootUserMessageId
                (AgentRoleHelpers.defaultFastManagedName agent)
        | _ -> ()

        match turn.Outcome with
        | TurnUnknown -> ()
        | TurnInProgress ->
            // Tool-calls in progress: never complete the run. Send zero-width
            // continuation so the model continues its work.
            if CompletedTurnClassifier.needsZeroWidthContinuation turn.AgentRole turn.Outcome turn.Parts then
                let sent =
                    HostSessionNudge.trySendInteractionRepair
                        sessionPort
                        turn.SessionId
                        "\u200B"
                        (if String.IsNullOrWhiteSpace turn.Directory then
                             None
                         else
                             Some turn.Directory)
                        journal
                        turn.AssistantMessageId
                        "zero-width"
                        (Some(fun messageId -> continuationAccepted turn.SessionId messageId))

                if not sent then
                    eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "MISSING_FINAL_REPORT")
                    |> ignore
        | TurnNeedsContinuation _ ->
            // Absorb text+reasoning into session-wide A even when this turn is
            // not yet completable (e.g. reasoning-only / empty formal text).
            TerminalSessionA.accumulateTurn eventPort turn |> ignore

            // No final formal report yet or length limit. Do NOT complete the run.
            // Send a repair prompt to get the final report.
            let repairPrompt =
                "Your tool work is complete, but no final task report was produced. "
                + "Return a concise final report containing:\n"
                + "- result\n- evidence\n- files changed\n- tests run\n- remaining risks or blockers\n"
                + "Do not call another tool unless necessary."

            let sent =
                HostSessionNudge.trySendInteractionRepair
                    sessionPort
                    turn.SessionId
                    repairPrompt
                    (if String.IsNullOrWhiteSpace turn.Directory then
                         None
                     else
                         Some turn.Directory)
                    journal
                    turn.AssistantMessageId
                    "missing-final-report"
                    (Some(fun messageId -> continuationAccepted turn.SessionId messageId))

            if not sent then
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "MISSING_FINAL_REPORT")
                |> ignore
        | TurnAborted reason ->
            abortedSessions.Add sessionKey |> ignore
            Pty.abortParent sessionKey
            sessionPort.AbortChildren turn.SessionId |> ignore

            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Aborted reason)
            |> ignore
        | TurnFailed error ->
            // Provider/transport failures that produced an assistant terminal are
            // surfaced as a failed completion; only the provider retry status signal may
            // advance the durable fallback cursor.
            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed error) |> ignore
        | TurnCompleted ->
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

            let runResult: AgentRunResult =
                { SessionId = turn.SessionId
                  RootUserMessageId = turn.RootUserMessageId
                  AssistantMessageId = turn.AssistantMessageId
                  Role = roleStr
                  Directory = turn.Directory
                  FinalText = finalA
                  Parts = turn.Parts }

            // A first PERFECT is not a child completion. Keep the reviewer's
            // physical handle live until the confirmation prompt causes a
            // distinct run that records the second PERFECT; otherwise Manager
            // `join()` can return before the ReviewWitness is confirmed.
            let awaitingReviewerConfirmation =
                turn.AgentRole = Some AgentRole.Reviewer
                && ReviewerGuardState.pendingConfirmation sessionParents journal sessionKey

            // This path runs only after an idle signal reconciles a terminal
            // reviewer turn. A first PERFECT remains pending; only the reviewer
            // that supplied the confirmed double-PERFECT clears prior inputs.
            match
                turn.AgentRole,
                ReviewerGuardState.confirmedOwner
                    sessionParents
                    journal
                    sessionKey
                    (MessageId.value turn.UserMessageId),
                journal
            with
            | Some AgentRole.Reviewer, Some reviewOwner, Some j ->
                match AgentJournal.recordReviewConfirmedIdle j reviewOwner turn.SessionId turn.AssistantMessageId with
                | Ok() -> ()
                | Error err ->
                    raise (InvalidOperationException(sprintf "Failed to checkpoint confirmed reviewer idle: %s" err))
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

            if wasAborted then
                ()
            elif not (TerminalPolicyHelpers.sessionDead journal turn.SessionId) then
                match turn.AgentRole with
                // A first PERFECT must always enter its causal confirmation
                // round-trip before the generic "missing verdict" branch.
                // `verdictSessions` is only a terminal bookkeeping hint; it
                // must never suppress the pending confirmation transition.
                | Some AgentRole.Reviewer when ReviewerGuardState.pendingConfirmation sessionParents journal sessionKey ->
                    verdictSessions.Remove sessionKey |> ignore

                    HostReviewGuard.confirmPerfect
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
                | Some AgentRole.Manager when TerminalPolicyHelpers.isTopLevelManager sessionParents journal sessionKey ->
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

                if CompletedTurnClassifier.needsZeroWidthContinuation turn.AgentRole turn.Outcome turn.Parts then
                    let sent =
                        HostSessionNudge.trySendInteractionRepair
                            sessionPort
                            turn.SessionId
                            "​"
                            (if String.IsNullOrWhiteSpace turn.Directory then
                                 None
                             else
                                 Some turn.Directory)
                            journal
                            turn.AssistantMessageId
                            "zero-width"
                            (Some(fun messageId -> continuationAccepted turn.SessionId messageId))

                    if not sent then
                        eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "MISSING_FINAL_REPORT")
                        |> ignore

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
