module VibeFs.MuxPlugin.MuxPrompts

open VibeFs.Kernel.Prompts

let private agentReportClosing =
    "When you have finished the task, you MUST call the agent_report tool. "
    + "Use structuredOutput with relatedFiles (and relatedCode where applicable) so the caller can act on your findings."

let formatMuxCoderUserPrompt (intent: string) (affectedFiles: string list) : string =
    coderPromptBody intent affectedFiles
    + "4. Finish by calling agent_report with a summary of changes and verification results.\n\n"
    + agentReportClosing + "\n\n"

let formatMuxReaderUserPrompt (intent: string) : string =
    readerPromptBody intent
    + "3. Finish by calling agent_report with structuredOutput containing relatedFiles and relatedCode.\n\n"
    + agentReportClosing + "\n\n"

let formatMuxMeditatorUserPrompt (intent: string) (files: string list) : string =
    meditatorPromptBody intent files
    + "3. Finish by calling agent_report with structuredOutput containing relatedFiles and relatedCode.\n\n"
    + agentReportClosing + "\n\n"

let formatMuxBrowserUserPrompt (intent: string) : string =
    browserPromptBody intent
    + "3. Finish by calling agent_report with a clear summary of what you found or did.\n\n"
    + agentReportClosing + "\n\n"

let formatMuxExecutorSummarizerUserPrompt (output: string) : string =
    executorSummarizerPromptBody output
    + "\n4. Finish by calling agent_report with the summary.\n\n"
    + agentReportClosing + "\n\n"

let formatMuxWebsearchSummarizerUserPrompt (whatToSummarize: string) (rawResults: string) : string =
    websearchSummarizerPromptBody whatToSummarize rawResults
    + "\n5. Finish by calling agent_report with the focused answer.\n\n"
    + agentReportClosing + "\n\n"

let agentReportReviewInstructions =
    readOnlyWorkspaceConstraint + "\n\n"
    + "You are a code reviewer performing a rigorous review of submitted work.\n\n"
    + reviewCriteria
    + "\n\nBased on the original task, change report, and affected files above, read and inspect the actual file contents before making your judgment. The original task is the authoritative requirement — verify that the implementation satisfies it, not just that it matches the self-reported change report.\n\n# Submitting Your Verdict\n\n"
    + "When you have finished the task, you MUST call the agent_report tool. "
    + "Use structuredOutput with relatedFiles (and relatedCode where applicable). "
    + "The reportMarkdown must be exactly one of:\n\nPASS\n\nor\n\nREJECT: <detailed, actionable feedback>\n\n"
    + "IMPORTANT: If you accept, reportMarkdown MUST be exactly \"PASS\". Do not write ACCEPT, praise, JSON, or any other text — it will be misinterpreted as rejection feedback."
