module Wanxiangshu.Kernel.Nudge.NudgePromptText

/// Shared prose anchors used by Runtime prompt builders and pure Kernel detectors.
let todoNudgePromptProse =
    "There are still incomplete todos. Continue working through the remaining items. "
    + "If they are irrelevant, remove them. "
    + "If you want to skip this check, respond with <skip-todo-check />"

let loopNudgePromptProse =
    "You are in loop mode. You must call the submit_review tool to\n"
    + "submit your detailed report and list of modified files for review\n"
    + "before finishing. Do not end the conversation without calling submit_review.\n"
    + "If you want to skip this review check, respond with <skip-review-check />."

let submitReviewWipAcknowledgment =
    "Your progress report was recorded. With-Review Mode is still active — continue working until the task is fully complete, then call submit_review again."
