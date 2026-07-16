module Wanxiangshu.Hosts.Mux.MessagingCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.MessagingPartCodec
open Wanxiangshu.Runtime.MessagingEncode
open Wanxiangshu.Runtime.MessagingDecode

let muxAdapters =
    { GetParts = fun msg -> decodePartsFromArray (Dyn.get msg "parts")
      PartType = fun p -> Dyn.str p "type"
      PartToolName = fun p -> normalizeToolName mux (Dyn.str p "toolName")
      PartCallID = fun p -> Dyn.str p "toolCallId"
      PartState = fun p -> Some p
      MessageID = fun m -> Dyn.str m "id"
      MessageRole = fun m -> Dyn.str m "role"
      MessageAgent = fun m -> Dyn.str m "agent"
      MessageToolName = fun _ -> ""
      MessageIsError = fun _ -> false
      MessageDetails = fun _ -> null
      MessageTime = fun _ -> null
      MessageSessionID = fun _ -> ""
      DecodeToolState = decodeMuxDynamicToolState
      DecodeTextPart = decodeTextPart
      RequireRole = false }

let decodeMessage sessionID msg =
    Wanxiangshu.Runtime.MessagingDecode.decodeMessage muxAdapters sessionID msg

let decodeMessages sessionID msgs =
    Wanxiangshu.Runtime.MessagingDecode.decodeMessages muxAdapters sessionID msgs

/// Encode a Part back to a host object. Text parts carry state="done";
/// Tool parts delegate to MessagingEncode.encodeMuxToolPart which builds
/// dynamic-tool objects and preserves raw reference identity when state matches.
let encodePart (part: Part<obj>) : obj =
    match part with
    | TextPart text -> encodeTextPartWithState text "done"
    | ToolPart(toolName, callID, stateOpt, raw) -> encodeMuxToolPart toolName callID stateOpt raw
    | RawPart raw -> raw

let encodeMessage (msg: Message<obj>) : obj =
    let encodedParts = msg.parts |> List.map encodePart |> List.toArray

    let role =
        match msg.info.role with
        | User -> "user"
        | Assistant -> "assistant"
        | ToolResult -> "tool-result"
        | System -> "system"

    if isNull msg.raw then
        box (createObj [ "id", box msg.info.id; "role", box role; "parts", box encodedParts ])
    else
        let rawParts = Dyn.get msg.raw "parts"

        let partsUnchanged =
            if Dyn.isNullish rawParts || not (Dyn.isArray rawParts) then
                false
            else
                let arr = rawParts :?> obj array

                arr.Length = encodedParts.Length
                && Array.forall2 partsEquivalent arr encodedParts

        if partsUnchanged then
            msg.raw
        else
            replacePartsOnRawMessage msg.raw encodedParts

let encodeMessages (messages: Message<obj> list) : obj array =
    messages |> List.map encodeMessage |> List.toArray
