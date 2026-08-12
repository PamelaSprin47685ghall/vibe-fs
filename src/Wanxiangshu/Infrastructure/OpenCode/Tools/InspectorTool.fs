namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open ToolHostCodec

/// Synchronous Inspector delegation via reusable SyncDelegate Session.
/// Ordinary assistant completion → bounded WorkRecord (EXEC-031).
module InspectorTool =

    let private execute
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        (args: HostToolArguments)
        (context: HostToolContext)
        =
        task {
            match syncDelegate with
            | None -> return tomlObject [ "error", TString "SyncDelegate runtime unavailable" ]
            | Some sd ->
                if String.IsNullOrWhiteSpace context.SessionId then
                    return tomlObject [ "error", TString "Missing sessionID" ]
                else
                    let charge = args.Text "charge"
                    let keywords = args.Text "keywords"

                    if String.IsNullOrWhiteSpace charge then
                        return tomlObject [ "error", TString "inspect charge required" ]
                    else
                        let prepareProviderPrompt () =
                            task {
                                match!
                                    RepositoryWarmStart.prepare
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
                        | Error error -> return tomlObject [ "error", TString error ]
        }

    let spec
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        { Name = "inspect"
          Description =
            "Ask an Inspector to establish a repository fact. Returns a bounded WorkRecord after ordinary completion."
          Arguments =
            [ "charge", ToolHostCodec.stringSchema factory
              "keywords", ToolHostCodec.optionalStringSchema factory ]
          Execute = execute scope syncDelegate }
