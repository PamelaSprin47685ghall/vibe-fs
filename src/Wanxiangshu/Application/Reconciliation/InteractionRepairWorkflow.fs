namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

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
        if isRecoveryContinue journal context.Turn then
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

        if isRecoveryContinue journal turn then
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
