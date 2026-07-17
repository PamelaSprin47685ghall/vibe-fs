module Wanxiangshu.Runtime.ReviewPrompts.Instructions

open Wanxiangshu.Runtime.PromptFrontMatter
open Wanxiangshu.Runtime.PromptFragments

let reviewInstructionsProse =
    readOnlyWorkspaceConstraint
    + "\n\n"
    + "You are a code reviewer performing a rigorous review of submitted work.\n\n"
    + reviewCriteria
    + "\n\nBased on the original task, change report, and affected files above, read and inspect the actual file contents before making your judgment. The original task is the authoritative requirement — verify that the implementation satisfies it, not just that it matches the self-reported change report.\n\n# Submitting Your Verdict\n\nreturn_reviewer({ \"verdict\": \"PERFECT\" })                          // Accept — no feedback needed\nreturn_reviewer({ \"verdict\": \"PERFECT\", \"feedback\": \"minor suggestions...\" }) // Accept with optional suggestions\nreturn_reviewer({ \"verdict\": \"REVISE\", \"feedback\": \"specific...\" }) // Request revision — provide detailed, actionable feedback\n\nIMPORTANT: verdict MUST be exactly \"PERFECT\" or \"REVISE\". When accepting, feedback is optional and may include minor suggestions. When requesting revision, feedback MUST be detailed and actionable.\n\nYou MUST call return_reviewer before finishing. Do not end the conversation without submitting your verdict."

let reviewInstructions = reviewInstructionsProse

let reviewerVerdictPrologue (subject: string) =
    $"You are a reviewer evaluating {subject}.\n\n"
    + "Call the agent_report tool to submit your verdict. Use exactly these fields:\n"
    + "- verdict: \"PERFECT\" if the changes are acceptable, \"REVISE\" otherwise\n"
    + "- feedback: optional suggestions when accepting; detailed, actionable feedback when requesting revision\n\n"
    + "Do not output free-form text as your final answer; the tool call is required."

let agentReportVerdictInstructions (acceptMeaning: string) =
    "Call the agent_report tool to submit your verdict. Use exactly these fields:\n"
    + "- verdict: \"PERFECT\" if "
    + acceptMeaning
    + ", \"REVISE\" otherwise\n"
    + "- feedback: optional suggestions when accepting; detailed, actionable feedback when requesting revision\n\n"
    + "IMPORTANT: If you accept, verdict MUST be \"PERFECT\". Feedback is optional when accepting — include minor suggestions if you have them. "
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

let agentReportReviewInstructions =
    readOnlyWorkspaceConstraint
    + "\n\n"
    + "You are a code reviewer performing a rigorous review of submitted work.\n\n"
    + reviewCriteria
    + "\n\nBased on the original task, change report, and affected files above, read and inspect the actual file contents before making your judgment. The original task is the authoritative requirement — verify that the implementation satisfies it, not just that it matches the self-reported change report.\n\n# Submitting Your Verdict\n\n"
    + "When you have finished the task, you MUST call the agent_report tool. Use structuredOutput with relatedFiles (and relatedCode where applicable). The reportMarkdown must be exactly one of:\n\nPERFECT\n\nor\n\nPERFECT: <optional suggestions>\n\nor\n\nREVISE: <detailed, actionable feedback>\n\n"
    + "IMPORTANT: If you accept, reportMarkdown MUST start with \"PERFECT\". Do not write ACCEPT, praise, JSON, or any other text — it will be misinterpreted as revision feedback."

let muxReviewerAgentReportDescription =
    "Submit a review verdict. Provide verdict and feedback; the wrapper forwards the verdict as the upstream agent_report markdown."

module ReviewerVerdictPrompts =
    let reviewerVerdictInstructions =
        reviewerVerdictPrologue "whether the reported changes satisfy the original task"

    let loopReviewVerdictInstructions =
        "You are a reviewer evaluating whether a task description is clear and actionable enough to begin work.\n\n"
        + "Call the agent_report tool to submit your verdict. Use exactly these fields:\n"
        + "- verdict: \"PERFECT\" if the task is clear, specific, and actionable, \"REVISE\" otherwise\n"
        + "- feedback: optional when accepting; detailed, actionable feedback when requesting revision\n\n"
        + "Do not output free-form text as your final answer; the tool call is required."
