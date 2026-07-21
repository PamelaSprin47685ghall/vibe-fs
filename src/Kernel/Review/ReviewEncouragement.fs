module Wanxiangshu.Kernel.Review.ReviewEncouragement

/// Shared encouragement text used in activation, nudge, and WIP acknowledgment.
/// Single source of truth — all hosts read from here, never hardcode.
/// Core sustained-work message embedded in activation prompts.
let sustainedWork: string =
    "Treat this as a long-horizon task. You have ample context and time. "
    + "You have already made progress, or you are about to. Work carefully, "
    + "continue for as long as the task requires, and never trade completeness "
    + "for speed. Thorough, sustained work is supported."

/// Instruction to continue working, used in WIP ack.
let updateTodoAndContinue: string =
    "Continue working carefully. Do not stop or wait after this acknowledgment."

/// Todo nudge — reminds the model that work remains.
let todoNudge: string =
    "You are making progress. There are still incomplete todos. "
    + "Take the time you need and continue one concrete "
    + "step at a time. Remove an item only when it is genuinely irrelevant."

/// Loop nudge — reminds the model that review mode is active.
let loopNudge: string =
    "Your work is worth completing carefully. With-Review Mode is still "
    + "active. Continue for as long as necessary, then call submit_review "
    + "with a detailed report and the complete affected-file list."

/// Activation message body — the sustained work encouragement.
let activationBody: string =
    "You are entering With-Review Mode.\n\n"
    + "Treat this as a long-horizon task. You have ample context and time.\n"
    + "You have already made progress, or you are about to. Work carefully\n"
    + "for as long as the task requires. Do not reduce scope merely to finish\n"
    + "quickly. Thorough, sustained work is supported.\n\n"
    + "Complete every requirement recorded in the task front matter."

/// WIP acknowledgment — informs the model that the progress report was saved
/// and instructs it to continue.
let wipAcknowledgment: string =
    "Your progress report was saved successfully. With-Review Mode remains active.\n\n"
    + "You have made real progress. Treat context and time as abundant for this task,\n"
    + "and continue carefully for as long as completion requires.\n\n"
    + "Continue working carefully. Do not stop or wait after this acknowledgment.\n\n"
    + "When every requirement is complete, call submit_review again with wip=false\n"
    + "and include the complete affected-file list."

/// Accepted verdict message.
let acceptedVerdict: string =
    "Review passed. Your careful work has been accepted.\n"
    + "With-Review Mode has ended. Preserve any useful lessons in permanent\n"
    + "tests or project documentation before moving on."

/// Needs-revision verdict message.
let needsRevisionVerdict: string =
    "The reviewer has requested revisions. This feedback is guidance, not failure.\n"
    + "You have already made progress. Preserve what is correct, address every\n"
    + "feedback item carefully, and continue for as long as needed.\n"
    + "With-Review Mode remains active."

/// Terminated verdict message.
let terminatedVerdict: string =
    "The review ended without a verdict, but your work is not discarded.\n"
    + "With-Review Mode remains active. Verify the current state,\n"
    + "resolve any blocker, and submit again when ready."
