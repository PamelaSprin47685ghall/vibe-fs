module VibeFs.Mux.Dedup

open VibeFs.Kernel.Dedup
open VibeFs.Kernel

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

/// Pure: fold `deduplicate` over read outputs already collected from the
/// conversation history, returning the final `seenOutputs` and any replacements
/// in the provided messages.
let deduplicateReadOutputsWithSeen
    (seenOutputs: string list)
    (messages: obj array)
    : string list * obj array =
    let mutable seen = seenOutputs
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
                                let result = deduplicate seen current
                                seen <- result.seenOutputs
                                if result.output = current then newParts.Add(part)
                                else
                                    newParts.Add(Dyn.withKey part "output" (box result.output))
                                    partChanged <- true
                            else newParts.Add(part)
                        if not partChanged then msg
                        else
                            anyChanged <- true
                            Dyn.withKey msg "parts" (box (Array.ofSeq newParts)))
    seen, if anyChanged then result else messages

/// Backwards-compatible one-shot deduplication: only compares read outputs
/// against each other within the supplied messages.
let deduplicateReadOutputs (messages: obj array) : obj array =
    deduplicateReadOutputsWithSeen [] messages |> snd

/// Collect read outputs from an arbitrary message array, treating earlier
/// messages as already seen.  Useful when the caller has access to the full
/// conversation history and wants to seed the deduper before folding in new
/// messages.
let collectReadOutputs (messages: obj array) : string list =
    messages
    |> Array.collect (fun msg ->
        if Dyn.isNullish msg then [||]
        else
            let parts = Dyn.get msg "parts"
            if Dyn.isNullish parts then [||]
            else
                let partsArr = parts :?> obj array
                partsArr
                |> Array.choose (fun part ->
                    let ty = Dyn.str part "type"
                    let toolName = Dyn.str part "toolName"
                    let state = Dyn.str part "state"
                    let output = Dyn.get part "output"
                    let key = extractReadOutputKey output
                    if ty = "dynamic-tool" && Set.contains toolName readToolNames && state = "output-available"
                       && key.Length > 0 then
                        Some key
                    else None))
    |> Array.toList
