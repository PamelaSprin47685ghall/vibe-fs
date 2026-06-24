module VibeFs.Kernel.ReviewPrompts

open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.ReviewSession
open VibeFs.Kernel.PromptFrontMatter
open VibeFs.Kernel.PromptFragments

let private reviewInstructionsProse =
    readOnlyWorkspaceConstraint
    + "\n\n"
    + "You are a code reviewer performing a rigorous review of submitted work.\n\n"
    + reviewCriteria
    + "\n\nBased on the original task, change report, and affected files above, read and inspect the actual file contents before making your judgment. The original task is the authoritative requirement — verify that the implementation satisfies it, not just that it matches the self-reported change report.\n\n# Submitting Your Verdict\n\nreturn_reviewer({ \"verdict\": \"PASS\" })                          // Accept — no feedback needed\nreturn_reviewer({ \"verdict\": \"REJECT\", \"feedback\": \"specific...\" }) // Reject — provide detailed, actionable feedback\n\nIMPORTANT: verdict MUST be exactly \"PASS\" or \"REJECT\". When passing, omit feedback. When rejecting, feedback MUST be detailed and actionable.\n\nYou MUST call return_reviewer before finishing. Do not end the conversation without submitting your verdict."

let reviewInstructions =
    frontMatterPrompt [ yamlField "role" "reviewer" ] reviewInstructionsProse

let private reviewerVerdictPrologue (subject: string) =
    $"You are a reviewer evaluating {subject}.\n\n"
    + "Call the agent_report tool to submit your verdict. Use exactly these fields:\n"
    + "- verdict: \"PASS\" if the changes are acceptable, \"REJECT\" otherwise\n"
    + "- feedback: detailed, actionable feedback when rejecting; empty string when passing\n\n"
    + "Do not output free-form text as your final answer; the tool call is required."

let private agentReportVerdictInstructions (passMeaning: string) =
    "Call the agent_report tool to submit your verdict. Use exactly these fields:\n"
    + "- verdict: \"PASS\" if "
    + passMeaning
    + ", \"REJECT\" otherwise\n"
    + "- feedback: detailed, actionable feedback when rejecting; empty string when passing\n\n"
    + "IMPORTANT: If you accept, verdict MUST be \"PASS\" and feedback MUST be an empty string. "
    + "Do not output free-form text as your final answer; the tool call is required."

let doubleCheckChallenge =
    "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?"

let doubleCheckPrompt (task: string) : string =
    let taskLine = if task <> "" then [ yamlField taskField task ] else []

    frontMatterPrompt
        ([ yamlField doubleCheckField doubleCheckChallenge ]
         @ taskLine)
        "If you insist on PASS, otherwise please REJECT with detailed feedback."

let reviewerPrompt (task: string) (report: string) (affectedFiles: string list) : string =
    let taskLine = if task <> "" then [ yamlField taskField task ] else []

    let filesLine =
        if affectedFiles.Length > 0 then
            [ yamlStringSeqField "affected_files" affectedFiles ]
        else
            []

    let body =
        if System.String.IsNullOrEmpty report then
            reviewInstructionsProse
        else
            reviewInstructionsProse + "\n\n# Worker Report\n\n" + report

    frontMatterPrompt (taskLine @ filesLine) body

let private reviewerPromptFrontMatter (fields: string list) : string list =
    [ yamlField "role" "reviewer" ] @ fields

let private reviewSubmissionVerdictBody =
    readOnlyWorkspaceConstraint
    + "\n\n"
    + "You are a code reviewer performing a rigorous review of submitted work.\n\n"
    + reviewCriteria
    + "\n\nBased on the original task, change report, and affected files above, read and inspect the actual file contents before making your judgment. "
    + "The original task is the authoritative requirement — verify that the implementation satisfies it, not just that it matches the self-reported change report.\n\n"
    + "# Submitting Your Verdict\n\n"
    + agentReportVerdictInstructions "current implementation is already complete and correct."

let private preReviewVerdictBody =
    readOnlyWorkspaceConstraint
    + "\n\n"
    + "You are a code reviewer evaluating whether the proposed task is already finished before beginning work.\n\n"
    + reviewCriteria
    + "REJECT when the task needs real work, otherwise PASS.\n\n"
    + "# Submitting Your Verdict\n\n"
    + agentReportVerdictInstructions "the task is clear, specific, and actionable enough to begin work"

let private reviewSubmissionFields (task: string) (report: string) (affectedFiles: string list) : string list =
    let taskLine = if task <> "" then [ yamlField taskField task ] else []

    let filesLine =
        if affectedFiles.Length > 0 then
            [ yamlStringSeqField "affected_files" affectedFiles ]
        else
            []

    let reportLine =
        if System.String.IsNullOrEmpty report then
            []
        else
            [ yamlField "report" report ]

    taskLine @ filesLine @ reportLine

