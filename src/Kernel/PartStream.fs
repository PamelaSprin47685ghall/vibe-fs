module VibeFs.Kernel.PartStream

open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.MessageDecoder

type FlatPart = {
    msgIndex: int
    partIndex: int
    isUser: bool
    part: obj
}

let messageIsUser (msg: obj) : bool =
    let info = messageInfo msg
    if isNullish info then false else infoRole info = "user"

let partIsTool (part: obj) : bool = partType part = "tool"
let partIsText (part: obj) : bool = partType part = "text"

let partToolName (part: obj) : string = str part "tool"

let partToolStatus (part: obj) : string =
    let state = partState part
    if isNullish state then "" else str state "status"

let partToolOutput (part: obj) : string =
    let state = partState part
    if isNullish state then "" else str state "output"

let partToolError (part: obj) : string =
    let state = partState part
    if isNullish state then "" else str state "error"

let partToolInput (part: obj) : obj =
    let state = partState part
    if isNullish state then null else get state "input"

let partCallID (part: obj) : string = str part "callID"

let partTextStr (part: obj) : string =
    let text = partText part
    if isNullish text then "" else string text

let setPartOutput (part: obj) (newOutput: string) : obj =
    let clone = clone part
    let state = get clone "state"
    if not (isNullish state) then state?("output") <- box newOutput
    clone

let flatten (messages: obj array) : FlatPart list =
    let entries = ResizeArray<FlatPart>()
    for msgIdx = 0 to messages.Length - 1 do
        let msg = messages.[msgIdx]
        if isNullish msg then ()
        else
            let isUser = messageIsUser msg
            let parts = messageParts msg
            if not (isNullish parts) && isArray parts then
                let partsArr = parts :?> obj array
                for partIdx = 0 to partsArr.Length - 1 do
                    let part = partsArr.[partIdx]
                    if not (isNullish part) then
                        entries.Add { msgIndex = msgIdx; partIndex = partIdx; isUser = isUser; part = part }
    List.ofSeq entries

let rebuild (messages: obj array) (visible: FlatPart list) : obj array =
    let byMessage = visible |> List.groupBy (fun e -> e.msgIndex) |> Map.ofList
    let result = ResizeArray<obj>()
    for msgIdx = 0 to messages.Length - 1 do
        let msg = messages.[msgIdx]
        if isNullish msg then ()
        else
            let isUser = messageIsUser msg
            match Map.tryFind msgIdx byMessage with
            | None ->
                if isUser then result.Add msg
            | Some entries ->
                if isUser then
                    result.Add msg
                else
                    let partMap = entries |> List.map (fun e -> e.partIndex, e.part) |> Map.ofList
                    let originalParts = messageParts msg
                    if isNullish originalParts || not (isArray originalParts) then
                        result.Add msg
                    else
                        let partsArr = originalParts :?> obj array
                        let newParts = ResizeArray<obj>()
                        for partIdx = 0 to partsArr.Length - 1 do
                            match Map.tryFind partIdx partMap with
                            | Some part -> newParts.Add part
                            | None -> ()
                        if newParts.Count > 0 then
                            let cloneMsg = clone msg
                            cloneMsg?("parts") <- box (newParts.ToArray())
                            result.Add cloneMsg
    result.ToArray()
