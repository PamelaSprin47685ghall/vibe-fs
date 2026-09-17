namespace Wanxiangshu.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Knowledge.Casebook

/// Conditional Casebook read. Provider identity is a public shelfmark; durable
/// session identity, freshness state and maintenance machinery remain internal.
module FetchTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/fetch/description"

        [<Literal>]
        let Fresh = "tool/fetch/fresh"

        [<Literal>]
        let Refreshed = "tool/fetch/refreshed"

        [<Literal>]
        let Stale = "tool/fetch/stale"

        [<Literal>]
        let NoCase = "tool/fetch/no-case"

        [<Literal>]
        let Unavailable = "tool/fetch/unavailable"

        [<Literal>]
        let ShelfmarkRequired = "tool/fetch/shelfmark-required"

    let private fetchGate = obj ()

    let private fetchInFlight =
        System.Collections.Generic.Dictionary<string, System.Threading.Tasks.Task<string>>()

    let private lang (ctx: HostToolContext) =
        ProviderLanguageBinding.forSessionText ctx.SessionId

    let private prose language path =
        ProviderProse.render language path Map.empty

    let private answerResult (consequence: string) (answer: string) =
        ToolHostCodec.tomlObjectWithInstructions [ consequence ] [ "answer", ToolHostCodec.TString answer ]

    let private fresh language answer =
        answerResult (prose language Path.Fresh) answer

    let private refreshed language answer =
        answerResult (prose language Path.Refreshed) answer

    let private stale language answer =
        answerResult (prose language Path.Stale) answer

    let private noCase language =
        ToolHostCodec.tomlObjectWithInstructions [ prose language Path.NoCase ] []

    let private unavailable language =
        ToolHostCodec.tomlObjectWithInstructions [ prose language Path.Unavailable ] []

    let private extractPaths (case: Case) : string list =
        if not (List.isEmpty case.RelatedPaths) then
            case.RelatedPaths
        else
            case.Observations
            |> List.choose (function
                | Observation.FileRead(path, _) -> Some path
                | _ -> None)
            |> List.distinct
            |> List.sort

    /// Serve the maintained body of a case reported as changed, or stay stale.
    let private serveMaintained language workspaceRoot store identity (cachedAnswer: string) =
        task {
            let! latest = CasebookWorkflow.fetchCase store 256 identity

            match latest with
            | Ok(Some updated) ->
                do! CasebookLifecycle.touchAccess workspaceRoot store identity
                return refreshed language updated.A
            | _ -> return stale language cachedAnswer
        }

    /// Maintain the case from the provided diff, then serve the maintained body.
    let private refreshFromDiff language workspaceRoot store identity (cachedAnswer: string) =
        task {
            let! changed = CasebookBookkeeper.refreshStale store workspaceRoot identity

            match changed with
            | Ok true -> return! serveMaintained language workspaceRoot store identity cachedAnswer
            | Ok false -> return fresh language cachedAnswer
            | Error _ -> return stale language cachedAnswer
        }

    let private handleResolvedCase language workspaceRoot store (case: Case) =
        task {
            let identity = case.Identity

            let baseline =
                if
                    not (String.IsNullOrWhiteSpace case.MaintenanceFileState)
                    && case.MaintenanceFileState <> "state-initial"
                then
                    box case.MaintenanceFileState
                elif
                    not (String.IsNullOrWhiteSpace case.CompletionFileState)
                    && case.CompletionFileState <> "state-initial"
                then
                    box case.CompletionFileState
                else
                    CasebookCapture.baselineFromObservations case.Observations case.RelatedPaths

            let! diffObj = CasebookCapture.computeMaintenanceDiff workspaceRoot baseline
            let hasDiff = unbox<bool> (diffObj?hasDiff)

            if not hasDiff then
                do! CasebookLifecycle.touchAccess workspaceRoot store identity
                return fresh language case.A
            else
                return! refreshFromDiff language workspaceRoot store identity case.A
        }

    let private runFetch
        (language: ProviderLanguage)
        (workspaceRoot: string)
        (store: IEventStore)
        (shelfmark: string)
        : System.Threading.Tasks.Task<string> =
        task {
            match! CasebookIndex.resolve store 256 shelfmark with
            | Error _ -> return unavailable language
            | Ok None -> return noCase language
            | Ok(Some case) -> return! handleResolvedCase language workspaceRoot store case
        }

    let private createFlightWork language workspaceRoot store shelfmark =
        task {
            try
                return! runFetch language workspaceRoot store shelfmark
            finally
                lock fetchGate (fun () -> fetchInFlight.Remove shelfmark |> ignore)
        }

    let private getOrCreateFlightWork language workspaceRoot store shelfmark =
        lock fetchGate (fun () ->
            match fetchInFlight.TryGetValue shelfmark with
            | true, existing -> existing
            | false, _ ->
                let work = createFlightWork language workspaceRoot store shelfmark
                fetchInFlight.[shelfmark] <- work
                work)

    let admission: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> OfficeCapability.isAllowed r ToolPermission.Fetch)

    let spec (factory: HostToolFactory) (workspaceRoot: string) (store: IEventStore) : ToolSpec =
        { Name = "fetch"
          Description = prose (ProviderLanguageBinding.readGlobalPreference ()) Path.Description
          Arguments = [ "shelfmark", ToolHostCodec.stringSchema factory ]
          Admission = admission
          Execute =
            fun args ctx ->
                task {
                    let language = lang ctx
                    let shelfmark = args.Text "shelfmark"

                    if not (CasebookFeature.isEnabled workspaceRoot) then
                        return unavailable language
                    elif String.IsNullOrWhiteSpace shelfmark then
                        return ToolHostCodec.tomlObjectWithInstructions [ prose language Path.ShelfmarkRequired ] []
                    else
                        return! getOrCreateFlightWork language workspaceRoot store shelfmark
                } }
