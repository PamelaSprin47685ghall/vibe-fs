module VibeFs.Omp.MessagingCodecEncode

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Messaging
open VibeFs.Shell.Dyn
module Dyn = VibeFs.Shell.Dyn

let private encodePart (part: Part<obj>) : obj =
    match part with
    | TextPart text -> box (createObj [ "type", box "text"; "text", box text ])
    | ToolPart(toolName, callID, Some state, raw) ->
        let rawState = if isNull raw then null else Dyn.get raw "state"
        if not (isNull raw) && not (Dyn.isNullish rawState)
           && Dyn.str rawState "status" = state.status
           && Dyn.str rawState "output" = state.output
           && Dyn.str rawState "error" = state.error then
            raw
        else
            let stateObj =
                if Dyn.isNullish rawState then
                    box (createObj [ "status", box state.status; "output", box state.output; "error", box state.error; "input", state.input ])
                else
                    let s1 = Dyn.withKey rawState "status" (box state.status)
                    let s2 = Dyn.withKey s1 "output" (box state.output)
                    Dyn.withKey s2 "error" (box state.error)
            if isNull raw then
                box (createObj [ "type", box "tool"; "tool", box toolName; "callID", box callID; "state", stateObj ])
            else
                Dyn.withKey raw "state" stateObj
    | ToolPart(toolName, callID, None, raw) ->
        if isNull raw then box (createObj [ "type", box "tool"; "tool", box toolName; "callID", box callID ])
        else raw
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