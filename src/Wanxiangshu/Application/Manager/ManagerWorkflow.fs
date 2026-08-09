namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// The sole owner of Manager terminal business sequencing.
module ManagerWorkflow =

    let private tryJobProgress (journal: AgentJournal option) (sessionId: SessionId) =
        journal
        |> Option.bind (fun durable ->
            OrchestratorProjection.tryFindByManagerSession
                sessionId
                (AgentJournal.snapshot durable).AgentProjections.Orchestrator)
        |> Option.map (fun job -> job.Progress)

    let private currentLife journal sessionId =
        journal
        |> Option.bind (fun durable ->
            AgentProjection.tryFind sessionId (AgentJournal.snapshot durable).AgentProjections)
        |> Option.bind (fun session -> session.ManagerLife)
        |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)

    let private sendActivation
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        =
        task {
            match!
                HostSessionNudge.sendContinuationResult
                    sessionPort
                    turn.SessionId
                    ManagerLifecyclePrompt.WorkActivation
                    PromptAuthority.ContinuationKind.ManagerWorkActivation
                    turn.Directory
                    journal
                    PromptDispatcher.AwaitMode.Detached
                    None
            with
            | Error error -> eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed error) |> ignore
            | Ok _ -> ()
        }
        :> Task

    let private sendJoinGuard
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (joinGuardNudges: HashSet<string>)
        (turn: ReconciledTurn)
        =
        task {
            match! HostJoinGuard.nudge sessionPort journal joinGuardNudges turn.SessionId turn.Directory with
            | HostJoinGuard.JoinGuardNudgeOutcome.Failed reason ->
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed reason)
                |> ignore
            | _ -> ()
        }
        :> Task

    let private encourageIdle
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        =
        let turn = context.Turn
        let sessionKey = SessionId.value turn.SessionId

        let encouragementKey =
            sprintf "manager-idle:%s:%s" sessionKey (ProviderRunIdentity.value turn.ProviderRun)

        match context.Quiescence, currentLife journal turn.SessionId with
        | Some permit, Some life when not (nudgeSent.Contains encouragementKey) ->
            let idleAlreadyClaimed =
                match journal, HostSessionNudge.tryActiveProfile journal turn.SessionId with
                | Some durable, Some profile ->
                    PromptDispatcher.forJournal(durable).IdleAlreadyClaimed profile life.LifeId turn.ProviderRun
                | _ -> false

            if idleAlreadyClaimed then
                AsyncSupport.completedTask ()
            else
                nudgeSent.Add encouragementKey |> ignore

                task {
                    match!
                        HostSessionNudge.trySendIdleManagerEncouragement
                            quiescence
                            permit
                            sessionPort
                            turn.SessionId
                            ManagerLifecyclePrompt.IdleEncouragement
                            turn.Directory
                            journal
                            life.LifeId
                            turn.ProviderRun
                    with
                    | HostSessionNudge.IdleContinuationOutcome.Sent _
                    | HostSessionNudge.IdleContinuationOutcome.Superseded -> ()
                    | HostSessionNudge.IdleContinuationOutcome.Failed error ->
                        eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed error) |> ignore
                }
                :> Task
        | _ -> AsyncSupport.completedTask ()

    /// Returns true when Manager business ownership consumed the observation.
    /// Other outcomes fall through to generic terminal plumbing.
    let tryObserve
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (abortedSessions: HashSet<string>)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        : Task<bool> =
        task {
            let turn = context.Turn

            match turn.Outcome with
            | ReconcileProgram.TurnInProgress ->
                match tryJobProgress journal turn.SessionId with
                | Some(JobProgress.CandidateReady _)
                | Some(JobProgress.RebasedCandidateReady _)
                | Some(JobProgress.PublishClaimed _)
                | Some(JobProgress.Published _)
                | Some(JobProgress.Failed _)
                | Some JobProgress.Abandoned ->
                    TurnCompletionProgram.completeAgent eventPort journal abortedSessions turn
                    |> ignore

                    return true
                | Some JobProgress.ManagerStarted
                | Some(JobProgress.ConflictPending _)
                | None -> return false
            | ReconcileProgram.TurnCompleted ->
                match tryJobProgress journal turn.SessionId with
                | Some(JobProgress.ConflictPending _)
                | Some(JobProgress.CandidateReady _)
                | Some(JobProgress.RebasedCandidateReady _)
                | Some(JobProgress.PublishClaimed _)
                | Some(JobProgress.Published _)
                | Some(JobProgress.Failed _)
                | Some JobProgress.Abandoned ->
                    let _, terminalValid =
                        TurnCompletionProgram.completeAgent eventPort journal abortedSessions turn

                    if terminalValid then
                        AgentJournal.recordDerivedFallbackSuccess journal turn.SessionId

                    return true
                | Some JobProgress.ManagerStarted
                | None ->
                    let sessionKey = SessionId.value turn.SessionId
                    let wasAborted = abortedSessions.Contains sessionKey
                    abortedSessions.Remove sessionKey |> ignore

                    if wasAborted || TerminalPolicy.sessionDead journal turn.SessionId then
                        return true
                    elif TerminalPolicy.outstandingBackground journal hasLivePty turn.Role turn.SessionId then
                        do! sendJoinGuard sessionPort eventPort journal joinGuardNudges turn
                        return true
                    else
                        match currentLife journal turn.SessionId with
                        | Some life ->
                            match life.ActiveFinality with
                            | Some request when ManagerLifecycleProjection.isOpen request -> return true
                            | _ when ManagerLifecycleGate.shouldActivate journal turn ->
                                do! sendActivation sessionPort eventPort journal turn
                                return true
                            | _ ->
                                do! encourageIdle sessionPort eventPort journal nudgeSent quiescence context
                                return true
                        | None when ManagerLifecycleGate.shouldActivate journal turn ->
                            do! sendActivation sessionPort eventPort journal turn
                            return true
                        | None -> return true
            | _ -> return false
        }
