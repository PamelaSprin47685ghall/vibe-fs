namespace Wanxiangshu.OpenCode.Host.RequirementGrounding

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Requirement.Grounding

module RequirementGroundingGate =

    [<Literal>]
    let RequiredError = "REQUIREMENT_GROUNDING_REQUIRED"

    let private textField (value: obj) name =
        if isNull value || isNull value?(name) then
            None
        else
            let text = string value?(name)
            if String.IsNullOrWhiteSpace text then None else Some text

    let private distinct values =
        values |> List.choose id |> List.distinct

    let private mutationPaths (toolName: string) (args: obj) =
        match toolName.ToLowerInvariant() with
        | "write"
        | "edit"
        | "patch"
        | "apply_patch" -> distinct [ textField args "filePath"; textField args "path" ]
        | "mv" -> distinct [ textField args "source"; textField args "destination" ]
        | "rm" -> distinct [ textField args "path"; textField args "filePath" ]
        | _ -> []

    let private observationPaths (toolName: string) (args: obj) =
        match toolName.ToLowerInvariant() with
        | "read" -> distinct [ textField args "filePath"; textField args "path" ]
        | _ -> []

    let private emptyDecision =
        { NeedsGrounding = false
          Requested = 0
          Packages = [] }

    let private requestMatched journal workspace sessionId paths =
        match journal with
        | None -> Task.FromResult(Error "requirement grounding requires a durable journal")
        | Some durable -> RequirementGroundingRuntime.requestPaths durable workspace (SessionId.create sessionId) paths

    let private request journal workspace sessionId paths =
        let matched =
            if List.isEmpty paths then
                []
            else
                GroundingCatalog.snapshotsForPaths workspace paths

        if List.isEmpty matched then
            Task.FromResult(Ok emptyDecision)
        else
            requestMatched journal workspace sessionId paths

    let before
        (journal: AgentJournal option)
        (workspace: string option)
        (toolInput: obj)
        (toolOutput: obj)
        : Task<Result<RequirementGroundingDecision, string>> =
        let toolName =
            if isNull toolInput || isNull toolInput?tool then
                ""
            else
                string toolInput?tool

        let sessionId =
            if isNull toolInput || isNull toolInput?sessionID then
                ""
            else
                string toolInput?sessionID

        let args =
            if isNull toolOutput || isNull toolOutput?args then
                null
            else
                toolOutput?args

        match workspace with
        | None -> Task.FromResult(Ok emptyDecision)
        | Some root when String.IsNullOrWhiteSpace sessionId -> Task.FromResult(Ok emptyDecision)
        | Some root -> request journal root sessionId (mutationPaths toolName args)

    let after
        (journal: AgentJournal option)
        (workspace: string option)
        (toolInput: obj)
        (toolOutput: obj)
        : Task<Result<RequirementGroundingDecision, string>> =
        let toolName =
            if isNull toolInput || isNull toolInput?tool then
                ""
            else
                string toolInput?tool

        let sessionId =
            if isNull toolInput || isNull toolInput?sessionID then
                ""
            else
                string toolInput?sessionID

        let args =
            if isNull toolInput || isNull toolInput?args then
                null
            else
                toolInput?args

        match workspace with
        | None -> Task.FromResult(Ok emptyDecision)
        | Some root when String.IsNullOrWhiteSpace sessionId -> Task.FromResult(Ok emptyDecision)
        | Some root -> request journal root sessionId (observationPaths toolName args)

    let programAdmission
        journal
        workspace
        sessionId
        paths
        : Task<Result<unit, Wanxiangshu.Repository.Programming.Js.JsFailure>> =
        task {
            match! request journal workspace sessionId paths with
            | Error _ -> return Error Wanxiangshu.Repository.Programming.Js.JsFailure.RequirementGroundingRequired
            | Ok decision when decision.NeedsGrounding ->
                return Error Wanxiangshu.Repository.Programming.Js.JsFailure.RequirementGroundingRequired
            | Ok _ -> return Ok()
        }
