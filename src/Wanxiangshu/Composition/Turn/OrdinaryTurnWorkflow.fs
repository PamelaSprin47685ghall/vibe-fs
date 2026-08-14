namespace Wanxiangshu.Composition.Turn

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Session
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
        match context.Turn.Observation with
        | Some ReconcileProgram.TurnUnknown ->
            InteractionRepairWorkflow.repairMissingFinalReport quiescence context sessionPort eventPort journal
        | None ->
            match context.Turn.Outcome with
            | ReconcileProgram.TurnInProgress ->
                InteractionRepairWorkflow.repairIncompleteInteraction quiescence context sessionPort eventPort journal
            | ReconcileProgram.TurnNeedsContinuation _ ->
                InteractionRepairWorkflow.repairMissingFinalReport quiescence context sessionPort eventPort journal
            | ReconcileProgram.TurnCompleted
            | ReconcileProgram.TurnAborted _
            | ReconcileProgram.TurnFailed _ -> AsyncSupport.completedTask ()

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
            InteractionRepairWorkflow.repairMissingFinalReport quiescence context sessionPort eventPort journal
        | None ->
            match turn.Outcome with
            | ReconcileProgram.TurnInProgress ->
                InteractionRepairWorkflow.repairIncompleteInteraction quiescence context sessionPort eventPort journal
            | ReconcileProgram.TurnNeedsContinuation _ ->
                // Absorb text and reasoning into the XTrace even though this turn is
                // not completable, then ask for the missing report. Still not fallback.
                // (The XTrace parts are captured at the transform boundary.)
                InteractionRepairWorkflow.repairMissingFinalReport quiescence context sessionPort eventPort journal
            | ReconcileProgram.TurnAborted reason ->
                let processReplacement = FissionRuntime.tryConsumeSilentInterrupt turn.SessionId

                let durableReplacement =
                    journal
                    |> Option.exists (fun durable ->
                        FissionProjection.tryActiveForOwner
                            turn.SessionId
                            (AgentJournal.snapshot durable).AgentProjections.Fission
                        |> Option.isSome)

                if processReplacement || durableReplacement then
                    // The old physical present was replaced by admitted sibling
                    // lanes. The durable active-group fact is sufficient after a
                    // crash even when the process-local interrupt marker is gone.
                    // Do not cascade children/PTY, publish Aborted, or route this
                    // observation into provider recovery.
                    AsyncSupport.completedTask ()
                else
                    // LOOP-006: our own kill is bridged into the provider-failure AABB path.
                    // User / cleanup aborts still report Aborted and do not advance the cursor.
                    let loopKill =
                        match loopSensor with
                        | Some sensor when sensor.IsArmed turn.SessionId ->
                            sensor.ClearArmed turn.SessionId
                            true
                        | _ -> false

                    if loopKill then
                        ProviderRecoveryWorkflow.continueAfterLoopKill timerPort sessionPort eventPort journal turn
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
                    (ProviderProse.documentFor turn.SessionId RuntimeNudge.ProviderRetry Map.empty)
            | ReconcileProgram.TurnCompleted ->
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

                    if terminalValid then
                        AgentJournal.recordDerivedFallbackSuccess journal turn.SessionId

                    if wasAborted || TerminalPolicy.sessionDead journal turn.SessionId then
                        return ()
                    elif joinOutstanding then
                        match!
                            HostJoinGuard.nudge sessionPort journal joinGuardNudges turn.SessionId turn.Directory
                        with
                        | HostJoinGuard.JoinGuardNudgeOutcome.Failed reason ->
                            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed reason)
                            |> ignore
                        | _ -> ()
                    else
                        ()
                }
                :> Task
