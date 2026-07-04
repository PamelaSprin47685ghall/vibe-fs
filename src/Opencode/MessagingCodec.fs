module Wanxiangshu.Opencode.MessagingCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell
open Wanxiangshu.Shell.MessagingPartCodec
open Wanxiangshu.Shell.MessagingEncodeHelpers
open Wanxiangshu.Shell.MessagingDecodeCore
open Wanxiangshu.Shell.MessagingEncodeCore

open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn

/// The single FFI boundary between host message objects and the strongly-typed
/// Kernel tree. All Dyn/JsInterop access for the message chain lives here.

let decodeToolState (state: obj) : ToolState<obj> option = decodeOpencodeToolStateBox state

let opencodeAdapters = {
    GetParts = fun msg -> decodePartsFromArray (Dyn.get msg "parts")
    PartType = fun p -> Dyn.str p "type"
    PartToolName = fun p -> Dyn.str p "tool"
    PartCallID = fun p -> Dyn.str p "callID"
    PartState = fun p -> let s = Dyn.get p "state" in if Dyn.isNullish s then None else Some s
    MessageID = fun m -> let i = Dyn.get m "info" in if Dyn.isNullish i then "" else Dyn.str i "id"
    MessageRole = fun m -> let i = Dyn.get m "info" in if Dyn.isNullish i then "" else Dyn.str i "role"
    MessageAgent = fun m -> let i = Dyn.get m "info" in if Dyn.isNullish i then "" else Dyn.str i "agent"
    MessageToolName = fun m -> let i = Dyn.get m "info" in if Dyn.isNullish i then "" else Dyn.str i "toolName"
    MessageIsError = fun m ->
        let i = Dyn.get m "info"
        if Dyn.isNullish i then false
        else let v = Dyn.get i "isError" in not (Dyn.isNullish v) && (v :?> bool)
    MessageDetails = fun m -> let i = Dyn.get m "info" in if Dyn.isNullish i then null else Dyn.get i "details"
    MessageTime = fun m -> let i = Dyn.get m "info" in if Dyn.isNullish i then null else Dyn.get i "time"
    MessageSessionID = fun m -> let i = Dyn.get m "info" in if Dyn.isNullish i then "" else Dyn.str i "sessionID"
    DecodeToolState = decodeToolState
    DecodeTextPart = decodeTextPart
    RequireRole = false
}

let decodeMessage msg = Wanxiangshu.Shell.MessagingDecodeCore.decodeMessage opencodeAdapters "" msg
let decodeMessages msgs = Wanxiangshu.Shell.MessagingDecodeCore.decodeMessages opencodeAdapters "" msgs

/// Encode a Part back to a host object. Text parts are rebuilt via
/// MessagingEncodeCore.encodeTextPartBasic; Tool parts delegate to
/// encodeOpencodeToolPart which preserves raw reference identity when state
/// matches and rebuilds state otherwise. Raw parts pass through.
let encodePart (part: Part<obj>) : obj =
    match part with
    | TextPart text -> encodeTextPartBasic text
    | ToolPart(toolName, callID, stateOpt, raw) -> encodeOpencodeToolPart toolName callID stateOpt raw
    | RawPart raw -> raw

let private encodeMessageInfo (info: MessageInfo<obj>) : obj =
    let timeObj = if isNull info.time then box (createObj [ "created", box 0 ]) else info.time
    createObj [
        "id", box info.id
        "sessionID", box info.sessionID
        "role", box (match info.role with User -> "user" | Assistant -> "assistant" | ToolResult -> "toolResult" | System -> "system")
        "agent", box info.agent
        "isError", box info.isError
        "toolName", box info.toolName
        "details", info.details
        "time", timeObj
        "model", box (createObj [ "providerID", box ""; "modelID", box "" ])
    ]

/// Encode a typed Message back to a host object. For native messages
/// (raw <> null) this returns the raw reference unchanged when no part was
/// mutated (preserving object identity for "preserves original" contracts);
/// only messages whose parts were rebuilt by a pure typed update get a shallow
/// copy with `parts` replaced. Synthetic messages (raw = null) build fresh.
let encodeMessage (msg: Message<obj>) : obj =
    if isNull msg.raw then
        let partsObj = msg.parts |> List.map encodePart |> List.toArray
        box (createObj ["info", box (encodeMessageInfo msg.info); "parts", box partsObj])
    else
        let rawParts = get msg.raw "parts"
        let encodedParts = msg.parts |> List.map encodePart |> List.toArray
        let partsUnchanged =
            not (isNullish rawParts) && isArray rawParts
            && (let arr = rawParts :?> obj array
                arr.Length = encodedParts.Length && Array.forall2 (fun a b -> obj.ReferenceEquals(a, b)) arr encodedParts)
        if partsUnchanged then msg.raw
        else replacePartsOnRawMessage msg.raw encodedParts

let encodeMessages (messages: Message<obj> list) : obj array =
    messages |> List.map encodeMessage |> List.toArray
