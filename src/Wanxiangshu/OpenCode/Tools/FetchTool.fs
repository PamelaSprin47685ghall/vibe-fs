namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Foundation.Identity
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
