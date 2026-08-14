namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

[<RequireQualifiedAccess>]
module SyncDelegateBatching =

    [<Literal>]
    let MergedReference = "tool/sync-delegate/merged-reference"

    let private roleOfToolName (name: string) =
        match name.Trim().ToLowerInvariant() with
        | "inspect" -> Some SyncDelegateRole.Inspector
        | "establish-behavior"
        | "repair-behavior" -> Some SyncDelegateRole.Coder
        | _ -> None

    let resolve (scope: ToolRuntimeScope) (role: SyncDelegateRole) (context: HostToolContext) =
        task {
            match scope.Snapshot, context.ProviderRunId, context.ToolCallId with
            | Some snapshot, Some providerRun, Some currentCall when not (String.IsNullOrWhiteSpace context.SessionId) ->
                match! snapshot.GetMessages(SessionId.create context.SessionId) with
                | Error _ -> return None
                | Ok messages ->
                    let providerRunKey = ProviderRunIdentity.value providerRun

                    match messages |> List.tryFind (fun message -> message.Id = providerRunKey) with
                    | None -> return None
                    | Some message ->
                        let callOrder =
                            message.ToolParts
                            |> Array.choose (fun part ->
                                match roleOfToolName part.ToolName with
                                | Some partRole when partRole = role -> Some part.ToolCallId
                                | _ -> None)
                            |> Array.toList

                        if callOrder |> List.exists (fun callId -> callId = currentCall) then
                            return
                                Some
                                    { ProviderRun = providerRun
                                      CallOrder = callOrder
                                      CurrentCall = currentCall }
                        else
                            return None
            | _ -> return None
        }

    let mergedInstruction language canonicalCall =
        ProviderProse.render language MergedReference (Map [ "call", ToolCallId.value canonicalCall ])
