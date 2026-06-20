module VibeFs.Kernel.Dedup

/// Marker inserted when an output merely repeats something already shown.
let dedupMarker = "[No Change Since Previous Read/Write]"

type DedupedOutput = { output: string; seenOutputs: string list }

type ReadPayload = { path: string; content: string }

type DedupState = { seenContents: string list }

let createDedupState () : DedupState = { seenContents = [] }

type DedupVerdict =
    | AlreadySeen
    | NewContent of ReadPayload

/// If `output` was already seen verbatim or is a substring of a previously-seen
/// output, replace with the marker; otherwise record it.  Pure: the seen list
/// grows immutably.
let deduplicate (seenOutputs: string list) (output: string) : DedupedOutput =
    if output.Length > 0 && List.exists (fun (seen: string) -> seen.Contains output) seenOutputs then
        { output = dedupMarker; seenOutputs = seenOutputs }
    else
        { output = output; seenOutputs = seenOutputs @ [ output ] }

let processDedup (state: DedupState) (payload: ReadPayload) : DedupVerdict * DedupState =
    let result = deduplicate state.seenContents payload.content
    if result.output = dedupMarker then
        AlreadySeen, state
    else
        NewContent payload, { state with seenContents = result.seenOutputs }
