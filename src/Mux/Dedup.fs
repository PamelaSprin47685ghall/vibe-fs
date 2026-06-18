module VibeFs.Mux.Dedup

open VibeFs.Kernel.Dedup
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

/// Tool names that represent a file-read operation across hosts.  The OpenCode
/// host names the tool `read`; the Mux host names it `file_read`.
let readToolNames = Set [ "read"; "file_read" ]

let private classifyMuxReadPart (part: obj) =
    match tryDecodeReadPart part with
    | Some rp when rp.partType = "dynamic-tool" && Set.contains rp.toolName readToolNames && rp.state = "output-available" ->
        let key = readPartOutputKey rp.output
        if key.Length > 0 then Some(readPartPath rp, key) else None
    | _ -> None

let private dedupForPath (seenByPath: Map<string, string list>) (pathKey: string) (current: string) =
    let pathSeen = Map.tryFind pathKey seenByPath |> Option.defaultValue []
    let verdict, nextState =
        processDedup { seenContents = pathSeen } { path = pathKey; content = current }
    let nextOutput =
        match verdict with
        | AlreadySeen -> dedupMarker
        | NewContent payload -> payload.content
    (Map.add pathKey nextState.seenContents seenByPath, nextOutput, verdict)

let private foldMuxReadPartsIntoSeenByPath (seenByPath: Map<string, string list>) (messages: obj array) : Map<string, string list> =
    let foldPart acc part =
        match classifyMuxReadPart part with
        | Some(pathKey, key) ->
            let nextSeen, _, _ = dedupForPath acc pathKey key
            nextSeen
        | None -> acc

    let foldMessage acc msg =
        if Dyn.isNullish msg then acc
        else
            let parts = messageParts msg
            if Dyn.isNullish parts then acc
            else (parts :?> obj array) |> Array.fold foldPart acc

    messages |> Array.fold foldMessage seenByPath

let deduplicateReadOutputsWithSeenByPath
    (seenByPath: Map<string, string list>)
    (messages: obj array)
    : string list * obj array =
    let mutable seenByPath = seenByPath
    let mutable seenOutputs = []
    let mutable anyChanged = false
    let result =
        messages |> Array.map (fun msg ->
            if Dyn.isNullish msg then msg
            else
                let parts = messageParts msg
                if Dyn.isNullish parts then msg
                else
                    let partsArr = parts :?> obj array
                    if Array.isEmpty partsArr then msg
                    else
                        let newParts = ResizeArray<obj>()
                        let mutable partChanged = false
                        for part in partsArr do
                            match tryDecodeReadPart part with
                            | Some rp when rp.partType = "dynamic-tool" && Set.contains rp.toolName readToolNames && rp.state = "output-available" ->
                                let current = readPartOutputKey rp.output
                                if current.Length > 0 then
                                    let pathKey = readPartPath rp
                                    let nextSeen, nextOutput, verdict = dedupForPath seenByPath pathKey current
                                    seenByPath <- nextSeen
                                    if nextOutput = current then newParts.Add(part)
                                    else
                                        newParts.Add(Dyn.withKey part "output" (box nextOutput))
                                        partChanged <- true
                                    match verdict with
                                    | NewContent _ -> seenOutputs <- current :: seenOutputs
                                    | AlreadySeen -> ()
                                else newParts.Add(part)
                            | _ -> newParts.Add(part)
                        if not partChanged then msg
                        else
                            anyChanged <- true
                            Dyn.withKey msg "parts" (box (Array.ofSeq newParts)))
    List.rev seenOutputs, if anyChanged then result else messages

let deduplicateReadOutputsWithSeen
    (seenOutputs: string list)
    (messages: obj array)
    : string list * obj array =
    let seenByPath =
        if List.isEmpty seenOutputs then Map.empty
        else Map.add "" seenOutputs Map.empty
    deduplicateReadOutputsWithSeenByPath seenByPath messages

let deduplicateModelReadOutputsWithSeen
    (previouslySeenOutputs: string list)
    (messages: obj array)
    : string list * obj array =
    let mutable seenByPath =
        if List.isEmpty previouslySeenOutputs then Map.empty
        else Map.add "" previouslySeenOutputs Map.empty
    let mutable seenOutputs = []
    let mutable anyChanged = false
    let result =
        messages |> Array.map (fun msg ->
            if Dyn.isNullish msg then msg
            else
                let content = messageContent msg
                if Dyn.isNullish content then msg
                elif Dyn.typeIs content "string" then msg
                elif not (Dyn.isArray content) then msg
                else
                    let contentArr = content :?> obj array
                    if Array.isEmpty contentArr then msg
                    else
                        let newContent = ResizeArray<obj>()
                        let mutable partChanged = false
                        for part in contentArr do
                            match tryDecodeModelReadPart part with
                            | Some rp when rp.partType = "tool-result" && Set.contains rp.toolName readToolNames ->
                                let current = modelReadPartOutputKey rp
                                if current.Length > 0 then
                                    let pathKey = modelReadPartPath rp
                                    let nextSeen, nextOutput, verdict = dedupForPath seenByPath pathKey current
                                    seenByPath <- nextSeen
                                    if nextOutput = current then newContent.Add(part)
                                    else
                                        let newOutput =
                                            Dyn.withKey
                                                (Dyn.withKey rp.output "type" (box "text"))
                                                "value"
                                                (box nextOutput)
                                        newContent.Add(Dyn.withKey part "output" (box newOutput))
                                        partChanged <- true
                                    match verdict with
                                    | NewContent _ -> seenOutputs <- current :: seenOutputs
                                    | AlreadySeen -> ()
                                else newContent.Add(part)
                            | _ -> newContent.Add(part)
                        if not partChanged then msg
                        else
                            anyChanged <- true
                            Dyn.withKey msg "content" (box (Array.ofSeq newContent)))
    List.rev seenOutputs, if anyChanged then result else messages

let deduplicateReadOutputs (messages: obj array) : obj array =
    deduplicateReadOutputsWithSeen [] messages |> snd

let deduplicateModelReadOutputs (messages: obj array) : obj array =
    deduplicateModelReadOutputsWithSeen [] messages |> snd

let private collectMuxReadOutputsInOrder (messages: obj array) : string list =
    let mutable outputs = []
    for msg in messages do
        if not (Dyn.isNullish msg) then
            let parts = messageParts msg
            if not (Dyn.isNullish parts) then
                let partsArr = parts :?> obj array
                for part in partsArr do
                    match tryDecodeReadPart part with
                    | Some rp when rp.partType = "dynamic-tool" && Set.contains rp.toolName readToolNames && rp.state = "output-available" ->
                        let key = readPartOutputKey rp.output
                        if key.Length > 0 then outputs <- key :: outputs
                    | _ -> ()
    List.rev outputs

let collectReadOutputsByPath (messages: obj array) : Map<string, string list> =
    foldMuxReadPartsIntoSeenByPath Map.empty messages

let collectReadOutputs (messages: obj array) : string list =
    collectMuxReadOutputsInOrder messages
