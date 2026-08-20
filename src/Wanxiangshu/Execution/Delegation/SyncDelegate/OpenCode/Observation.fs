namespace Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

[<RequireQualifiedAccess>]
module SyncDelegateHostObservation =

    let private eventString (value: obj) =
        if isNull value then
            None
        else
            let text = string value
            if String.IsNullOrWhiteSpace text then None else Some text

    let private observeRoleOfTool (runtime: SyncDelegateRuntime) owner messageId callId toolName =
        SyncDelegate.tryRoleOfToolName toolName
        |> Option.iter (fun role ->
            runtime.ObserveProviderToolCall(
                owner,
                ProviderRunIdentity.create messageId,
                role,
                ToolCallId.create callId
            ))

    let private observeToolCallIdentity (runtime: SyncDelegateRuntime) owner (part: obj) =
        match eventString part?messageID, eventString part?callID, eventString part?tool with
        | Some messageId, Some callId, Some toolName -> observeRoleOfTool runtime owner messageId callId toolName
        | _ -> ()

    let private observeToolPart (runtime: SyncDelegateRuntime) owner raw =
        let properties = raw?properties
        let part = if isNull properties then null else properties?part

        if not (isNull part) && eventString part?``type`` = Some "tool" then
            observeToolCallIdentity runtime owner part

    let private observeEvent (runtime: SyncDelegateRuntime) (raw: obj) =
        match HostEventCodec.eventTypeOf raw, HostEventCodec.trySessionId raw with
        | "message.part.updated", Some owner -> observeToolPart runtime owner raw
        | _ -> ()

    let observe (runtime: SyncDelegateRuntime option) (rawInput: obj) =
        runtime
        |> Option.iter (fun active -> observeEvent active (HostEventCodec.unwrap rawInput))
