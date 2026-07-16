module Wanxiangshu.Runtime.ReviewPrompts.Format

open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Runtime.PromptFrontMatter

let submitReviewIsWip (wip: bool option) : bool = defaultArg wip true

let submitReviewWipAcknowledgment: string =
    Wanxiangshu.Kernel.Nudge.NudgePromptText.submitReviewWipAcknowledgment

let formatWipAcknowledgment (task: string) : string =
    frontMatterPrompt [ yamlField "task" task ] submitReviewWipAcknowledgment

let formatReviewResult (result: ReviewResult) : string =
    match result with
    | ReviewResult.Accepted feedback ->
        let trimmed = (if isNull feedback then "" else feedback).Trim()

        let body =
            if trimmed = "" then
                "Review passed. Your changes have been accepted. With-Review Mode has ended."
            else
                "Review passed with the following feedback:\n\n"
                + trimmed
                + "\n\nYour changes have been accepted. With-Review Mode has ended."

        frontMatterPrompt [ yamlField verdictField verdictAccepted ] body
    | ReviewResult.Terminated ->
        frontMatterPrompt
            [ yamlField verdictField verdictTerminated ]
            "Review terminated without verdict. With-Review Mode is still active; fix the issues and call submit_review again."
    | ReviewResult.NeedsRevision feedback ->
        frontMatterPrompt
            [ yamlField verdictField verdictNeedsRevision; yamlField "feedback" feedback ]
            "Address the feedback above. With-Review Mode is still active — fix the issues and call submit_review again."
