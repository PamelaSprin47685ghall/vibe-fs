module VibeFs.Mux.Dedup

open VibeFs.Kernel.Dedup
open VibeFs.Kernel
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Mux.PartDecoder

/// Tool names that represent a file-read operation across hosts.  The OpenCode
/// host names the tool `read`; the Mux host names it `file_read`.
let readToolNames = Set [ "read"; "file_read" ]

let private classifyMuxReadPart (part: obj) =
    match tryDecodeReadPart part with
    | Some rp when rp.partType = "dynamic-tool" && Set.contains rp.toolName readToolNames && rp.state = "output-available" ->
        let key = readPartOutputKey rp.output
        if key.Length > 0 then Some(readPartPath rp, key) else None
    | _ -> None

/// Dedup within one path scope; returns updated map entry for that path.
let private dedupForPath (seenByPath: Map<string, string list>) (pathKey: string) (current: string) =
    let pathSeen = Map.tryFind pathKey seenByPath |> Option.defaultValue []
    let verdict, nextState =
        processDedup { seenContents = pathSeen } { path = pathKey; content = current }
    let nextOutput =
        match verdict with
        | AlreadySeen -> dedupMarker
        | NewContent payload -> payload.content
    (Map.add pathKey nextState.seenContents seenByPath, nextOutput, verdict)

/// Fold read-output keys from Mux dynamic-tool parts into per-path seen state (message order).
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

/// Pure: fold read-output dedup over messages, scoped by file path when known.
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

/// Pure: fold `deduplicate` over read tool-result parts inside ModelMessage
/// arrays, returning the final `seenOutputs` and any replacements in the
/// provided messages.  This handles the AI SDK `ToolResultPart` shape where the
/// useful payload lives at `output.value` rather than directly on `output`.
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

/// Backwards-compatible one-shot deduplication: only compares read outputs
/// against each other within the supplied messages.
let deduplicateReadOutputs (messages: obj array) : obj array =
    deduplicateReadOutputsWithSeen [] messages |> snd

/// One-shot deduplication for AI SDK ModelMessage arrays.
let deduplicateModelReadOutputs (messages: obj array) : obj array =
    deduplicateModelReadOutputsWithSeen [] messages |> snd

/// Extract read-output keys from Mux dynamic-tool parts in message order.
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

/// Collect per-path read-output seen state from history (Mux dynamic-tool shape).
/// Used to seed `deduplicateReadOutputsWithSeenByPath` before folding the active window.
let collectReadOutputsByPath (messages: obj array) : Map<string, string list> =
    foldMuxReadPartsIntoSeenByPath Map.empty messages

/// Collect read outputs from an arbitrary message array as a flat list (legacy API).
/// Prefer `collectReadOutputsByPath` when history includes path-scoped reads.
let collectReadOutputs (messages: obj array) : string list =
    collectMuxReadOutputsInOrder messages
