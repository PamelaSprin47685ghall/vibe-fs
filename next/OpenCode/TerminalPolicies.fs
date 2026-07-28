namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

/// Business decisions on a fully reconciled turn. Never sees raw host events.
module TerminalPolicies =

    let private sessionDead (journal: AgentJournal option) (sessionId: SessionId) =
        match journal with
        | Some j -> j.IsPoisoned || DurableFallback.isDead sessionId (AgentJournal.snapshot j)
        | None -> false

    let private roleName (role: AgentRole option) =
        role |> Option.map (fun value -> value.ToString().ToLowerInvariant())

    let private textOutput (turn: ReconciledTurn) =
        CompletedTurnClassifier.partsText turn.Parts

    let private recordOutput (eventPort: IEventObservationPort) (sessionId: SessionId) (text: string) =
        match eventPort with
        | :? Events.HostEventPort as hostPort -> hostPort.RecordSessionOutput sessionId text
        | _ -> ()

    /// True when this session is a linked child of some parent in the durable
    /// journal projection. Used when the in-memory sessionParents map is empty
    /// (worktree plugin instance) so Orchestrator managers never receive the
    /// top-level ReviewGuard nudge.
    let private isLinkedChild (journal: AgentJournal option) (sessionKey: string) =
        match journal with
        | None -> false
        | Some j ->
            let child = ChildId.create sessionKey

            (AgentJournal.snapshot j).AgentProjections.Sessions
            |> Map.exists (fun _ session ->
                match session.Linkage with
                | Some linkage -> Map.containsKey child linkage.LinkedChildren
                | None -> false)

    let private isTopLevelManager
        (sessionParents: Dictionary<string, string>)
        (journal: AgentJournal option)
        (sessionKey: string)
        =
        not (sessionParents.ContainsKey sessionKey)
        && not (isLinkedChild journal sessionKey)

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
        (turn: ReconciledTurn)
        =
        let sessionKey = SessionId.value turn.SessionId
        disposeExecutorRuntime sessionKey

        match turn.AgentRole with
        | Some agent when isLinkedChild journal sessionKey || sessionParents.ContainsKey sessionKey ->
            HostSessionNudge.ensureAgentOwnerAuthority journal turn.SessionId turn.RootUserMessageId agent turn.Model
        | _ -> ()

        match turn.Outcome with
        | TurnUnknown -> ()
        | TurnInProgress ->
            // Tool-calls in progress: never complete the run. Send zero-width
            // continuation so the model continues its work.
            if CompletedTurnClassifier.needsZeroWidthContinuation turn.AgentRole turn.Outcome turn.Parts then
                HostSessionNudge.sendContinuation
                    sessionPort
                    turn.SessionId
                    "\u200B"
                    PromptAuthority.InteractionRepair
                    { Model = turn.Model
                      Agent = roleName turn.AgentRole
                      Directory =
                        if String.IsNullOrWhiteSpace turn.Directory then
                            None
                        else
                            Some turn.Directory
                      Metadata = None }
                    journal
                    (Some(fun messageId -> continuationAccepted turn.SessionId messageId))
        | TurnNeedsContinuation reason ->
            // No final text or length limit. Do NOT complete the run. Send a
            // repair prompt to get the final report.
            let roleName =
                turn.AgentRole |> Option.map (fun r -> r.ToString().ToLowerInvariant())

            let repairPrompt =
                "Your tool work is complete, but no final task report was produced. "
                + "Return a concise final report containing:\n"
                + "- result\n- evidence\n- files changed\n- tests run\n- remaining risks or blockers\n"
                + "Do not call another tool unless necessary."

            HostSessionNudge.sendContinuation
                sessionPort
                turn.SessionId
                repairPrompt
                PromptAuthority.InteractionRepair
                { Model = turn.Model
                  Agent = roleName
                  Directory =
                    if String.IsNullOrWhiteSpace turn.Directory then
                        None
                    else
                        Some turn.Directory
                  Metadata = None }
                journal
                (Some(fun messageId -> continuationAccepted turn.SessionId messageId))
        | TurnAborted reason ->
            abortedSessions.Add sessionKey |> ignore
            Pty.abortParent sessionKey
            sessionPort.AbortChildren turn.SessionId |> ignore

            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Aborted reason)
            |> ignore
        | TurnFailed error -> eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed error) |> ignore
        | TurnCompleted ->
            let wasAborted = abortedSessions.Contains sessionKey
            abortedSessions.Remove sessionKey |> ignore

            let roleStr =
                turn.AgentRole
                |> Option.map (fun r -> r.ToString().ToLowerInvariant())
                |> Option.defaultValue "coder"

            let runResult: AgentRunResult =
                { SessionId = turn.SessionId
                  RootUserMessageId = turn.RootUserMessageId
                  AssistantMessageId = turn.AssistantMessageId
                  Role = roleStr
                  Directory = turn.Directory
                  FinalText = textOutput turn
                  Parts = turn.Parts }

            // A first PERFECT is not a child completion. Keep the reviewer's
            // physical handle live until the confirmation prompt causes a
            // distinct run that records the second PERFECT; otherwise Manager
            // `join()` can return before the ReviewWitness is confirmed.
            let awaitingReviewerConfirmation =
                turn.AgentRole = Some AgentRole.Reviewer
                && ReviewerGuardState.pendingConfirmation sessionParents journal sessionKey

            if not awaitingReviewerConfirmation then
                if String.IsNullOrWhiteSpace runResult.FinalText then
                    eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "completed with empty final text")
                    |> ignore
                else
                    // Companion / fork consumers read session output from the
                    // event port watermark, not from TerminalOutcome alone.
                    // Record the formal A-text before NotifyTerminal so a
                    // SubscribeTerminal listener that joins immediately still
                    // observes non-empty assistant output.
                    recordOutput eventPort turn.SessionId runResult.FinalText

                    eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Completed runResult)
                    |> ignore

            if wasAborted then
                ()
            elif not (sessionDead journal turn.SessionId) then
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
                        turn.Model
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
                        turn.Model
                        continuationAccepted
                | Some AgentRole.Manager when isTopLevelManager sessionParents journal sessionKey ->
                    match HostReviewGuard.missingTree journal gitTreePort sessionKey with
                    | HostReviewGuard.ReviewGuardMissing treeHash ->
                        HostReviewGuard.nudgeManager
                            sessionPort
                            journal
                            managerGuardNudges
                            sessionKey
                            (MessageId.value turn.AssistantMessageId)
                            treeHash
                            turn.Model
                            continuationAccepted
                    | HostReviewGuard.ReviewGuardConfirmed -> ()
                    | HostReviewGuard.ReviewGuardUnavailable reason ->
                        raise (InvalidOperationException(sprintf "Review guard unavailable: %s" reason))
                | _ -> ()

                if CompletedTurnClassifier.needsZeroWidthContinuation turn.AgentRole turn.Outcome turn.Parts then
                    HostSessionNudge.sendContinuation
                        sessionPort
                        turn.SessionId
                        "\u200B"
                        PromptAuthority.InteractionRepair
                        { Model = None
                          Agent = roleName turn.AgentRole
                          Directory =
                            if String.IsNullOrWhiteSpace turn.Directory then
                                None
                            else
                                Some turn.Directory
                          Metadata = None }
                        journal
                        (Some(fun messageId -> continuationAccepted turn.SessionId messageId))

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
            turn
