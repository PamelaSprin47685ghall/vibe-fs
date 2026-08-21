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

    [<RequireQualifiedAccess>]
    type private SubmittedClosureEvidence =
        | NoAttempt
        | AlreadyClosed
        | ToolResultMissing
        | ToolResultReady of attempt: ReviewAttemptIdentity * frontier: int64

    let private matchingToolResultFrontier
        (attempt: ReviewAttemptIdentity)
        (xTrace: XTraceProjectionState)
        : int64 option =
        XTraceProjection.parts xTrace
        |> List.tryFind (fun part ->
            part.Kind = "tool_result"
            && part.ProviderRun = Some attempt.ProviderRun
            && part.ToolCallId = Some attempt.ToolCallId)
        |> Option.map (fun part -> part.Cursor.Sequence + 1L)

    let private appendSubmittedAttemptClosed
        (journal: AgentJournal)
        (attempt: ReviewAttemptIdentity)
        (frontier: int64)
        : Task<Result<unit, string>> =
        task {
            let closed =
                ReviewFact.ReviewAttemptClosed
                    {| ReviewerSessionId = attempt.ReviewerSessionId
                       BarrierId = attempt.ReviewBarrierId
                       GitTreeHash = attempt.GitTreeHash
                       ProviderRun = attempt.ProviderRun
                       ToolCallId = attempt.ToolCallId
                       FrozenFrontierSequence = frontier |}

            match!
                AgentJournal.appendAgent
                    (StreamId.Session attempt.ReviewerSessionId)
                    (Some attempt.ProviderRun)
                    closed
                    journal
            with
            | Ok _ -> return Ok()
            | Error failure -> return Error(JournalAppendFailure.describe failure)
        }

    let private classifySubmittedClosure
        (reviewerSessionId: SessionId)
        (snapshot: ProjectionSet)
        : SubmittedClosureEvidence =
        let evidence =
            AgentProjection.tryFind reviewerSessionId snapshot.AgentProjections
            |> Option.bind (fun session ->
                session.ReviewGuard
                |> Option.bind ReviewProjection.latestObservedAttempt
                |> Option.map (fun attempt -> session, attempt))

        let closed =
            evidence
            |> Option.bind (fun (session, attempt) ->
                session.ReviewGuard
                |> Option.bind (ReviewProjection.closedAttemptOf attempt))

        let ready =
            evidence
            |> Option.bind (fun (session, attempt) ->
                session.XTrace
                |> Option.bind (matchingToolResultFrontier attempt)
                |> Option.map (fun frontier -> attempt, frontier))

        match evidence, closed, ready with
        | None, _, _ -> SubmittedClosureEvidence.NoAttempt
        | Some _, Some _, _ -> SubmittedClosureEvidence.AlreadyClosed
        | Some _, None, None -> SubmittedClosureEvidence.ToolResultMissing
        | Some _, None, Some(attempt, frontier) -> SubmittedClosureEvidence.ToolResultReady(attempt, frontier)

    /// REVIEW-013/017: process-review terminality may stop the provider before
    /// the generic turn-completion observer runs. Once the exact judge tool
    /// result is already durable in XTrace, that result is the last legitimate
    /// part of this tool-only work unit. Freeze the closure at its exclusive
    /// frontier before interrupting, or recover the same fact after a crash.
    ///
    /// Ok true  = closure already existed or was durably appended.
    /// Ok false = the matching durable tool_result is not present yet.
    let ensureSubmittedAttemptClosed
        (journal: AgentJournal)
        (reviewerSessionId: SessionId)
        : Task<Result<bool, string>> =
        let evidence = classifySubmittedClosure reviewerSessionId (AgentJournal.snapshot journal)

        match evidence with
        | SubmittedClosureEvidence.NoAttempt
        | SubmittedClosureEvidence.ToolResultMissing -> Task.FromResult(Ok false)
        | SubmittedClosureEvidence.AlreadyClosed -> Task.FromResult(Ok true)
        | SubmittedClosureEvidence.ToolResultReady(attempt, frontier) ->
            taskResult {
                do! appendSubmittedAttemptClosed journal attempt frontier
                return true
            }

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

    let private reportResolvedReviewerRun
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (runResult: AgentRunResult)
        =
        task {
            if runResult.IsValid then
                do! XTraceCapture.captureTerminal journal turn
                do! appendAttemptClosed journal turn

                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Completed runResult)
                |> ignore
            else
                do! appendAttemptClosed journal turn

                eventPort.NotifyTerminal
                    turn.SessionId
                    (TerminalOutcome.Failed(
                        TerminalStop.forAuthority turn.AuthorityRootUserMessageId "completed with empty terminal output"
                    ))
                |> ignore
        }

    /// Build the `AgentRunResult`, validate via `runResult.IsValid`, capture the
    /// XTrace terminal segment, close the carried attempt, and report.
    /// `allowToolOnlyFallback` lets a review protocol whose physical contract is
    /// one typed `judge` call report a tool-only judgement terminal without
    /// inventing an extra prose round.
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
                    // The typed judge delivery is already durable review evidence;
                    // terminal reporting only needs a non-empty physical completion
                    // value. This does not mint a verdict or a Finality witness.
                    "Review judgement submitted."
                else
                    sessionWide

            // REVIEW-006: nothing is inferred here. Completed confirmation is
            // written only by the direct Finality CE; this path reports the turn.
            // PROMPT-008: the Role comes from the reconciled turn, and there is no
            // default.
            match turn.Role with
            | None ->
                eventPort.NotifyTerminal
                    turn.SessionId
                    (TerminalOutcome.Failed(
                        TerminalStop.forAuthority turn.AuthorityRootUserMessageId "completed with no resolved role"
                    ))
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

                return! reportResolvedReviewerRun eventPort journal turn runResult
        }

    let private reportContinuationFailure
        (eventPort: IEventObservationPort)
        (turn: ReconciledTurn)
        (outcome: Result<unit, string>)
        =
        match outcome with
        | Error reason ->
            eventPort.NotifyTerminal
                turn.SessionId
                (TerminalOutcome.Failed(TerminalStop.forAuthority turn.AuthorityRootUserMessageId reason))
            |> ignore
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

                reportContinuationFailure eventPort turn outcome
            }
            :> Task
        | ReviewerEvidence.Need.CompleteProcessReview -> completeReviewer eventPort journal turn true

    /// REVIEW-ASSURANCE-009: process review is explicitly judge-only. If stable
    /// idle arrives after its verdict is durable, close that physical turn here
    /// instead of entering generic missing-final-report repair. `false` leaves
    /// ordinary routing in ownership of every other idle occasion.
    let tryCompleteProcessReviewAtIdle
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (reviewerKey: string)
        : Task<bool> =
        let resolveIdleDisposition () =
            match ReviewerEvidence.processIdleDisposition journal reviewerKey with
            | ReviewerEvidence.ProcessIdleDisposition.OrdinaryRepair -> Task.FromResult false
            | ReviewerEvidence.ProcessIdleDisposition.CompleteToolOnlyProcessReview ->
                task {
                    do! completeReviewer eventPort journal turn true
                    return true
                }

        if ReviewJudgementInbox.isOwned turn.SessionId then
            Task.FromResult false
        else
            resolveIdleDisposition ()

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
