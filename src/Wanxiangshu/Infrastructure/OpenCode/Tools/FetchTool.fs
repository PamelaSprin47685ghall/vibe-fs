namespace Wanxiangshu.Infrastructure

open System
open Wanxiangshu.Domain
open Wanxiangshu.Session
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.OpenCode

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
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

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
            | Ok(Some case) ->
                let sessionId = case.SessionId
                let replayed = CasebookReplay.replayAll workspaceRoot case.Observations

                match CasebookWorkflow.checkFreshness case replayed with
                | ReplayResult.Fresh ->
                    do! CasebookLifecycle.touchAccess workspaceRoot sessionId
                    return fresh language case.A
                | ReplayResult.Stale ->
                    match! CasebookBookkeeper.refreshStale store workspaceRoot sessionId with
                    | Ok true ->
                        match! CasebookWorkflow.fetchCase store 256 sessionId with
                        | Error _
                        | Ok None -> return stale language case.A
                        | Ok(Some updated) ->
                            let again = CasebookReplay.replayAll workspaceRoot updated.Observations

                            match CasebookWorkflow.checkFreshness updated again with
                            | ReplayResult.Fresh ->
                                do! CasebookLifecycle.touchAccess workspaceRoot sessionId
                                return refreshed language updated.A
                            | ReplayResult.Stale -> return stale language updated.A
                    | Ok false
                    | Error _ -> return stale language case.A
        }

    let spec (factory: HostToolFactory) (workspaceRoot: string) (store: IEventStore) : ToolSpec =
        { Name = "fetch"
          Description = prose (ProviderLanguageBinding.readGlobalPreference ()) Path.Description
          Arguments = [ "shelfmark", ToolHostCodec.stringSchema factory ]
          Execute =
            fun args ctx ->
                task {
                    let language = lang ctx
                    let shelfmark = args.Text "shelfmark"

                    if String.IsNullOrWhiteSpace shelfmark then
                        return ToolHostCodec.tomlObjectWithInstructions [ prose language Path.ShelfmarkRequired ] []
                    else
                        let! result =
                            lock fetchGate (fun () ->
                                match fetchInFlight.TryGetValue shelfmark with
                                | true, existing -> existing
                                | false, _ ->
                                    let work =
                                        task {
                                            try
                                                return! runFetch language workspaceRoot store shelfmark
                                            finally
                                                lock fetchGate (fun () -> fetchInFlight.Remove shelfmark |> ignore)
                                        }

                                    fetchInFlight.[shelfmark] <- work
                                    work)

                        return result
                } }
