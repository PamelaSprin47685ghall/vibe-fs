namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Resources

/// docs/what/enforcer.md — the `chronicle` tool (ENFORCER-010/020/040/041/061 tip v2).
/// Provider schema: required charge/occurrence/settlement/consequence, a free tipName,
/// opinion and advice lesson fields, plus optional evidence.
module ChronicleTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/chronicle/description"

        [<Literal>]
        let ArgCharge = "tool/chronicle/arg-charge"

        [<Literal>]
        let ArgOccurrence = "tool/chronicle/arg-occurrence"

        [<Literal>]
        let ArgSettlement = "tool/chronicle/arg-settlement"

        [<Literal>]
        let ArgConsequence = "tool/chronicle/arg-consequence"

        [<Literal>]
        let ArgEvidence = "tool/chronicle/arg-evidence"

        [<Literal>]
        let ArgTipName = "tool/chronicle/arg-tip-name"

        [<Literal>]
        let ArgOpinion = "tool/chronicle/arg-opinion"

        [<Literal>]
        let ArgAdvice = "tool/chronicle/arg-advice"

        [<Literal>]
        let Remembered = "tool/chronicle/remembered"

        [<Literal>]
        let NothingToRemember = "tool/chronicle/nothing-to-remember"

        [<Literal>]
        let InvalidTip = "tool/chronicle/invalid-tip"

    [<Literal>]
    let EmptyTextError = ChronicleExecution.EmptyTextError

    [<Literal>]
    let NoLiveCycleError = ChronicleExecution.NoLiveCycleError

    let private lang (ctx: HostToolContext) =
        ProviderLanguageBinding.forSessionText ctx.SessionId

    let private prose language path =
        ProviderProse.render language path Map.empty

    let tryCanonicalText (rawText: string) : Result<string, string> =
        ChronicleExecution.tryCanonicalText rawText

    let private tryCurrentRequest (bloggerHost: IBloggerRuntimeHost option) (sessionId: string) =
        match bloggerHost with
        | None -> None
        | Some host -> host.TryPeekCurrentRequest sessionId

    let hasLiveCycle (bloggerHost: IBloggerRuntimeHost option) (sessionId: string) : bool =
        // Live-cycle authority is the exact current Blogger request context,
        // never a bare session boolean: only a live request may authorize
        // chronicle effects, and only its own release may end them.
        (tryCurrentRequest bloggerHost sessionId).IsSome

    let private enforcerRules () =
        RuntimeResources.current().EnforcerRules

    let tipFieldNames () : string list =
        EnforcerCatalog.fieldNames (enforcerRules ())

    let private remembered language =
        ToolHostCodec.tomlObjectWithInstructions [ prose language Path.Remembered ] []

    let private nothingToRemember language =
        ToolHostCodec.tomlObjectWithInstructions [ prose language Path.NothingToRemember ] []

    let private invalidTip language =
        ToolHostCodec.tomlObjectWithInstructions [ prose language Path.InvalidTip ] []

    let private terminateSessionIfPresent terminateSession sessionId : System.Threading.Tasks.Task =
        task {
            if not (String.IsNullOrWhiteSpace sessionId) then
                let! _ = terminateSession (sessionId, NoLiveCycleError)
                ()
        }

    let private applyNoLiveCycleEffect terminateSession (ctx: HostToolContext) : System.Threading.Tasks.Task =
        task {
            Diagnostic.emit "chronicle-execute" [ "session_id", ctx.SessionId; "result", NoLiveCycleError ]
            do! terminateSessionIfPresent terminateSession ctx.SessionId
        }

    let private tipFieldError (args: HostToolArguments) : string option =
        match ChronicleExecution.tryTipName (args.Text "tipName") with
        | Error error -> Some error
        | Ok _ ->
            EnforcerCodec.tipSectionNames
            |> List.tryPick (fun field ->
                match ChronicleExecution.tryTipField field (args.Text field) with
                | Error error -> Some error
                | Ok _ -> None)

    let private resultForLesson language (args: HostToolArguments) : string =
        match tipFieldError args with
        | Some _ -> invalidTip language
        | None -> remembered language

    let private allContentFieldsValid (args: HostToolArguments) =
        [ "charge"; "occurrence"; "settlement"; "consequence" ]
        |> List.forall (fun name ->
            match tryCanonicalText (args.Text name) with
            | Ok _ -> true
            | Error _ -> false)

    let private evidenceValid (args: HostToolArguments) =
        let evidence = args.Text "evidence"

        String.IsNullOrWhiteSpace evidence
        || evidence.Length <= EnforcerCodec.MaxInlineEvidenceChars

    let private executeValidChronicle language (args: HostToolArguments) : string =
        if allContentFieldsValid args && evidenceValid args then
            resultForLesson language args
        else
            nothingToRemember language

    let private executeChronicle
        (terminateSession: string * string -> System.Threading.Tasks.Task<Result<unit, string>>)
        (bloggerHost: IBloggerRuntimeHost option)
        language
        (args: HostToolArguments)
        (ctx: HostToolContext)
        : System.Threading.Tasks.Task<ChronicleExecution> =
        task {
            let execution =
                match tryCurrentRequest bloggerHost ctx.SessionId with
                | Some _ -> ChronicleExecution.decide true (executeValidChronicle language args)
                | None -> ChronicleExecution.decide false ""

            match execution with
            | ChronicleExecution.Completed _ -> return execution
            | ChronicleExecution.NoLiveCycle ->
                do! applyNoLiveCycleEffect terminateSession ctx
                return execution
        }

    let admission (bloggerHost: IBloggerRuntimeHost option) : ToolAdmission =
        // Admission owns office authority only. A stale physical chronicle call
        // with no live cycle is still a valid Blogger-originated invocation that
        // must reach executeChronicle so the Chronicle owner can produce the
        // typed NoLiveCycle result, terminate the stale session, and let only the
        // outer Host adapter encode that result as an SDK exception (behavior-diagnosis-006).
        ToolAdmission.OfficeRole(fun _ role -> role = Role.Blogger)

    let spec
        (factory: HostToolFactory)
        (terminateSession: string * string -> System.Threading.Tasks.Task<Result<unit, string>>)
        (bloggerHost: IBloggerRuntimeHost option)
        : ToolSpec =
        let fields = tipFieldNames ()
        let ruleCount = List.length fields

        let catalogDescription =
            ProviderProse.render
                (ProviderLanguageBinding.readGlobalPreference ())
                Path.Description
                (Map [ "rule_count", string ruleCount ])

        let language = ProviderLanguageBinding.readGlobalPreference ()

        { Name = "chronicle"
          Description = catalogDescription
          Arguments =
            [ "charge",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.ArgCharge Map.empty) factory
              "occurrence",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.ArgOccurrence Map.empty) factory
              "settlement",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.ArgSettlement Map.empty) factory
              "consequence",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.ArgConsequence Map.empty) factory
              "tipName",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.ArgTipName Map.empty) factory
              "opinion",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.ArgOpinion Map.empty) factory
              "advice",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.ArgAdvice Map.empty) factory
              "evidence",
              ToolHostCodec.optionalStringSchemaDescribed
                  (ProviderProse.render language Path.ArgEvidence Map.empty)
                  factory ]
          Admission = admission bloggerHost
          Execute =
            fun args ctx ->
                task {
                    let language = lang ctx
                    let! execution = executeChronicle terminateSession bloggerHost language args ctx

                    match execution with
                    | ChronicleExecution.Completed value -> return value
                    | ChronicleExecution.NoLiveCycle ->
                        let hostError = InvalidOperationException(NoLiveCycleError)
                        return raise hostError
                } }
