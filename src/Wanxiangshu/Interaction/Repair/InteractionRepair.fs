namespace Wanxiangshu.Interaction.Repair

open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Interaction.Dispatch.OpenCode
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
open Wanxiangshu.Host
open Wanxiangshu.Resources
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
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// Idle-derived interaction repair (missing-final-report / incomplete interaction).
/// HOST-004 admission: no idle permit, or a permit that no longer holds at send
/// time → zero physical prompt, zero claim, zero terminal.
module InteractionRepairWorkflow =

    /// FALLBACK-008: one repair per unusable terminal, gated on a fresh idle
    /// permit (HOST-004).
    ///
    /// The task is awaited rather than discarded. `|> ignore` on the task also
    /// discarded the claim/abandon bookkeeping inside it, so a failed repair left
    /// a Claimed fact with nothing after it and no terminal for the caller.
    ///
    /// `Superseded` (stale permit) is not a failure: nothing was claimed, nothing
    /// was sent — the system is doing something fresher.
    let private sendRepair
        (quiescence: SessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (prompt: string)
        (repairKind: string)
        : Task =
        task {
            let! outcome =
                HostSessionNudge.trySendIdleInteractionRepair
                    quiescence
                    permit
                    sessionPort
                    turn.SessionId
                    prompt
                    turn.Directory
                    journal
                    turn.ProviderRun
                    repairKind

            match outcome with
            | HostSessionNudge.IdleContinuationOutcome.Sent _ -> ()
            | HostSessionNudge.IdleContinuationOutcome.Superseded -> ()
            | HostSessionNudge.IdleContinuationOutcome.Failed _ ->
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "MISSING_FINAL_REPORT")
                |> ignore
        }
        :> Task

    /// HOST-004: idle-derived repair sends funnel through one admission point.
    let private trySendIdleRepair
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (prompt: string)
        (repairKind: string)
        : Task =
        match context.Quiescence with
        | None -> AsyncSupport.completedTask ()
        | Some permit -> sendRepair quiescence permit sessionPort eventPort journal context.Turn prompt repairKind

    /// CTX-010 recovery continue owns the physical run until its own terminal is
    /// published. Missing-final-report / interaction-repair on that run hijacks the
    /// recovery slot: the interleaved idle reads finish=None (Unknown) or a
    /// provisional NeedsContinuation while the probe response is still on the wire,
    /// and a fresh SessionIdle of the *same* provider attempt mints a valid
    /// quiescence permit (BeginProviderAttempt already ran for the probe itself).
    /// Stale-permit gating cannot suppress that race — the permit is not stale.
    ///
    /// The durable fact is the authority ledger: this PhysicalUserMessageId was
    /// accepted as `ProviderRetryAttempt`. That is the recovery continue's identity,
    /// not a runtime whitelist and not a substitute for HOST-004 on ordinary mains.
    let private isRecoveryContinue (journal: AgentJournal option) (turn: ReconciledTurn) : bool =
        match journal with
        | None -> false
        | Some durable ->
            AgentProjection.tryFind turn.SessionId (AgentJournal.snapshot durable).AgentProjections
            |> Option.bind (fun session -> session.PromptAuthority)
            |> Option.exists (fun authority ->
                authority.AcceptedContinuationIds
                |> Map.tryFind turn.PhysicalUserMessageId
                |> Option.exists (fun kind -> kind = PromptAuthority.ContinuationKind.ProviderRetryAttempt))

    let private isFissionReplaced (journal: AgentJournal option) (sessionId: SessionId) : bool =
        FissionRuntime.isSilentInterrupt sessionId
        || (journal
            |> Option.exists (fun durable ->
                FissionProjection.tryActiveForOwner sessionId (AgentJournal.snapshot durable).AgentProjections.Fission
                |> Option.isSome))

    /// GLORY-070 / HOST-004 rev.3: a stable idle that never produced a final
    /// report is repaired exactly once (reconcile maps dedupe the turn token),
    /// and only when the pass carried idle evidence. ProviderRetryAttempt
    /// continues own the recovery slot — suppress missing-final-report so the
    /// probe's own terminal can promote.
    let repairMissingFinalReport
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        : Task =
        if
            isFissionReplaced journal context.Turn.SessionId
            || isRecoveryContinue journal context.Turn
        then
            AsyncSupport.completedTask ()
        else
            trySendIdleRepair
                quiescence
                context
                sessionPort
                eventPort
                journal
                (ProviderProse.documentFor context.Turn.SessionId RuntimeNudge.MissingClosingReport Map.empty)
                "missing-final-report"

    /// Incomplete in-progress interaction: classify then idle-repair, unless a
    /// ProviderRetryAttempt continue owns the recovery slot.
    let repairIncompleteInteraction
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        : Task =
        let turn = context.Turn

        if isFissionReplaced journal turn.SessionId || isRecoveryContinue journal turn then
            AsyncSupport.completedTask ()
        elif CompletedTurnClassifier.needsInteractionRepair turn.Role (box turn.Outcome) turn.Parts then
            trySendIdleRepair
                quiescence
                context
                sessionPort
                eventPort
                journal
                (ProviderProse.documentFor turn.SessionId RuntimeNudge.InteractionContinue Map.empty)
                "interaction-repair"
        else
            AsyncSupport.completedTask ()
