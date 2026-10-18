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
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Resources

/// Ordinary turn observation policy (INTERACTION-REPAIR / PROVIDER-RECOVERY / TERMINAL-REPORT).
module OrdinaryTurnWorkflow =

    type private LongWorkGate =
        | Ordinary
        | Wait
        | Continue

    let private isExplicitWaitOrOldTurn
        (authority: PromptAuthority.PromptAuthorityProjection option)
        (turn: ReconciledTurn)
        : bool =
        authority
        |> Option.exists (fun state ->
            let obsolete =
                match state.ActiveLogicalRun with
                | Some active -> active.AuthorityRootUserMessageId <> turn.AuthorityRootUserMessageId
                | None -> state.LastAuthorityProfile.IsSome

            obsolete
            || (state.UserQuestions
                |> Map.exists (fun _ question ->
                    question.Request.AuthorityRootUserMessageId = turn.AuthorityRootUserMessageId
                    && (question.Answer.IsNone || question.Request.ProviderRunId = turn.ProviderRun))))

    let private isTurnWorkSettled (turn: ReconciledTurn) : bool =
        turn.Directory
        |> Option.bind (fun directory ->
            let commonDir = RuntimePath.stateRoot directory
            Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected commonDir (SessionId.value turn.SessionId))
        |> Option.exists (fun work ->
            work.Delivered
            || List.contains (ProviderRunIdentity.value turn.ProviderRun) work.RetiredRuns)

    let private classifyManagerOrPlanTurn (context: ReconciledTurnContext) : LongWorkGate =
        let turn = context.Turn

        match context.Failure, turn.Observation, turn.Outcome with
        | None, None, ReconcileProgram.TurnCompleted -> Continue
        | None, None, ReconcileProgram.TurnNeedsContinuation _ when
            turn.Finish
            |> Option.exists (fun finish ->
                System.String.Equals(finish, "stop", System.StringComparison.OrdinalIgnoreCase))
            ->
            Continue
        | _ -> Ordinary

    let private longWorkGate (journal: AgentJournal option) (context: ReconciledTurnContext) =
        let turn = context.Turn

        let isManaged =
            match turn.Role with
            | Some(Role.Manager | Role.Plan) -> true
            | _ -> false

        let authority () =
            journal
            |> Option.bind (fun durable ->
                (AgentJournal.snapshot durable).AgentProjections.Sessions
                |> Map.tryFind turn.SessionId
                |> Option.bind (fun session -> session.PromptAuthority))

        if not isManaged then
            Ordinary
        elif isExplicitWaitOrOldTurn (authority ()) turn || isTurnWorkSettled turn then
            Wait
        else
            classifyManagerOrPlanTurn context

    let private bloggerReceiptKind (port: TurnObservationJournalPort) (turn: ReconciledTurn) =
        port.TryBloggerReceiptKind turn.SessionId turn.ProviderRun

    let private requestKindWithoutBloggerReceipt (port: TurnObservationJournalPort) (turn: ReconciledTurn) =
        let continuationKind =
            port.TryContinuationKind turn.SessionId turn.PhysicalUserMessageId

        match continuationKind, turn.Role with
        | Some PromptContinuationKind.InteractionRepair, _ -> Some ProviderRequestKind.InteractionRepair
        // A Blogger terminal without a durable cycle receipt did not prove a
        // business Main success. Never clear the failure budget
        // from Role alone.
        | _, Some Role.Blogger -> None
        | _ -> Some ProviderRequestKind.WorkMain

    let private requestKindOfCompleted (port: TurnObservationJournalPort) (turn: ReconciledTurn) =
        match bloggerReceiptKind port turn with
        | Some BlogFrameKind.Entry -> Some ProviderRequestKind.BloggerMain
        | None -> requestKindWithoutBloggerReceipt port turn

    let private recordSuccessIfValid
        (journal: AgentJournal option)
        (observation: TurnObservationJournalPort option)
        (turn: ReconciledTurn)
        =
        task {
            let clearingKind =
                observation
                |> Option.bind (fun port ->
                    requestKindOfCompleted port turn
                    |> Option.filter ProviderRequestKind.clearsFailureCountOnSuccess)

            match journal, clearingKind with
            | Some durable, Some _ ->
                let port = AgentJournalPortAdapter.forProviderFailure durable
                let! _ = ProviderFailureLedger.recordConfirmedSuccess port turn.SessionId turn.ProviderRun
                return ()
            | _ -> return ()
        }

    /// Revisit a previously delivered turn only for work whose authority comes
    /// from a fresh idle observation. Terminal plumbing remains first-delivery only.
    let observeIdle
        (quiescence: ISessionQuiescenceGate)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (observation: TurnObservationJournalPort option)
        (context: ReconciledTurnContext)
        (observer: ContinuationAcceptanceObserver option)
        : Task =
        let isFissionReplaced =
            FissionRuntime.isSilentInterrupt context.Turn.SessionId
            || (observation
                |> Option.exists (fun port -> port.IsFissionActive context.Turn.SessionId))

        let workGate =
            if isFissionReplaced then
                Wait
            else
                longWorkGate journal context

        match isFissionReplaced, context.Turn.Observation, context.Turn.Outcome with
        | true, _, _ -> AsyncSupport.completedTask ()
        | false, _, _ when workGate = Wait -> AsyncSupport.completedTask ()
        | false, _, _ when workGate = Continue ->
            InteractionRepairWorkflow.repairUnfinishedWork
                quiescence
                context
                sessionPort
                rootWorkspace
                eventPort
                journal
                observer
        | false, Some ReconcileProgram.TurnUnknown, _ ->
            InteractionRepairWorkflow.repairMissingFinalReport
                quiescence
                context
                sessionPort
                rootWorkspace
                eventPort
                journal
                observation
                observer
        | false, None, ReconcileProgram.TurnInProgress ->
            InteractionRepairWorkflow.repairIncompleteInteraction
                quiescence
                context
                sessionPort
                rootWorkspace
                eventPort
                journal
                observation
                observer
        | false, None, ReconcileProgram.TurnNeedsContinuation _ ->
            InteractionRepairWorkflow.repairMissingFinalReport
                quiescence
                context
                sessionPort
                rootWorkspace
                eventPort
                journal
                observation
                observer
        | false, None, (ReconcileProgram.TurnCompleted | ReconcileProgram.TurnAborted _ | ReconcileProgram.TurnFailed _) ->
            AsyncSupport.completedTask ()

    /// Own the reconciled ordinary-turn outcome match.
    /// `abortCause` is the Host boundary typed outcome consumed exactly once (structured-workflow-017 ①).
    /// Guard armed state is not exposed; CE branches on the typed abort outcome.
    let private handleAborted
        (eventPort: IEventObservationPort)
        (abortCause: AbortCause)
        (turn: ReconciledTurn)
        (reason: string)
        =
        // degeneration-guard-009: degeneration-guard already owns its successor. Application must
        // not become a second recovery owner. External aborts retain normal cleanup.
        match abortCause with
        | AbortCause.DegenerationGuard _ -> AsyncSupport.completedTask ()
        | AbortCause.External ->
            task {
                // managed-session-lifecycle-018: TurnAborted is an attempt observation, not
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
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (joinGuardNudges: HashSet<string>)
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        : Task<bool> =
        task {
            let turn = context.Turn

            match context.Quiescence with
            // No idle permit: no reminder can be claimed on this occasion, and
            // the reconcile pass dedupes the published turn — this terminal will
            // not be re-offered. Only publishing now can still reach the parent.
            | None -> return false
            | Some permit ->
                match!
                    HostJoinGuard.nudge
                        sessionPort
                        rootWorkspace
                        (journal |> Option.map AgentJournalPortAdapter.forHostJoinGuard)
                        journal
                        joinGuardNudges
                        (fun () -> quiescence.TryConsume permit)
                        (fun () -> quiescence.TryRelease permit)
                        turn.SessionId
                        turn.ProviderRun
                        turn.Directory
                with
                | HostJoinGuard.JoinGuardNudgeOutcome.Failed reason ->
                    eventPort.NotifyTerminal
                        turn.SessionId
                        (TerminalOutcome.Failed(TerminalStop.forAuthority turn.AuthorityRootUserMessageId reason))
                    |> ignore
                    // The Failed terminal was published: the parent already
                    // learns this occasion resolved. Do not publish again.
                    return true
                | HostJoinGuard.JoinGuardNudgeOutcome.Sent _
                | HostJoinGuard.JoinGuardNudgeOutcome.AlreadyOutstanding ->
                    // A durable continuation claim exists on this session: the
                    // woken run will report a fresh terminal occasion.
                    return true
                | HostJoinGuard.JoinGuardNudgeOutcome.NotSent
                | HostJoinGuard.JoinGuardNudgeOutcome.Superseded
                | HostJoinGuard.JoinGuardNudgeOutcome.AdmissionRejected _ ->
                    // No durable claim exists for the reminder, and the reconcile
                    // pass already consumed this published turn: nothing will
                    // re-offer this terminal. Withholding it would strand every
                    // parent waiting on this run.
                    return false
        }

    let private handleCompleted
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (observation: TurnObservationJournalPort option)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (completeAgent: unit -> Task<XTraceTerminalCompletion>)
        =
        task {
            let turn = context.Turn

            let terminalPolicyPort =
                Option.map AgentJournalPortAdapter.forTerminalPolicy journal

            let joinOutstanding =
                TerminalPolicy.outstandingBackground terminalPolicyPort hasLivePty turn.Role turn.SessionId

            // A withheld completion is only safe when a later pass is promised
            // to re-offer this same terminal. When JoinGuard cannot make that
            // promise (nudge unsent or superseded), the completed terminal is
            // published instead — deferring would strand every parent waiting
            // on a fact that already happened. (managed-session-lifecycle:
            // terminal admission deferral)
            let resolveOutstandingJoinCompletion () =
                task {
                    let! deferred =
                        applyJoinGuardNudge
                            sessionPort
                            rootWorkspace
                            eventPort
                            journal
                            joinGuardNudges
                            quiescence
                            context

                    if deferred then
                        return XTraceTerminalCompletion.RejectedEmptyOutput
                    else
                        return! completeAgent ()
                }

            let! completion =
                if not joinOutstanding then
                    completeAgent ()
                else
                    resolveOutstandingJoinCompletion ()

            let terminalValid =
                match completion with
                | XTraceTerminalCompletion.Published _ -> true
                | XTraceTerminalCompletion.CaptureFailed _
                | XTraceTerminalCompletion.RejectedMissingRole
                | XTraceTerminalCompletion.RejectedEmptyOutput -> false

            if terminalValid then
                do! recordSuccessIfValid journal observation turn

            return ()
        }
        :> Task

    let private handleFailedTurn
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (recoveryScope: IBloggerRuntimeHost)
        (context: ReconciledTurnContext)
        (error: string)
        =
        match context.Failure with
        | Some failure ->
            ProviderRecoveryWorkflow.continueAfterConfirmedFailure
                sessionPort
                rootWorkspace
                eventPort
                journal
                recoveryScope
                context.Turn
                failure
                error
                (ProviderProse.documentFor context.Turn.SessionId RuntimeNudge.ProviderRetry Map.empty)
        | None ->
            eventPort.NotifyTerminal
                context.Turn.SessionId
                (TerminalOutcome.Failed(TerminalStop.forAuthority context.Turn.AuthorityRootUserMessageId error))
            |> ignore

            Task.FromResult(()) :> Task

    let private handleOutcome
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (observation: TurnObservationJournalPort option)
        (recoveryScope: IBloggerRuntimeHost)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (abortCause: AbortCause)
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (completeAgent: unit -> Task<XTraceTerminalCompletion>)
        (observer: ContinuationAcceptanceObserver option)
        =
        let turn = context.Turn

        match turn.Outcome with
        | ReconcileProgram.TurnInProgress ->
            InteractionRepairWorkflow.repairIncompleteInteraction
                quiescence
                context
                sessionPort
                rootWorkspace
                eventPort
                journal
                observation
                observer
        | ReconcileProgram.TurnNeedsContinuation _ ->
            // Absorb text and reasoning into the XTrace even though this turn is
            // not completable, then ask for the missing report. Still not provider recovery.
            // (The XTrace parts are captured at the transform boundary.)
            InteractionRepairWorkflow.repairMissingFinalReport
                quiescence
                context
                sessionPort
                rootWorkspace
                eventPort
                journal
                observation
                observer
        | ReconcileProgram.TurnAborted reason -> handleAborted eventPort abortCause turn reason
        | ReconcileProgram.TurnFailed error ->
            handleFailedTurn sessionPort rootWorkspace eventPort journal recoveryScope context error
        | ReconcileProgram.TurnCompleted ->
            handleCompleted
                sessionPort
                rootWorkspace
                eventPort
                journal
                observation
                joinGuardNudges
                hasLivePty
                quiescence
                context
                completeAgent

    let observe
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (observation: TurnObservationJournalPort option)
        (recoveryScope: IBloggerRuntimeHost)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (abortCause: AbortCause)
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (observer: ContinuationAcceptanceObserver option)
        : Task =
        let turn = context.Turn

        let isFissionReplaced =
            FissionRuntime.isSilentInterrupt turn.SessionId
            || (observation |> Option.exists (fun port -> port.IsFissionActive turn.SessionId))

        let workGate =
            if isFissionReplaced then
                Wait
            else
                longWorkGate journal context

        match isFissionReplaced, turn.Observation with
        | true, _ -> AsyncSupport.completedTask ()
        | false, _ when workGate = Wait -> AsyncSupport.completedTask ()
        | false, _ when workGate = Continue ->
            InteractionRepairWorkflow.repairUnfinishedWork
                quiescence
                context
                sessionPort
                rootWorkspace
                eventPort
                journal
                observer
        | false, Some ReconcileProgram.TurnUnknown ->
            InteractionRepairWorkflow.repairMissingFinalReport
                quiescence
                context
                sessionPort
                rootWorkspace
                eventPort
                journal
                observation
                observer
        | false, None ->
            let completeAgent () =
                let tracePort = journal |> Option.map TerminalTracePort.forJournal
                TerminalReporter.completeWithEvidence eventPort tracePort turn

            handleOutcome
                sessionPort
                rootWorkspace
                eventPort
                journal
                observation
                recoveryScope
                joinGuardNudges
                hasLivePty
                abortCause
                quiescence
                context
                completeAgent
                observer
