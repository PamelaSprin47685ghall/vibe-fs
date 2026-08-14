namespace Wanxiangshu.Review

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

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
