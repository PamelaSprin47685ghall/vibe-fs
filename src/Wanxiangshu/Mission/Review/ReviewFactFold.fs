namespace Wanxiangshu.Mission.Review

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
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable.ProjectionUpdate
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.ProjectionUpdate
open Wanxiangshu.Mission.Review.Barrier

module ReviewFactFold =

    let private reject = FoldRejection.reject

    let private verdictOutcome factName projection result =
        match result with
        | Ok updated -> Ok updated
        | Error DuplicateAttempt -> Ok projection
        | Error NotDistinctAttempt ->
            reject factName "confirmed witness violates REVIEW-003 (same provider run or same tool call)"

    let private exactClosedToolResult (closed: ClosedAttempt) (session: SessionAgentProjection) =
        session.XTrace
        |> Option.bind (fun xTrace ->
            XTraceProjection.parts xTrace
            |> List.tryFind (fun part ->
                part.Kind = "tool_result"
                && part.ProviderRun = Some closed.Attempt.ProviderRun
                && part.ToolCallId = Some closed.Attempt.ToolCallId
                && part.Cursor.Sequence + 1L = closed.FrozenFrontier.Sequence))

    let private applyAttemptClosure (closed: ClosedAttempt) (session: SessionAgentProjection) =
        let closedGuard =
            Option.defaultValue ReviewProjection.empty session.ReviewGuard
            |> ReviewProjection.applyAttemptClosed closed

        let guard =
            exactClosedToolResult closed session
            |> Option.map (fun part ->
                ReviewProjection.recordClosedAttemptFrontier part.TextRef part.TextDigest closed closedGuard)
            |> Option.defaultValue closedGuard

        { session with
            ReviewGuard = Some guard }

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

        | ReviewFactCases.ReviewAttemptClosed payload ->
            let closed =
                { Attempt =
                    { ReviewBarrierId = payload.BarrierId
                      GitTreeHash = payload.GitTreeHash
                      ReviewerSessionId = payload.ReviewerSessionId
                      ProviderRun = payload.ProviderRun
                      ToolCallId = payload.ToolCallId }
                  FrozenFrontier = { Sequence = payload.FrozenFrontierSequence } }

            AgentProjection.tryUpdate payload.ReviewerSessionId (applyAttemptClosure closed >> Ok) projection
            |> function
                | Ok updated -> Ok updated
                | Error error -> reject "ReviewAttemptClosed" error

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
                        payload.FirstPhysicalUserMessageId
                        payload.SecondPhysicalUserMessageId
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
                                payload.FirstPhysicalUserMessageId
                                payload.SecondPhysicalUserMessageId
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
