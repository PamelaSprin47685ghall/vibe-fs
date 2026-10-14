namespace Wanxiangshu.Mission.Work.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Mission.Work
open Wanxiangshu.Mission.Work.Review
open Wanxiangshu.Mission.Planning
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider

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
            if review.Outcome = PlanReviewOutcome.Promoted then
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

    // Same findings schema and three-question semantics as the Manager
    // backend (relay-assessment-011 shared shell).
    let private parseFindings (args: HostToolArguments) : Result<ReviewFindings, string> =
        match Model.tryParse args.Raw with
        | Error _ -> Error ReviewBackendManager.Path.InvalidFindings
        | Ok findings -> Ok findings

    let private activeIncumbencyId (activeId: string option) : Result<string, string> =
        match activeId with
        | Some id -> Ok id
        | None -> Error ReviewBackendManager.Path.BindingUnavailable

    let private resolveActiveIncumbency (gitCommonDir: string) (workId: string) : Result<string, string> =
        Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected gitCommonDir workId
        |> Option.bind (fun work -> work.ActiveId)
        |> activeIncumbencyId

    let private gapsOf (findings: ReviewFindings) : (string * string) list =
        ReviewFindings.values findings
        |> List.map (fun finding -> (finding.AcceptanceCriteria, finding.WorkPlan))

    // One preparation chain: findings and the durable active incumbency resolve
    // to the exact review invocation. The old workspace EventStore is not a
    // lifecycle prerequisite.
    let private preparePlanReview
        (gitCommonDir: string)
        (args: HostToolArguments)
        (context: HostToolContext)
        : Result<string * (string * string) list, string> =
        if String.IsNullOrWhiteSpace gitCommonDir then
            Error "git common directory is missing"
        else
            parseFindings args
            |> Result.bind (fun findings ->
                resolveActiveIncumbency gitCommonDir context.SessionId
                |> Result.map (fun incumbencyId -> (incumbencyId, gapsOf findings)))

    let private executePlan
        (gitCommonDir: string)
        (args: HostToolArguments)
        (context: HostToolContext)
        : Task<string> =
        task {
            // planning-022 binding: workId is the caller's session.
            match preparePlanReview gitCommonDir args context with
            | Error path -> return planRejected path
            | Ok(incumbencyId, gaps) ->
                let! result = ReviewPlanBackend.executeReview gitCommonDir context.SessionId incumbencyId gaps

                return renderPlanReview result
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
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
                executePlan gitCommonDir args context
            else
                ReviewBackendManager.execute scope args context

        { Name = "review"
          Description = providerText Path.Description Map.empty
          Arguments = [ "findings", findingsSchema; noteArgument ]
          Admission = admission
          Execute = dispatch }
