module Wanxiangshu.Kernel.ReviewPrompts.OmpVariant

open Wanxiangshu.Kernel.ReviewPrompts.Instructions

let reviewerNudgePrompt =
    "Submit your review verdict now via return_reviewer:\n"
    + "  return_reviewer({ \"verdict\": \"PERFECT\" })                          // Accept\n"
    + "  return_reviewer({ \"verdict\": \"REVISE\", \"feedback\": \"details...\" }) // Request revision\n\n"
    + "verdict MUST be exactly \"PERFECT\" or \"REVISE\". Do not explain what you plan to do — call the tool immediately."

let reviewInstructionsOmp = reviewInstructionsProse

let reviewerNudgePromptOmp = reviewerNudgePrompt

let buildOmpReviewInitialPrompt (report: string) (affectedFiles: string list) (task: string option) : string =
    let affectedSection =
        if affectedFiles.IsEmpty then
            ""
        else
            "=== Affected Files ===\n"
            + (affectedFiles |> String.concat "\n")
            + "\n\n"

    let taskSection =
        match task with
        | Some t when not (System.String.IsNullOrWhiteSpace t) ->
            "=== Original Task ===\n" + t + "\n\n"
        | _ -> ""

    let reportSection =
        if System.String.IsNullOrEmpty report then
            ""
        else
            "=== Change Report ===\n" + report + "\n\n"

    reviewInstructionsProse + "\n\n" + reportSection + affectedSection + taskSection
