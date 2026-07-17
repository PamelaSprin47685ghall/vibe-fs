module Wanxiangshu.Kernel.Nudge.SubmitReviewHooks

open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.Nudge.NudgePromptText

/// Parse YAML front-matter block from text and extract scalars.
/// Only handles simple `key: value` pairs in a block delimited by `---`.
let private parseFrontMatterScalars (text: string) : Map<string, string> =
    let trimmed = if isNull text then "" else text.Trim()

    if not (trimmed.StartsWith "---") then
        Map.empty
    else
        let afterFirst = (trimmed.Substring 3).TrimStart()

        let endIdx =
            let idx = afterFirst.IndexOf "---"
            if idx < 0 then afterFirst.Length else idx

        let block = afterFirst.Substring(0, endIdx)

        let mutable result = Map.empty
        let lines = block.Split('\n')

        for line in lines do
            let colonIdx = line.IndexOf ':'

            if colonIdx > 0 then
                let key = line.Substring(0, colonIdx).Trim()
                let value = line.Substring(colonIdx + 1).Trim()
                result <- Map.add key value result

        result

/// The YAML key used to mark WIP acknowledgment.
/// Must match the corresponding field in the Runtime prompt builder.
let private wipAcknowledgmentAnchor = "review_progress"

/// The expected value for WIP acknowledgment.
/// Must match the corresponding field in the Runtime prompt builder.
let private wipAcknowledgmentRecorded = "recorded"

let isSubmitReviewWipProgressOutput (text: string) : bool =
    if isNull text then
        false
    else
        let scalars = parseFrontMatterScalars text
        Map.tryFind wipAcknowledgmentAnchor scalars = Some wipAcknowledgmentRecorded

let isSubmitReviewToolName (name: string) : bool =
    name.Trim().Equals("submit_review", System.StringComparison.OrdinalIgnoreCase)

let submitReviewWipToolClearsNudgeDedup (toolName: string) (outputText: string) : bool =
    isSubmitReviewToolName toolName && isSubmitReviewWipProgressOutput outputText
