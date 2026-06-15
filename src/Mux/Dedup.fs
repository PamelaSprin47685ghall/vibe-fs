module VibeFs.Mux.Dedup

open VibeFs.Kernel.Dedup
open VibeFs.Kernel
open VibeFs.Kernel.TreeSitterKernel

/// Tool names that represent a file-read operation across hosts.  The OpenCode
/// host names the tool `read`; the Mux host names it `file_read`.
let readToolNames = Set [ "read"; "file_read" ]

/// Extract the dedup key from a read-tool output.  Mux's `file_read` returns an
/// object `{ success, content, ... }`; OpenCode's `read` returns a string.
/// Returns "" when no usable key can be extracted.
let private extractReadOutputKey (output: obj) : string =
    if Dyn.isNullish output then ""
    elif Dyn.typeIs output "string" then string output
    else
        let content = Dyn.get output "content"
        if Dyn.isNullish content then "" else string content

/// Read path from a dynamic-tool part (`input`) or OpenCode tool part (`state.input`).
let private extractReadPathFromPart (part: obj) : string =
    let fromInput (input: obj) =
        match extractFilePaths input with
        | path :: _ -> path
        | [] -> ""
    let direct = Dyn.get part "input"
    if not (Dyn.isNullish direct) then fromInput direct
    else
        let state = Dyn.get part "state"
        if Dyn.isNullish state then ""
        else fromInput (Dyn.get state "input")

/// Flatten per-path seen lists into the legacy global list (order: paths then outputs per path).
let private flattenSeenByPath (seenByPath: Map<string, string list>) : string list =
    seenByPath |> Map.toList |> List.collect (fun (_, outputs) -> outputs)

/// Seed per-path state from a flat legacy `seenOutputs` list (content-only history).
let private seedSeenByPath (seenOutputs: string list) : Map<string, string list> =
    if List.isEmpty seenOutputs then Map.empty
    else Map.add "" seenOutputs Map.empty

/// Dedup within one path scope; returns updated map entry for that path.
/// Falls back to the legacy "" bucket so old flat seen lists still affect path-scoped reads.
let private dedupForPath (seenByPath: Map<string, string list>) (pathKey: string) (current: string) =
    let pathSeen = Map.tryFind pathKey seenByPath |> Option.defaultValue []
    let legacySeen = if pathKey = "" then [] else Map.tryFind "" seenByPath |> Option.defaultValue []
    let combinedSeen = pathSeen @ legacySeen
    let result = deduplicate combinedSeen current
    let nextPathSeen = if result.output = current then pathSeen @ [ current ] else pathSeen
    (Map.add pathKey nextPathSeen seenByPath, result.output)

/// Fold read-output keys from Mux dynamic-tool parts into per-path seen state (message order).
let private foldMuxReadPartsIntoSeenByPath (seenByPath: Map<string, string list>) (messages: obj array) : Map<string, string list> =
    let mutable acc = seenByPath
    for msg in messages do
        if not (Dyn.isNullish msg) then
            let parts = Dyn.get msg "parts"
            if not (Dyn.isNullish parts) then
                let partsArr = parts :?> obj array
                for part in partsArr do
                    let ty = Dyn.str part "type"
                    let toolName = Dyn.str part "toolName"
                    let state = Dyn.str part "state"
                    let output = Dyn.get part "output"
                    let key = extractReadOutputKey output
                    if ty = "dynamic-tool" && Set.contains toolName readToolNames && state = "output-available"
                       && key.Length > 0 then
                        let pathKey = extractReadPathFromPart part
                        let nextSeen, _ = dedupForPath acc pathKey key
                        acc <- nextSeen
    acc

/// Pure: fold read-output dedup over messages, scoped by file path when known.
/// When path is missing, uses "" so behavior matches legacy content-only dedup.
let deduplicateReadOutputsWithSeenByPath
    (seenByPath: Map<string, string list>)
    (messages: obj array)
    : string list * obj array =
    let mutable seenByPath = seenByPath
    let mutable anyChanged = false
    let result =
        messages |> Array.map (fun msg ->
            if Dyn.isNullish msg then msg
            else
                let parts = Dyn.get msg "parts"
                if Dyn.isNullish parts then msg
                else
                    let partsArr = parts :?> obj array
                    if Array.isEmpty partsArr then msg
                    else
                        let newParts = ResizeArray<obj>()
                        let mutable partChanged = false
                        for part in partsArr do
                            let ty = Dyn.str part "type"
                            let toolName = Dyn.str part "toolName"
                            let state = Dyn.str part "state"
                            let output = Dyn.get part "output"
                            let current = extractReadOutputKey output
                            if ty = "dynamic-tool" && Set.contains toolName readToolNames && state = "output-available"
                               && current.Length > 0 then
                                let pathKey = extractReadPathFromPart part
                                let nextSeen, nextOutput = dedupForPath seenByPath pathKey current
                                seenByPath <- nextSeen
                                if nextOutput = current then newParts.Add(part)
                                else
                                    newParts.Add(Dyn.withKey part "output" (box nextOutput))
                                    partChanged <- true
                            else newParts.Add(part)
                        if not partChanged then msg
                        else
                            anyChanged <- true
                            Dyn.withKey msg "parts" (box (Array.ofSeq newParts)))
    flattenSeenByPath seenByPath, if anyChanged then result else messages

