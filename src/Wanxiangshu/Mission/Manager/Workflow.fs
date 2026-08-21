namespace Wanxiangshu.Mission.Manager

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Change
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// Manager terminal business story: handoff → background → idle labor.
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

        match currentLife journal turn.SessionId with
        | Some life when mayEncourageLabor journal hasLivePty turn.Role turn.SessionId ->
            ManagerIdle.encourageLabor sessionPort eventPort journal nudgeSent quiescence context life
        | _ -> AsyncSupport.completedTask ()

    let private handleBackgroundSettlement
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (hasLivePty: string -> bool)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (turn: ReconciledTurn)
        (settled: ManagerBackground.BackgroundSettlement)
        : Task =
        match settled with
        | ManagerBackground.BackgroundSettlement.Deferred -> AsyncSupport.completedTask ()
        | ManagerBackground.BackgroundSettlement.Settled ->
            observeIdle sessionPort eventPort journal nudgeSent hasLivePty quiescence context

    let private handleCompletedManager
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (turn: ReconciledTurn)
        : Task =
        if TerminalPolicy.sessionDead journal turn.SessionId then
            AsyncSupport.completedTask ()
        else
            task {
                let! settled =
                    ManagerBackground.ensureSettled sessionPort eventPort journal joinGuardNudges hasLivePty turn

                return!
                    handleBackgroundSettlement
                        sessionPort
                        eventPort
                        journal
                        nudgeSent
                        hasLivePty
                        quiescence
                        context
                        turn
                        settled
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
        (quiescence: SessionQuiescenceGate)
        (observeOrdinary: ReconciledTurnContext -> Task)
        (context: ReconciledTurnContext)
        : Task =
        task {
            let turn = context.Turn
            let! handoff = ManagerJobHandoff.completeIfTransferred eventPort journal turn

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
                quiescence
                observeOrdinary
                context
