namespace Wanxiangshu.Mission.Planning

open System.Threading.Tasks

/// work-038: one plan review fixes the promotion eligibility of an incumbency.
/// A review never advances the stage: promotion is the deliver tool's job.
[<RequireQualifiedAccess>]
type PlanReviewOutcome =
    | Promoted
    | Stayed

/// work-038: the outcome of one review on a plan incumbency.
/// Promoted means ELIGIBLE (first review empty), not promoted; Stayed means
/// obligations remain. FirstReview is true only when this review fixed the
/// tenure's eligibility. Gaps carries the remaining (non-deleted) ledger.
type PlanReviewResult =
    { Outcome: PlanReviewOutcome
      Target: string
      Gaps: (string * string) list
      FirstReview: bool }

/// work-038: typed rejections for the plan review gate. Multiple reviews per
/// tenure are allowed, so there is no already-reviewed case.
[<RequireQualifiedAccess>]
type PlanReviewError =
    | NoActiveIncumbency
    | IncumbencyMismatch of activeId: string * targetId: string
    | RecordFailed of reason: string

module PlanReviewError =
    val render: error: PlanReviewError -> string

module ReviewPlanBackend =

    /// work-038: the review target is the next milestone derived from the
    /// current stage. S1 reviews toward S2, S2 toward S3, S3 toward delivery
    /// authorization.
    val reviewTargetFor: stage: string -> string

    /// Execute one review. requestId is the exact invocation identity
    /// (provider run + tool call fence), NOT a content address. Findings are
    /// keyed nullable obligations: a null value deletes the key. Multiple
    /// reviews per tenure are allowed; an empty first review grants promotion
    /// eligibility (Promoted), which deliver later performs.
    val executeReview:
        gitCommonDir: string ->
        workId: string ->
        incumbencyId: string ->
        requestId: string ->
        gaps: (string * string option) list ->
            Task<Result<PlanReviewResult, PlanReviewError>>

    /// reviewFinishedOnly: true once the named incumbency has finished its
    /// first review, which unlocks draft writes for that tenure.
    val isReviewFinished: gitCommonDir: string -> workId: string -> incumbencyId: string -> bool
