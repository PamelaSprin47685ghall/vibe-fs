namespace Wanxiangshu.Mission.Review.Judgement

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
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
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Trace
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Mission.Review
open Wanxiangshu.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
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

/// The single business owner of a reconciled Reviewer turn's continuation
/// (REVIEW-002/007).
///
/// `observe` is the story: durable `ReviewerEvidence` facts choose the branch;
/// `ReviewerContinuation` owns the named send promises; physical delivery is an
/// injected Review port. There is no stored State/Stage counter.
module ReviewerWorkflow =

    /// REVIEW-013/017: the latest verdict attempt this turn carried plus the
    /// XTrace head at closure time. `None` when the turn produced no verdict.
    let private closedAttemptEvidence (journal: AgentJournal option) (turn: ReconciledTurn) =
        journal
        |> Option.bind (fun durable ->
            let snapshot = AgentJournal.snapshot durable

            let frontier =
                AgentProjection.tryFind turn.SessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.XTrace)
                |> Option.map XTraceProjection.head
                |> Option.defaultValue 0L

            AgentProjection.tryFind turn.SessionId snapshot.AgentProjections
            |> Option.bind (fun session -> session.ReviewGuard)
            |> Option.bind ReviewProjection.latestObservedAttempt
            |> Option.map (fun attempt -> attempt, frontier))

    /// One closure append, one flat result match.
    let private writeAttemptClosed
        (journal: AgentJournal)
        (turn: ReconciledTurn)
        (attempt: ReviewAttemptIdentity)
        (frontier: int64)
        : Task =
        task {
            let closed =
                ReviewFact.ReviewAttemptClosed
                    {| ReviewerSessionId = turn.SessionId
                       BarrierId = attempt.ReviewBarrierId
                       GitTreeHash = attempt.GitTreeHash
                       ProviderRun = attempt.ProviderRun
                       ToolCallId = attempt.ToolCallId
                       FrozenFrontierSequence = frontier |}

            // REVIEW-013/017: `ReviewVerdictRecorded` only proves the judge
            // executed; the attempt closes only once this reconciled turn has
            // fully completed and its XTrace converged. Append failures leave
            // it unclosed — the conclusion stays Pending and an idle revisit
            // re-runs this path, with the fold deduping by attempt identity.
            let! appended =
                AgentJournal.appendAgent (StreamId.Session turn.SessionId) (Some attempt.ProviderRun) closed journal

            match appended with
            | Ok _ -> ()
            | Error _ -> ()
        }

    /// REVIEW-013/017: append the closure fact at turn completion. Flat by
    /// construction — each decision is a single top-level match.
    let private appendAttemptClosed (journal: AgentJournal option) (turn: ReconciledTurn) : Task =
        match closedAttemptEvidence journal turn with
        | None -> AsyncSupport.completedTask ()
        | Some(attempt, frontier) -> writeAttemptClosed (Option.get journal) turn attempt frontier

    /// Build the `AgentRunResult`, validate via `runResult.IsValid`, capture the
    /// XTrace terminal segment, close the carried attempt, and report.
    /// `allowToolOnlyFallback` lets an active Finality CE report a tool-only
    /// judgement terminal without encoding review stage in this observer.
    let private completeReviewer
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (allowToolOnlyFallback: bool)
        : Task =
        task {
            // COMPANION-003: the terminal text is this turn's formal text plus
            // host-visible reasoning — the XTrace terminal segment.
            let sessionWide = CompletedTurnClassifier.partsSessionText turn.Parts

            let sessionWideText =
                if not (String.IsNullOrWhiteSpace sessionWide) then
                    sessionWide
                elif allowToolOnlyFallback then
                    // Finality judgement turns may be tool-only. The CE consumes
                    // the typed judge delivery separately, so terminal reporting
                    // needs only a non-empty physical completion value.
                    "Review judgement submitted."
                else
                    sessionWide

            // REVIEW-006: nothing is inferred here. Completed confirmation is
            // written only by the direct Finality CE; this path reports the turn.
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

                    // REVIEW-013/017: the turn is over and its XTrace converged;
                    // freeze the attempt's closure frontier now.
                    do! appendAttemptClosed journal turn

                    eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Completed runResult)
                    |> ignore
                else
                    // The turn still ended — an attempt it recorded is closed so
                    // the conclusion is not left waiting for a terminal text
                    // that will never come.
                    do! appendAttemptClosed journal turn

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

    let private observeProcessReview
        (continuationPort: ReviewerContinuationPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (reviewerKey: string)
        : Task =
        match ReviewerEvidence.classifyNeed journal reviewerKey with
        | ReviewerEvidence.Need.NotProcessReview -> AsyncSupport.completedTask ()
        | ReviewerEvidence.Need.EnsureVerdictSubmitted ->
            task {
                let! outcome =
                    ReviewerContinuation.ensureVerdictSubmitted continuationPort journal turn.SessionId reviewerKey

                reportContinuationFailure eventPort turn.SessionId outcome
            }
            :> Task
        | ReviewerEvidence.Need.CompleteProcessReview -> completeReviewer eventPort journal turn false

    /// Physical terminal observer. Active Finality reviewers are owned by the
    /// direct ReviewBarrierWorkflow CE, so this function only reports their turn;
    /// it never selects challenge/confirmation work from durable state.
    let observe
        (continuationPort: ReviewerContinuationPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (reviewerKey: string)
        : Task =
        if ReviewJudgementInbox.isOwned turn.SessionId then
            completeReviewer eventPort journal turn true
        else
            observeProcessReview continuationPort eventPort journal turn reviewerKey