let deduplicateReadOutputsWithSeen
    (seenOutputs: string list)
    (messages: obj array)
    : string list * obj array =
    deduplicateReadOutputsWithSeenByPath (seedSeenByPath seenOutputs) messages

/// Extract the dedup key from a tool-result part in the Vercel AI SDK
/// ModelMessage shape.  Text outputs keep their value; JSON outputs are
/// delegated to the Mux-message extractor so file_read `{ content }` objects
/// are handled without duplicating the parsing rule.
let private extractModelReadOutputKey (part: obj) : string =
    let output = Dyn.get part "output"
    if Dyn.isNullish output then ""
    else
        let outputType = Dyn.str output "type"
        let value = Dyn.get output "value"
        if outputType = "text" && not (Dyn.isNullish value) then string value
        elif outputType = "json" && not (Dyn.isNullish value) then extractReadOutputKey value
        else ""

/// Path for AI SDK tool-result parts when `input` is present on the part.
let private extractModelReadPath (part: obj) : string =
    match extractFilePaths (Dyn.get part "input") with
    | path :: _ -> path
    | [] -> ""

/// Pure: fold `deduplicate` over read tool-result parts inside ModelMessage
/// arrays, returning the final `seenOutputs` and any replacements in the
/// provided messages.  This handles the AI SDK `ToolResultPart` shape where the
/// useful payload lives at `output.value` rather than directly on `output`.
let deduplicateModelReadOutputsWithSeen
    (seenOutputs: string list)
    (messages: obj array)
    : string list * obj array =
    let mutable seenByPath = seedSeenByPath seenOutputs
    let mutable anyChanged = false
    let result =
        messages |> Array.map (fun msg ->
            if Dyn.isNullish msg then msg
            else
                let content = Dyn.get msg "content"
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
                            let ty = Dyn.str part "type"
                            let toolName = Dyn.str part "toolName"
                            let current = extractModelReadOutputKey part
                            if ty = "tool-result" && Set.contains toolName readToolNames && current.Length > 0 then
                                let pathKey = extractModelReadPath part
                                let nextSeen, nextOutput = dedupForPath seenByPath pathKey current
                                seenByPath <- nextSeen
                                if nextOutput = current then newContent.Add(part)
                                else
                                    let newOutput =
                                        Dyn.withKey
                                            (Dyn.withKey (Dyn.get part "output") "type" (box "text"))
                                            "value"
                                            (box nextOutput)
                                    newContent.Add(Dyn.withKey part "output" (box newOutput))
                                    partChanged <- true
                            else newContent.Add(part)
                        if not partChanged then msg
                        else
                            anyChanged <- true
                            Dyn.withKey msg "content" (box (Array.ofSeq newContent)))
    flattenSeenByPath seenByPath, if anyChanged then result else messages

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
            let parts = Dyn.get msg "parts"
            if not (Dyn.isNullish parts) then
                let partsArr = parts :?> obj array
                for part in partsArr do
                    let ty = Dyn.str part "type"
                    let toolName = Dyn.str part "toolName"
                    let state = Dyn.str part "state"
                    let output = Dyn.get part "output"
                    let key = extractReadOutputKey output
                    if ty = "dynamic-tool" && Set.contains toolName readToolNames && state = "output-available"
                       && key.Length > 0 then
                        outputs <- key :: outputs
    List.rev outputs

/// Collect per-path read-output seen state from history (Mux dynamic-tool shape).
/// Used to seed `deduplicateReadOutputsWithSeenByPath` before folding the active window.
let collectReadOutputsByPath (messages: obj array) : Map<string, string list> =
    foldMuxReadPartsIntoSeenByPath Map.empty messages

/// Collect read outputs from an arbitrary message array as a flat list (legacy API).
/// Prefer `collectReadOutputsByPath` when history includes path-scoped reads.
let collectReadOutputs (messages: obj array) : string list =
    collectMuxReadOutputsInOrder messages
