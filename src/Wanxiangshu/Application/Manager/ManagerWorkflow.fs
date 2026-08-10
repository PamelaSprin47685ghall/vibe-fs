namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Manager terminal business story: handoff → background → activation → idle labor.
module ManagerWorkflow =

    let private currentLife journal sessionId =
        journal
        |> Option.bind (fun durable ->
            AgentProjection.tryFind sessionId (AgentJournal.snapshot durable).AgentProjections)
        |> Option.bind (fun session -> session.ManagerLife)
        |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)

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

            match! ManagerJobHandoff.completeIfTransferred eventPort journal abortedSessions turn with
            | ManagerJobHandoff.HandoffOutcome.Transferred -> return true
            | ManagerJobHandoff.HandoffOutcome.ManagerOwnsTurn ->
                match turn.Outcome with
                | ReconcileProgram.TurnInProgress -> return false
                | ReconcileProgram.TurnCompleted ->
                    let sessionKey = SessionId.value turn.SessionId
                    let wasAborted = abortedSessions.Contains sessionKey
                    abortedSessions.Remove sessionKey |> ignore

                    if wasAborted || TerminalPolicy.sessionDead journal turn.SessionId then
                        return true
                    else
                        match!
                            ManagerBackground.ensureSettled
                                sessionPort
                                eventPort
                                journal
                                joinGuardNudges
                                hasLivePty
                                turn
                        with
                        | ManagerBackground.BackgroundSettlement.Deferred -> return true
                        | ManagerBackground.BackgroundSettlement.Settled ->
                            match currentLife journal turn.SessionId with
                            | Some life when
                                life.ActiveFinality
                                |> Option.exists ManagerLifecycleProjection.isOpen
                                ->
                                return true
                            | _ ->
                                match!
                                    ManagerActivation.ensureAccepted sessionPort eventPort journal turn
                                with
                                | ManagerActivation.EnsureAcceptedResult.Deferred -> return true
                                | ManagerActivation.EnsureAcceptedResult.Ready life ->
                                    do!
                                        ManagerIdle.encourageLabor
                                            sessionPort
                                            eventPort
                                            journal
                                            nudgeSent
                                            quiescence
                                            context
                                            life

                                    return true
                | _ -> return false
        }
