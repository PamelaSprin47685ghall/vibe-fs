namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open ToolHostCodec

/// DevOps synchronous Coder delegation via reusable SyncDelegate Session.
/// `establish-behavior` / `repair-behavior` replace the old coder(tdd=...) verb.
/// Ordinary assistant completion → bounded WorkRecord (EXEC-031).
module CoderTool =

    let private consequence message =
        tomlObjectWithInstructions [ message ] []

    let private execute
        (roleVerb: string)
        (syncDelegate: SyncDelegateRuntime option)
        (args: HostToolArguments)
        (context: HostToolContext)
        =
        task {
            match syncDelegate with
            | None -> return consequence "No Coder is available from this execution context."
            | Some sd ->
                if String.IsNullOrWhiteSpace context.SessionId then
                    return consequence "A Coder cannot be charged before the caller's authority is established."
                else
                    let charge = args.Text "charge"

                    if String.IsNullOrWhiteSpace charge then
                        return consequence (sprintf "%s needs a charge." roleVerb)
                    else
                        match! sd.Invoke(context.SessionId, SyncDelegateRole.Coder, charge) with
                        | Ok workRecord ->
                            let instructions =
                                if String.IsNullOrWhiteSpace workRecord then
                                    []
                                else
                                    [ workRecord ]

                            return tomlObjectWithInstructions instructions []
                        | Error _ -> return consequence "The Coder could not complete this charge."
        }

    let private behaviorSpec
        (name: string)
        (description: string)
        (factory: HostToolFactory)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        { Name = name
          Description = description
          Arguments = [ "charge", ToolHostCodec.stringSchema factory ]
          Execute = execute name syncDelegate }

    let establishSpec
        (factory: HostToolFactory)
        (_scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        behaviorSpec
            "establish-behavior"
            "Ask a Coder to establish a failing behavior test. Returns a bounded WorkRecord after ordinary completion."
            factory
            syncDelegate

    let repairSpec
        (factory: HostToolFactory)
        (_scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        behaviorSpec
            "repair-behavior"
            "Ask a Coder to implement the smallest production change that makes an established behavior test pass. Returns a bounded WorkRecord after ordinary completion."
            factory
            syncDelegate

