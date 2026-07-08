module Wanxiangshu.Kernel.ReviewReplayPolicy

open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.LoopMessages

let textsFromFlatParts (flat: FlatPart<'raw> seq) : string seq =
    flat
    |> Seq.map (fun fp ->
        match fp.part with
        | TextPart text -> text
        | ToolPart(_, _, Some state, _) -> state.output
        | _ -> "")

let reviewTaskFromTexts = inferReviewTaskFromTexts
