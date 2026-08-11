namespace Wanxiangshu.Journal

open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal.ProjectionUpdate

module ReviewFactFold =

    let private reject = FoldRejection.reject

    let private verdictOutcome factName projection result =
        match result with
        | Ok updated -> Ok updated
        | Error DuplicateAttempt -> Ok projection
        | Error NotDistinctAttempt ->
            reject factName "confirmed witness violates REVIEW-003 (same provider run or same tool call)"

    let fold (projection: AgentProjectionSet) (fact: ReviewFactCases) : Result<AgentProjectionSet, FoldRejection> =
        // ── review ──────────────────────────────────────────────────────────
        match fact with
        | ReviewFactCases.ReviewBarrierStarted payload ->
            let startBarrier =
                ReviewProjection.startBarrier payload.ManagerSessionId payload.BarrierId payload.GitTreeHash

            projection
            |> updateReviewGuard payload.ReviewerSessionId startBarrier
            |> updateReviewGuard payload.ManagerSessionId startBarrier
            |> Ok

        | ReviewFactCases.PerfectChallengeIssued payload ->
            let challenge =
                { BarrierId = payload.BarrierId
                  GitTreeHash = payload.GitTreeHash
                  ReviewerSessionId = payload.ReviewerSessionId
                  FirstProviderRun = payload.FirstProviderRun
                  FirstToolCallId = payload.FirstToolCallId
                  ChallengeTextVersion = payload.ChallengeTextVersion
                  ChallengeContentDigest = payload.ChallengeContentDigest }

            Ok(updateReviewGuard payload.ReviewerSessionId (ReviewProjection.applyChallengeIssued challenge) projection)

        | ReviewFactCases.ProviderInputSealed payload ->
            let seal =
                { SessionId = payload.SessionId
                  ProviderRun = payload.ProviderRun
                  PhysicalUserMessageId = payload.PhysicalUserMessageId
                  SealDigest = payload.SealDigest
                  CanonicalVersion = payload.CanonicalVersion
                  IncludedToolResultDigests =
                    payload.IncludedToolResultDigests |> List.map SealDigest.value |> Set.ofList }

            Ok(updateReviewGuard payload.SessionId (ReviewProjection.applySeal seal) projection)

        | ReviewFactCases.ReviewVerdictRecorded payload ->
            let attempt =
                { ReviewBarrierId = payload.BarrierId
                  GitTreeHash = payload.GitTreeHash
                  ReviewerSessionId = payload.ReviewerSessionId
                  ProviderRun = payload.ProviderRun
                  ToolCallId = payload.ToolCallId }

            AgentProjection.tryUpdate
                payload.ReviewerSessionId
                (fun session ->
                    ReviewProjection.applyVerdict
                        attempt
                        payload.Verdict
                        (Option.defaultValue ReviewProjection.empty session.ReviewGuard)
                    |> Result.map (fun updated ->
                        { session with
                            ReviewGuard = Some updated }))
                projection
            |> verdictOutcome "ReviewVerdictRecorded" projection

        | ReviewFactCases.ConfirmedReviewWitness payload ->
            // The witness lands on the reviewer session, where the rest of the
            // review facts live; the requirement clearance lands on the Manager,
            // where REVIEW-007's Guard asks. Two sessions, two updates — the
            // previous version only did the second, so a confirmed dual-PERFECT
            // never became a `Confirmed` witness anywhere and the Guard could not
            // pass no matter how many PERFECT verdicts the reviewer submitted.
            //
            // The third update is the Guard's own mirror: `missingTree` reads the
            // MANAGER session's ReviewGuard, and nothing else ever writes it — so
            // without this mirror the guard stayed missing forever and the Manager
            // was nudged on every completion even after its Reviewer confirmed
            // (measured on Host 1.18.10: `guard.IsConfirmed` never true).
            let first =
                { ProviderRun = payload.FirstProviderRun
                  ToolCallId = payload.FirstToolCallId
                  GitTreeHash = payload.GitTreeHash
                  ReviewerSessionId = payload.ReviewerSessionId }

            let second =
                { ProviderRun = payload.SecondProviderRun
                  ToolCallId = payload.SecondToolCallId
                  GitTreeHash = payload.GitTreeHash
                  ReviewerSessionId = payload.ReviewerSessionId }

            AgentProjection.tryUpdate
                payload.ReviewerSessionId
                (fun session ->
                    ReviewProjection.applyConfirmedWitness
                        payload.BarrierId
                        payload.ChallengeResultDigest
                        payload.SecondProviderInputDigest
                        first
                        second
                        (Option.defaultValue ReviewProjection.empty session.ReviewGuard)
                    |> Result.map (fun updated ->
                        { session with
                            ReviewGuard = Some updated }))
                projection
            |> verdictOutcome "ConfirmedReviewWitness" projection
            |> Result.map (
                updateRequirements
                    payload.ManagerSessionId
                    (ReviewRequirementProjection.clearOnConfirmation payload.SecondProviderRun)
            )
            |> Result.map (fun updated ->
                // REVIEW-007 mirror, non-blocking: the reviewer's witness is the
                // durable fact; this copy only lets the Manager's guard answer
                // "is the current tree confirmed" from its own projection. A
                // refusal here must not fail the journal — the confirmation
                // already happened on the reviewer side.
                match
                    AgentProjection.tryUpdate
                        payload.ManagerSessionId
                        (fun session ->
                            ReviewProjection.applyConfirmedWitness
                                payload.BarrierId
                                payload.ChallengeResultDigest
                                payload.SecondProviderInputDigest
                                first
                                second
                                (Option.defaultValue ReviewProjection.empty session.ReviewGuard)
                            |> Result.map (fun mirrored ->
                                { session with
                                    ReviewGuard = Some mirrored }))
                        updated
                with
                | Ok mirrored -> mirrored
                | Error _ -> updated)
