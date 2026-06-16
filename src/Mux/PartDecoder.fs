module VibeFs.Mux.PartDecoder

open VibeFs.Kernel
open VibeFs.Kernel.TreeSitterKernel

type ReadPart =
    { output: obj
      input: obj
      toolName: string
      state: string
      partType: string }

let tryDecodeReadPart (part: obj) : ReadPart option =
    let toolName = Dyn.str part "toolName"
    let partType = Dyn.str part "type"
    let state = Dyn.str part "state"
    let output = Dyn.get part "output"
    if Dyn.isNullish output then None
    else
        let input = Dyn.get part "input"
        if not (Dyn.isNullish input) then
            Some { output = output; input = input; toolName = toolName; state = state; partType = partType }
        else
            let st = Dyn.get part "state"
            if Dyn.isNullish st then None
            else
                let inp = Dyn.get st "input"
                Some { output = output; input = inp; toolName = toolName; state = state; partType = partType }

let readPartOutputKey (output: obj) : string =
    if Dyn.isNullish output then ""
    elif Dyn.typeIs output "string" then string output
    else
        let content = Dyn.get output "content"
        if Dyn.isNullish content then "" else string content

let readPartPath (rp: ReadPart) : string =
    match extractFilePaths rp.input with
    | path :: _ -> path
    | [] -> ""

type ModelReadPart =
    { output: obj
      input: obj
      toolName: string
      partType: string
      outputType: string
      outputValue: obj }

let tryDecodeModelReadPart (part: obj) : ModelReadPart option =
    let toolName = Dyn.str part "toolName"
    let partType = Dyn.str part "type"
    let output = Dyn.get part "output"
    if Dyn.isNullish output then None
    else
        let outputType = Dyn.str output "type"
        let outputValue = Dyn.get output "value"
        let input = Dyn.get part "input"
        Some { output = output; input = input; toolName = toolName; partType = partType
               outputType = outputType; outputValue = outputValue }

let modelReadPartOutputKey (part: ModelReadPart) : string =
    let value = part.outputValue
    if part.outputType = "text" && not (Dyn.isNullish value) then string value
    elif part.outputType = "json" && not (Dyn.isNullish value) then readPartOutputKey value
    else ""

let modelReadPartPath (part: ModelReadPart) : string =
    match extractFilePaths part.input with
    | path :: _ -> path
    | [] -> ""

let messageParts (msg: obj) : obj = Dyn.get msg "parts"

let messageContent (msg: obj) : obj = Dyn.get msg "content"
