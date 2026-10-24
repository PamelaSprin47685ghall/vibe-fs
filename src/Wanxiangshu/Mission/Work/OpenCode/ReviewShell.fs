namespace Wanxiangshu.Mission.Work.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Host
open Wanxiangshu.Host.Ports
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Work
open Wanxiangshu.Mission.Work.Review
open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Mission.Planning
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider

/// work-038: the shared `review` shell. Owns the one ToolSpec
/// (name, description, findings schema) and dispatches by caller
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

    let private planRejected path =
        ToolResultText.tomlObjectWithInstructions
            [ providerText path Map.empty ]
            [ "recorded", ToolResultText.TBool false ]

    let private rejectionPath (error: PlanReviewError) =
        match error with
        | PlanReviewError.NoActiveIncumbency
        | PlanReviewError.IncumbencyMismatch _ -> ReviewBackendManager.Path.BindingUnavailable
        | PlanReviewError.RecordFailed _ -> ReviewBackendManager.Path.RecordFailed

    let private gapLine gap =
        let (key, value) = gap
        sprintf "- key: %s | value: %s" key value

    let private planAccepted (review: PlanReviewResult) =
        // work-038: review grants promotion eligibility only, never the stage
        // advance itself. Deliver performs the promotion and returns the next
        // stage method; review echoes the remaining obligations verbatim.
        let instructions =
            if review.Outcome = PlanReviewOutcome.Promoted then
                [ providerText Path.PlanPromoted Map.empty ]
            else
                [ providerText Path.PlanStayed Map.empty ] @ (review.Gaps |> List.map gapLine)

        ToolResultText.tomlObjectWithInstructions instructions [ "recorded", ToolResultText.TBool true ]

    let private renderPlanReview (result: Result<PlanReviewResult, PlanReviewError>) : string =
        match result with
        | Error error -> planRejected (rejectionPath error)
        | Ok review -> planAccepted review

    // Same findings schema and three-question semantics as the Manager
    // backend (work-038 shared shell).
    let private parseFindings (args: HostToolArguments) : Result<ReviewFindings, string> =
        match Model.tryParse args.Raw with
        | Error _ -> Error ReviewBackendManager.Path.InvalidFindings
        | Ok findings -> Ok findings

    let private activeIncumbencyId (activeId: string option) : Result<string, string> =
        match activeId with
        | Some id -> Ok id
        | None -> Error ReviewBackendManager.Path.BindingUnavailable

    let private resolveActiveIncumbency (gitCommonDir: string) (workId: string) : Result<string, string> =
        DurableWorkHost.inspected gitCommonDir workId
        |> Option.bind (fun work -> work.ActiveId)
        |> activeIncumbencyId

    let private gapsOf (findings: ReviewFindings) : (string * string option) list =
        ReviewFindings.values findings
        |> List.map (fun finding -> (finding.Key, finding.Value))

    let private reviewRequestIdOf (incumbencyId: string) (context: HostToolContext) : string =
        match context.ProviderRunId, context.ToolCallId with
        | Some run, Some call -> ProviderRunIdentity.value run + ":" + ToolCallId.value call
        | _ -> incumbencyId + ":review"

    // One preparation chain: findings and the durable active incumbency resolve
    // to the exact review invocation. The old workspace EventStore is not a
    // lifecycle prerequisite.
    let private preparePlanReview
        (gitCommonDir: string)
        (args: HostToolArguments)
        (context: HostToolContext)
        : Result<string * (string * string option) list * string, string> =
        if String.IsNullOrWhiteSpace gitCommonDir then
            Error "git common directory is missing"
        else
            parseFindings args
            |> Result.bind (fun findings ->
                resolveActiveIncumbency gitCommonDir context.SessionId
                |> Result.map (fun incumbencyId ->
                    (incumbencyId, gapsOf findings, reviewRequestIdOf incumbencyId context)))

    let private executePlan (gitCommonDir: string) (args: HostToolArguments) (context: HostToolContext) : Task<string> =
        task {
            // work-038 binding: workId is the caller's session.
            match preparePlanReview gitCommonDir args context with
            | Error path -> return planRejected path
            | Ok(incumbencyId, gaps, requestId) ->
                let! result = ReviewPlanBackend.executeReview gitCommonDir context.SessionId incumbencyId requestId gaps

                return renderPlanReview result
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        let findingSchema =
            ToolHostCodec.objectSchemaOf
                [ "key", ToolHostCodec.stringSchema factory
                  "value", ToolHostCodec.anySchema factory ]
                factory

        let findingsSchema =
            ToolHostCodec.arraySchemaOf findingSchema (providerText Path.FindingsArgument Map.empty) factory

        let noteArgument = "note", ToolHostCodec.optionalStringSchema factory

        let dispatch args context : Task<string> =
            task {
                let (Ports.WorkspacePath _ws) =
                    Ports.WorkspacePath(scope.WorkspaceDirectory |> Option.defaultValue ".")

                let! gitCommonDir =
                    match scope.WorkspaceDirectory with
                    | Some ws -> RuntimePath.stateRoot ws
                    | None -> Task.FromResult ""

                if scope.IsRole(context, Role.Plan) then
                    return! executePlan gitCommonDir args context
                else
                    return! ReviewBackendManager.execute scope args context
            }

        { Name = "review"
          Description = providerText Path.Description Map.empty
          Arguments = [ "findings", findingsSchema; noteArgument ]
          Execute = dispatch }
