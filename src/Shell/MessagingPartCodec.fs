// Kernel Messaging tree decode (tool state, text parts, host wire shapes).
// For dedup / text projection on msg.parts only, use HostMessagePartCodec.
module Wanxiangshu.Shell.MessagingPartCodec

open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn

let operationActionFromInput (input: obj) : string =
    if Dyn.isNullish input then
        ""
    else
        let operation = Dyn.get input "operation"

        if Dyn.isNullish operation then
            ""
        else
            Dyn.str operation "action"

let toolOutputAndErrorFromHostOutput (output: obj) : string * string =
    if Dyn.isNullish output then "", ""
    elif Dyn.typeIs output "string" then string output, ""
    else Dyn.str output "content", Dyn.str output "error"

let decodeOpencodeToolStateBox (state: obj) : ToolState<obj> option =
    if Dyn.isNullish state then
        None
    else
        let input = Dyn.get state "input"

        Some
            { status = Dyn.str state "status"
              output = Dyn.str state "output"
              error = Dyn.str state "error"
              input = input
              operationAction = operationActionFromInput input }

let muxPartStateToKernelStatus (partState: string) : string =
    match partState with
    | "output-available" -> "completed"
    | "input-available" -> "pending"
    | other -> other

let decodeMuxDynamicToolState (part: obj) : ToolState<obj> option =
    let output = Dyn.get part "output"
    let input = Dyn.get part "input"

    if Dyn.isNullish output && Dyn.isNullish input then
        None
    else
        let out, err = toolOutputAndErrorFromHostOutput output

        Some
            { status = muxPartStateToKernelStatus (Dyn.str part "state")
              output = out
              error = err
              input = input
              operationAction = operationActionFromInput input }

let decodeTextPart (part: obj) : string = Dyn.str part "text"

let decodePartsFromArray (parts: obj) : obj array =
    if Dyn.isNullish parts || not (Dyn.isArray parts) then
        [||]
    else
        parts :?> obj array
