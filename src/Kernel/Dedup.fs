module VibeFs.Kernel.Dedup

/// Marker inserted when an output merely repeats something already shown.
let dedupMarker = "[No Change Since Previous Read/Write]"

type DedupedOutput = { output: string; seenOutputs: string list }

/// If `output` matches a previously-seen output exactly, or contains a
/// previously-seen output as a non-trivial substring, replace it with the
/// marker; otherwise record it.  Pure: the seen list grows immutably and the
/// caller owns the thread of state.
let deduplicate (seenOutputs: string list) (output: string) : DedupedOutput =
    if output.Length > 0 && List.contains output seenOutputs then
        { output = dedupMarker; seenOutputs = seenOutputs }
    else
        let isRepeat (seen: string) =
            seen.Length > 0 && output.Contains(seen)
            && output.Length - seen.Length > dedupMarker.Length
        match List.tryFind isRepeat seenOutputs with
        | Some _ -> { output = dedupMarker; seenOutputs = seenOutputs }
        | None -> { output = output; seenOutputs = seenOutputs @ [ output ] }
