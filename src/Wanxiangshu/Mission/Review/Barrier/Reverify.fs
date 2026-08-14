namespace Wanxiangshu.Mission.Review.Barrier

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
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
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
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

[<RequireQualifiedAccess>]
type ReviewBarrierOutcome =
    | Confirmed of reviewerSessionId: SessionId * barrierId: ReviewBarrierId * gitTreeHash: GitTreeHash
    | RevisionRequired of reviewerSessionId: SessionId * barrierId: ReviewBarrierId * gitTreeHash: GitTreeHash

type ReviewBarrierFailure =
    | CannotCreateReviewer of string
    | CannotOpenBarrier of string
    | CannotAwaitReviewer of string
    | ReviewerProducedNoVerdict
    | ConfirmationUnproven

/// Application CE for one review barrier. Host creation/waiting are injected;
/// durable evidence decides whether the barrier is confirmed or revised.
module ReviewBarrierWorkflow =

    [<RequireQualifiedAccess>]
    type private ReviewStatus =
        | Confirmed
        | PendingConfirmation
        | NeedsReview
        | RevisionRequired

    let private readStatus (journal: AgentJournal option) (reviewerSessionId: SessionId) (tree: GitTreeHash) =
        let guard =
            journal
            |> Option.bind (fun durable ->
                AgentProjection.tryFind reviewerSessionId (AgentJournal.snapshot durable).AgentProjections)
            |> Option.bind (fun session -> session.ReviewGuard)

        match guard with
        | None -> ReviewStatus.NeedsReview
        | Some value ->
            if ReviewProjection.satisfiesGuard tree value then
                ReviewStatus.Confirmed
            elif
                ReviewWitness.isRevision value.Witness
                && value.LastGitTreeHash = Some tree
                && value.CurrentBarrierId.IsSome
            then
                ReviewStatus.RevisionRequired
            elif
                ReviewWitness.isPerfectPending value.Witness
                && value.LastGitTreeHash = Some tree
            then
                ReviewStatus.PendingConfirmation
            else
                ReviewStatus.NeedsReview

    let private readOutcome
        (journal: AgentJournal option)
        (barrierId: ReviewBarrierId)
        (reviewerSessionId: SessionId)
        (tree: GitTreeHash)
        : Result<ReviewBarrierOutcome, ReviewBarrierFailure> =
        match readStatus journal reviewerSessionId tree with
        | ReviewStatus.Confirmed -> Ok(ReviewBarrierOutcome.Confirmed(reviewerSessionId, barrierId, tree))
        | ReviewStatus.RevisionRequired -> Ok(ReviewBarrierOutcome.RevisionRequired(reviewerSessionId, barrierId, tree))
        | ReviewStatus.PendingConfirmation -> Error ReviewBarrierFailure.ConfirmationUnproven
        | ReviewStatus.NeedsReview -> Error ReviewBarrierFailure.ReviewerProducedNoVerdict

    /// Fork/enlist reviewer, open durable barrier, then re-enter on each reviewer
    /// terminal until durable evidence becomes decisive.
    let reverify
        (journal: AgentJournal option)
        (host: ReviewHostPort)
        (managerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (tree: GitTreeHash)
        : Task<Result<ReviewBarrierOutcome, ReviewBarrierFailure>> =
        task {
            match! host.ForkReviewer() with
            | Error error -> return Error(ReviewBarrierFailure.CannotCreateReviewer error)
            | Ok reviewerSessionId ->
                match! ReviewBarrier.openBarrier journal managerSessionId reviewerSessionId barrierId tree with
                | Error error -> return Error(ReviewBarrierFailure.CannotOpenBarrier error)
                | Ok() ->
                    let rec awaitWitness () =
                        task {
                            let descriptor =
                                DiagnosticWait.create
                                    "reviewer-terminal"
                                    (CausalOwner.create
                                        "ReviewBarrierWorkflow"
                                        [ "manager", SessionId.value managerSessionId
                                          "barrier", ReviewBarrierId.value barrierId ])
                                    [ "manager", SessionId.value managerSessionId
                                      "reviewer", SessionId.value reviewerSessionId
                                      "barrier", ReviewBarrierId.value barrierId ]
                                    (WorkflowProducer(
                                        CausalOwner.create
                                            "ReviewerWorkflow"
                                            [ "session", SessionId.value reviewerSessionId
                                              "barrier", ReviewBarrierId.value barrierId ]
                                    ))
                                    [ WaitEscape.SessionLifetime
                                      WaitEscape.CancelledBy(
                                          CausalOwner.create
                                              "ReviewBarrierWorkflow"
                                              [ "manager", SessionId.value managerSessionId ]
                                      ) ]
                                    "ReviewBarrierWorkflow.awaitWitness"

                            match! CausalAwait.awaitTask CausalWaitHub.observer descriptor (host.AwaitReviewer()) with
                            | Error error -> return Error(ReviewBarrierFailure.CannotAwaitReviewer error)
                            | Ok() ->
                                match readOutcome journal barrierId reviewerSessionId tree with
                                | Ok outcome -> return Ok outcome
                                | Error ReviewBarrierFailure.ReviewerProducedNoVerdict
                                | Error ReviewBarrierFailure.ConfirmationUnproven -> return! awaitWitness ()
                                | Error failure -> return Error failure
                        }

                    return! awaitWitness ()
        }
