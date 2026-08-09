namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// GLORY-042/043/044: the ONE review program shared by the Orchestrator's
/// post-rebase barrier and the Manager's Finality workflow. It forks/enlists,
/// opens the barrier, and waits on durable reviewer evidence. ReviewerWorkflow
/// alone sends a missing-verdict guard or the confirmation challenge.
///
/// REVISE is a legal business outcome (`Ok(RevisionRequired ...)`), never an
/// error (GLORY-044). Infrastructure failures are the only `Error`s.
module HostReviewProgram =

    [<RequireQualifiedAccess>]
    type HostReviewOutcome =
        | Confirmed of reviewerSessionId: SessionId * barrierId: ReviewBarrierId * gitTreeHash: GitTreeHash
        | RevisionRequired of
            reviewerSessionId: SessionId *
            barrierId: ReviewBarrierId *
            gitTreeHash: GitTreeHash

    type HostReviewFailure =
        | CannotReadTree of string
        | CannotCreateReviewer of string
        | CannotOpenBarrier of string
        | CannotSendPrompt of string
        | CannotAwaitReviewer of string
        | ReviewerProducedNoVerdict
        | ConfirmationUnproven
        | WorkRecordUnavailable
        | JournalFailure of string

    let private readOutcome
        (journal: AgentJournal option)
        (managerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (reviewerSessionId: SessionId)
        (tree: GitTreeHash)
        : Result<HostReviewOutcome, HostReviewFailure> =
        match OrchestratorReviewRead.read journal reviewerSessionId tree with
        | OrchestratorReviewRead.Confirmed -> Ok(HostReviewOutcome.Confirmed(reviewerSessionId, barrierId, tree))
        | OrchestratorReviewRead.RevisionRequired -> Ok(HostReviewOutcome.RevisionRequired(reviewerSessionId, barrierId, tree))
        | OrchestratorReviewRead.PendingConfirmation -> Error HostReviewFailure.ConfirmationUnproven
        | OrchestratorReviewRead.NeedsReview -> Error HostReviewFailure.ReviewerProducedNoVerdict

    /// One review barrier, driven by any host-owned caller.
    ///
    /// `forkReviewer` returns the reviewer's Host session id; `awaitReviewer`
    /// waits for that run to reach terminal. They are separate because the
    /// barrier fact must be written between them — after the session exists,
    /// before any verdict arrives (REVIEW-008). The tree is read by the caller
    /// and passed in: an unreadable tree yields a sentinel hash that
    /// `satisfiesGuard` can never match, so it cannot pass as confirmed
    /// (GLORY-037.14 fail closed).
    let reverify
        (journal: AgentJournal option)
        (forkReviewer: unit -> Task<Result<SessionId, string>>)
        (awaitReviewer: unit -> Task<Result<unit, string>>)
        (managerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (tree: GitTreeHash)
        : Task<Result<HostReviewOutcome, HostReviewFailure>> =
        task {
            match! forkReviewer () with
            | Error error -> return Error(HostReviewFailure.CannotCreateReviewer error)
            | Ok reviewerSessionId ->
                match HostReviewGuard.openBarrier journal managerSessionId reviewerSessionId barrierId tree with
                | Error error -> return Error(HostReviewFailure.CannotOpenBarrier error)
                | Ok() ->
                    // Finality owns the barrier and observes facts only. A reviewer
                    // terminal wakes ReviewerWorkflow, the sole continuation writer;
                    // this CE then waits for the next terminal until durable evidence
                    // says Revision or Confirmed. It never sends a guard/challenge.
                    let rec awaitWitness () =
                        task {
                            match! awaitReviewer () with
                            | Error error -> return Error(HostReviewFailure.CannotAwaitReviewer error)
                            | Ok() ->
                                match readOutcome journal managerSessionId barrierId reviewerSessionId tree with
                                | Ok outcome -> return Ok outcome
                                | Error HostReviewFailure.ReviewerProducedNoVerdict
                                | Error HostReviewFailure.ConfirmationUnproven -> return! awaitWitness ()
                                | Error failure -> return Error failure
                        }

                    return! awaitWitness ()
        }
