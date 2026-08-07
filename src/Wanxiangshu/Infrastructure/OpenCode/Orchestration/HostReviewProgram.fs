namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// GLORY-042/043/044: the ONE review program shared by the Orchestrator's
/// post-rebase barrier and the Manager's Finality workflow. Fork a reviewer,
/// open its barrier, await the first verdict, and — when the first verdict is a
/// PERFECT — nudge the SAME reviewer with `ReviewChallenge.Prompt` and require
/// a causally confirmed second PERFECT.
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
            gitTreeHash: GitTreeHash *
            workRecord: string

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

    /// GLORY-049: the work record is the reviewer's canonical LWR
    /// (includeOpening=false). `None` means the LWR is unavailable — that is an
    /// infrastructure failure, never a wound record (GLORY-051/056).
    let private workRecordOf (journal: AgentJournal option) (reviewerSessionId: SessionId) =
        match XTraceCapture.lifecycleWorkRecord journal reviewerSessionId false with
        | Some record when not (System.String.IsNullOrWhiteSpace record) -> Some record
        | _ -> None

    let private readOutcome
        (journal: AgentJournal option)
        (managerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (reviewerSessionId: SessionId)
        (tree: GitTreeHash)
        : Result<HostReviewOutcome, HostReviewFailure> =
        match OrchestratorReviewRead.read journal reviewerSessionId tree with
        | OrchestratorReviewRead.Confirmed -> Ok(HostReviewOutcome.Confirmed(reviewerSessionId, barrierId, tree))
        | OrchestratorReviewRead.RevisionRequired ->
            match workRecordOf journal reviewerSessionId with
            | Some record -> Ok(HostReviewOutcome.RevisionRequired(reviewerSessionId, barrierId, tree, record))
            | None -> Error HostReviewFailure.WorkRecordUnavailable
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
        (continueReviewer: string -> Task<Result<unit, string>>)
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
                    match! awaitReviewer () with
                    | Error error -> return Error(HostReviewFailure.CannotAwaitReviewer error)
                    | Ok() ->
                        match readOutcome journal managerSessionId barrierId reviewerSessionId tree with
                        | Ok outcome -> return Ok outcome
                        | Error HostReviewFailure.ReviewerProducedNoVerdict ->
                            match! continueReviewer RuntimeNudge.reviewerVerdictGuard with
                            | Error error -> return Error(HostReviewFailure.CannotSendPrompt error)
                            | Ok() ->
                                match readOutcome journal managerSessionId barrierId reviewerSessionId tree with
                                | Ok outcome -> return Ok outcome
                                | Error failure -> return Error failure
                        | Error HostReviewFailure.ConfirmationUnproven ->
                            match! continueReviewer ReviewChallenge.Prompt with
                            | Error error -> return Error(HostReviewFailure.CannotSendPrompt error)
                            | Ok() ->
                                match readOutcome journal managerSessionId barrierId reviewerSessionId tree with
                                | Ok outcome -> return Ok outcome
                                | Error _ -> return Error HostReviewFailure.ConfirmationUnproven
                        | Error failure -> return Error failure
        }
