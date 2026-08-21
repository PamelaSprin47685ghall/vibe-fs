namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
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

    let private render ctx path substitutions =
        ProviderProse.instructionLines (languageOf ctx) path substitutions
        |> LlmFacing.renderInstructions

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

    let private persistDeferred durable sessionId occurrence text providerRun =
        taskResult {
            let projection = (AgentJournal.snapshot durable).AgentProjections.Attention

            match AttentionProjection.tryFind sessionId occurrence projection with
            | Some _ -> return ()
            | None ->
                let fact =
                    AgentFact.Attention(
                        AttentionFactCases.DeferredWorkRecorded
                            {| SessionId = sessionId
                               OccurrenceId = occurrence
                               Text = text |}
                    )

                let! _ = AgentJournal.appendAgent (StreamId.Session sessionId) providerRun fact durable
                return ()
        }

    let private deferForDurable durable occurrence text (ctx: HostToolContext) =
        task {
            let sessionId = SessionId.create ctx.SessionId
            let! persisted = persistDeferred durable sessionId occurrence text ctx.ProviderRunId

            match persisted with
            | Ok() -> return render ctx Path.DeferAccepted (Map [ "value", text ])
            | Error _ -> return render ctx Path.DurableUnavailable Map.empty
        }

    let private deferExecute (journal: AgentJournal option) (args: HostToolArguments) (ctx: HostToolContext) =
        task {
            let text = args.Text "new_work" |> nonBlank

            match journal, occurrenceId ctx with
            | _, _ when text.Length = 0 -> return render ctx Path.Invalid Map.empty
            | Some durable, Some occurrence when not (String.IsNullOrWhiteSpace ctx.SessionId) ->
                return! deferForDurable durable occurrence text ctx
            | _ -> return render ctx Path.DurableUnavailable Map.empty
        }

    let private argumentSchema factory language path =
        ToolHostCodec.stringSchemaDescribed (ProviderProse.render language path Map.empty) factory

    let admission: ToolAdmission = fun _ (r: Role) -> r <> Role.Blogger && r <> Role.Distiller

    let specs factory journal =
        let language = ProviderLanguageBinding.readGlobalPreference ()
        let language = ProviderLanguageBinding.readGlobalPreference ()

        [ { Name = "enough"
            Description = ProviderProse.render language Path.EnoughDescription Map.empty
            Arguments = [ "decision", argumentSchema factory language Path.EnoughArgument ]
            Admission = admission
            Execute = simpleExecute "decision" Path.EnoughAccepted }
          { Name = "abandon"
            Description = ProviderProse.render language Path.AbandonDescription Map.empty
            Arguments = [ "commitment", argumentSchema factory language Path.AbandonArgument ]
            Admission = admission
            Execute = simpleExecute "commitment" Path.AbandonAccepted }
          { Name = "defer"
            Description = ProviderProse.render language Path.DeferDescription Map.empty
            Arguments = [ "new_work", argumentSchema factory language Path.DeferArgument ]
            Admission = admission
            Execute = deferExecute journal } ]
