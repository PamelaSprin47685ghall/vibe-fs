namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
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
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private render (ctx: HostToolContext) path substitutions =
        ProviderProse.instructionLines (languageOf ctx) path substitutions
        |> LlmFacing.renderInstructions

    let private trim (value: string) =
        if isNull value then "" else value.Trim()

    let private occurrenceId (ctx: HostToolContext) =
        ctx.ToolCallId |> Option.map ToolCallId.value

    let private append (journal: AgentJournal) (sessionId: SessionId) providerRun fact =
        AgentJournal.appendAgent (StreamId.Session sessionId) providerRun fact journal

    type private SubscribeFailure =
        | Conflict
        | DurableUnavailable

    type private PublishFailure =
        | UnknownMailbox
        | DurableUnavailable

    let private persistSubscription durable owner occurrence id concern providerRun =
        taskResult {
            let state = (AgentJournal.snapshot durable).AgentProjections.Concern

            let! fact =
                ConcernProjection.subscribe owner occurrence id concern state
                |> Result.mapError (fun _ -> SubscribeFailure.Conflict)

            match fact with
            | None -> return ()
            | Some value ->
                let! _ =
                    append durable owner providerRun (AgentFact.Concern value)
                    |> TaskResult.mapError (fun _ -> SubscribeFailure.DurableUnavailable)

                return ()
        }

    let private subscribeForDurable durable occurrence id concern (ctx: HostToolContext) =
        task {
            let owner = SessionId.create ctx.SessionId
            let! result = persistSubscription durable owner occurrence id concern ctx.ProviderRunId

            match result with
            | Ok() -> return render ctx Path.SubscribeAccepted (Map [ "id", id; "concern", concern ])
            | Error SubscribeFailure.Conflict -> return render ctx Path.SubscribeConflict (Map [ "id", id ])
            | Error SubscribeFailure.DurableUnavailable -> return render ctx Path.DurableUnavailable Map.empty
        }

    let private persistPublication durable sender occurrence id message providerRun =
        taskResult {
            let state = (AgentJournal.snapshot durable).AgentProjections.Concern

            match ConcernProjection.tryFindMessage occurrence state with
            | Some _ -> return ()
            | None ->
                let! fact =
                    ConcernProjection.publish sender occurrence id message state
                    |> Result.mapError (fun _ -> PublishFailure.UnknownMailbox)

                let! _ =
                    append durable sender providerRun (AgentFact.Concern fact)
                    |> TaskResult.mapError (fun _ -> PublishFailure.DurableUnavailable)

                return ()
        }

    let private publishForDurable durable occurrence id message (ctx: HostToolContext) =
        task {
            let sender = SessionId.create ctx.SessionId
            let! result = persistPublication durable sender occurrence id message ctx.ProviderRunId

            match result with
            | Ok() -> return render ctx Path.PublishAccepted (Map [ "id", id ])
            | Error PublishFailure.UnknownMailbox -> return render ctx Path.PublishUnknown (Map [ "id", id ])
            | Error PublishFailure.DurableUnavailable -> return render ctx Path.DurableUnavailable Map.empty
        }

    let private subscribeExecute (journal: AgentJournal option) (args: HostToolArguments) (ctx: HostToolContext) =
        task {
            let id = args.Text "id" |> trim
            let concern = args.Text "concern" |> trim

            match journal, occurrenceId ctx with
            | _, _ when id.Length = 0 || concern.Length = 0 -> return render ctx Path.Invalid Map.empty
            | Some durable, Some occurrence when not (String.IsNullOrWhiteSpace ctx.SessionId) ->
                return! subscribeForDurable durable occurrence id concern ctx
            | _ -> return render ctx Path.DurableUnavailable Map.empty
        }

    let private publishExecute (journal: AgentJournal option) (args: HostToolArguments) (ctx: HostToolContext) =
        task {
            let id = args.Text "id" |> trim
            let message = args.Text "message" |> trim

            match journal, occurrenceId ctx with
            | _, _ when id.Length = 0 || message.Length = 0 -> return render ctx Path.Invalid Map.empty
            | Some durable, Some occurrence when not (String.IsNullOrWhiteSpace ctx.SessionId) ->
                return! publishForDurable durable occurrence id message ctx
            | _ -> return render ctx Path.DurableUnavailable Map.empty
        }

    let admission: ToolAdmission =
        fun _ (r: Role) -> r <> Role.Blogger && r <> Role.Distiller

    let specs factory journal =
        let language = ProviderLanguageBinding.readGlobalPreference ()
        let language = ProviderLanguageBinding.readGlobalPreference ()

        [ { Name = "subscribe"
            Description = ProviderProse.render language Path.SubscribeDescription Map.empty
            Arguments =
              [ "id",
                ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.SubscribeId Map.empty) factory
                "concern",
                ToolHostCodec.stringSchemaDescribed
                    (ProviderProse.render language Path.SubscribeConcern Map.empty)
                    factory ]
            Admission = admission
            Execute = subscribeExecute journal }
          { Name = "publish"
            Description = ProviderProse.render language Path.PublishDescription Map.empty
            Arguments =
              [ "id",
                ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.PublishId Map.empty) factory
                "message",
                ToolHostCodec.stringSchemaDescribed
                    (ProviderProse.render language Path.PublishMessage Map.empty)
                    factory ]
            Admission = admission
            Execute = publishExecute journal } ]
