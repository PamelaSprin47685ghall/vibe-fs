namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

/// Business decisions on a fully reconciled turn. Never sees raw host events.
module TerminalPolicies =

    let private sessionDead (journal: AgentJournal option) (sessionId: SessionId) =
        match journal with
        | Some j ->
            j.IsPoisoned
            || DurableFallback.isDead sessionId (AgentJournal.snapshot j)
        | None -> false

    let private roleName (role: AgentRole option) =
        role |> Option.map (fun value -> value.ToString().ToLowerInvariant())

    let private textOutput (turn: ReconciledTurn) =
        CompletedTurnClassifier.partsText turn.Parts

    let private recordOutput (eventPort: IEventObservationPort) (sessionId: SessionId) (text: string) =
        match eventPort with
        | :? Events.HostEventPort as hostPort -> hostPort.RecordSessionOutput sessionId text
        | _ -> ()

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
        | TurnAborted reason ->
            abortedSessions.Add sessionKey |> ignore
            Pty.abortParent sessionKey
            sessionPort.AbortChildren turn.SessionId |> ignore
            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Aborted reason) |> ignore
        | TurnFailed error ->
            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed error) |> ignore
        | TurnCompleted ->
            // A successful/failed terminal after abort clears the latch only when
            // this turn is a genuine new completion; abort latch blocks nudges.
            let wasAborted = abortedSessions.Contains sessionKey
            abortedSessions.Remove sessionKey |> ignore

            let output = textOutput turn
            recordOutput eventPort turn.SessionId output
            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Completed turn.AssistantMessageId)
            |> ignore

            if wasAborted then
                ()
            elif not (sessionDead journal turn.SessionId) then
                match turn.AgentRole with
                | Some AgentRole.Reviewer when not (verdictSessions.Remove sessionKey) ->
                    HostReviewGuard.nudgeReviewer
                        sessionPort
                        journal
                        nudgeSent
                        sessionKey
                        (MessageId.value turn.AssistantMessageId)
                        turn.Model
                | Some AgentRole.Manager when not (sessionParents.ContainsKey sessionKey) ->
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