let reviewSubmissionVerdictPrompt
    (task: string)
    (report: string)
    (affectedFiles: string list)
    : string =
    frontMatterPrompt (reviewerPromptFrontMatter (reviewSubmissionFields task report affectedFiles)) reviewSubmissionVerdictBody

/// Mux double-check round: a fresh reviewer re-examines the same submission with
/// the skeptical `double-check` framing. Carries the full review context (task,
/// report, files) plus the `double-check` front-matter anchor so a restart
/// replay recognizes the second round structurally.
let reviewSubmissionDoubleCheckPrompt
    (task: string)
    (report: string)
    (affectedFiles: string list)
    : string =
    let fields =
        [ yamlField "role" "reviewer"; yamlField doubleCheckField doubleCheckChallenge ]
        @ reviewSubmissionFields task report affectedFiles

    frontMatterPrompt fields reviewSubmissionVerdictBody

let preReviewVerdictPrompt (task: string) : string =
    let taskLine = if task <> "" then [ yamlField taskField task ] else []

    frontMatterPrompt (reviewerPromptFrontMatter taskLine) preReviewVerdictBody

let withReviewCommandTemplate =
    frontMatterPrompt
        [ yamlField commandField commandWithReview
          yamlField taskField "$ARGUMENTS" ]
        (String.concat
            "\n"
            [ "You are entering With-Review Mode."
              "Complete the task recorded in the front matter."
              ""
              "The reviewer will judge your eventual submission using these criteria:"
              ""
              reviewCriteria
              ""
              "Before finishing, you must call submit_review with:"
              "- report: a detailed description of what you did and why"
              "- affectedFiles: every file you modified or created"
              "Defend proactively against reviewer rejection: keep the implementation natural, minimal, complete, and well-tested."
              "Do not end the conversation without submit_review." ])

let withReviewPrecheckCommandTemplate =
    frontMatterPrompt
        [ yamlField commandField commandWithReviewPrecheck
          yamlField taskField "$ARGUMENTS" ]
        (String.concat
            "\n"
            [ "You are requesting With-Review Mode with pre-review."
              "The task recorded in the front matter will be pre-reviewed first."
              ""
              "If the task is activated, the reviewer will later judge your submission using these criteria:"
              ""
              reviewCriteria
              ""
              "If activated, complete the task and later submit your work via submit_review."
              "Do not treat this message itself as completed work." ])

let reviewerNudgePrompt =
    "Submit your review verdict now via return_reviewer:\n"
    + "  return_reviewer({ \"verdict\": \"PASS\" })                          // Accept\n"
    + "  return_reviewer({ \"verdict\": \"REJECT\", \"feedback\": \"details...\" }) // Reject\n\n"
    + "verdict MUST be exactly \"PASS\" or \"REJECT\". Do not explain what you plan to do — call the tool immediately."

let agentReportReviewInstructions =
    readOnlyWorkspaceConstraint
    + "\n\n"
    + "You are a code reviewer performing a rigorous review of submitted work.\n\n"
    + reviewCriteria
    + "\n\nBased on the original task, change report, and affected files above, read and inspect the actual file contents before making your judgment. The original task is the authoritative requirement — verify that the implementation satisfies it, not just that it matches the self-reported change report.\n\n# Submitting Your Verdict\n\n"
    + "When you have finished the task, you MUST call the agent_report tool. Use structuredOutput with relatedFiles (and relatedCode where applicable). The reportMarkdown must be exactly one of:\n\nPASS\n\nor\n\nREJECT: <detailed, actionable feedback>\n\n"
    + "IMPORTANT: If you accept, reportMarkdown MUST be exactly \"PASS\". Do not write ACCEPT, praise, JSON, or any other text — it will be misinterpreted as rejection feedback."

let formatReviewResult (result: ReviewResult) : string =
    match result with
    | Accepted ->
        frontMatterPrompt
            [ yamlField verdictField verdictAccepted ]
            "Review passed. Your changes have been accepted. With-Review Mode has ended."
    | Terminated ->
        frontMatterPrompt
            [ yamlField verdictField verdictTerminated ]
            "Review terminated without verdict. With-Review Mode is still active; fix the issues and call submit_review again."
    | Rejected feedback ->
        frontMatterPrompt
            [ yamlField verdictField verdictRejected
              yamlField "feedback" feedback ]
            "Address the feedback above. With-Review Mode is still active — fix the issues and call submit_review again."

module ReviewerVerdictPrompts =
    let reviewerVerdictInstructions =
        reviewerVerdictPrologue "whether the reported changes satisfy the original task"

    let loopReviewVerdictInstructions =
        "You are a reviewer evaluating whether a task description is clear and actionable enough to begin work.\n\n"
        + "Call the agent_report tool to submit your verdict. Use exactly these fields:\n"
        + "- verdict: \"PASS\" if the task is clear, specific, and actionable, \"REJECT\" otherwise\n"
        + "- feedback: detailed, actionable feedback when rejecting; empty string when passing\n\n"
        + "Do not output free-form text as your final answer; the tool call is required."
