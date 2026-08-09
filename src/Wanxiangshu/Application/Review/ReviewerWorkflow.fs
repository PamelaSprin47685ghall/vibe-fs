namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.Session
open Wanxiangshu.Host

/// The single business owner of a reconciled Reviewer turn's continuation
/// (REVIEW-002/007).
///
/// `observe` is the one writer that decides what a reconciled reviewer turn
/// needs. It reads only durable witness facts (`ReviewerGuardState`) — there is
/// no stored State/Stage counter. `HostReviewGuard` is a transport primitive
/// here: every reviewer send funnels through it, but the decision to send
/// belongs to this module, not to the guard.
module ReviewerWorkflow =

    /// Build the `AgentRunResult`, validate via `runResult.IsValid`, capture the
    /// XTrace terminal segment, and report Completed / Failed.
    /// `confirmedReviewerEmptyTextFallback` covers the confirmed double-PERFECT
    /// that ends tool-only with empty terminal text.
    let private completeReviewer
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (confirmedReviewerEmptyTextFallback: bool)
        : unit =
        // COMPANION-003: the terminal text is this turn's formal text plus
        // host-visible reasoning — the XTrace terminal segment.
        let sessionWide = CompletedTurnClassifier.partsSessionText turn.Parts

        let sessionWideText =
            if not (String.IsNullOrWhiteSpace sessionWide) then
                sessionWide
            elif confirmedReviewerEmptyTextFallback then
                // A confirmed double-PERFECT often ends on a tool-only frame.
                // The witness is already Confirmed, so expose a minimal A rather
                // than failing a review that actually succeeded.
                "Review confirmed."
            else
                sessionWide

        // REVIEW-006: nothing is written here. Confirmation is a fact
        // ReviewController already journalled from the seal evidence, so the
        // completion path only reports the run.
        // PROMPT-008: the Role comes from the reconciled turn, and there is no
        // default.
        match turn.Role with
        | None ->
            eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "completed with no resolved role")
            |> ignore
        | Some role ->
            let runResult: AgentRunResult =
                { SessionId = turn.SessionId
                  AuthorityRootUserMessageId = turn.AuthorityRootUserMessageId
                  ProviderRun = turn.ProviderRun
                  Role = AgentRoleIdentity.toRole role
                  Directory = turn.Directory
                  TerminalText = sessionWideText
                  TurnFormalText = CompletedTurnClassifier.partsText turn.Parts }

            // EXEC-006: `IsValid` is the single place that decides whether a
            // completed run carries terminal output.
            if runResult.IsValid then
                // COMPANION-003: capture the XTrace terminal segment.
                // Idempotent (PERSIST-010).
                XTraceCapture.captureTerminal journal turn

                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Completed runResult)
                |> ignore
            else
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "completed with empty terminal output")
                |> ignore

    /// The only continuation writer. Finality and terminal plumbing may observe
    /// facts, but they never choose or send a reviewer continuation.
    let ensureContinuation
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (reviewerKey: string)
        : Task<Result<unit, string>> =
        task {
            if not (ReviewerGuardState.continuationOpen journal reviewerKey) then
                return Ok()
            elif ReviewerGuardState.pendingConfirmation journal reviewerKey then
                let! outcome =
                    HostReviewGuard.requestPerfectConfirmation sessionPort journal nudgeSent sessionId providerRun

                match outcome with
                | HostReviewGuard.GuardNudgeOutcome.Failed reason -> return Error reason
                | _ -> return Ok()
            elif not (ReviewerGuardState.submitted journal reviewerKey) then
                let! _ = HostReviewGuard.nudgeReviewer sessionPort journal nudgeSent sessionId providerRun
                return Ok()
            else
                return Ok()
        }

    /// The single writer deciding what a reconciled reviewer turn needs.
    ///
    /// Witness-driven, NOT a program counter: every branch reads a durable
    /// `ReviewerGuardState` fact. Completion reports the run; the two guard
    /// branches send exactly-once through `HostReviewGuard` and fail closed on a
    /// `Failed` send.
    let observe
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (turn: ReconciledTurn)
        (reviewerKey: string)
        : Task =
        // 1. Confirmed reviewer (dual-PERFECT witness) → complete. A confirmed
        //    double-PERFECT often ends tool-only; expose the minimal A.
        if ReviewerGuardState.isConfirmedReviewer journal reviewerKey then
            completeReviewer eventPort journal turn true
            AsyncSupport.completedTask ()
        // 2. First PERFECT awaiting its causal confirmation round-trip → send the
        //    confirmation challenge exactly once. With no continuation in flight
        //    the run would wait forever — fail closed on a failed send.
        elif
            ReviewerGuardState.pendingConfirmation journal reviewerKey
            || not (ReviewerGuardState.submitted journal reviewerKey)
        then
            task {
                match! ensureContinuation sessionPort journal nudgeSent turn.SessionId turn.ProviderRun reviewerKey with
                | Error reason ->
                    eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed reason)
                    |> ignore
                | Ok() -> ()
            }
            :> Task
        // 4. Revision / already-handled (a verdict is on record) → completion path.
        else
            completeReviewer eventPort journal turn false
            AsyncSupport.completedTask ()
