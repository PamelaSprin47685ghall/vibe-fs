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

/// Synchronous Inspector delegation via reusable SyncDelegate Session.
/// Ordinary assistant completion → bounded WorkRecord (EXEC-031).
module InspectorTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/inspect/description"

        [<Literal>]
        let ArgCharge = "tool/inspect/arg-charge"

        [<Literal>]
        let ArgKeywords = "tool/inspect/arg-keywords"

        [<Literal>]
        let Unavailable = "tool/inspect/unavailable"

        [<Literal>]
        let AuthorityRequired = "tool/inspect/authority-required"

        [<Literal>]
        let NeedsCharge = "tool/inspect/needs-charge"

        [<Literal>]
        let Incomplete = "tool/inspect/incomplete"

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private consequence ctx path subs =
        tomlObjectWithInstructions [ ProviderProse.render (lang ctx) path subs ] []

    let private execute
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        (args: HostToolArguments)
        (context: HostToolContext)
        =
        task {
            match syncDelegate with
            | None -> return consequence context Path.Unavailable Map.empty
            | Some sd ->
                if String.IsNullOrWhiteSpace context.SessionId then
                    return consequence context Path.AuthorityRequired Map.empty
                else
                    let charge = args.Text "charge"
                    let keywords = args.Text "keywords"

                    if String.IsNullOrWhiteSpace charge then
                        return consequence context Path.NeedsCharge (Map [ "tool", "inspect" ])
                    else
                        let prepareProviderPrompt () =
                            task {
                                match!
                                    RepositoryWarmStart.prepare
                                        (SessionId.create context.SessionId)
                                        Role.Inspector
                                        scope.WorkspaceDirectory
                                        keywords
                                        charge
                                with
                                | Ok prompt -> return prompt
                                | Error _ -> return charge
                            }

                        let! batch = SyncDelegateBatching.resolve scope SyncDelegateRole.Inspector context

                        let! result =
                            match batch with
                            | Some semanticBatch ->
                                sd.InvokeBatchPrepared(
                                    context.SessionId,
                                    SyncDelegateRole.Inspector,
                                    charge,
                                    semanticBatch,
                                    prepareProviderPrompt
                                )
                            | None ->
                                task {
                                    match!
                                        sd.InvokePrepared(
                                            context.SessionId,
                                            SyncDelegateRole.Inspector,
                                            charge,
                                            prepareProviderPrompt
                                        )
                                    with
                                    | Ok workRecord ->
                                        return Ok(SyncDelegateInvocationResult.WorkRecord workRecord)
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
                        | Error _ -> return consequence context Path.Incomplete Map.empty
        }

    let spec
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        let language = ProviderLanguageBinding.readGlobalPreference ()

        { Name = "inspect"
          Description = ProviderProse.render language Path.Description Map.empty
          Arguments =
            [ "charge",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.ArgCharge Map.empty) factory
              "keywords",
              ToolHostCodec.optionalStringSchemaDescribed
                  (ProviderProse.render language Path.ArgKeywords Map.empty)
                  factory ]
          Execute = execute scope syncDelegate }
