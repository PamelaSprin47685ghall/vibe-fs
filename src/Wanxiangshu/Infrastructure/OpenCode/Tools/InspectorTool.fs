namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure
open Wanxiangshu.Infrastructure.Resources
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

                        match!
                            sd.InvokePrepared(
                                context.SessionId,
                                SyncDelegateRole.Inspector,
                                charge,
                                prepareProviderPrompt
                            )
                        with
                        | Ok workRecord ->
                            let instructions =
                                if String.IsNullOrWhiteSpace workRecord then
                                    []
                                else
                                    [ workRecord ]

                            return tomlObjectWithInstructions instructions []
                        | Error _ -> return consequence context Path.Incomplete Map.empty
        }

    let spec
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        { Name = "inspect"
          Description =
            ProviderProse.render
                (ProviderLanguageBinding.readGlobalPreference ())
                Path.Description
                Map.empty
          Arguments =
            [ "charge", ToolHostCodec.stringSchema factory
              "keywords", ToolHostCodec.optionalStringSchema factory ]
          Execute = execute scope syncDelegate }
