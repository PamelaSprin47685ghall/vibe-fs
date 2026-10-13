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

    let private lifecycleWorkOf
        (store: IEventStore)
        (workId: string)
        : Result<Wanxiangshu.Mission.Lifecycle.LifecycleWork, PlanReviewError> =
        match LifecycleKernel.tryWorkOfStore store workId with
        | None -> Error PlanReviewError.NoActiveIncumbency
        | Some work -> Ok work

    // planning-022: each incumbency reviews exactly once. The kernel refuses a
    // second review for the same tenure; that refusal is the already-reviewed
    // error, not a record failure.
    let executeReview
        (store: IEventStore)
        (workId: string)
        (incumbencyId: string)
        (gaps: (string * string) list)
        : Task<Result<PlanReviewResult, PlanReviewError>> =
        task {
            match lifecycleWorkOf store workId with
            | Error error -> return Error error
            | Ok work ->
                match work.Active with
                | None -> return Error PlanReviewError.NoActiveIncumbency
                | Some active when active.Id <> incumbencyId ->
                    return Error(PlanReviewError.IncumbencyMismatch(active.Id, incumbencyId))
                | Some _ ->
                    let outcome = reviewOutcome gaps

                    match
                        LifecycleKernel.review work incumbencyId outcome
                    with
                    | KernelDecision.Refuse reason ->
                        if reason = "incumbency already reviewed" then
                            return Error(PlanReviewError.AlreadyReviewed incumbencyId)
                        else
                            return Error(PlanReviewError.RecordFailed reason)
                    | KernelDecision.Done _ ->
                        return Error(PlanReviewError.RecordFailed "review was not written")
                    | KernelDecision.Write(events, _) ->
                        match! LifecycleKernel.commit store workId events with
                        | Ok(CommitOutcome.AppendDurable) ->
                            return Ok { Outcome = outcome; Gaps = gaps }
                        | Ok _ -> return Error(PlanReviewError.RecordFailed "review commit is not durable")
                        | Error reason -> return Error(PlanReviewError.RecordFailed reason)
        }
