namespace Wanxiangshu.Mission.Manager

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Strength.Persistence

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Host
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// Manager terminal business story: handoff → background → activation → idle labor.
module ManagerWorkflow =

    let private currentLife journal sessionId =
        journal
        |> Option.bind (fun durable ->
            AgentProjection.tryFind sessionId (AgentJournal.snapshot durable).AgentProjections)
        |> Option.bind (fun session -> session.ManagerLife)
        |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)

    let private isFissionReplaced journal sessionId =
        FissionRuntime.isSilentInterrupt sessionId
        || (journal
            |> Option.exists (fun durable ->
                FissionProjection.tryActiveForOwner sessionId (AgentJournal.snapshot durable).AgentProjections.Fission
                |> Option.isSome))

    /// A fresh idle observation may arrive after this terminal fact was already
    /// delivered on another wake. Only idle-derived labor may run again here.
    let observeIdle
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (hasLivePty: string -> bool)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        : Task =
        let turn = context.Turn

        match isFissionReplaced journal turn.SessionId, turn.Outcome with
        | false, ReconcileProgram.TurnCompleted when
            not (TerminalPolicy.sessionDead journal turn.SessionId)
            && not (TerminalPolicy.outstandingBackground journal hasLivePty turn.Role turn.SessionId)
            ->
            match currentLife journal turn.SessionId with
            | Some life when ManagerFinality.admitLabor life = ManagerFinality.LaborAdmission.LaborMayContinue ->
                ManagerIdle.encourageLabor sessionPort eventPort journal nudgeSent quiescence context life
            | _ -> AsyncSupport.completedTask ()
        | _ -> AsyncSupport.completedTask ()

    let private observeActiveManager
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
                            | Some life when
                                ManagerFinality.admitLabor life = ManagerFinality.LaborAdmission.FinalityOwnsLife
                                ->
                                return ()
                            | _ ->
                                // GLORY-018/070: production has no planning-terminal →
                                // ManagerWorkActivation protocol. LifeOpened is already
                                // sufficient to continue; T1/BlindPlan progression is
                                // represented by Magic Todo / WorkRecord facts, not an
                                // Activation continuation.
                                match currentLife journal turn.SessionId with
                                | Some life ->
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
                                | None -> return ()
                | _ -> return! observeOrdinary context
        }

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
        if isFissionReplaced journal context.Turn.SessionId then
            AsyncSupport.completedTask ()
        else
            observeActiveManager
                sessionPort
                eventPort
                journal
                nudgeSent
                joinGuardNudges
                hasLivePty
                abortedSessions
                quiescence
                observeOrdinary
                context
