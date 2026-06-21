module VibeFs.Kernel.MessageDedup

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

type private ReadOp =
    { pathKey: string
      current: string
      apply: string -> obj }

let private dedupForPath (seenByPath: Map<string, string list>) (pathKey: string) (current: string) =
    let pathSeen = Map.tryFind pathKey seenByPath |> Option.defaultValue []
    let verdict, nextState =
        processDedup { seenContents = pathSeen } { path = pathKey; content = current }
    let nextOutput =
        match verdict with
        | AlreadySeen -> dedupMarker
        | NewContent payload -> payload.content
    (Map.add pathKey nextState.seenContents seenByPath, nextOutput, verdict)

let private deduplicateMessages
    (getContainer: obj -> obj)
    (setContainer: obj -> obj array -> obj)
    (classify: obj -> ReadOp option)
    (seenByPath: Map<string, string list>)
    (messages: obj array)
    : string list * obj array =
    let foldPart (seenByPath, outputsRev) part : ((Map<string, string list> * string list) * obj * bool) =
        match classify part with
        | None -> ((seenByPath, outputsRev), part, false)
        | Some op ->
            if op.current.Length = 0 then ((seenByPath, outputsRev), part, false)
            else
                let nextSeen, nextOutput, verdict = dedupForPath seenByPath op.pathKey op.current
                let newPart, changed = if nextOutput = op.current then part, false else op.apply nextOutput, true
                let outputsRev' = match verdict with NewContent _ -> op.current :: outputsRev | AlreadySeen -> outputsRev
                ((nextSeen, outputsRev'), newPart, changed)
    let processMsg (seenByPath, outputsRev, anyChanged) msg : ((Map<string, string list> * string list * bool) * obj) =
        if Dyn.isNullish msg then ((seenByPath, outputsRev, anyChanged), msg)
        else
            let container = getContainer msg
            if Dyn.isNullish container || not (Dyn.isArray container) then ((seenByPath, outputsRev, anyChanged), msg)
            else
                let arr = container :?> obj array
                if Array.isEmpty arr then ((seenByPath, outputsRev, anyChanged), msg)
                else
                    let ((fs, fo), revParts2, partChanged2) =
                        arr
                        |> Array.fold
                            (fun ((s, o), revParts, ch) part ->
                                let ((s', o'), newPart, partCh) = foldPart (s, o) part
                                ((s', o'), newPart :: revParts, ch || partCh))
                            ((seenByPath, outputsRev), [], false)
                    if not partChanged2 then ((fs, fo, anyChanged), msg)
                    else ((fs, fo, true), setContainer msg (List.toArray (List.rev revParts2)))
    let finalState, resultsRev =
        messages
        |> Array.fold
            (fun (state, revMsgs) msg ->
                let nextState, newMsg = processMsg state msg
                (nextState, newMsg :: revMsgs))
            ((seenByPath, [], false), [])
    let (_, outputsRev, anyChanged) = finalState
    List.rev outputsRev, (if anyChanged then List.rev resultsRev |> List.toArray else messages)

let private muxReadOp (part: obj) : ReadOp option =
    match tryDecodeReadPart part with
    | Some rp when rp.partType = "dynamic-tool" && Set.contains rp.toolName readToolNames && rp.state = "output-available" ->
        let current = readPartOutputKey rp.output
        if current.Length = 0 then None
        else Some { pathKey = readPartPath rp; current = current; apply = fun nextOutput -> Dyn.withKey part "output" (box nextOutput) }
    | _ -> None

let private modelReadOp (part: obj) : ReadOp option =
    match tryDecodeModelReadPart part with
    | Some rp when rp.partType = "tool-result" && Set.contains rp.toolName readToolNames ->
        let current = modelReadPartOutputKey rp
        if current.Length = 0 then None
        else
            Some { pathKey = modelReadPartPath rp; current = current;
                   apply = fun nextOutput ->
                       let newOutput = Dyn.withKey (Dyn.withKey rp.output "type" (box "text")) "value" (box nextOutput)
                       Dyn.withKey part "output" (box newOutput) }
    | _ -> None

let private foldMuxReadPartsIntoSeenByPath (seenByPath: Map<string, string list>) (messages: obj array) : Map<string, string list> =
    let foldPart acc part =
        match muxReadOp part with
        | Some op ->
            let nextSeen, _, _ = dedupForPath acc op.pathKey op.current
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
    deduplicateMessages messageParts (fun m arr -> Dyn.withKey m "parts" (box arr)) muxReadOp seenByPath messages

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
    let initialSeenByPath =
        if List.isEmpty previouslySeenOutputs then Map.empty
        else Map.add "" previouslySeenOutputs Map.empty
    deduplicateMessages messageContent (fun m arr -> Dyn.withKey m "content" (box arr)) modelReadOp initialSeenByPath messages

let deduplicateReadOutputs (messages: obj array) : obj array =
    deduplicateReadOutputsWithSeen [] messages |> snd

let deduplicateModelReadOutputs (messages: obj array) : obj array =
    deduplicateModelReadOutputsWithSeen [] messages |> snd

let private collectMuxReadOutputsInOrder (messages: obj array) : string list =
    messages
    |> Seq.collect (fun msg -> if Dyn.isNullish msg then Seq.empty else let p = messageParts msg in if Dyn.isNullish p then Seq.empty else (p :?> obj array) :> seq<_>)
    |> Seq.choose (fun part -> match muxReadOp part with Some op -> Some op.current | None -> None)
    |> List.ofSeq

let collectReadOutputsByPath (messages: obj array) : Map<string, string list> =
    foldMuxReadPartsIntoSeenByPath Map.empty messages

let collectReadOutputs (messages: obj array) : string list =
    collectMuxReadOutputsInOrder messages