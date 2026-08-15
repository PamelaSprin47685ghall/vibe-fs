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
    let private mayEncourageLabor journal hasLivePty role sessionId =
        not (isFissionReplaced journal sessionId)
        && not (TerminalPolicy.sessionDead journal sessionId)
        && not (TerminalPolicy.outstandingBackground journal hasLivePty role sessionId)

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
        let lifeOpt = currentLife journal turn.SessionId

        match turn.Outcome, mayEncourageLabor journal hasLivePty turn.Role turn.SessionId, lifeOpt with
        | ReconcileProgram.TurnCompleted, true, Some life when
            ManagerFinality.admitLabor life = ManagerFinality.LaborAdmission.LaborMayContinue
            ->
            ManagerIdle.encourageLabor sessionPort eventPort journal nudgeSent quiescence context life
        | _ -> AsyncSupport.completedTask ()

    let private handleLaborContinuation
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (life: LifeProjection)
        : Task =
        match ManagerFinality.admitLabor life with
        | ManagerFinality.LaborAdmission.FinalityOwnsLife -> AsyncSupport.completedTask ()
        | ManagerFinality.LaborAdmission.LaborMayContinue ->
            ManagerIdle.encourageLabor sessionPort eventPort journal nudgeSent quiescence context life

    let private handleSettledManager
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (turn: ReconciledTurn)
        : Task =
        match currentLife journal turn.SessionId with
        | Some life -> handleLaborContinuation sessionPort eventPort journal nudgeSent quiescence context life
        | None -> AsyncSupport.completedTask ()

    let private handleCompletedManager
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (abortedSessions: HashSet<string>)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (turn: ReconciledTurn)
        : Task =
        let sessionKey = SessionId.value turn.SessionId
        let wasAborted = abortedSessions.Contains sessionKey
        abortedSessions.Remove sessionKey |> ignore

        if wasAborted || TerminalPolicy.sessionDead journal turn.SessionId then
            AsyncSupport.completedTask ()
        else
            task {
                let! settled =
                    ManagerBackground.ensureSettled sessionPort eventPort journal joinGuardNudges hasLivePty turn

                match settled with
                | ManagerBackground.BackgroundSettlement.Deferred -> return ()
                | ManagerBackground.BackgroundSettlement.Settled ->
                    let! _ = handleSettledManager sessionPort eventPort journal nudgeSent quiescence context turn
                    return ()
            }
            :> Task

    let private handleManagerTurnOutcome observeOrdinary context (turn: ReconciledTurn) handleCompleted : Task =
        match turn.Outcome with
        | ReconcileProgram.TurnInProgress -> observeOrdinary context
        | ReconcileProgram.TurnCompleted -> handleCompleted ()
        | _ -> observeOrdinary context

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
            let! handoff = ManagerJobHandoff.completeIfTransferred eventPort journal abortedSessions turn

            match handoff with
            | ManagerJobHandoff.HandoffOutcome.Transferred -> return ()
            | ManagerJobHandoff.HandoffOutcome.ManagerOwnsTurn ->
                let handleCompleted () =
                    handleCompletedManager
                        sessionPort
                        eventPort
                        journal
                        nudgeSent
                        joinGuardNudges
                        hasLivePty
                        abortedSessions
                        quiescence
                        context
                        turn

                let! _ = handleManagerTurnOutcome observeOrdinary context turn handleCompleted
                return ()
        }
        :> Task

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
