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

    /// Reviewer already left a durable verdict on its parent manager's guard.
    let private reviewerSubmittedVerdict (journal: AgentJournal option) (reviewerKey: string) =
        match journal with
        | None -> false
        | Some j ->
            let snapshot = AgentJournal.snapshot j
            let child = ChildId.create reviewerKey

            snapshot.AgentProjections.Sessions
            |> Map.exists (fun _ session ->
                match session.Linkage, session.ReviewGuard with
                | Some linkage, Some guard when Map.containsKey child linkage.LinkedChildren ->
                    guard.IsConfirmed
                    || guard.ConsecutivePerfects > 0
                    || not (List.isEmpty guard.RecentToolCallIds)
                | _ -> false)

    /// Reviewer gave a first PERFECT on the current tree but the confirmed
    /// double-PERFECT barrier has not landed yet. Distinct from
    /// `reviewerSubmittedVerdict` (which is only "has said anything at all") --
    /// this specifically detects the "awaiting confirmation" state so the guard
    /// can send the confirm-perfect nudge instead of silently going idle.
    let private reviewerPendingConfirmation (journal: AgentJournal option) (reviewerKey: string) =
        match journal with
        | None -> false
        | Some j ->
            let snapshot = AgentJournal.snapshot j
            let child = ChildId.create reviewerKey

            snapshot.AgentProjections.Sessions
            |> Map.exists (fun _ session ->
                match session.Linkage, session.ReviewGuard with
                | Some linkage, Some guard when Map.containsKey child linkage.LinkedChildren ->
                    guard.ConsecutivePerfects = 1 && not guard.IsConfirmed
                | _ -> false)

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
        let sessionKey = SessionId.value turn.SessionId
        disposeExecutorRuntime sessionKey

        match turn.Outcome with
        | TurnUnknown -> ()
        | TurnInProgress ->
            // Tool-calls in progress: never complete the run. Send zero-width
            // continuation so the model continues its work.
            if CompletedTurnClassifier.needsZeroWidthContinuation turn.AgentRole turn.Outcome turn.Parts then
                HostSessionNudge.send
                    sessionPort
                    turn.SessionId
                    "\u200B"
                    { Model = turn.Model
                      Agent = roleName turn.AgentRole
                      Directory =
                        if String.IsNullOrWhiteSpace turn.Directory then
                            None
                        else
                            Some turn.Directory }
                    ignore
                    journal
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

            HostSessionNudge.send
                sessionPort
                turn.SessionId
                repairPrompt
                { Model = turn.Model
                  Agent = roleName
                  Directory =
                    if String.IsNullOrWhiteSpace turn.Directory then
                        None
                    else
                        Some turn.Directory }
                ignore
                journal
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
                  RootUserMessageId = turn.UserMessageId
                  AssistantMessageId = turn.AssistantMessageId
                  Role = roleStr
                  Directory = turn.Directory
                  FinalText = textOutput turn
                  Parts = turn.Parts }

            if String.IsNullOrWhiteSpace runResult.FinalText then
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "completed with empty final text")
            else
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Completed runResult)
            |> ignore

            if wasAborted then
                ()
            elif not (sessionDead journal turn.SessionId) then
                match turn.AgentRole with
                | Some AgentRole.Reviewer when
                    not (verdictSessions.Remove sessionKey)
                    && not (reviewerSubmittedVerdict journal sessionKey)
                    ->
                    HostReviewGuard.nudgeReviewer
                        sessionPort
                        journal
                        nudgeSent
                        sessionKey
                        (MessageId.value turn.AssistantMessageId)
                        turn.Model
                | Some AgentRole.Reviewer when reviewerPendingConfirmation journal sessionKey ->
                    HostReviewGuard.confirmPerfect
                        sessionPort
                        journal
                        nudgeSent
                        sessionKey
                        (MessageId.value turn.AssistantMessageId)
                        turn.Model
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
                    | HostReviewGuard.ReviewGuardConfirmed -> ()
                    | HostReviewGuard.ReviewGuardUnavailable reason ->
                        raise (InvalidOperationException(sprintf "Review guard unavailable: %s" reason))
                | _ -> ()

                if CompletedTurnClassifier.needsZeroWidthContinuation turn.AgentRole turn.Outcome turn.Parts then
                    HostSessionNudge.send
                        sessionPort
                        turn.SessionId
                        "\u200B"
                        { Model = None
                          Agent = roleName turn.AgentRole
                          Directory =
                            if String.IsNullOrWhiteSpace turn.Directory then
                                None
                            else
                                Some turn.Directory }
                        ignore
                        journal
