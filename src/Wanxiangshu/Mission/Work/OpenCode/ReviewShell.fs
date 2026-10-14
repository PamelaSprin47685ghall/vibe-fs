namespace Wanxiangshu.Mission.Work.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Mission.Work
open Wanxiangshu.Mission.Work.Review
open Wanxiangshu.Mission.Planning
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.EventStore

/// relay-assessment-011: the shared `review` shell. Owns the one ToolSpec
/// (name, description, findings schema, admission) and dispatches by caller
/// role: Plan goes to the plan backend (ReviewPlanBackend), every other
/// admitted caller goes to the Manager backend (ReviewBackendManager). The
/// shell renders the plan backend's result; the Manager backend keeps its own
/// result rendering so its byte behavior is unchanged.
module ReviewShell =

    [<RequireQualifiedAccess>]
    module private Path =
        [<Literal>]
        let Description = "tool/review/description"

        [<Literal>]
        let FindingsArgument = "tool/review/findings-argument"

        [<Literal>]
        let PlanPromoted = "tool/review/plan-promoted"

        [<Literal>]
        let PlanStayed = "tool/review/plan-stayed"

    let private providerText (path: string) substitutions =
        ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) path substitutions

    let admission =
        ToolAdmission.OfficeRole(fun _ role ->
            OfficeCapability.isAllowed role ToolPermission.ReviewAssessment
            || OfficeCapability.isAllowed role ToolPermission.Review)

    let private planRejected path =
        ToolHostCodec.tomlObjectWithInstructions
            [ providerText path Map.empty ]
            [ "recorded", ToolHostCodec.TBool false ]

    let private rejectionPath (error: PlanReviewError) =
        match error with
        | PlanReviewError.AlreadyReviewed _ -> ReviewBackendManager.Path.AlreadySubmitted
        | PlanReviewError.NoActiveIncumbency
        | PlanReviewError.IncumbencyMismatch _ -> ReviewBackendManager.Path.BindingUnavailable
        | PlanReviewError.RecordFailed _ -> ReviewBackendManager.Path.RecordFailed

    let private gapLine gap =
        let (acceptance, workPlan) = gap
        sprintf "- acceptance_criteria: %s | work_plan: %s" acceptance workPlan

    let private planAccepted (review: PlanReviewResult) =
        // planning-022: the gaps return to the model as repair obligations,
        // echoed verbatim below the outcome instruction.
        let instructionPath =
            if review.Outcome = Wanxiangshu.Mission.Lifecycle.ReviewOutcome.Promoted then
                Path.PlanPromoted
            else
                Path.PlanStayed

        let instructions =
            [ providerText instructionPath Map.empty ] @ (review.Gaps |> List.map gapLine)

        ToolHostCodec.tomlObjectWithInstructions instructions [ "recorded", ToolHostCodec.TBool true ]

    let private renderPlanReview (result: Result<PlanReviewResult, PlanReviewError>) : string =
        match result with
        | Error error -> planRejected (rejectionPath error)
        | Ok review -> planAccepted review

    let private resolveStore (planStore: IEventStore option) : Result<IEventStore, string> =
        match planStore with
        | None -> Error ReviewBackendManager.Path.BindingUnavailable
        | Some store -> Ok store

    // Same findings schema and three-question semantics as the Manager
    // backend (relay-assessment-011 shared shell).
    let private parseFindings (args: HostToolArguments) : Result<ReviewFindings, string> =
        match Model.tryParse args.Raw with
        | Error _ -> Error ReviewBackendManager.Path.InvalidFindings
        | Ok findings -> Ok findings

    let private resolveActiveIncumbency (gitCommonDir: string) (workId: string) : Result<string, string> =
        match Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected gitCommonDir workId with
        | Some work ->
            match work.ActiveId with
            | Some id -> Ok id
            | None -> Error ReviewBackendManager.Path.BindingUnavailable
        | None -> Error ReviewBackendManager.Path.BindingUnavailable

    let private gapsOf (findings: ReviewFindings) : (string * string) list =
        ReviewFindings.values findings
        |> List.map (fun finding -> (finding.AcceptanceCriteria, finding.WorkPlan))

    // One preparation chain: store wiring, findings parsing and the active
    // incumbency resolve to the exact review invocation, or to the rejection
    // path the old nested matches produced.
    let private preparePlanReview
        (gitCommonDir: string)
        (planStore: IEventStore option)
        (args: HostToolArguments)
        (context: HostToolContext)
        : Result<(IEventStore * string * (string * string) list), string> =
        if String.IsNullOrWhiteSpace gitCommonDir then
            Error "git common directory is missing"
        else
            resolveStore planStore
            |> Result.bind (fun store -> parseFindings args |> Result.map (fun findings -> (store, findings)))
            |> Result.bind (fun (store, findings) ->
                resolveActiveIncumbency gitCommonDir context.SessionId
                |> Result.map (fun incumbencyId -> (store, incumbencyId, gapsOf findings)))

    let private executePlan
        (gitCommonDir: string)
        (planStore: IEventStore option)
        (args: HostToolArguments)
        (context: HostToolContext)
        : Task<string> =
        task {
            // planning-022 binding: workId is the caller's session.
            match preparePlanReview gitCommonDir planStore args context with
            | Error path -> return planRejected path
            | Ok(store, incumbencyId, gaps) ->
                let! result = ReviewPlanBackend.executeReview gitCommonDir store context.SessionId incumbencyId gaps

                return renderPlanReview result
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) (planStore: IEventStore option) : ToolSpec =
        let findingSchema =
            ToolHostCodec.objectSchemaOf
                [ "acceptance_criteria", ToolHostCodec.stringSchema factory
                  "work_plan", ToolHostCodec.stringSchema factory ]
                factory

        let findingsSchema =
            ToolHostCodec.arraySchemaOf findingSchema (providerText Path.FindingsArgument Map.empty) factory

        let noteArgument = "note", ToolHostCodec.optionalStringSchema factory

        let dispatch args context =
            let gitCommonDir =
                scope.WorkspaceDirectory
                |> Option.map Wanxiangshu.Persistence.Journal.RuntimePath.gitCommonDir
                |> Option.defaultValue ""
            if scope.IsRole(context, Role.Plan) then
                executePlan gitCommonDir planStore args context
            else
                ReviewBackendManager.execute scope args context

        { Name = "review"
          Description = providerText Path.Description Map.empty
          Arguments = [ "findings", findingsSchema; noteArgument ]
          Admission = admission
          Execute = dispatch }
