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
    + "\n\nBased on the original task, change report, and affected files above, read and inspect the actual file contents before making your judgment. The original task is the authoritative requirement — verify that the implementation satisfies it, not just that it matches the self-reported change report.\n\n# Submitting Your Verdict\n\nreturn_reviewer({ \"feedback\": null })          // Accept — pass with no feedback\nreturn_reviewer({ \"feedback\": \"specific...\" }) // Reject — provide detailed, actionable feedback\n\nIMPORTANT: If you accept, feedback MUST be null. Do not write praise or any other text — it will be misinterpreted as rejection feedback.\n\nYou MUST call return_reviewer before finishing. Do not end the conversation without submitting your verdict."

let reviewInstructions =
    frontMatterPrompt [ yamlScalarField "role" "reviewer" ] reviewInstructionsProse

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

let doubleCheckPrompt (task: string) : string =
    let taskLine = if task <> "" then [ yamlBlockField taskField task ] else []

    frontMatterPrompt
        ([ yamlScalarField
               doubleCheckField
               "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?" ]
         @ taskLine)
        "If you insist on PASS, otherwise please REJECT with detailed feedback."

let reviewerPrompt (task: string) (report: string) (affectedFiles: string list) : string =
    let taskLine = if task <> "" then [ yamlBlockField taskField task ] else []

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

let private reviewerPromptFrontMatter (callId: string) (fields: string list) : string list =
    [ yamlScalarField "role" "reviewer"; yamlScalarField "call_id" callId ] @ fields

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

let reviewerVerdictPrompt (subject: string) (callId: string) (fields: string list) : string =
    frontMatterPrompt (reviewerPromptFrontMatter callId fields) (reviewerVerdictPrologue subject)

let reviewSubmissionVerdictPrompt
    (task: string)
    (report: string)
    (affectedFiles: string list)
    (callId: string)
    : string =
    let taskLine = if task <> "" then [ yamlBlockField taskField task ] else []

    let filesLine =
        if affectedFiles.Length > 0 then
            [ yamlStringSeqField "affected_files" affectedFiles ]
        else
            []

    let reportLine =
        if System.String.IsNullOrEmpty report then
            []
        else
            [ yamlBlockField "report" report ]

    frontMatterPrompt (reviewerPromptFrontMatter callId (taskLine @ filesLine @ reportLine)) reviewSubmissionVerdictBody

let preReviewVerdictPrompt (task: string) (callId: string) : string =
    let taskLine = if task <> "" then [ yamlBlockField taskField task ] else []

    frontMatterPrompt (reviewerPromptFrontMatter callId taskLine) preReviewVerdictBody

let withReviewCommandTemplate =
    frontMatterPrompt
        [ yamlScalarField commandField commandWithReview
          yamlBlockField taskField "$ARGUMENTS" ]
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
        [ yamlScalarField commandField commandWithReviewPrecheck
          yamlBlockField taskField "$ARGUMENTS" ]
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
    + "  return_reviewer({ \"feedback\": null })          // Accept\n"
    + "  return_reviewer({ \"feedback\": \"details...\" })  // Reject\n\n"
    + "Do not explain what you plan to do — call the tool immediately."

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
            [ yamlPlainField verdictField verdictAccepted ]
            "Review passed. Your changes have been accepted. With-Review Mode has ended."
    | Terminated ->
        frontMatterPrompt
            [ yamlPlainField verdictField verdictTerminated ]
            "Review terminated without verdict. With-Review Mode is still active; fix the issues and call submit_review again."
    | Rejected feedback ->
        frontMatterPrompt
            [ yamlPlainField verdictField verdictRejected
              yamlBlockField "feedback" feedback ]
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
