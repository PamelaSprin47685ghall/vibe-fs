namespace Wanxiangshu.Composition.Turn

open System.Collections.Generic
open System.Threading.Tasks
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
open Wanxiangshu.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
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
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
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
open Wanxiangshu.Host

/// Ordinary (non-role-specialized) turn outcome routing: observation / outcome
/// match that drives repair, recovery, abort, and completed join-guard paths.
module OrdinaryTurnWorkflow =

    /// Revisit a previously delivered turn only for work whose authority comes
    /// from a fresh idle observation. Terminal plumbing remains first-delivery only.
    let observeIdle
        (quiescence: SessionQuiescenceGate)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (context: ReconciledTurnContext)
        : Task =
        let isFissionReplaced =
            FissionRuntime.isSilentInterrupt context.Turn.SessionId
            || (journal
                |> Option.exists (fun durable ->
                    FissionProjection.tryActiveForOwner
                        context.Turn.SessionId
                        (AgentJournal.snapshot durable).AgentProjections.Fission
                    |> Option.isSome))

        match isFissionReplaced, context.Turn.Observation, context.Turn.Outcome with
        | true, _, _ -> AsyncSupport.completedTask ()
        | false, Some ReconcileProgram.TurnUnknown, _ ->
            InteractionRepairWorkflow.repairMissingFinalReport quiescence context sessionPort eventPort journal
        | false, None, ReconcileProgram.TurnInProgress ->
            InteractionRepairWorkflow.repairIncompleteInteraction quiescence context sessionPort eventPort journal
        | false, None, ReconcileProgram.TurnNeedsContinuation _ ->
            InteractionRepairWorkflow.repairMissingFinalReport quiescence context sessionPort eventPort journal
        | false, None, (ReconcileProgram.TurnCompleted | ReconcileProgram.TurnAborted _ | ReconcileProgram.TurnFailed _) ->
            AsyncSupport.completedTask ()

    /// Own the reconciled ordinary-turn outcome match.
    /// `timerPort` and `abortParent` are injected by Host composition (Process is not Application).
    let private clearArmedLoopKill (loopSensor: LoopSensor option) (sessionId: SessionId) =
        match loopSensor with
        | Some sensor when sensor.IsArmed sessionId ->
            sensor.ClearArmed sessionId
            true
        | _ -> false

    let private handleAborted
        (timerPort: ITimerPort)
        (abortParent: string -> unit)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (abortedSessions: HashSet<string>)
        (loopSensor: LoopSensor option)
        (turn: ReconciledTurn)
        (sessionKey: string)
        (reason: string)
        =
        // LOOP-006: our own kill is bridged into the provider-failure AABB path.
        // User / cleanup aborts still report Aborted and do not advance the cursor.
        if clearArmedLoopKill loopSensor turn.SessionId then
            ProviderRecoveryWorkflow.continueAfterLoopKill timerPort sessionPort eventPort journal turn
        else
            abortedSessions.Add sessionKey |> ignore
            abortParent sessionKey
            sessionPort.AbortChildren turn.SessionId |> ignore

            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Aborted reason)
            |> ignore

            AsyncSupport.completedTask ()

    let private applyJoinGuardNudge
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

    let private handleCompleted
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (abortedSessions: HashSet<string>)
        (turn: ReconciledTurn)
        (sessionKey: string)
        (completeAgent: unit -> Task<bool * bool>)
        =
        task {
            let joinOutstanding =
                TerminalPolicy.outstandingBackground journal hasLivePty turn.Role turn.SessionId

            let! wasAborted, terminalValid =
                if joinOutstanding then
                    let aborted = abortedSessions.Contains sessionKey
                    abortedSessions.Remove sessionKey |> ignore
                    Task.FromResult(aborted, false)
                else
                    completeAgent ()

            let recordSuccessIfValid (journal: AgentJournal option) =
                task {
                    match journal with
                    | Some j ->
                        let! _ = FallbackLedger.recordConfirmedSuccess j turn.SessionId turn.ProviderRun
                        ()
                    | None -> ()
                }

            if terminalValid then
                do! recordSuccessIfValid journal

            if wasAborted || TerminalPolicy.sessionDead journal turn.SessionId then
                return ()
            elif joinOutstanding then
                return! applyJoinGuardNudge sessionPort eventPort journal joinGuardNudges turn
            else
                return ()
        }
        :> Task

    let private handleOutcome
        (timerPort: ITimerPort)
        (abortParent: string -> unit)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (abortedSessions: HashSet<string>)
        (loopSensor: LoopSensor option)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (completeAgent: unit -> Task<bool * bool>)
        =
        let turn = context.Turn
        let sessionKey = SessionId.value turn.SessionId

        match turn.Outcome with
        | ReconcileProgram.TurnInProgress ->
            InteractionRepairWorkflow.repairIncompleteInteraction quiescence context sessionPort eventPort journal
        | ReconcileProgram.TurnNeedsContinuation _ ->
            // Absorb text and reasoning into the XTrace even though this turn is
            // not completable, then ask for the missing report. Still not fallback.
            // (The XTrace parts are captured at the transform boundary.)
            InteractionRepairWorkflow.repairMissingFinalReport quiescence context sessionPort eventPort journal
        | ReconcileProgram.TurnAborted reason ->
            handleAborted
                timerPort
                abortParent
                sessionPort
                eventPort
                journal
                abortedSessions
                loopSensor
                turn
                sessionKey
                reason
        | ReconcileProgram.TurnFailed error ->
            ProviderRecoveryWorkflow.continueAfterConfirmedFailure
                timerPort
                sessionPort
                eventPort
                journal
                turn
                error
                (ProviderProse.documentFor turn.SessionId RuntimeNudge.ProviderRetry Map.empty)
        | ReconcileProgram.TurnCompleted ->
            handleCompleted
                sessionPort
                eventPort
                journal
                joinGuardNudges
                hasLivePty
                abortedSessions
                turn
                sessionKey
                completeAgent

    let observe
        (timerPort: ITimerPort)
        (abortParent: string -> unit)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (abortedSessions: HashSet<string>)
        (loopSensor: LoopSensor option)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        : Task =
        let turn = context.Turn

        let isFissionReplaced =
            FissionRuntime.isSilentInterrupt turn.SessionId
            || (journal
                |> Option.exists (fun durable ->
                    FissionProjection.tryActiveForOwner
                        turn.SessionId
                        (AgentJournal.snapshot durable).AgentProjections.Fission
                    |> Option.isSome))

        match isFissionReplaced, turn.Observation with
        | true, _ -> AsyncSupport.completedTask ()
        | false, Some ReconcileProgram.TurnUnknown ->
            InteractionRepairWorkflow.repairMissingFinalReport quiescence context sessionPort eventPort journal
        | false, None ->
            let completeAgent () =
                TerminalReporter.complete eventPort journal abortedSessions turn

            handleOutcome
                timerPort
                abortParent
                sessionPort
                eventPort
                journal
                joinGuardNudges
                hasLivePty
                abortedSessions
                loopSensor
                quiescence
                context
                completeAgent
