module VibeFs.Kernel.ReviewPrompts.OmpVariant

open VibeFs.Kernel.ReviewPrompts.Instructions

let reviewerNudgePrompt =
    "Submit your review verdict now via return_reviewer:\n"
    + "  return_reviewer({ \"verdict\": \"PASS\" })                          // Accept\n"
    + "  return_reviewer({ \"verdict\": \"REJECT\", \"feedback\": \"details...\" }) // Reject\n\n"
    + "verdict MUST be exactly \"PASS\" or \"REJECT\". Do not explain what you plan to do — call the tool immediately."

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
