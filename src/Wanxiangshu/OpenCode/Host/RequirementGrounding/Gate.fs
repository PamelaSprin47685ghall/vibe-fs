namespace Wanxiangshu.OpenCode.Host.RequirementGrounding

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Requirement.Grounding

module RequirementGroundingGate =

    [<Emit("typeof $0 === 'string'")>]
    let private isString (value: obj) : bool = jsNative

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
        | "read" ->
            textField args "filePath"
            |> Option.orElseWith (fun () -> textField args "path")
            |> Option.toList
        | _ -> []

    let nativeReadObservations toolName (args: obj) (output: obj) =
        if not (isString output) then
            []
        else
            observationPaths toolName args
            |> List.map (fun path ->
                { Path = path
                  ResultBytes = string output
                  Coverage = GroundingReadCoverage.PartialFile })

    let private emptyDecision =
        { NeedsGrounding = false
          Requested = 0
          Packages = [] }

    let private requestMatched
        (journal: AgentJournal option)
        (workspace: string)
        (sessionId: string)
        (paths: string list)
        : Task<Result<RequirementGroundingDecision, string>> =
        match journal with
        | None -> Task.FromResult(Error "requirement grounding requires a durable journal")
        | Some durable ->
            let port = AgentJournalPortAdapter.forRequirementGrounding durable
            RequirementGroundingRuntime.requestPaths port workspace (SessionId.create sessionId) paths

    let private request
        (journal: AgentJournal option)
        (workspace: string)
        (sessionId: string)
        (paths: string list)
        : Task<Result<RequirementGroundingDecision, string>> =
        let matched =
            if List.isEmpty paths then
                []
            else
                GroundingCatalog.snapshotsForPaths workspace paths

        if List.isEmpty matched then
            Task.FromResult(Ok emptyDecision)
        else
            requestMatched journal workspace sessionId paths

    let private observeReads
        (journal: AgentJournal option)
        (workspace: string)
        (sessionId: string)
        (reads: GroundingFileRead list)
        : Task<Result<RequirementGroundingDecision, string>> =
        match journal with
        | None -> Task.FromResult(Error "requirement grounding requires a durable journal")
        | Some durable ->
            let port = AgentJournalPortAdapter.forRequirementGrounding durable
            RequirementGroundingRuntime.observeFileReads port workspace (SessionId.create sessionId) reads

    let private ignoreDecision (operation: unit -> Task<Result<RequirementGroundingDecision, string>>) : Task<unit> =
        task {
            try
                let! _ = operation ()
                return ()
            with _ ->
                return ()
        }

    let decideMutation
        (journal: AgentJournal option)
        (workspace: string)
        (sessionId: string)
        (paths: string list)
        : Task<Result<RequirementGroundingDecision, string>> =
        request journal workspace sessionId paths

    let decideRead
        (journal: AgentJournal option)
        (workspace: string)
        (sessionId: string)
        (reads: GroundingFileRead list)
        : Task<Result<RequirementGroundingDecision, string>> =
        if List.isEmpty reads then
            Task.FromResult(Ok emptyDecision)
        else
            observeReads journal workspace sessionId reads

    let before
        (journal: AgentJournal option)
        (workspace: string option)
        (toolInput: obj)
        (toolOutput: obj)
        : Task<unit> =
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
        | None -> Task.FromResult(())
        | Some _ when String.IsNullOrWhiteSpace sessionId -> Task.FromResult(())
        | Some root -> ignoreDecision (fun () -> request journal root sessionId (mutationPaths toolName args))

    let after
        (journal: AgentJournal option)
        (workspace: string option)
        (toolInput: obj)
        (toolOutput: obj)
        : Task<unit> =
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
        | None -> Task.FromResult(())
        | Some _ when String.IsNullOrWhiteSpace sessionId -> Task.FromResult(())
        | Some root ->
            let output = if isNull toolOutput then null else toolOutput?output
            ignoreDecision (fun () -> decideRead journal root sessionId (nativeReadObservations toolName args output))

    let programObservation
        (journal: AgentJournal option)
        (workspace: string)
        (sessionId: string)
        (reads: GroundingFileRead list)
        (effectPaths: string list)
        : Task<unit> =
        task {
            if not (List.isEmpty reads) then
                do! ignoreDecision (fun () -> decideRead journal workspace sessionId reads)

            if not (List.isEmpty effectPaths) then
                do! ignoreDecision (fun () -> decideMutation journal workspace sessionId effectPaths)
        }
