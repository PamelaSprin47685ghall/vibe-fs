namespace Wanxiangshu.Mission.Review.Judgement

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Strength.Persistence

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
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
/// physical delivery is an injected Review port. There is no stored State/Stage counter.
module ReviewerWorkflow =

    let private reviewerHasChronicle (snapshot: ProjectionSet) (reviewerSessionId: SessionId) =
        AgentProjection.tryFind reviewerSessionId snapshot.AgentProjections
        |> Option.bind (fun session -> session.Blog)
        |> Option.map BlogProjection.frames
        |> Option.exists (List.isEmpty >> not)

    let private reviewerHasLinkedBlogger (snapshot: ProjectionSet) (reviewerSessionId: SessionId) =
        SessionAssociationProjection.tryBloggerOf reviewerSessionId snapshot.AgentProjections.Associations
        |> Option.isSome

    let private requireChronicleAfterSettlement (journal: AgentJournal) (reviewerSessionId: SessionId) =
        if reviewerHasChronicle (AgentJournal.snapshot journal) reviewerSessionId then
            Ok()
        else
            Error "reviewer Blogger producer settled without a durable Chronicle frame"

    [<RequireQualifiedAccess>]
    type private SubmittedRecordCaptureDecision =
        | AlreadyCaptured
        | NoBloggerRequired
        | AwaitFirstChronicle

    let private decideSubmittedRecordCapture (snapshot: ProjectionSet) (reviewerSessionId: SessionId) =
        if reviewerHasChronicle snapshot reviewerSessionId then
            SubmittedRecordCaptureDecision.AlreadyCaptured
        elif not (reviewerHasLinkedBlogger snapshot reviewerSessionId) then
            SubmittedRecordCaptureDecision.NoBloggerRequired
        else
            SubmittedRecordCaptureDecision.AwaitFirstChronicle

    let private resultAfterProducerSettlement
        (journal: AgentJournal)
        (reviewerSessionId: SessionId)
        (settlement: BloggerRuntimeHost.ProducerSettlement)
        =
        let hasChronicle =
            reviewerHasChronicle (AgentJournal.snapshot journal) reviewerSessionId

        match settlement, hasChronicle with
        | BloggerRuntimeHost.ProducerSettlement.Committed, _ ->
            requireChronicleAfterSettlement journal reviewerSessionId
        | BloggerRuntimeHost.ProducerSettlement.NoOpenProducer, true -> Ok()
        | BloggerRuntimeHost.ProducerSettlement.NoOpenProducer, false ->
            Error "linked Reviewer has no durable Chronicle and no open Blogger producer"
        | BloggerRuntimeHost.ProducerSettlement.Abandoned, _ ->
            Error "Reviewer Blogger producer was abandoned before Chronicle capture"
        | BloggerRuntimeHost.ProducerSettlement.Cancelled, _ ->
            Error "Reviewer Blogger producer wait was cancelled before Chronicle capture"

    let private awaitFirstChronicle
        (cancellation: CancellationToken)
        (journal: AgentJournal)
        (reviewerSessionId: SessionId)
        : Task<Result<unit, string>> =
        task {
            let! settlement = BloggerRuntimeHost.awaitOpenProducerSettlement cancellation journal reviewerSessionId
            return resultAfterProducerSettlement journal reviewerSessionId settlement
        }

    /// REVIEW-013 / FINALITY-011 race closure: a terminal judge may only abort
    /// the managed Reviewer after the Blogger producer that was already
    /// durable-open for that Reviewer has settled. Otherwise AbortSession can
    /// win the physical race against the Blogger's next transform and starve the
    /// record-ready Chronicle forever. The verdict remains durable immediately;
    /// this barrier owns only physical interrupt ordering.
    let awaitSubmittedRecordCapture
        (cancellation: CancellationToken)
        (journal: AgentJournal)
        (reviewerSessionId: SessionId)
        : Task<Result<unit, string>> =
        let initial = AgentJournal.snapshot journal

        match decideSubmittedRecordCapture initial reviewerSessionId with
        | SubmittedRecordCaptureDecision.AlreadyCaptured
        | SubmittedRecordCaptureDecision.NoBloggerRequired -> Task.FromResult(Ok())
        | SubmittedRecordCaptureDecision.AwaitFirstChronicle ->
            awaitFirstChronicle cancellation journal reviewerSessionId

    [<RequireQualifiedAccess>]
    type private SubmittedClosureEvidence =
        | NoAttempt
        | AlreadyClosed
        | ToolResultMissing
        | ToolResultReady of attempt: ReviewAttemptIdentity * frontier: XTraceCursor

    let private matchingToolResultFrontier
        (attempt: ReviewAttemptIdentity)
        (xTrace: XTraceProjectionState)
        : XTraceCursor option =
        XTraceProjection.toolResultParts attempt.ProviderRun attempt.ToolCallId xTrace
        |> List.tryHead
        |> Option.map XTraceProjection.frontierAfter

    let private tryAppendAgent
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: Fact.AgentFact)
        (journal: AgentJournal)
        : Task<Result<ProjectionSet, JournalAppendFailure>> =
        task {
            try
                return! AgentJournal.appendAgent stream providerRun fact journal
            with ex ->
                return
                    Error(
                        JournalAppendFailure.WriteUnknown(
                            EventId.create "err",
                            Outcome.JournalFailure.WriteFailed ex.Message
                        )
                    )
        }

    let private appendSubmittedAttemptClosed
        (journal: AgentJournal)
        (attempt: ReviewAttemptIdentity)
        (frontier: XTraceCursor)
        : Task<Result<unit, string>> =
        task {
            let closed =
                ReviewFact.ReviewAttemptClosed
                    {| ReviewerSessionId = attempt.ReviewerSessionId
                       BarrierId = attempt.ReviewBarrierId
                       GitTreeHash = attempt.GitTreeHash
                       ProviderRun = attempt.ProviderRun
                       ToolCallId = attempt.ToolCallId
                       FrozenFrontierSequence = XTraceCursor.sequence frontier |}

            match!
                tryAppendAgent (StreamId.Session attempt.ReviewerSessionId) (Some attempt.ProviderRun) closed journal
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
                session.ReviewGuard |> Option.bind (ReviewProjection.closedAttemptOf attempt))

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

    /// Freeze the closure at its exclusive frontier before interrupting,
    /// or recover the same fact after a crash.
    ///
    /// Ok true  = closure already existed or was durably appended.
    /// Ok false = the matching durable tool_result is not present yet.
    let ensureSubmittedAttemptClosed
        (journal: AgentJournal)
        (reviewerSessionId: SessionId)
        : Task<Result<bool, string>> =
        let evidence =
            classifySubmittedClosure reviewerSessionId (AgentJournal.snapshot journal)

        match evidence with
        | SubmittedClosureEvidence.NoAttempt
        | SubmittedClosureEvidence.ToolResultMissing -> Task.FromResult(Ok false)
        | SubmittedClosureEvidence.AlreadyClosed -> Task.FromResult(Ok true)
        | SubmittedClosureEvidence.ToolResultReady(attempt, frontier) ->
            taskResult {
                do! appendSubmittedAttemptClosed journal attempt frontier
                return true
            }

    /// The latest verdict attempt this turn carried plus the captured terminal
    /// frontier (or current trace frontier when completion was rejected).
    /// `None` when the turn produced no verdict.
    let private closedAttemptEvidence (journal: AgentJournal option) (turn: ReconciledTurn) =
        journal
        |> Option.bind (fun durable ->
            let snapshot = AgentJournal.snapshot durable
            AgentProjection.tryFind turn.SessionId snapshot.AgentProjections
            |> Option.bind (fun session ->
                session.ReviewGuard
                |> Option.bind ReviewProjection.latestObservedAttempt
                |> Option.map (fun attempt ->
                    let frontier =
                        session.XTrace
                        |> Option.map (fun xTrace ->
                            XTraceProjection.terminalEvidenceForProviderRun turn.ProviderRun xTrace
                            |> Option.map (fun terminal -> terminal.Frontier)
                            |> Option.defaultWith (fun () -> XTraceProjection.headCursor xTrace))
                        |> Option.defaultValue XTraceCursor.originCursor

                    attempt, frontier)))

    /// One closure append, one flat result match.
    let private writeAttemptClosed
        (journal: AgentJournal)
        (turn: ReconciledTurn)
        (attempt: ReviewAttemptIdentity)
        (frontier: XTraceCursor)
        : Task =
        task {
            let closed =
                ReviewFact.ReviewAttemptClosed
                    {| ReviewerSessionId = turn.SessionId
                       BarrierId = attempt.ReviewBarrierId
                       GitTreeHash = attempt.GitTreeHash
                       ProviderRun = attempt.ProviderRun
                       ToolCallId = attempt.ToolCallId
                       FrozenFrontierSequence = XTraceCursor.sequence frontier |}

            let! appended =
                AgentJournal.appendAgent (StreamId.Session turn.SessionId) (Some attempt.ProviderRun) closed journal

            match appended with
            | Ok _ -> ()
            | Error _ -> ()
        }

    /// Append the closure fact at turn completion. Flat by
    /// construction — each decision is a single top-level match.
    let private appendAttemptClosed (journal: AgentJournal option) (turn: ReconciledTurn) : Task =
        match closedAttemptEvidence journal turn with
        | None -> AsyncSupport.completedTask ()
        | Some(attempt, frontier) -> writeAttemptClosed (Option.get journal) turn attempt frontier

    let private reportCaptureFailure
        (eventPort: IEventObservationPort)
        (turn: ReconciledTurn)
        (error: XTraceCaptureError)
        =
        let reason =
            match error with
            | XTraceCaptureError.Refused detail -> sprintf "review terminal capture refused: %s" detail
            | XTraceCaptureError.StorageFailed detail ->
                sprintf "review terminal capture storage failed: %s" detail

        eventPort.NotifyTerminal
            turn.SessionId
            (TerminalOutcome.Failed(TerminalStop.forAuthority turn.AuthorityRootUserMessageId reason))
        |> ignore

    let private reportToolOnlyReviewerRun
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (role: Role)
        =
        task {
            let terminalText = "Review judgement submitted."

            let runResult: AgentRunResult =
                { SessionId = turn.SessionId
                  AuthorityRootUserMessageId = turn.AuthorityRootUserMessageId
                  ProviderRun = turn.ProviderRun
                  Role = AgentRoleIdentity.toRole role
                  Directory = turn.Directory
                  TerminalText = terminalText
                  TurnFormalText = CompletedTurnClassifier.partsText turn.Parts }

            match!
                XTraceCapture.captureTerminalTextWithReceipt
                    journal
                    turn.SessionId
                    terminalText
                    turn.ProviderRun
            with
            | Ok _ ->
                do! appendAttemptClosed journal turn

                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Completed runResult)
                |> ignore
            | Error error -> reportCaptureFailure eventPort turn error
        }

    [<RequireQualifiedAccess>]
    type private ReviewerCompletionDecision =
        | ToolOnlyFallback of Role
        | TurnEvidence

    let private decideReviewerCompletion (sessionWide: string) (role: Role option) =
        match String.IsNullOrWhiteSpace sessionWide, role with
        | true, Some resolved -> ReviewerCompletionDecision.ToolOnlyFallback resolved
        | _ -> ReviewerCompletionDecision.TurnEvidence

    let private reportReviewerTurnEvidence
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        : Task =
        task {
            match! TerminalReporter.completeWithEvidence eventPort journal turn with
            | XTraceTerminalCompletion.Published _ -> do! appendAttemptClosed journal turn
            | XTraceTerminalCompletion.CaptureFailed error -> reportCaptureFailure eventPort turn error
            | XTraceTerminalCompletion.RejectedEmptyOutput -> do! appendAttemptClosed journal turn
            | XTraceTerminalCompletion.RejectedMissingRole -> ()
        }

    /// Build the `AgentRunResult`, validate via `runResult.IsValid`, capture the
    /// XTrace terminal segment, close the carried attempt, and report.
    let private completeReviewer
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        : Task =
        let sessionWide = CompletedTurnClassifier.partsSessionText turn.Parts

        match decideReviewerCompletion sessionWide turn.Role with
        | ReviewerCompletionDecision.ToolOnlyFallback role ->
            reportToolOnlyReviewerRun eventPort journal turn role
        | ReviewerCompletionDecision.TurnEvidence -> reportReviewerTurnEvidence eventPort journal turn

    /// Physical terminal observer. Active Finality reviewers are owned by the
    /// direct ReviewBarrierWorkflow CE, so this function reports their turn.
    let observe
        (_continuationPort: ReviewerContinuationPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (_reviewerKey: string)
        : Task =
        completeReviewer eventPort journal turn
