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

    /// Observe one Manager-role turn. Manager-specific business branches stay here;
    /// non-Manager terminal semantics are delegated through the injected ordinary
    /// workflow rather than returned as a handled-bool program counter.
    let observe
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (abortedSessions: HashSet<string>)
        (quiescence: SessionQuiescenceGate)
        (observeOrdinary: ReconciledTurnContext -> Task)
        (context: ReconciledTurnContext)
        : Task =
        task {
            let turn = context.Turn

            match! ManagerJobHandoff.completeIfTransferred eventPort journal abortedSessions turn with
            | ManagerJobHandoff.HandoffOutcome.Transferred -> return ()
            | ManagerJobHandoff.HandoffOutcome.ManagerOwnsTurn ->
                match turn.Outcome with
                | ReconcileProgram.TurnInProgress -> return! observeOrdinary context
                | ReconcileProgram.TurnCompleted ->
                    let sessionKey = SessionId.value turn.SessionId
                    let wasAborted = abortedSessions.Contains sessionKey
                    abortedSessions.Remove sessionKey |> ignore

                    if wasAborted || TerminalPolicy.sessionDead journal turn.SessionId then
                        return ()
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
                        | ManagerBackground.BackgroundSettlement.Deferred -> return ()
                        | ManagerBackground.BackgroundSettlement.Settled ->
                            match currentLife journal turn.SessionId with
                            | Some life when ManagerFinality.admitLabor life = ManagerFinality.LaborAdmission.FinalityOwnsLife ->
                                return ()
                            | _ ->
                                match!
                                    ManagerActivation.ensureAccepted sessionPort eventPort journal turn
                                with
                                | ManagerActivation.EnsureAcceptedResult.Deferred -> return ()
                                | ManagerActivation.EnsureAcceptedResult.Ready life ->
                                    match ManagerFinality.admitLabor life with
                                    | ManagerFinality.LaborAdmission.FinalityOwnsLife -> return ()
                                    | ManagerFinality.LaborAdmission.LaborMayContinue ->
                                        do!
                                            ManagerIdle.encourageLabor
                                                sessionPort
                                                eventPort
                                                journal
                                                nudgeSent
                                                quiescence
                                                context
                                                life

                                        return ()
                | _ -> return! observeOrdinary context
        }
