namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open ToolHostCodec

/// DevOps synchronous Coder delegation via reusable SyncDelegate Session.
/// `establish-behavior` / `repair-behavior` replace the old coder(tdd=...) verb.
/// Ordinary assistant completion → bounded WorkRecord (EXEC-031).
module CoderTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<RequireQualifiedAccess>]
        module Establish =
            [<Literal>]
            let Description = "tool/establish-behavior/description"

            [<Literal>]
            let ArgCharge = "tool/establish-behavior/arg-charge"

            [<Literal>]
            let ArgKeywords = "tool/establish-behavior/arg-keywords"

            [<Literal>]
            let Unavailable = "tool/establish-behavior/unavailable"

            [<Literal>]
            let AuthorityRequired = "tool/establish-behavior/authority-required"

            [<Literal>]
            let NeedsCharge = "tool/establish-behavior/needs-charge"

            [<Literal>]
            let Incomplete = "tool/establish-behavior/incomplete"

        [<RequireQualifiedAccess>]
        module Repair =
            [<Literal>]
            let Description = "tool/repair-behavior/description"

            [<Literal>]
            let ArgCharge = "tool/repair-behavior/arg-charge"

            [<Literal>]
            let ArgKeywords = "tool/repair-behavior/arg-keywords"

            [<Literal>]
            let Unavailable = "tool/repair-behavior/unavailable"

            [<Literal>]
            let AuthorityRequired = "tool/repair-behavior/authority-required"

            [<Literal>]
            let NeedsCharge = "tool/repair-behavior/needs-charge"

            [<Literal>]
            let Incomplete = "tool/repair-behavior/incomplete"

    type private Surface =
        { Description: string
          ArgCharge: string
          ArgKeywords: string
          Unavailable: string
          AuthorityRequired: string
          NeedsCharge: string
          Incomplete: string }

    let private establishSurface =
        { Description = Path.Establish.Description
          ArgCharge = Path.Establish.ArgCharge
          ArgKeywords = Path.Establish.ArgKeywords
          Unavailable = Path.Establish.Unavailable
          AuthorityRequired = Path.Establish.AuthorityRequired
          NeedsCharge = Path.Establish.NeedsCharge
          Incomplete = Path.Establish.Incomplete }

    let private repairSurface =
        { Description = Path.Repair.Description
          ArgCharge = Path.Repair.ArgCharge
          ArgKeywords = Path.Repair.ArgKeywords
          Unavailable = Path.Repair.Unavailable
          AuthorityRequired = Path.Repair.AuthorityRequired
          NeedsCharge = Path.Repair.NeedsCharge
          Incomplete = Path.Repair.Incomplete }

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private consequence ctx path subs =
        tomlObjectWithInstructions [ ProviderProse.render (lang ctx) path subs ] []

    let private execute
        (toolName: string)
        (surface: Surface)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        (args: HostToolArguments)
        (context: HostToolContext)
        =
        task {
            match syncDelegate with
            | None -> return consequence context surface.Unavailable Map.empty
            | Some sd ->
                if String.IsNullOrWhiteSpace context.SessionId then
                    return consequence context surface.AuthorityRequired Map.empty
                else
                    let charge = args.Text "charge"
                    let keywords = args.Text "keywords"

                    if String.IsNullOrWhiteSpace charge then
                        return consequence context surface.NeedsCharge (Map [ "tool", toolName ])
                    else
                        let prepareProviderPrompt () =
                            task {
                                match!
                                    RepositoryWarmStart.prepare
                                        (SessionId.create context.SessionId)
                                        Role.Coder
                                        scope.WorkspaceDirectory
                                        keywords
                                        charge
                                with
                                | Ok prompt -> return prompt
                                | Error _ -> return charge
                            }

                        let! batch = SyncDelegateBatching.resolve scope SyncDelegateRole.Coder context

                        let! result =
                            match batch with
                            | Some semanticBatch ->
                                sd.InvokeBatchPrepared(
                                    context.SessionId,
                                    SyncDelegateRole.Coder,
                                    charge,
                                    semanticBatch,
                                    prepareProviderPrompt
                                )
                            | None ->
                                task {
                                    match!
                                        sd.InvokePrepared(
                                            context.SessionId,
                                            SyncDelegateRole.Coder,
                                            charge,
                                            prepareProviderPrompt
                                        )
                                    with
                                    | Ok workRecord -> return Ok(SyncDelegateInvocationResult.WorkRecord workRecord)
                                    | Error error -> return Error error
                                }

                        match result with
                        | Ok(SyncDelegateInvocationResult.WorkRecord workRecord) ->
                            let instructions =
                                if String.IsNullOrWhiteSpace workRecord then
                                    []
                                else
                                    [ workRecord ]

                            return tomlObjectWithInstructions instructions []
                        | Ok(SyncDelegateInvocationResult.MergedInto canonicalCall) ->
                            return
                                tomlObjectWithInstructions
                                    [ SyncDelegateBatching.mergedInstruction (lang context) canonicalCall ]
                                    []
                        | Error _ -> return consequence context surface.Incomplete Map.empty
        }

    let private behaviorSpec
        (name: string)
        (surface: Surface)
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        let language = ProviderLanguageBinding.readGlobalPreference ()

        { Name = name
          Description = ProviderProse.render language surface.Description Map.empty
          Arguments =
            [ "charge",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language surface.ArgCharge Map.empty) factory
              "keywords",
              ToolHostCodec.optionalStringSchemaDescribed
                  (ProviderProse.render language surface.ArgKeywords Map.empty)
                  factory ]
          Execute = execute name surface scope syncDelegate }

    let establishSpec
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        behaviorSpec "establish-behavior" establishSurface factory scope syncDelegate

    let repairSpec
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        behaviorSpec "repair-behavior" repairSurface factory scope syncDelegate
