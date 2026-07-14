module Wanxiangshu.Kernel.ReviewReplayPolicy

open Wanxiangshu.Kernel.Messaging

let textsFromFlatParts (flat: FlatPart<'raw> seq) : string seq =
    flat
    |> Seq.map (fun fp ->
        match fp.part with
        | TextPart text -> text
        | ToolPart(_, _, Some state, _) -> state.output
        | _ -> "")
