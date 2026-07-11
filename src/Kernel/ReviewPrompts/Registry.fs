module Wanxiangshu.Kernel.ReviewPrompts

let reviewInstructionsProse =
    Wanxiangshu.Kernel.ReviewPrompts.Instructions.reviewInstructionsProse

let reviewInstructions =
    Wanxiangshu.Kernel.ReviewPrompts.Instructions.reviewInstructions

let agentReportReviewInstructions =
    Wanxiangshu.Kernel.ReviewPrompts.Instructions.agentReportReviewInstructions

let muxReviewerAgentReportDescription =
    Wanxiangshu.Kernel.ReviewPrompts.Instructions.muxReviewerAgentReportDescription

let doubleCheckChallenge =
    Wanxiangshu.Kernel.ReviewPrompts.Submission.doubleCheckChallenge

let doubleCheckPrompt =
    Wanxiangshu.Kernel.ReviewPrompts.Submission.doubleCheckPrompt

let reviewerPrompt = Wanxiangshu.Kernel.ReviewPrompts.Submission.reviewerPrompt

let reviewSubmissionVerdictPrompt =
    Wanxiangshu.Kernel.ReviewPrompts.Submission.reviewSubmissionVerdictPrompt

let reviewSubmissionDoubleCheckPrompt =
    Wanxiangshu.Kernel.ReviewPrompts.Submission.reviewSubmissionDoubleCheckPrompt

let preReviewVerdictPrompt =
    Wanxiangshu.Kernel.ReviewPrompts.Submission.preReviewVerdictPrompt

let withReviewCommandTemplate =
    Wanxiangshu.Kernel.ReviewPrompts.Commands.withReviewCommandTemplate

let withReviewPrecheckCommandTemplate =
    Wanxiangshu.Kernel.ReviewPrompts.Commands.withReviewPrecheckCommandTemplate

let reviewerNudgePrompt =
    Wanxiangshu.Kernel.ReviewPrompts.OmpVariant.reviewerNudgePrompt

let reviewInstructionsOmp =
    Wanxiangshu.Kernel.ReviewPrompts.OmpVariant.reviewInstructionsOmp

let reviewerNudgePromptOmp =
    Wanxiangshu.Kernel.ReviewPrompts.OmpVariant.reviewerNudgePromptOmp

let buildOmpReviewInitialPrompt =
    Wanxiangshu.Kernel.ReviewPrompts.OmpVariant.buildOmpReviewInitialPrompt

let submitReviewIsWip = Wanxiangshu.Kernel.ReviewPrompts.Format.submitReviewIsWip

let submitReviewWipAcknowledgment =
    Wanxiangshu.Kernel.ReviewPrompts.Format.submitReviewWipAcknowledgment

let formatWipAcknowledgment =
    Wanxiangshu.Kernel.ReviewPrompts.Format.formatWipAcknowledgment

let formatReviewResult = Wanxiangshu.Kernel.ReviewPrompts.Format.formatReviewResult

let returnReviewerVerdictSubmittedMessage =
    Wanxiangshu.Kernel.ReviewPrompts.Submission.returnReviewerVerdictSubmittedMessage

module ReviewerVerdictPrompts = Wanxiangshu.Kernel.ReviewPrompts.Instructions.ReviewerVerdictPrompts
