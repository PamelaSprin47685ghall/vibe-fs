namespace Wanxiangshu.Review

open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal

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
        match AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) fact journal with
        | Ok updated -> Ok updated
        | Error failure -> Error(JournalAppendFailure.describe failure)

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
        : Result<VerdictDecision, string> =
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

        if ReviewProjection.hasObservedAttempt attempt guard then
            Ok VerdictDecision.AlreadyCounted
        else
            match submission.Verdict with
            | ReviewGuardVerdict.Revise ->
                append submission.ReviewerSessionId submission.ProviderRun (verdictFact submission) journal
                |> Result.map (fun _ -> VerdictDecision.Revised)

            | ReviewGuardVerdict.Perfect ->
                match guard.PendingChallenge with
                | Some challenge when challenge.FirstProviderRun = submission.ProviderRun ->
                    Ok VerdictDecision.AlreadyCounted

                | None ->
                    if ReviewProjection.satisfiesGuard submission.GitTreeHash guard then
                        Ok VerdictDecision.AlreadyCounted
                    else
                        let challengeDigest = ReviewChallenge.contentDigest sha256

                        append submission.ReviewerSessionId submission.ProviderRun (verdictFact submission) journal
                        |> Result.bind (fun _ ->
                            let issued =
                                ReviewFact.PerfectChallengeIssued
                                    {| BarrierId = submission.BarrierId
                                       GitTreeHash = submission.GitTreeHash
                                       ReviewerSessionId = submission.ReviewerSessionId
                                       FirstProviderRun = submission.ProviderRun
                                       FirstToolCallId = submission.ToolCallId
                                       ChallengeTextVersion = ReviewChallenge.TextVersion
                                       ChallengeContentDigest = challengeDigest |}

                            append submission.ReviewerSessionId submission.ProviderRun issued journal)
                        |> Result.map (fun _ -> VerdictDecision.ChallengeIssued ReviewChallenge.Text)

                | Some challenge ->
                    match provenSeal challenge submission.ProviderRun guard with
                    | None -> Ok VerdictDecision.ChallengeUnproven
                    | Some seal ->
                        append submission.ReviewerSessionId submission.ProviderRun (verdictFact submission) journal
                        |> Result.bind (fun _ ->
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

                            append submission.ReviewerSessionId submission.ProviderRun witness journal)
                        |> Result.map (fun _ -> VerdictDecision.Confirmed)
