[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Wanxiangshu.Runtime.ReviewPrompts

let reviewInstructions =
    Wanxiangshu.Runtime.ReviewPrompts.Instructions.reviewInstructions

let agentReportReviewInstructions =
    Wanxiangshu.Runtime.ReviewPrompts.Instructions.agentReportReviewInstructions

let muxReviewerAgentReportDescription =
    Wanxiangshu.Runtime.ReviewPrompts.Instructions.muxReviewerAgentReportDescription

let doubleCheckChallenge =
    Wanxiangshu.Runtime.ReviewPrompts.Submission.doubleCheckChallenge

let doubleCheckPrompt =
    Wanxiangshu.Runtime.ReviewPrompts.Submission.doubleCheckPrompt

let reviewerPrompt = Wanxiangshu.Runtime.ReviewPrompts.Submission.reviewerPrompt

let reviewSubmissionVerdictPrompt =
    Wanxiangshu.Runtime.ReviewPrompts.Submission.reviewSubmissionVerdictPrompt

let reviewSubmissionDoubleCheckPrompt =
    Wanxiangshu.Runtime.ReviewPrompts.Submission.reviewSubmissionDoubleCheckPrompt

let withReviewCommandTemplate =
    Wanxiangshu.Runtime.ReviewPrompts.Commands.withReviewCommandTemplate

let reviewerNudgePrompt =
    Wanxiangshu.Runtime.ReviewPrompts.OmpVariant.reviewerNudgePrompt

let reviewInstructionsOmp =
    Wanxiangshu.Runtime.ReviewPrompts.OmpVariant.reviewInstructionsOmp

let reviewerNudgePromptOmp =
    Wanxiangshu.Runtime.ReviewPrompts.OmpVariant.reviewerNudgePromptOmp

let buildOmpReviewInitialPrompt =
    Wanxiangshu.Runtime.ReviewPrompts.OmpVariant.buildOmpReviewInitialPrompt

let submitReviewIsWip = Wanxiangshu.Runtime.ReviewPrompts.Format.submitReviewIsWip

let formatWipAcknowledgment =
    Wanxiangshu.Runtime.ReviewPrompts.Format.formatWipAcknowledgment

/// Structured WIP acknowledgment (same as formatWipAcknowledgment with empty task objective).
let submitReviewWipAcknowledgment =
    Wanxiangshu.Runtime.ReviewPrompts.Format.formatWipAcknowledgment "Progress recorded"

let formatReviewResult = Wanxiangshu.Runtime.ReviewPrompts.Format.formatReviewResult

let returnReviewerVerdictSubmittedMessage =
    Wanxiangshu.Runtime.ReviewPrompts.Submission.returnReviewerVerdictSubmittedMessage

module ReviewerVerdictPrompts = Wanxiangshu.Runtime.ReviewPrompts.Instructions.ReviewerVerdictPrompts
