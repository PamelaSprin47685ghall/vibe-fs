namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open ToolHostCodec

/// Synchronous read-only Inspector delegation via reusable SyncDelegate Session
/// (Returned → Completion). Dedicated Inspector is Work+Attached; not dispose-after.
module InspectorTool =

    let private tryAgentName (scope: ToolRuntimeScope) (ownerKey: string) =
        match scope.Journal with
        | None -> None
        | Some journal ->
            SyncDelegateTier.fromJournal journal (SessionId.create ownerKey)
            |> Option.map (fun tier -> SyncDelegate.agentNameFor SyncDelegateRole.Inspector tier)

    let private encode
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime)
        (ownerKey: string)
        (answer: string)
        =
        let instructions = if String.IsNullOrWhiteSpace answer then [] else [ answer ]

        let inspectorId =
            syncDelegate.TryFind(SessionId.create ownerKey, SyncDelegateRole.Inspector)
            |> Option.map SessionId.value

        let fields =
            [ match inspectorId with
              | Some id -> yield "inspector_id", TString id
              | None -> ()
              match tryAgentName scope ownerKey with
              | Some agent -> yield "agent", TString agent
              | None -> () ]

        tomlObjectWithInstructions instructions fields

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
                    let prompt = OneShotAgentTool.promptFrom args

                    if String.IsNullOrWhiteSpace prompt then
                        return tomlObject [ "error", TString "inspector prompt required" ]
                    else
                        match! sd.Invoke(context.SessionId, SyncDelegateRole.Inspector, prompt) with
                        | Ok answer -> return encode scope sd context.SessionId answer
                        | Error error -> return tomlObject [ "error", TString error ]
        }

    let spec
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        { Name = "inspector"
          Description =
            "Reusable dedicated Inspector Session (Returned→Completion); not dispose-after. Owner tier binds the delegate."
          Arguments =
            [ "prompt", ToolHostCodec.optionalStringSchema factory
              "prompts", ToolHostCodec.optionalStringArraySchema factory ]
          Execute = execute scope syncDelegate }
