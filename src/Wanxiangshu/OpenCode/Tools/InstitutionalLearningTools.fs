namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.InstitutionalLearning
open Wanxiangshu.Foundation
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
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private trim (value: string) =
        if isNull value then "" else value.Trim()

    let private dispositionInstructions language disposition =
        match disposition with
        | LearningDisposition.Absorb rule ->
            ProviderProse.instructionLines language Path.Absorbed (Map [ "rule", rule ])
        | LearningDisposition.Birth tip -> ProviderProse.instructionLines language Path.Absorbed (Map [ "rule", tip ])
        | LearningDisposition.Discard _ -> ProviderProse.instructionLines language Path.Discarded Map.empty

    let private resurfacedInstructions language (items: DeferredWorkItem list) =
        match items with
        | [] -> []
        | values ->
            ProviderProse.instructionLines language Path.ResurfacedHeading Map.empty
            @ (values
               |> List.collect (fun item ->
                   ProviderProse.instructionLines language Path.ResurfacedItem (Map [ "work", item.Text ])))

    let private instructionResult language path subs =
        ProviderProse.instructionLines language path subs
        |> LlmFacing.renderInstructions

    let private pendingFor kind sessionId attention =
        match kind with
        | ExperienceKind.Celebrate -> AttentionProjection.pending sessionId attention
        | ExperienceKind.Regret -> []

    let private commitLearning kind durable experience language sessionId occurrence providerRun =
        taskResult {
            let snapshot = AgentJournal.snapshot durable

            match
                InstitutionalLearningProjection.tryFind
                    sessionId
                    occurrence
                    snapshot.AgentProjections.InstitutionalLearning
            with
            | Some record -> return record.FrozenResult
            | None ->
                let rules = EnforcerCatalogResource.loadFor language
                let revision = InstitutionalEnhancer.rulebookRevision rules
                let disposition = InstitutionalEnhancer.evaluate experience rules
                let pending = pendingFor kind sessionId snapshot.AgentProjections.Attention

                let frozen =
                    LlmFacing.renderInstructions (
                        dispositionInstructions language disposition
                        @ resurfacedInstructions language pending
                    )

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

                let! _ = AgentJournal.appendAgent (StreamId.Session sessionId) providerRun fact durable
                return frozen
        }

    let private executeDurable kind durable experience language callId (ctx: HostToolContext) =
        task {
            let sessionId = SessionId.create ctx.SessionId
            let occurrence = ToolCallId.value callId
            let! result = commitLearning kind durable experience language sessionId occurrence ctx.ProviderRunId

            match result with
            | Ok frozen -> return frozen
            | Error _ -> return instructionResult language Path.DurableUnavailable Map.empty
        }

    let private execute kind (journal: AgentJournal option) (args: HostToolArguments) (ctx: HostToolContext) =
        task {
            let experience = args.Text "experience" |> trim
            let language = languageOf ctx

            match journal, ctx.ToolCallId with
            | _, _ when experience.Length = 0 -> return instructionResult language Path.Invalid Map.empty
            | Some durable, Some callId when not (String.IsNullOrWhiteSpace ctx.SessionId) ->
                return! executeDurable kind durable experience language callId ctx
            | _ -> return instructionResult language Path.DurableUnavailable Map.empty
        }

    let admission: ToolAdmission =
        fun _ (r: Role) -> r <> Role.Blogger && r <> Role.Distiller

    let specs factory journal =
        let language = ProviderLanguageBinding.readGlobalPreference ()
        let language = ProviderLanguageBinding.readGlobalPreference ()

        let argument =
            ToolHostCodec.stringSchemaDescribed
                (ProviderProse.render language Path.ExperienceArgument Map.empty)
                factory

        [ { Name = "celebrate"
            Description = ProviderProse.render language Path.CelebrateDescription Map.empty
            Arguments = [ "experience", argument ]
            Admission = admission
            Execute = execute ExperienceKind.Celebrate journal }
          { Name = "regret"
            Description = ProviderProse.render language Path.RegretDescription Map.empty
            Arguments = [ "experience", argument ]
            Admission = admission
            Execute = execute ExperienceKind.Regret journal } ]
