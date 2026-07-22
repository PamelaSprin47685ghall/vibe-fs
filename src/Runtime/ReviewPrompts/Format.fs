module Wanxiangshu.Runtime.ReviewPrompts.Format

open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.Review.ReviewEncouragement

/// Backward-compatible alias for the WIP acknowledgment body.
let submitReviewWipAcknowledgment: string = wipAcknowledgment

let submitReviewIsWip (wip: bool option) : bool = defaultArg wip true

/// Structured anchor for WIP acknowledgment detection.
let wipAcknowledgmentAnchor = "review_progress"

/// Value of the anchor when a WIP was recorded.
let wipAcknowledgmentRecorded = "recorded"

let formatWipAcknowledgment (task: string) : string =
    let docView =
        { objective = task
          background = Some wipAcknowledgment
          agentRole = AgentRole.CodeReview
          targets = []
          boundaries = []
          rules = [ PromptRule.Policy "Continue working carefully on the task." ]
          outcomes =
            [ { label = wipAcknowledgmentAnchor
                text = wipAcknowledgmentRecorded } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create formatWipAcknowledgment doc: %A" errs

let formatReviewResult (result: ReviewResult) : string =
    let docView =
        match result with
        | ReviewResult.Accepted feedback ->
            let trimmed = (if isNull feedback then "" else feedback).Trim()

            let body =
                if trimmed = "" then
                    acceptedVerdict
                else
                    acceptedVerdict + "\n\n" + trimmed

            { objective = "Review verdict: accepted."
              background = Some body
              agentRole = AgentRole.CodeReview
              targets = []
              boundaries = []
              rules = []
              outcomes = [ { label = "verdict"; text = "accepted" } ] }
        | ReviewResult.Terminated ->
            { objective = "Review verdict: terminated."
              background = Some terminatedVerdict
              agentRole = AgentRole.CodeReview
              targets = []
              boundaries = []
              rules = []
              outcomes =
                [ { label = "verdict"
                    text = "terminated" } ] }
        | ReviewResult.NeedsRevision feedback ->
            let trimmed = (if isNull feedback then "" else feedback).Trim()

            let body =
                if trimmed = "" then
                    needsRevisionVerdict
                else
                    needsRevisionVerdict + "\n\n" + trimmed

            { objective = "Review verdict: needs revision."
              background = Some body
              agentRole = AgentRole.CodeReview
              targets = []
              boundaries = []
              rules = []
              outcomes =
                [ { label = "verdict"
                    text = "needs_revision" } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create formatReviewResult doc: %A" errs
