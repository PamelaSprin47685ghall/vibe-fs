namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// docs/what/enforcer.md — the `chronicle` tool (ENFORCER-010/020/040/041/061 tip v2).
/// Provider schema: required `entry` + required `tip`; no legacy blog/text alias.
module ChronicleTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/chronicle/description"

        [<Literal>]
        let Remembered = "tool/chronicle/remembered"

        [<Literal>]
        let NothingToRemember = "tool/chronicle/nothing-to-remember"

        [<Literal>]
        let UnknownTip = "tool/chronicle/unknown-tip"

    let EmptyTextError = "CHRONICLE_EMPTY_ENFORCER_061"

    let NoLiveCycleError = "CHRONICLE_NO_LIVE_CYCLE"

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private prose language path =
        ProviderProse.render language path Map.empty

    let tryCanonicalText (rawText: string) : Result<string, string> =
        let trimmed = if isNull rawText then "" else rawText.Trim()

        if trimmed.Length = 0 then
            Error EmptyTextError
        else
            Ok trimmed

    let hasLiveCycle (parkedHost: IParkedTransformHost option) (sessionId: string) : bool =
        match parkedHost with
        | None -> false
        | Some host -> host.HasFlight sessionId

    let private enforcerRules () =
        RuntimeResources.current().EnforcerRules

    let tipFieldNames () : string list =
        EnforcerCatalog.fieldNames (enforcerRules ())

    let private remembered language =
        ToolHostCodec.tomlObjectWithInstructions [ prose language Path.Remembered ] []

    let private nothingToRemember language =
        ToolHostCodec.tomlObjectWithInstructions [ prose language Path.NothingToRemember ] []

    let private unknownTip language =
        ToolHostCodec.tomlObjectWithInstructions [ prose language Path.UnknownTip ] []

    let spec
        (factory: HostToolFactory)
        (runtime: ToolRuntimeScope)
        (parkedHost: IParkedTransformHost option)
        : ToolSpec =
        let fields = tipFieldNames ()
        let ruleCount = List.length fields

        let catalogDescription =
            ProviderProse.render
                (ProviderLanguageBinding.readGlobalPreference ())
                Path.Description
                (Map [ "rule_count", string ruleCount ])

        { Name = "chronicle"
          Description = catalogDescription
          Arguments =
            [ "entry", ToolHostCodec.stringSchema factory
              "tip", ToolHostCodec.enumSchema fields factory ]
          Execute =
            fun args ctx ->
                task {
                    let language = lang ctx

                    if not (hasLiveCycle parkedHost ctx.SessionId) then
                        Diagnostic.emit
                            "chronicle-execute"
                            [ "session_id", ctx.SessionId; "result", NoLiveCycleError ]

                        if not (String.IsNullOrWhiteSpace ctx.SessionId) then
                            let! _ = runtime.Sessions.AbortSession(SessionId.create ctx.SessionId)
                            ()

                        return raise (InvalidOperationException(NoLiveCycleError))
                    else
                        match tryCanonicalText (args.Text "entry") with
                        | Error _ -> return nothingToRemember language
                        | Ok _ ->
                            let tipRaw = args.Text "tip"

                            if String.IsNullOrWhiteSpace tipRaw then
                                return unknownTip language
                            else
                                match EnforcerCatalog.tryFindByField tipRaw (enforcerRules ()) with
                                | None -> return unknownTip language
                                | Some _ -> return remembered language
                } }
