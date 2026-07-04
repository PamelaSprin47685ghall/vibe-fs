module Wanxiangshu.Omp.MessagingCodecEncode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.MessagingEncodeCore
module Dyn = Wanxiangshu.Shell.Dyn

let private encodePart (part: Part<obj>) : obj =
    match part with
    | TextPart text -> encodeTextPartBasic text
    | ToolPart(toolName, callID, stateOpt, raw) -> encodeOpencodeToolPart toolName callID stateOpt raw
    | RawPart raw -> raw

let private encodeRole = function
    | User -> "user"
    | Assistant -> "assistant"
    | ToolResult -> "tool"
    | System -> "system"

let encodeMessage (msg: Message<obj>) : obj =
    if isNull msg.raw then
        let partsObj = msg.parts |> List.map encodePart |> List.toArray
        box (createObj [ "info", box(createObj [ "id", box msg.info.id; "role", box (encodeRole msg.info.role) ]); "parts", box partsObj ])
    else
        let rawParts = Dyn.get msg.raw "parts"
        let encodedParts = msg.parts |> List.map encodePart |> List.toArray
        let partsUnchanged =
            not (Dyn.isNullish rawParts) && Dyn.isArray rawParts
            && (let arr = rawParts :?> obj array
                arr.Length = encodedParts.Length && Array.forall2 (fun a b -> obj.ReferenceEquals(a, b)) arr encodedParts)
        if partsUnchanged then msg.raw else Dyn.withKey msg.raw "parts" (box encodedParts)

let encodeMessages (messages: Message<obj> list) : obj array =
    messages |> List.map encodeMessage |> List.toArray