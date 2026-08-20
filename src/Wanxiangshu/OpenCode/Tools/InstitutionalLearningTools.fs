namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.InstitutionalLearning
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Resources

[<RequireQualifiedAccess>]
module InstitutionalLearningTools =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let CelebrateDescription = "institutional-learning/celebrate-description"
        [<Literal>]
        let RegretDescription = "institutional-learning/regret-description"
        [<Literal>]
        let ExperienceArgument = "institutional-learning/experience-argument"
        [<Literal>]
        let Absorbed = "institutional-learning/absorbed"
        [<Literal>]
        let Discarded = "institutional-learning/discarded"
        [<Literal>]
        let ResurfacedHeading = "institutional-learning/resurfaced-heading"
        [<Literal>]
        let ResurfacedItem = "institutional-learning/resurfaced-item"
        [<Literal>]
        let Invalid = "institutional-learning/invalid"
        [<Literal>]
        let DurableUnavailable = "institutional-learning/durable-unavailable"

    let private languageOf (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then ProviderLanguageBinding.readGlobalPreference ()
        else ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private trim (value: string) = if isNull value then "" else value.Trim()

    let private renderDisposition language disposition =
        match disposition with
        | LearningDisposition.Absorb rule ->
            ProviderProse.render language Path.Absorbed (Map [ "rule", rule ])
        | LearningDisposition.Birth tip ->
            ProviderProse.render language Path.Absorbed (Map [ "rule", tip ])
        | LearningDisposition.Discard _ -> ProviderProse.render language Path.Discarded Map.empty

    let private renderResurfaced language (items: DeferredWorkItem list) =
        match items with
        | [] -> ""
        | values ->
            let body =
                values
                |> List.map (fun item -> ProviderProse.render language Path.ResurfacedItem (Map [ "work", item.Text ]))
                |> String.concat "\n"

            "\n\n" + ProviderProse.render language Path.ResurfacedHeading Map.empty + "\n" + body

    let private execute kind (journal: AgentJournal option) (args: HostToolArguments) (ctx: HostToolContext) =
        task {
            let experience = args.Text "experience" |> trim
            let language = languageOf ctx

            match journal, ctx.ToolCallId with
            | _, _ when experience.Length = 0 -> return ProviderProse.render language Path.Invalid Map.empty
            | Some durable, Some callId when not (String.IsNullOrWhiteSpace ctx.SessionId) ->
                let sessionId = SessionId.create ctx.SessionId
                let occurrence = ToolCallId.value callId
                let snapshot = AgentJournal.snapshot durable

                match InstitutionalLearningProjection.tryFind sessionId occurrence snapshot.AgentProjections.InstitutionalLearning with
                | Some record -> return record.FrozenResult
                | None ->
                    let rules = EnforcerCatalogResource.loadFor language
                    let revision = InstitutionalEnhancer.rulebookRevision rules
                    let disposition = InstitutionalEnhancer.evaluate experience rules
                    let pending =
                        match kind with
                        | ExperienceKind.Celebrate -> AttentionProjection.pending sessionId snapshot.AgentProjections.Attention
                        | ExperienceKind.Regret -> []

                    let frozen = renderDisposition language disposition + renderResurfaced language pending
                    let fact =
                        AgentFact.InstitutionalLearning(
                            InstitutionalLearningFactCases.LearningDispositionCommitted
                                {| SessionId = sessionId
                                   OccurrenceId = occurrence
                                   Kind = kind
                                   Experience = experience
                                   RulebookRevision = revision
                                   Disposition = disposition
                                   FrozenResult = frozen
                                   ResurfacedDeferredWorkIds = pending |> List.map _.OccurrenceId |}
                        )

                    match! AgentJournal.appendAgent (StreamId.Session sessionId) ctx.ProviderRunId fact durable with
                    | Ok _ -> return frozen
                    | Error _ -> return ProviderProse.render language Path.DurableUnavailable Map.empty
            | _ -> return ProviderProse.render language Path.DurableUnavailable Map.empty
        }

    let specs factory journal =
        let language = ProviderLanguageBinding.readGlobalPreference ()
        let argument = ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.ExperienceArgument Map.empty) factory

        [ { Name = "celebrate"
            Description = ProviderProse.render language Path.CelebrateDescription Map.empty
            Arguments = [ "experience", argument ]
            Execute = execute ExperienceKind.Celebrate journal }
          { Name = "regret"
            Description = ProviderProse.render language Path.RegretDescription Map.empty
            Arguments = [ "experience", argument ]
            Execute = execute ExperienceKind.Regret journal } ]

