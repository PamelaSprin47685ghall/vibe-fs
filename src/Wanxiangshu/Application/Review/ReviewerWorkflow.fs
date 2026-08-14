namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Trace
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Review
open Wanxiangshu.Session
open Wanxiangshu.Host

/// The single business owner of a reconciled Reviewer turn's continuation
/// (REVIEW-002/007).
///
/// `observe` is the story: durable `ReviewerEvidence` facts choose the branch;
/// `ReviewerContinuation` owns the named send promises; physical delivery is an
/// injected Review port. There is no stored State/Stage counter.
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
        : Task =
        task {
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
            // VerdictWorkflow already journalled from the seal evidence, so the
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
                    do! XTraceCapture.captureTerminal journal turn

                    eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Completed runResult)
                    |> ignore
                else
                    eventPort.NotifyTerminal
                        turn.SessionId
                        (TerminalOutcome.Failed "completed with empty terminal output")
                    |> ignore
        }

    let private reportContinuationFailure
        (eventPort: IEventObservationPort)
        (sessionId: SessionId)
        (outcome: Result<unit, string>)
        =
        match outcome with
        | Error reason -> eventPort.NotifyTerminal sessionId (TerminalOutcome.Failed reason) |> ignore
        | Ok() -> ()

    /// The single writer deciding what a reconciled reviewer turn needs.
    ///
    /// Witness-driven, NOT a program counter: every branch reads a durable
    /// `ReviewerEvidence` classification. Completion reports the run; continuation
    /// branches call named Vocabulary and fail closed on a `Failed` send.
    let observe
        (continuationPort: ReviewerContinuationPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (reviewerKey: string)
        : Task =
        match ReviewerEvidence.classifyNeed journal reviewerKey with
        | ReviewerEvidence.Need.CompleteConfirmed ->
            // Confirmed dual-PERFECT often ends tool-only; expose the minimal A.
            completeReviewer eventPort journal turn true
        | ReviewerEvidence.Need.EnsurePerfectConfirmed ->
            task {
                let! outcome =
                    ReviewerContinuation.ensurePerfectConfirmed
                        continuationPort
                        journal
                        turn.SessionId
                        turn.ProviderRun
                        reviewerKey

                reportContinuationFailure eventPort turn.SessionId outcome
            }
            :> Task
        | ReviewerEvidence.Need.EnsureVerdictSubmitted ->
            task {
                let! outcome =
                    ReviewerContinuation.ensureVerdictSubmitted
                        continuationPort
                        journal
                        turn.SessionId
                        turn.ProviderRun
                        reviewerKey

                reportContinuationFailure eventPort turn.SessionId outcome
            }
            :> Task
        | ReviewerEvidence.Need.CompleteRevision -> completeReviewer eventPort journal turn false
