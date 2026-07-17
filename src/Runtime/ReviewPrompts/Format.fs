module Wanxiangshu.Runtime.ReviewPrompts.Format

open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.Review.ReviewEncouragement
open Wanxiangshu.Runtime.PromptFrontMatter

/// Backward-compatible alias for the WIP acknowledgment body.
let submitReviewWipAcknowledgment: string = wipAcknowledgment

let submitReviewIsWip (wip: bool option) : bool = defaultArg wip true

/// Structured YAML anchor for WIP acknowledgment detection.
/// Used by isSubmitReviewWipProgressOutput instead of scanning prose.
let wipAcknowledgmentAnchor = "review_progress"

/// Value of the anchor when a WIP was recorded.
let wipAcknowledgmentRecorded = "recorded"

let formatWipAcknowledgment (task: string) : string =
    frontMatterPrompt
        [ yamlField "task" task
          yamlField wipAcknowledgmentAnchor wipAcknowledgmentRecorded ]
        wipAcknowledgment

let formatReviewResult (result: ReviewResult) : string =
    match result with
    | ReviewResult.Accepted feedback ->
        let trimmed = (if isNull feedback then "" else feedback).Trim()

        let body =
            if trimmed = "" then
                acceptedVerdict
            else
                acceptedVerdict + "\n\n" + trimmed

        frontMatterPrompt [ yamlField verdictField verdictAccepted ] body
    | ReviewResult.Terminated -> frontMatterPrompt [ yamlField verdictField verdictTerminated ] terminatedVerdict
    | ReviewResult.NeedsRevision feedback ->
        frontMatterPrompt
            [ yamlField verdictField verdictNeedsRevision; yamlField "feedback" feedback ]
            needsRevisionVerdict
