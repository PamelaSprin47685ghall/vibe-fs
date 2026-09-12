namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.InstitutionalLearning
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider
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
        ProviderLanguageBinding.forSessionText ctx.SessionId

    let private trim (value: string) =
        if isNull value then "" else value.Trim()

    let private dispositionInstructions language disposition =
        match disposition with
        | LearningDisposition.Absorb rule ->
            ProviderProse.instructionLines language Path.Absorbed (Map [ "rule", rule ])
        | LearningDisposition.Birth tip -> ProviderProse.instructionLines language Path.Absorbed (Map [ "rule", tip ])
        | LearningDisposition.Discard _ -> ProviderProse.instructionLines language Path.Discarded Map.empty

    let private resurfacedInstructions language (items: (string * string) list) =
        match items with
        | [] -> []
        | values ->
            ProviderProse.instructionLines language Path.ResurfacedHeading Map.empty
            @ (values
               |> List.collect (fun (_, text) ->
                   ProviderProse.instructionLines language Path.ResurfacedItem (Map [ "work", text ])))

    let private instructionResult language path subs =
        ProviderProse.instructionLines language path subs
        |> LlmFacing.renderInstructions

    let private pendingFor kind (durable: InstitutionalLearningJournalPort) sessionId =
        match kind with
        | ExperienceKind.Celebrate -> durable.PendingAttentionWorkPairs sessionId
        | ExperienceKind.Regret -> []

    let private commitLearning
        kind
        (durable: InstitutionalLearningJournalPort)
        experience
        language
        sessionId
        occurrence
        providerRun
        =
        taskResult {
            match InstitutionalLearningProjection.tryFind sessionId occurrence (durable.ReadState sessionId) with
            | Some record -> return record.FrozenResult
            | None ->
                let rules = EnforcerCatalogResource.loadFor language
                let revision = InstitutionalEnhancer.rulebookRevision rules
                let disposition = InstitutionalEnhancer.evaluate experience rules
                let pending = pendingFor kind durable sessionId

                let frozen =
                    LlmFacing.renderInstructions (
                        dispositionInstructions language disposition
                        @ resurfacedInstructions language pending
                    )

                let fact =
                    InstitutionalLearningFactCases.LearningDispositionCommitted
                        {| SessionId = sessionId
                           OccurrenceId = occurrence
                           Kind = kind
                           Experience = experience
                           RulebookRevision = revision
                           Disposition = disposition
                           FrozenResult = frozen
                           ResurfacedDeferredWorkIds = pending |> List.map fst |}

                let! _ = durable.Append sessionId providerRun fact
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

    let private execute
        kind
        (journal: InstitutionalLearningJournalPort option)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        =
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
        ToolAdmission.OfficeRole(fun _ (r: Role) -> r <> Role.Blogger && r <> Role.Distiller)

    let specs factory (journal: InstitutionalLearningJournalPort option) =
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
