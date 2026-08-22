namespace Wanxiangshu.Composition.Turn

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Resources

/// Ordinary turn observation policy (INTERACTION-REPAIR / FALLBACK / TERMINAL-REPORT).
module OrdinaryTurnWorkflow =

    let private bloggerReceiptKind (journal: AgentJournal) (turn: ReconciledTurn) =
        (AgentJournal.snapshot journal).AgentProjections.Sessions
        |> Map.tryFind turn.SessionId
        |> Option.bind (fun session -> session.BloggerCycles)
        |> Option.bind (BloggerCycleProjection.tryReceipt turn.ProviderRun)
        |> Option.map (fun receipt -> receipt.Kind)

    let private requestKindWithoutBloggerReceipt (journal: AgentJournal) (turn: ReconciledTurn) =
        let continuationKind =
            (AgentJournal.snapshot journal).AgentProjections.Sessions
            |> Map.tryFind turn.SessionId
            |> Option.bind (fun session -> session.PromptAuthority)
            |> Option.bind (fun authority -> Map.tryFind turn.PhysicalUserMessageId authority.AcceptedContinuationIds)

        match continuationKind, turn.Role with
        | Some PromptAuthority.InteractionRepair, _ -> Some ProviderRequestKind.InteractionRepair
        // A Blogger terminal without a durable cycle receipt did not prove a
        // business Main or maintenance Squash success. Never clear fallback
        // from Role alone.
        | _, Some Role.Blogger -> None
        | _ -> Some ProviderRequestKind.WorkMain

    let private requestKindOfCompleted (journal: AgentJournal) (turn: ReconciledTurn) =
        match bloggerReceiptKind journal turn with
        | Some BlogFrameKind.Squash -> Some ProviderRequestKind.BloggerSquash
        | Some BlogFrameKind.Entry -> Some ProviderRequestKind.BloggerMain
        | None -> requestKindWithoutBloggerReceipt journal turn

    let private successClearingRequest (journal: AgentJournal option) (turn: ReconciledTurn) =
        journal
        |> Option.bind (fun durable ->
            requestKindOfCompleted durable turn
            |> Option.filter ProviderRequestKind.clearsFailureCountOnSuccess
            |> Option.map (fun _ -> durable))

    let private recordSuccessIfValid (journal: AgentJournal option) (turn: ReconciledTurn) =
        task {
            match successClearingRequest journal turn with
            | Some durable ->
                let! _ = FallbackLedger.recordConfirmedSuccess durable turn.SessionId turn.ProviderRun
                return ()
            | None -> return ()
        }

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
    /// `abortCause` is the Host boundary typed outcome consumed exactly once (SW-017 ①).
    /// Guard armed state is not exposed; CE branches on the typed abort outcome.
    let private handleAborted
        (eventPort: IEventObservationPort)
        (abortCause: AbortCause)
        (turn: ReconciledTurn)
        (reason: string)
        =
        // DG-009: degeneration-guard already owns its successor. Application must
        // not become a second recovery owner. External aborts retain normal cleanup.
        match abortCause with
        | AbortCause.DegenerationGuard _ -> AsyncSupport.completedTask ()
        | AbortCause.External ->
            task {
                // MANAGED-SESSION-018: TurnAborted is an attempt observation, not
                // proof that the logical parent/session ceased to exist. Do not
                // escalate ambiguous AbortError into ParentCancelled and do not
                // physically destroy background children. SessionDeleted or an
                // explicit successor-less termination owns that irreversible act.

                eventPort.NotifyTerminal
                    turn.SessionId
                    (TerminalOutcome.Aborted(TerminalStop.forAuthority turn.AuthorityRootUserMessageId reason))
                |> ignore

                return ()
            }
            :> Task

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
                eventPort.NotifyTerminal
                    turn.SessionId
                    (TerminalOutcome.Failed(TerminalStop.forAuthority turn.AuthorityRootUserMessageId reason))
                |> ignore
            | _ -> ()
        }

    let private handleCompleted
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (turn: ReconciledTurn)
        (completeAgent: unit -> Task<bool * bool>)
        =
        task {
            let joinOutstanding =
                TerminalPolicy.outstandingBackground journal hasLivePty turn.Role turn.SessionId

            let! wasAborted, terminalValid =
                if joinOutstanding then
                    Task.FromResult(false, false)
                else
                    completeAgent ()

            if terminalValid then
                do! recordSuccessIfValid journal turn

            if wasAborted || TerminalPolicy.sessionDead journal turn.SessionId then
                return ()
            elif joinOutstanding then
                return! applyJoinGuardNudge sessionPort eventPort journal joinGuardNudges turn
            else
                return ()
        }
        :> Task

    let private handleOutcome
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (recoveryScope: IBloggerRuntimeHost)
        (armRecovery: SessionId -> unit)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (abortCause: AbortCause)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (completeAgent: unit -> Task<bool * bool>)
        =
        let turn = context.Turn

        match turn.Outcome with
        | ReconcileProgram.TurnInProgress ->
            InteractionRepairWorkflow.repairIncompleteInteraction quiescence context sessionPort eventPort journal
        | ReconcileProgram.TurnNeedsContinuation _ ->
            // Absorb text and reasoning into the XTrace even though this turn is
            // not completable, then ask for the missing report. Still not fallback.
            // (The XTrace parts are captured at the transform boundary.)
            InteractionRepairWorkflow.repairMissingFinalReport quiescence context sessionPort eventPort journal
        | ReconcileProgram.TurnAborted reason -> handleAborted eventPort abortCause turn reason
        | ReconcileProgram.TurnFailed error ->
            ProviderRecoveryWorkflow.continueAfterConfirmedFailure
                sessionPort
                eventPort
                journal
                recoveryScope
                armRecovery
                turn
                error
                (ProviderProse.documentFor turn.SessionId RuntimeNudge.ProviderRetry Map.empty)
        | ReconcileProgram.TurnCompleted ->
            handleCompleted sessionPort eventPort journal joinGuardNudges hasLivePty turn completeAgent

    let observe
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (recoveryScope: IBloggerRuntimeHost)
        (armRecovery: SessionId -> unit)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (abortCause: AbortCause)
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
                TerminalReporter.complete eventPort journal turn

            handleOutcome
                sessionPort
                eventPort
                journal
                recoveryScope
                armRecovery
                joinGuardNudges
                hasLivePty
                abortCause
                quiescence
                context
                completeAgent
