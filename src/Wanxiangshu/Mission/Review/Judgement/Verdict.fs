namespace Wanxiangshu.Mission.Review.Judgement

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength.Persistence

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
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Persistence.Journal

/// One `verdict` tool call, with every identity needed for durable judgement.
type VerdictSubmission =
    { BarrierId: ReviewBarrierId
      GitTreeHash: GitTreeHash
      ManagerSessionId: SessionId
      ReviewerSessionId: SessionId
      ManagerJobId: ManagerJobId option
      WorktreeIdentity: WorktreeIdentity option
      ProviderRun: ProviderRunIdentity
      ToolCallId: ToolCallId
      Verdict: ReviewGuardVerdict }

[<RequireQualifiedAccess>]
type VerdictDecision =
    | Revised
    | ChallengeIssued of challenge: string
    | Confirmed
    | ChallengeUnproven
    | AlreadyCounted
    /// REVIEW-013: TodoProcessReview one durable judge is terminal VerdictKnown.
    /// No PerfectChallengeIssued / dual-PERFECT witness.
    | ProcessTerminal

/// REVIEW-003/006/010: single writer for PerfectChallengeIssued and
/// ConfirmedReviewWitness. This is Application judgement, not Session runtime.
module VerdictWorkflow =

    let private provenSeal
        (challenge: PerfectChallenge)
        (providerRun: ProviderRunIdentity)
        (guard: ReviewGuardProjection)
        =
        match Map.tryFind providerRun guard.Seals with
        | None -> None
        | Some seal ->
            if Set.contains (SealDigest.value challenge.ChallengeContentDigest) seal.IncludedToolResultDigests then
                Some seal
            else
                None

    let private append (sessionId: SessionId) (providerRun: ProviderRunIdentity) fact journal =
        task {
            match! AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) fact journal with
            | Ok updated -> return Ok updated
            | Error failure -> return Error(JournalAppendFailure.describe failure)
        }

    let private verdictFact (submission: VerdictSubmission) =
        ReviewFact.ReviewVerdictRecorded
            {| ReviewerSessionId = submission.ReviewerSessionId
               ManagerSessionId = submission.ManagerSessionId
               BarrierId = submission.BarrierId
               GitTreeHash = submission.GitTreeHash
               ProviderRun = submission.ProviderRun
               ToolCallId = submission.ToolCallId
               Verdict = submission.Verdict |}

    let submit
        (journal: AgentJournal)
        (sha256: string -> string)
        (submission: VerdictSubmission)
        : Task<Result<VerdictDecision, string>> =
        task {
            let snapshot = AgentJournal.snapshot journal

            let guard =
                AgentProjection.tryFind submission.ReviewerSessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.ReviewGuard)
                |> Option.defaultValue ReviewProjection.empty

            let attempt: ReviewAttemptIdentity =
                { ReviewBarrierId = submission.BarrierId
                  GitTreeHash = submission.GitTreeHash
                  ReviewerSessionId = submission.ReviewerSessionId
                  ProviderRun = submission.ProviderRun
                  ToolCallId = submission.ToolCallId }

            let processReview =
                MagicTodoProjection.pendingProcessReviewForReviewer
                    submission.ReviewerSessionId
                    (AgentJournal.snapshot journal).AgentProjections.MagicTodo
                |> Option.isSome

            if ReviewProjection.hasObservedAttempt attempt guard then
                return Ok VerdictDecision.AlreadyCounted
            elif processReview then
                match! append submission.ReviewerSessionId submission.ProviderRun (verdictFact submission) journal with
                | Error error -> return Error error
                | Ok _ -> return Ok VerdictDecision.ProcessTerminal
            else
                match submission.Verdict with
                | ReviewGuardVerdict.Revise ->
                    match!
                        append submission.ReviewerSessionId submission.ProviderRun (verdictFact submission) journal
                    with
                    | Error error -> return Error error
                    | Ok _ -> return Ok VerdictDecision.Revised

                | ReviewGuardVerdict.Perfect ->
                    match guard.PendingChallenge with
                    | Some challenge when challenge.FirstProviderRun = submission.ProviderRun ->
                        return Ok VerdictDecision.AlreadyCounted

                    | None ->
                        if ReviewProjection.satisfiesGuard submission.GitTreeHash guard then
                            return Ok VerdictDecision.AlreadyCounted
                        else
                            let lang = ProviderProse.languageOf submission.ReviewerSessionId
                            let challengeText = ProviderProse.render lang ReviewChallenge.Path Map.empty
                            let challengePrompt = ProviderProse.document lang ReviewChallenge.Path Map.empty
                            let challengeDigest = ReviewChallenge.contentDigest sha256 challengePrompt

                            match!
                                append
                                    submission.ReviewerSessionId
                                    submission.ProviderRun
                                    (verdictFact submission)
                                    journal
                            with
                            | Error error -> return Error error
                            | Ok _ ->
                                let issued =
                                    ReviewFact.PerfectChallengeIssued
                                        {| BarrierId = submission.BarrierId
                                           GitTreeHash = submission.GitTreeHash
                                           ReviewerSessionId = submission.ReviewerSessionId
                                           FirstProviderRun = submission.ProviderRun
                                           FirstToolCallId = submission.ToolCallId
                                           ChallengeTextVersion = ReviewChallenge.TextVersion
                                           ChallengeContentDigest = challengeDigest |}

                                match! append submission.ReviewerSessionId submission.ProviderRun issued journal with
                                | Error error -> return Error error
                                | Ok _ -> return Ok(VerdictDecision.ChallengeIssued challengeText)

                    | Some challenge ->
                        match provenSeal challenge submission.ProviderRun guard with
                        | None -> return Ok VerdictDecision.ChallengeUnproven
                        | Some seal ->
                            match!
                                append
                                    submission.ReviewerSessionId
                                    submission.ProviderRun
                                    (verdictFact submission)
                                    journal
                            with
                            | Error error -> return Error error
                            | Ok _ ->
                                let witness =
                                    ReviewFact.ConfirmedReviewWitness
                                        {| ManagerJobId = submission.ManagerJobId
                                           ManagerSessionId = submission.ManagerSessionId
                                           ReviewerSessionId = submission.ReviewerSessionId
                                           WorktreeIdentity = submission.WorktreeIdentity
                                           BarrierId = submission.BarrierId
                                           GitTreeHash = submission.GitTreeHash
                                           FirstProviderRun = challenge.FirstProviderRun
                                           FirstToolCallId = challenge.FirstToolCallId
                                           ChallengeResultDigest = challenge.ChallengeContentDigest
                                           SecondProviderRun = submission.ProviderRun
                                           SecondProviderInputDigest = seal.SealDigest
                                           SecondToolCallId = submission.ToolCallId |}

                                match! append submission.ReviewerSessionId submission.ProviderRun witness journal with
                                | Error error -> return Error error
                                | Ok _ -> return Ok VerdictDecision.Confirmed
        }
