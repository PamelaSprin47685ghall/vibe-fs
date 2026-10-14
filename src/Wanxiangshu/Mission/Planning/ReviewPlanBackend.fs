namespace Wanxiangshu.Mission.Planning

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.Persistence.EventStore

/// planning-022: the outcome of one review gate on a plan incumbency.
type PlanReviewResult =
    { Outcome: ReviewOutcome
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

    let private reviewOutcome (gaps: (string * string) list) : ReviewOutcome =
        if List.isEmpty gaps then
            ReviewOutcome.Promoted
        else
            ReviewOutcome.Stayed

    // planning-022: each incumbency reviews exactly once. The kernel refuses a
    // second review for the same tenure; that refusal is the already-reviewed
    // error, not a record failure.
    /// The reviewer is never the incumbency owner. A plan work opens with the
    /// session id as owner, so the reviewer must be a distinct bound identity.
    [<Literal>]
    let private ReviewerId = "plan-reviewer"

    let executeReview
        (gitCommonDir: string)
        (store: IEventStore)
        (workId: string)
        (incumbencyId: string)
        (gaps: (string * string) list)
        : Task<Result<PlanReviewResult, PlanReviewError>> =
        task {
            if String.IsNullOrWhiteSpace gitCommonDir then
                return Error(PlanReviewError.RecordFailed "git common directory is missing")
            else
                match Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected gitCommonDir workId with
                | None -> return Error PlanReviewError.NoActiveIncumbency
                | Some work ->
                    match work.ActiveId with
                    | None -> return Error PlanReviewError.NoActiveIncumbency
                    | Some active when active <> incumbencyId ->
                        return Error(PlanReviewError.IncumbencyMismatch(active, incumbencyId))
                    | Some _ ->
                        let outcome = reviewOutcome gaps
                        let findings =
                            gaps
                            |> List.map (fun (left, right) -> left + "\n" + right)
                            |> List.toArray
                            |> box
                        let requestId = incumbencyId + ":review"
                        let command =
                            DurableWorkHost.command
                                "Review"
                                [ "incumbencyId", box incumbencyId
                                  "reviewerId", box ReviewerId
                                  "findings", findings ]
                        let receipt = DurableWorkHost.execute gitCommonDir workId requestId command
                        // The library refuses a second review under either a
                        // fresh request id (Incumbency already reviewed) or the
                        // same request id with different bytes (RequestIdConflict).
                        if receipt.Reason = "Incumbency already reviewed" || receipt.Reason = "RequestIdConflict" then
                            return Error(PlanReviewError.AlreadyReviewed incumbencyId)
                        elif receipt.Status = "Committed" || receipt.Status = "Replayed" then
                            return Ok { Outcome = outcome; Gaps = gaps }
                        else
                            return Error(PlanReviewError.RecordFailed(if receipt.Reason = "" then receipt.Status else receipt.Reason))
        }
