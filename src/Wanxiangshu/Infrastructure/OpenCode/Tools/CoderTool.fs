namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open ToolHostCodec

/// DevOps synchronous Coder delegation via reusable SyncDelegate Session.
/// `establish-behavior` / `repair-behavior` replace the old coder(tdd=...) verb.
/// Ordinary assistant completion → bounded WorkRecord (EXEC-031).
module CoderTool =

    let private execute
        (roleVerb: string)
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
                        return tomlObject [ "error", TString(sprintf "%s charge required" roleVerb) ]
                    else
                        let prepareProviderPrompt () =
                            task {
                                match!
                                    RepositoryWarmStart.prepare Role.Coder scope.WorkspaceDirectory keywords charge
                                with
                                | Ok prompt -> return prompt
                                | Error _ -> return charge
                            }

                        match!
                            sd.InvokePrepared(
                                context.SessionId,
                                SyncDelegateRole.Coder,
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

    let private behaviorSpec
        (name: string)
        (description: string)
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        { Name = name
          Description = description
          Arguments =
            [ "charge", ToolHostCodec.stringSchema factory
              "keywords", ToolHostCodec.optionalStringSchema factory ]
          Execute = execute name scope syncDelegate }

    let establishSpec
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        behaviorSpec
            "establish-behavior"
            "Ask a Coder to establish a failing behavior test. Returns a bounded WorkRecord after ordinary completion."
            factory
            scope
            syncDelegate

    let repairSpec
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        behaviorSpec
            "repair-behavior"
            "Ask a Coder to implement the smallest production change that makes an established behavior test pass. Returns a bounded WorkRecord after ordinary completion."
            factory
            scope
            syncDelegate

    /// Back-compat alias used by older call sites that registered a single coder tool.
    let spec factory scope syncDelegate =
        establishSpec factory scope syncDelegate
