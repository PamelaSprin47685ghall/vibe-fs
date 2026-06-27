module Wanxiangshu.Kernel.ReviewPrompts.Instructions

open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Kernel.PromptFragments

let reviewInstructionsProse =
    readOnlyWorkspaceConstraint
    + "\n\n"
    + "You are a code reviewer performing a rigorous review of submitted work.\n\n"
    + reviewCriteria
    + "\n\nBased on the original task, change report, and affected files above, read and inspect the actual file contents before making your judgment. The original task is the authoritative requirement — verify that the implementation satisfies it, not just that it matches the self-reported change report.\n\n# Submitting Your Verdict\n\nreturn_reviewer({ \"verdict\": \"PASS\" })                          // Accept — no feedback needed\nreturn_reviewer({ \"verdict\": \"PASS\", \"feedback\": \"minor suggestions...\" }) // Accept with optional suggestions\nreturn_reviewer({ \"verdict\": \"REJECT\", \"feedback\": \"specific...\" }) // Reject — provide detailed, actionable feedback\n\nIMPORTANT: verdict MUST be exactly \"PASS\" or \"REJECT\". When passing, feedback is optional and may include minor suggestions. When rejecting, feedback MUST be detailed and actionable.\n\nYou MUST call return_reviewer before finishing. Do not end the conversation without submitting your verdict."

let reviewInstructions =
    frontMatterPrompt [ yamlField "role" "reviewer" ] reviewInstructionsProse

let reviewerVerdictPrologue (subject: string) =
    $"You are a reviewer evaluating {subject}.\n\n"
    + "Call the agent_report tool to submit your verdict. Use exactly these fields:\n"
    + "- verdict: \"PASS\" if the changes are acceptable, \"REJECT\" otherwise\n"
    + "- feedback: optional suggestions when passing; detailed, actionable feedback when rejecting\n\n"
    + "Do not output free-form text as your final answer; the tool call is required."

let agentReportVerdictInstructions (passMeaning: string) =
    "Call the agent_report tool to submit your verdict. Use exactly these fields:\n"
    + "- verdict: \"PASS\" if "
    + passMeaning
    + ", \"REJECT\" otherwise\n"
    + "- feedback: optional suggestions when passing; detailed, actionable feedback when rejecting\n\n"
    + "IMPORTANT: If you accept, verdict MUST be \"PASS\". Feedback is optional when passing — include minor suggestions if you have them. "
    + "Do not output free-form text as your final answer; the tool call is required."

let reviewSubmissionVerdictBody =
    readOnlyWorkspaceConstraint
    + "\n\n"
    + "You are a code reviewer performing a rigorous review of submitted work.\n\n"
    + reviewCriteria
    + "\n\nBased on the original task, change report, and affected files above, read and inspect the actual file contents before making your judgment. "
    + "The original task is the authoritative requirement — verify that the implementation satisfies it, not just that it matches the self-reported change report.\n\n"
    + "# Submitting Your Verdict\n\n"
    + agentReportVerdictInstructions "current implementation is already complete and correct."

let preReviewVerdictBody =
    readOnlyWorkspaceConstraint
    + "\n\n"
    + "You are a code reviewer evaluating whether the proposed task is already finished before beginning work.\n\n"
    + reviewCriteria
    + "REJECT when the task needs real work, otherwise PASS.\n\n"
    + "# Submitting Your Verdict\n\n"
    + agentReportVerdictInstructions "the task is clear, specific, and actionable enough to begin work"

let agentReportReviewInstructions =
    readOnlyWorkspaceConstraint
    + "\n\n"
    + "You are a code reviewer performing a rigorous review of submitted work.\n\n"
    + reviewCriteria
    + "\n\nBased on the original task, change report, and affected files above, read and inspect the actual file contents before making your judgment. The original task is the authoritative requirement — verify that the implementation satisfies it, not just that it matches the self-reported change report.\n\n# Submitting Your Verdict\n\n"
    + "When you have finished the task, you MUST call the agent_report tool. Use structuredOutput with relatedFiles (and relatedCode where applicable). The reportMarkdown must be exactly one of:\n\nPASS\n\nor\n\nPASS: <optional suggestions>\n\nor\n\nREJECT: <detailed, actionable feedback>\n\n"
    + "IMPORTANT: If you accept, reportMarkdown MUST start with \"PASS\". Do not write ACCEPT, praise, JSON, or any other text — it will be misinterpreted as rejection feedback."

let muxReviewerAgentReportDescription =
    "Submit a review verdict. Provide verdict and feedback; the wrapper forwards the verdict as the upstream agent_report markdown."

module ReviewerVerdictPrompts =
    let reviewerVerdictInstructions =
        reviewerVerdictPrologue "whether the reported changes satisfy the original task"

    let loopReviewVerdictInstructions =
        "You are a reviewer evaluating whether a task description is clear and actionable enough to begin work.\n\n"
        + "Call the agent_report tool to submit your verdict. Use exactly these fields:\n"
        + "- verdict: \"PASS\" if the task is clear, specific, and actionable, \"REJECT\" otherwise\n"
        + "- feedback: optional when passing; detailed, actionable feedback when rejecting\n\n"
        + "Do not output free-form text as your final answer; the tool call is required."
