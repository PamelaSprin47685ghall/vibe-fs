namespace Wanxiangshu.Mission.Planning

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host

/// work-038: one plan review fixes the promotion eligibility of an incumbency.
/// A review never advances the stage: promotion is the deliver tool's job.
[<RequireQualifiedAccess>]
type PlanReviewOutcome =
    | Promoted
    | Stayed

/// work-038: the outcome of one review on a plan incumbency.
/// Promoted here means ELIGIBLE for promotion (first review empty), not
/// promoted; Stayed means obligations remain. FirstReview is true exactly
/// when this review fixed the tenure's eligibility. Gaps carries the remaining
/// (non-deleted) obligation ledger.
type PlanReviewResult =
    { Outcome: PlanReviewOutcome
      Target: string
      Gaps: (string * string) list
      FirstReview: bool }

/// work-038: typed rejections for the plan review gate. Multiple reviews per
/// tenure are allowed, so there is no already-reviewed case: later reviews
/// overwrite the obligation ledger item by item.
[<RequireQualifiedAccess>]
type PlanReviewError =
    | NoActiveIncumbency
    | IncumbencyMismatch of activeId: string * targetId: string
    | RecordFailed of reason: string

module PlanReviewError =
    let render (error: PlanReviewError) : string =
        match error with
        | PlanReviewError.NoActiveIncumbency -> "No active incumbency found for this work"
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

    /// The reviewer is never the incumbency owner. A plan work opens with the
    /// session id as owner, so the reviewer must be a distinct bound identity.
    [<Literal>]
    let private ReviewerId = "plan-reviewer"

    /// A review finding is a keyed obligation: the key names the obligation,
    /// the value carries the work plan. A null value deletes the key. The
    /// value is nullable end to end; an empty string is NOT a deletion alias.
    let private obligationUpdates (gaps: (string * string option) list) : obj array =
        gaps
        |> List.map (fun (key, value) ->
            box
                {| key = key
                   value =
                    match value with
                    | Some v -> box v
                    | None -> null |})
        |> List.toArray

    let private activeStageResult
        (active: (string * string) option)
        (incumbencyId: string)
        : Result<string, PlanReviewError> =
        match active with
        | None -> Error PlanReviewError.NoActiveIncumbency
        | Some(activeId, _) when activeId <> incumbencyId ->
            Error(PlanReviewError.IncumbencyMismatch(activeId, incumbencyId))
        | Some(_, stage) -> Ok stage

    let private prepareReview
        (gitCommonDir: string)
        (workId: string)
        (incumbencyId: string)
        : Result<string, PlanReviewError> =
        if String.IsNullOrWhiteSpace gitCommonDir then
            Error(PlanReviewError.RecordFailed "git common directory is missing")
        else
            let active =
                Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected gitCommonDir workId
                |> Option.bind (fun work -> work.ActiveId |> Option.map (fun id -> id, work.Stage))

            activeStageResult active incumbencyId

    let private text (value: obj) : string =
        if isNull value || not (emitJsExpr value "typeof $0 === 'string'") then
            ""
        else
            unbox<string> value

    let private field (value: obj) (name: string) : obj =
        if isNull value then null else value?(name)

    let private boolOf (value: obj) : bool =
        not (isNull value) && emitJsExpr value "!!$0"

    let private obligationEntry (entry: obj) : (string * string) option =
        let key = text (field entry "key")
        let value = text (field entry "value")

        if key = "" then None else Some(key, value)

    let private obligationsOf (value: obj) : (string * string) list =
        if isNull value || not (emitJsExpr value "Array.isArray($0)") then
            []
        else
            unbox<obj array> value |> Array.toList |> List.choose obligationEntry

    let private receiptOutcome
        (receipt: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.HostReceipt)
        : PlanReviewOutcome =
        let promoted =
            receipt.Kind = "Reviewed"
            && Wanxiangshu.Mission.Lifecycle.DurableWorkHost.resultText receipt "outcome" = "Promoted"

        if promoted then
            PlanReviewOutcome.Promoted
        else
            PlanReviewOutcome.Stayed

    let private receiptReason (receipt: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.HostReceipt) : string =
        if receipt.Reason = "" then
            receipt.Status
        else
            receipt.Reason

    let private executePreparedReview
        (gitCommonDir: string)
        (workId: string)
        (incumbencyId: string)
        (stageBeforeReview: string)
        (requestId: string)
        (gaps: (string * string option) list)
        : Task<Result<PlanReviewResult, PlanReviewError>> =
        task {
            let updates = obligationUpdates gaps

            let command =
                Wanxiangshu.Mission.Lifecycle.DurableWorkHost.command
                    "Review"
                    [ "incumbencyId", box incumbencyId
                      "reviewerId", box ReviewerId
                      "updates", box updates ]

            let receipt =
                Wanxiangshu.Mission.Lifecycle.DurableWorkHost.execute gitCommonDir workId requestId command

            match receipt.Status with
            | "Committed"
            | "Replayed" ->
                return
                    Ok
                        { Outcome = receiptOutcome receipt
                          Target = reviewTargetFor stageBeforeReview
                          Gaps = obligationsOf (field receipt.Result "obligations")
                          FirstReview = boolOf (field receipt.Result "firstReview") }
            | _ -> return Error(PlanReviewError.RecordFailed(receiptReason receipt))
        }

    /// Execute one review. The requestId is the exact invocation identity
    /// (provider run + tool call fence), NOT a content address: distinct
    /// reviews with identical content are still distinct review facts, while
    /// an exact retry replays. Findings are keyed nullable obligations (null
    /// deletes the key).
    let executeReview
        (gitCommonDir: string)
        (workId: string)
        (incumbencyId: string)
        (requestId: string)
        (gaps: (string * string option) list)
        : Task<Result<PlanReviewResult, PlanReviewError>> =
        task {
            match prepareReview gitCommonDir workId incumbencyId with
            | Error error -> return Error error
            | Ok stageBeforeReview ->
                return! executePreparedReview gitCommonDir workId incumbencyId stageBeforeReview requestId gaps
        }

    /// reviewFinishedOnly: the named incumbency has finished its first review,
    /// which unlocks draft writes for that tenure.
    let isReviewFinished (gitCommonDir: string) (workId: string) (incumbencyId: string) : bool =
        PlanKernelSeam.isReviewFinished gitCommonDir workId incumbencyId
