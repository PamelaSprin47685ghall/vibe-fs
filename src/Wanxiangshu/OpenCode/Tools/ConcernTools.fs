namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Concern
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Resources

[<RequireQualifiedAccess>]
module ConcernTools =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let SubscribeDescription = "concern-routing/subscribe-description"
        [<Literal>]
        let SubscribeId = "concern-routing/subscribe-id"
        [<Literal>]
        let SubscribeConcern = "concern-routing/subscribe-concern"
        [<Literal>]
        let SubscribeAccepted = "concern-routing/subscribe-accepted"
        [<Literal>]
        let SubscribeConflict = "concern-routing/subscribe-conflict"
        [<Literal>]
        let PublishDescription = "concern-routing/publish-description"
        [<Literal>]
        let PublishId = "concern-routing/publish-id"
        [<Literal>]
        let PublishMessage = "concern-routing/publish-message"
        [<Literal>]
        let PublishAccepted = "concern-routing/publish-accepted"
        [<Literal>]
        let PublishUnknown = "concern-routing/publish-unknown"
        [<Literal>]
        let Invalid = "concern-routing/invalid"
        [<Literal>]
        let DurableUnavailable = "concern-routing/durable-unavailable"

    let private languageOf (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then ProviderLanguageBinding.readGlobalPreference ()
        else ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private render (ctx: HostToolContext) path substitutions = ProviderProse.render (languageOf ctx) path substitutions
    let private trim (value: string) = if isNull value then "" else value.Trim()
    let private occurrenceId (ctx: HostToolContext) = ctx.ToolCallId |> Option.map ToolCallId.value

    let private append (journal: AgentJournal) (sessionId: SessionId) providerRun fact =
        AgentJournal.appendAgent (StreamId.Session sessionId) providerRun fact journal

    let private subscribeExecute (journal: AgentJournal option) (args: HostToolArguments) (ctx: HostToolContext) =
        task {
            let id = args.Text "id" |> trim
            let concern = args.Text "concern" |> trim

            match journal, occurrenceId ctx with
            | _, _ when id.Length = 0 || concern.Length = 0 -> return render ctx Path.Invalid Map.empty
            | Some durable, Some occurrence when not (String.IsNullOrWhiteSpace ctx.SessionId) ->
                let owner = SessionId.create ctx.SessionId
                let state = (AgentJournal.snapshot durable).AgentProjections.Concern

                match ConcernProjection.subscribe owner occurrence id concern state with
                | Error _ -> return render ctx Path.SubscribeConflict (Map [ "id", id ])
                | Ok None -> return render ctx Path.SubscribeAccepted (Map [ "id", id; "concern", concern ])
                | Ok(Some fact) ->
                    match! append durable owner ctx.ProviderRunId (AgentFact.Concern fact) with
                    | Ok _ -> return render ctx Path.SubscribeAccepted (Map [ "id", id; "concern", concern ])
                    | Error _ -> return render ctx Path.DurableUnavailable Map.empty
            | _ -> return render ctx Path.DurableUnavailable Map.empty
        }

    let private publishExecute (journal: AgentJournal option) (args: HostToolArguments) (ctx: HostToolContext) =
        task {
            let id = args.Text "id" |> trim
            let message = args.Text "message" |> trim

            match journal, occurrenceId ctx with
            | _, _ when id.Length = 0 || message.Length = 0 -> return render ctx Path.Invalid Map.empty
            | Some durable, Some occurrence when not (String.IsNullOrWhiteSpace ctx.SessionId) ->
                let sender = SessionId.create ctx.SessionId
                let state = (AgentJournal.snapshot durable).AgentProjections.Concern

                match ConcernProjection.tryFindMessage occurrence state with
                | Some _ -> return render ctx Path.PublishAccepted (Map [ "id", id ])
                | None ->
                    match ConcernProjection.publish sender occurrence id message state with
                    | Error _ -> return render ctx Path.PublishUnknown (Map [ "id", id ])
                    | Ok fact ->
                        match! append durable sender ctx.ProviderRunId (AgentFact.Concern fact) with
                        | Ok _ -> return render ctx Path.PublishAccepted (Map [ "id", id ])
                        | Error _ -> return render ctx Path.DurableUnavailable Map.empty
            | _ -> return render ctx Path.DurableUnavailable Map.empty
        }

    let specs factory journal =
        let language = ProviderLanguageBinding.readGlobalPreference ()
        [ { Name = "subscribe"
            Description = ProviderProse.render language Path.SubscribeDescription Map.empty
            Arguments =
                [ "id", ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.SubscribeId Map.empty) factory
                  "concern",
                  ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.SubscribeConcern Map.empty) factory ]
            Execute = subscribeExecute journal }
          { Name = "publish"
            Description = ProviderProse.render language Path.PublishDescription Map.empty
            Arguments =
                [ "id", ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.PublishId Map.empty) factory
                  "message",
                  ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.PublishMessage Map.empty) factory ]
            Execute = publishExecute journal } ]

