namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.Session
open Wanxiangshu.Host

/// Ordinary (non-role-specialized) turn outcome routing: observation / outcome
/// match that drives repair, recovery, abort, and completed join-guard paths.
module OrdinaryTurnWorkflow =

    /// Own the reconciled ordinary-turn outcome match.
    /// `timerPort` and `abortParent` are injected by Host composition (Process is not Application).
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
        let sessionKey = SessionId.value turn.SessionId

        let completeAgent () =
            TerminalReporter.complete eventPort journal abortedSessions turn

        match turn.Observation with
        | Some ReconcileProgram.TurnUnknown ->
            InteractionRepairWorkflow.repairMissingFinalReport
                quiescence
                context
                sessionPort
                eventPort
                journal
        | None ->
            match turn.Outcome with
            | ReconcileProgram.TurnInProgress ->
                InteractionRepairWorkflow.repairIncompleteInteraction
                    quiescence
                    context
                    sessionPort
                    eventPort
                    journal
            | ReconcileProgram.TurnNeedsContinuation _ ->
                // Absorb text and reasoning into the XTrace even though this turn is
                // not completable, then ask for the missing report. Still not fallback.
                // (The XTrace parts are captured at the transform boundary.)
                InteractionRepairWorkflow.repairMissingFinalReport
                    quiescence
                    context
                    sessionPort
                    eventPort
                    journal
            | ReconcileProgram.TurnAborted reason ->
                // LOOP-006: our own kill is bridged into the provider-failure AABB path.
                // User / cleanup aborts still report Aborted and do not advance the cursor.
                let loopKill =
                    match loopSensor with
                    | Some sensor when sensor.IsArmed turn.SessionId ->
                        sensor.ClearArmed turn.SessionId
                        true
                    | _ -> false

                if loopKill then
                    ProviderRecoveryWorkflow.continueAfterLoopKill
                        timerPort
                        sessionPort
                        eventPort
                        journal
                        turn
                else
                    abortedSessions.Add sessionKey |> ignore
                    abortParent sessionKey
                    sessionPort.AbortChildren turn.SessionId |> ignore

                    eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Aborted reason)
                    |> ignore

                    AsyncSupport.completedTask ()
            | ReconcileProgram.TurnFailed error ->
                ProviderRecoveryWorkflow.continueAfterConfirmedFailure
                    timerPort
                    sessionPort
                    eventPort
                    journal
                    turn
                    error
                    RuntimeNudge.providerRetry
            | ReconcileProgram.TurnCompleted ->
                let joinOutstanding =
                    TerminalPolicy.outstandingBackground journal hasLivePty turn.Role turn.SessionId

                let wasAborted, terminalValid =
                    if joinOutstanding then
                        let aborted = abortedSessions.Contains sessionKey

                        abortedSessions.Remove sessionKey |> ignore
                        aborted, false
                    else
                        completeAgent ()

                if terminalValid then
                    AgentJournal.recordDerivedFallbackSuccess journal turn.SessionId

                if wasAborted || TerminalPolicy.sessionDead journal turn.SessionId then
                    AsyncSupport.completedTask ()
                elif joinOutstanding then
                    task {
                        match!
                            HostJoinGuard.nudge sessionPort journal joinGuardNudges turn.SessionId turn.Directory
                        with
                        | HostJoinGuard.JoinGuardNudgeOutcome.Failed reason ->
                            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed reason)
                            |> ignore
                        | _ -> ()
                    }
                    :> Task
                else
                    AsyncSupport.completedTask ()
