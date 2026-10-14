namespace Wanxiangshu.Mission.Planning

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// planning-022: one plan review either promotes the incumbency or keeps it
/// in place.
[<RequireQualifiedAccess>]
type PlanReviewOutcome =
    | Promoted
    | Stayed

/// planning-022: the outcome of one review gate on a plan incumbency.
type PlanReviewResult =
    { Outcome: PlanReviewOutcome
      Gaps: (string * string) list }

[<RequireQualifiedAccess>]
type PlanReviewError =
    | NoActiveIncumbency
    | AlreadyReviewed of incumbencyId: string
    | IncumbencyMismatch of activeId: string * targetId: string
    | RecordFailed of reason: string

module PlanReviewError =
    let render (error: PlanReviewError) : string =
        match error with
        | PlanReviewError.NoActiveIncumbency -> "No active incumbency found for this work"
        | PlanReviewError.AlreadyReviewed incumbencyId ->
            sprintf
                "This incumbency has already submitted its review; each tenure reviews exactly once (%s)"
                incumbencyId
        | PlanReviewError.IncumbencyMismatch(activeId, targetId) ->
            sprintf "Active incumbency ID '%s' does not match review target '%s'" activeId targetId
        | PlanReviewError.RecordFailed reason -> sprintf "Failed to record plan review: %s" reason

module ReviewPlanBackend =

    let reviewTargetFor (stage: string) : string =
        match stage with
        | "S1" -> "S2"
        | "S2" -> "S3"
        | "S3" -> "authorized"
        | _ -> raise (ArgumentException(sprintf "Unknown plan stage: %s" stage))

    let private reviewOutcome (gaps: (string * string) list) : PlanReviewOutcome =
        if List.isEmpty gaps then
            PlanReviewOutcome.Promoted
        else
            PlanReviewOutcome.Stayed

    // planning-022: each incumbency reviews exactly once. The kernel refuses a
    // second review for the same tenure; that refusal is the already-reviewed
    // error, not a record failure.
    /// The reviewer is never the incumbency owner. A plan work opens with the
    /// session id as owner, so the reviewer must be a distinct bound identity.
    [<Literal>]
    let private ReviewerId = "plan-reviewer"

    let private activeIdResult (activeId: string option) (incumbencyId: string) : Result<unit, PlanReviewError> =
        match activeId with
        | None -> Error PlanReviewError.NoActiveIncumbency
        | Some active when active <> incumbencyId -> Error(PlanReviewError.IncumbencyMismatch(active, incumbencyId))
        | Some _ -> Ok()

    let private prepareReview
        (gitCommonDir: string)
        (workId: string)
        (incumbencyId: string)
        : Result<unit, PlanReviewError> =
        if String.IsNullOrWhiteSpace gitCommonDir then
            Error(PlanReviewError.RecordFailed "git common directory is missing")
        else
            let activeId =
                Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected gitCommonDir workId
                |> Option.bind (fun work -> work.ActiveId)

            activeIdResult activeId incumbencyId

    let private executePreparedReview
        (gitCommonDir: string)
        (workId: string)
        (incumbencyId: string)
        (gaps: (string * string) list)
        : Task<Result<PlanReviewResult, PlanReviewError>> =
        task {
            let outcome = reviewOutcome gaps

            let findings =
                gaps
                |> List.map (fun (left, right) -> left + "\n" + right)
                |> List.toArray
                |> box

            let requestId = incumbencyId + ":review"

            let command =
                Wanxiangshu.Mission.Lifecycle.DurableWorkHost.command
                    "Review"
                    [ "incumbencyId", box incumbencyId
                      "reviewerId", box ReviewerId
                      "findings", findings ]

            let receipt =
                Wanxiangshu.Mission.Lifecycle.DurableWorkHost.execute gitCommonDir workId requestId command

            // The library refuses a second review under either a
            // fresh request id (Incumbency already reviewed) or the
            // same request id with different bytes (RequestIdConflict).
            if
                receipt.Reason = "Incumbency already reviewed"
                || receipt.Reason = "RequestIdConflict"
            then
                return Error(PlanReviewError.AlreadyReviewed incumbencyId)
            elif receipt.Status = "Committed" || receipt.Status = "Replayed" then
                return Ok { Outcome = outcome; Gaps = gaps }
            else
                return
                    Error(
                        PlanReviewError.RecordFailed(
                            if receipt.Reason = "" then
                                receipt.Status
                            else
                                receipt.Reason
                        )
                    )
        }

    let executeReview
        (gitCommonDir: string)
        (store: IEventStore)
        (workId: string)
        (incumbencyId: string)
        (gaps: (string * string) list)
        : Task<Result<PlanReviewResult, PlanReviewError>> =
        task {
            match prepareReview gitCommonDir workId incumbencyId with
            | Error error -> return Error error
            | Ok() -> return! executePreparedReview gitCommonDir workId incumbencyId gaps
        }
