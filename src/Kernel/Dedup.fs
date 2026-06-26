module Wanxiangshu.Kernel.Dedup

open Wanxiangshu.Kernel.ToolOutputInfo

type DedupedOutput = { output: string; seenOutputs: string list }

let isNoChangeOutput (output: string) : bool =
    match tryParse output with
    | Some msg ->
        msg.info
        |> List.exists (function
            | InfoItem.BodyRef ToolOutputBodyRef.NoChangeSincePreviousReadWrite -> true
            | _ -> false)
    | None -> false

/// If `output` was already seen verbatim or is a substring of a previously-seen
/// output, replace with the no-change envelope; otherwise record it.
let deduplicate (seenOutputs: string list) (output: string) : DedupedOutput =
    if output.Length > 0 && List.exists (fun (seen: string) -> seen.Contains output) seenOutputs then
        { output = noChangeEnvelope (); seenOutputs = seenOutputs }
    else
        { output = output; seenOutputs = seenOutputs @ [ output ] }
