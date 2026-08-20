namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Resources

[<RequireQualifiedAccess>]
module AttentionTools =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let EnoughDescription = "attention-regulation/enough-description"
        [<Literal>]
        let EnoughArgument = "attention-regulation/enough-argument"
        [<Literal>]
        let EnoughAccepted = "attention-regulation/enough-accepted"
        [<Literal>]
        let AbandonDescription = "attention-regulation/abandon-description"
        [<Literal>]
        let AbandonArgument = "attention-regulation/abandon-argument"
        [<Literal>]
        let AbandonAccepted = "attention-regulation/abandon-accepted"
        [<Literal>]
        let DeferDescription = "attention-regulation/defer-description"
        [<Literal>]
        let DeferArgument = "attention-regulation/defer-argument"
        [<Literal>]
        let DeferAccepted = "attention-regulation/defer-accepted"
        [<Literal>]
        let Invalid = "attention-regulation/invalid"
        [<Literal>]
        let DurableUnavailable = "attention-regulation/durable-unavailable"

    let private languageOf (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private render ctx path substitutions = ProviderProse.render (languageOf ctx) path substitutions

    let private nonBlank (value: string) =
        if isNull value then "" else value.Trim()

    let private simpleExecute argument resultPath (args: HostToolArguments) ctx =
        task {
            let value = args.Text argument |> nonBlank
            if value.Length = 0 then
                return render ctx Path.Invalid Map.empty
            else
                return render ctx resultPath (Map [ "value", value ])
        }

    let private occurrenceId (ctx: HostToolContext) =
        ctx.ToolCallId |> Option.map ToolCallId.value

    let private deferExecute (journal: AgentJournal option) (args: HostToolArguments) (ctx: HostToolContext) =
        task {
            let text = args.Text "new_work" |> nonBlank

            match journal, occurrenceId ctx with
            | _, _ when text.Length = 0 -> return render ctx Path.Invalid Map.empty
            | Some durable, Some occurrence when not (String.IsNullOrWhiteSpace ctx.SessionId) ->
                let sessionId = SessionId.create ctx.SessionId
                let projection = (AgentJournal.snapshot durable).AgentProjections.Attention

                match AttentionProjection.tryFind sessionId occurrence projection with
                | Some _ -> return render ctx Path.DeferAccepted (Map [ "value", text ])
                | None ->
                    let fact =
                        AgentFact.Attention(
                            AttentionFactCases.DeferredWorkRecorded
                                {| SessionId = sessionId
                                   OccurrenceId = occurrence
                                   Text = text |}
                        )

                    match! AgentJournal.appendAgent (StreamId.Session sessionId) ctx.ProviderRunId fact durable with
                    | Ok _ -> return render ctx Path.DeferAccepted (Map [ "value", text ])
                    | Error _ -> return render ctx Path.DurableUnavailable Map.empty
            | _ -> return render ctx Path.DurableUnavailable Map.empty
        }

    let private oneStringSpec factory name descriptionPath argument argumentPath execute =
        let language = ProviderLanguageBinding.readGlobalPreference ()
        { Name = name
          Description = ProviderProse.render language descriptionPath Map.empty
          Arguments =
            [ argument,
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language argumentPath Map.empty) factory ]
          Execute = execute }

    let specs factory journal =
        [ oneStringSpec
              factory
              "enough"
              Path.EnoughDescription
              "decision"
              Path.EnoughArgument
              (simpleExecute "decision" Path.EnoughAccepted)
          oneStringSpec
              factory
              "abandon"
              Path.AbandonDescription
              "commitment"
              Path.AbandonArgument
              (simpleExecute "commitment" Path.AbandonAccepted)
          oneStringSpec factory "defer" Path.DeferDescription "new_work" Path.DeferArgument (deferExecute journal) ]

