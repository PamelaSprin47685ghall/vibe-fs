module VibeFs.Kernel.Prompts

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.SubagentIntents
open VibeFs.Kernel.ReviewSession

type SearchResult =
    { title: string
      url: string
      content: string }

type FetchResponse =
    { title: string option
      byline: string option
      length: int option
      content: string option }

let meditatorNudge =
    "// Think thrice before acting — NOW consider calling meditator tool to improve reasoning"

let todoNudgePrompt =
    "There are still incomplete todos. Continue working through the remaining items. "
    + "If stuck or blocked, explain the situation and ask for guidance. "
    + "If you want to skip this check, respond with <skip-todo-check />"

let readOnlyRulesFor (host: Host) =
    "READ-ONLY: You may only use read, search, and discovery tools. "
    + "You must NOT write, edit, patch, or create files. "
    + "You must NOT run commands or call " + todoWritePromptName host + " or any mutating tool. "
    + "You must NOT change workspace state. Output reports only."

let readOnlyRules = readOnlyRulesFor opencode

let loopNudgePrompt =
    "You are in loop mode. You must call the submit_review tool to\n"
    + "submit your detailed report and list of modified files for review\n"
    + "before finishing. Do not end the conversation without calling submit_review."

let managerSystemPromptFor (host: Host) =
    let todoLine =
        match host with
        | Opencode ->
            "For multi-step work, keep " + todoWriteToolName host + " current. Every " + todoWriteToolName host
            + " call must provide the full todos list plus a detailed completedWorkReport that can survive context folding."
        | Mimocode ->
            "For multi-step work, drive the session task registry via the " + todoWriteToolName host
            + " tool (`operation` with create/list/start/done/…). On calls where you make or plan meaningful progress, also include a detailed completedWorkReport so Magic Todo can survive context folding."
    "You are the manager agent. Coordinate the overall task, decide when to delegate to subagents, and synthesize their outputs into a final answer that satisfies the user's original goal.\n\n"
    + todoLine

let managerSystemPrompt = managerSystemPromptFor opencode

let reviewCriteria =
    """# Evaluation Criteria

1. Does the implementation make full use of language features? Are the correct algorithms and data structures used?
2. Is the implementation no more complex than necessary? Are there any garbage code, dead code, legacy compatible wrappers or unnecessary workarounds?
3. Is the program structure elegant and free of redundancy?
4. Are there no oversized files, overly long functions, or avoidable complexity?
5. Are there necessary unit or integration tests?
6. Are there design flaws, logic errors, or best-practice violations?
7. Is the result natural and intuitive for the user or caller?
8. Does it fully satisfy the original task without cutting corners?"""

let readOnlyWorkspaceConstraint = readOnlyRules

let reviewInstructions =
    readOnlyWorkspaceConstraint + "\n\n"
    + "You are a code reviewer performing a rigorous review of submitted work.\n\n"
    + reviewCriteria
    + "\n\nBased on the original task, change report, and affected files above, read and inspect the actual file contents before making your judgment. The original task is the authoritative requirement — verify that the implementation satisfies it, not just that it matches the self-reported change report.\n\n# Submitting Your Verdict\n\nreturn_reviewer({ \"feedback\": null })          // Accept — pass with no feedback\nreturn_reviewer({ \"feedback\": \"specific...\" }) // Reject — provide detailed, actionable feedback\n\nIMPORTANT: If you accept, feedback MUST be null. Do not write praise or any other text — it will be misinterpreted as rejection feedback.\n\nYou MUST call return_reviewer before finishing. Do not end the conversation without submitting your verdict."

let reviewerNudgePrompt =
    "You have not submitted your review verdict yet.\n\n"
    + "You must call return_reviewer to submit your verdict:\n"
    + "  return_reviewer({ \"feedback\": null })          // Accept\n"
    + "  return_reviewer({ \"feedback\": \"details...\" })  // Reject\n\n"
    + "Do not explain what you plan to do — call the tool immediately."

let private bulletLines (items: string seq) =
    items |> Seq.map (fun s -> $"- {s}") |> String.concat "\n"

let coderPromptBody (intent: CoderIntent) : string =
    let targets =
        intent.targets
        |> List.map (fun t ->
            match t.draft with
            | Some draft -> $"- {t.file}\n  Guide: {t.guide}\n  Draft: {draft}"
            | None -> $"- {t.file}\n  Guide: {t.guide}")
        |> String.concat "\n"
    let targetInstruction =
        "Targets:\n" + targets + "\n\n"
    let doNotTouch =
        if intent.doNotTouch.Length = 0 then ""
        else "Do not touch:\n" + bulletLines intent.doNotTouch + "\n\n"
    "You are an implementation agent (coder). Implement the objective using the background and per-file guides below.\n\n"
    + "Objective:\n" + intent.objective + "\n\n"
    + "Background:\n" + intent.background + "\n\n"
    + targetInstruction
    + doNotTouch
    + "Instructions:\n"
    + "1. Read the listed files and related code needed for the change.\n"
    + "2. Edit or create files to satisfy the objective and each file guide.\n"
    + "3. Perform static verification only (read, inspect, type-check). Do NOT run tests, execute code, or run any commands.\n"

let formatCoderUserPrompt (intent: CoderIntent) : string =
    coderPromptBody intent
    + "4. Return a concise summary of changes and verification results.\n\n"

let investigatorPromptBody (intent: InvestigatorIntent) : string =
    let entries =
        if intent.entries.Length = 0 then "(none — choose entry points from the codebase)"
        else bulletLines intent.entries
    "You are a codebase search agent (investigator). Explore the workspace and answer every required question.\n\n"
    + readOnlyRules + "\n\n"
    + "Objective:\n" + intent.objective + "\n\n"
    + "Background:\n" + intent.background + "\n\n"
    + "Questions you must answer:\n" + bulletLines intent.questions + "\n\n"
    + "Suggested entries:\n" + entries + "\n\n"
    + "Instructions:\n"
    + "1. Use fuzzy_find, glob, fuzzy_grep, and read tools to locate relevant code.\n"
    + "2. Report concrete file paths and line-number references.\n"
    + "3. Answer each required question explicitly in your report.\n"

let formatInvestigatorUserPrompt (intent: InvestigatorIntent) : string =
    investigatorPromptBody intent
    + "4. Return a structured report with relatedFiles and relatedCode.\n\n"

let meditatorPromptBody (intent: string) (files: string list) : string =
    let fileList = files |> List.map (fun f -> $"- {f}") |> String.concat "\n"
    "You are a deep-reasoning agent (meditator). Read and analyze the files below, then answer the question.\n\n"
    + readOnlyRules + "\n\n"
    + "Files to analyze:\n" + fileList + "\n\n"
    + "Question:\n" + intent + "\n\n"
    + "Instructions:\n"
    + "1. The file contents are provided above; read and analyze every listed file carefully.\n"
    + "2. Produce a thorough analysis covering tradeoffs, risks, and concrete recommendations.\n"

let formatMeditatorUserPrompt (intent: string) (files: string list) : string =
    meditatorPromptBody intent files
    + "3. Return a structured report with relatedFiles and relatedCode.\n\n"

let meditatorSectionSeparator = "\n---\n"

type MeditatorFileSection =
    { file: string
      content: string option }

type private PromptSection =
    | FileSection of fileName: string * body: string
    | InstructionSection of body: string

let private renderPromptSection = function
    | FileSection(fileName, body) -> $"=== {fileName} ===\n\n{body}"
    | InstructionSection body -> body

let buildMeditatorPrompt (sections: MeditatorFileSection list) (intent: string) : string =
    let skipped = "(skipped)"
    let promptSections =
        sections
        |> List.map (fun section -> FileSection(section.file, Option.defaultValue skipped section.content))
    let files = sections |> List.map (fun s -> s.file)
    let allSections = promptSections @ [ InstructionSection(formatMeditatorUserPrompt intent files) ]
    allSections |> List.map renderPromptSection |> String.concat "\n\n"

let browserPromptBody (intent: string) : string =
    "You are a browser automation agent. Complete the web task described below.\n\n"
    + readOnlyRules + "\n\n"
    + "Web task:\n" + intent + "\n\n"
    + "Instructions:\n"
    + "1. Use only stealth-browser-mcp tools to interact with web pages.\n"
    + "2. Do not write files or run shell commands.\n"

let formatBrowserUserPrompt (intent: string) : string =
    browserPromptBody intent
    + "3. Return a clear summary of what you found or did.\n\n"

let executorSummarizerPromptBody (output: string) : string =
    "You are a summarizer for executor (shell) output. Condense the raw output below into an actionable summary.\n\n"
    + readOnlyRules + "\n\n"
    + "Instructions:\n"
    + "1. Preserve errors, non-zero exit status, and key paths or values.\n"
    + "2. Omit noise, repeated lines, and progress banners.\n"
    + "3. Do not invent details that are not in the output.\n"
    + "Raw output:\n" + output

let formatExecutorSummarizerUserPrompt (output: string) : string =
    executorSummarizerPromptBody output
    + "\n4. Return a concise, actionable summary.\n\n"

let websearchSummarizerPromptBody (whatToSummarize: string) (rawResults: string) : string =
    "You are a summarizer for web search results. The caller searched the web and needs you to extract and synthesize content focused on a specific question.\n\n"
    + readOnlyRules + "\n\n"
    + "Question to answer:\n" + whatToSummarize + "\n\n"
    + "Instructions:\n"
    + "1. Focus on answering the question above using the raw search results below.\n"
    + "2. Preserve concrete facts: URLs, names, version numbers, code samples, and exact values.\n"
    + "3. Omit boilerplate, navigation noise, and results unrelated to the question.\n"
    + "4. Do not invent details not present in the results.\n"
    + "Raw search results:\n" + rawResults

let formatWebsearchSummarizerUserPrompt (whatToSummarize: string) (rawResults: string) : string =
    websearchSummarizerPromptBody whatToSummarize rawResults
    + "\n5. Return a focused, ready-to-use answer.\n\n"

let agentReportReviewInstructions =
    readOnlyWorkspaceConstraint + "\n\n"
    + "You are a code reviewer performing a rigorous review of submitted work.\n\n"
    + reviewCriteria
    + "\n\nBased on the original task, change report, and affected files above, read and inspect the actual file contents before making your judgment. The original task is the authoritative requirement — verify that the implementation satisfies it, not just that it matches the self-reported change report.\n\n# Submitting Your Verdict\n\n"
    + "When you have finished the task, you MUST call the agent_report tool. Use structuredOutput with relatedFiles (and relatedCode where applicable). The reportMarkdown must be exactly one of:\n\nPASS\n\nor\n\nREJECT: <detailed, actionable feedback>\n\n"
    + "IMPORTANT: If you accept, reportMarkdown MUST be exactly \"PASS\". Do not write ACCEPT, praise, JSON, or any other text — it will be misinterpreted as rejection feedback."

let formatSearchResults (results: SearchResult list) : string =
    if results.IsEmpty then "No results found."
    else
        results
        |> List.mapi (fun i r -> $"{i + 1}. {r.title}\n   URL: {r.url}\n   {r.content}")
        |> String.concat "\n\n"

let formatFetchResponse (data: FetchResponse) : string =
    let nonEmpty (s: string) = not (System.String.IsNullOrEmpty s)

    let title = defaultArg data.title ""

    [ yield $"Title: {title}"
      match data.byline with
      | Some b when nonEmpty b -> yield $"By: {b}"
      | _ -> ()
      match data.length with
      | Some l -> yield $"Length: {l}"
      | None -> ()
      match data.content with
      | Some c when nonEmpty c -> yield c
      | _ -> () ]
    |> String.concat "\n"

let private verdictPrologue (subject: string) =
    $"You are a reviewer evaluating {subject}.\n\n"
    + "Call the agent_report tool to submit your verdict. Use exactly these fields:\n"
    + "- verdict: \"PASS\" if the changes are acceptable, \"REJECT\" otherwise\n"
    + "- feedback: detailed, actionable feedback when rejecting; empty string when passing\n"
    + "- callId: the callId supplied in this prompt\n\n"
    + "Do not output free-form text as your final answer; the tool call is required."

let reviewerVerdictInstructions =
    verdictPrologue "whether the reported changes satisfy the original task"

let loopReviewVerdictInstructions =
    "You are a reviewer evaluating whether a task description is clear and actionable enough to begin work.\n\n"
    + "Call the agent_report tool to submit your verdict. Use exactly these fields:\n"
    + "- verdict: \"PASS\" if the task is clear, specific, and actionable, \"REJECT\" otherwise\n"
    + "- feedback: detailed, actionable feedback when rejecting; empty string when passing\n"
    + "- callId: the callId supplied in this prompt\n\n"
    + "Do not output free-form text as your final answer; the tool call is required."

let formatReviewResult (result: ReviewResult) : string =
    match result with
    | Accepted ->
        "Review passed. Your changes have been accepted. loop mode has ended."
    | Terminated ->
        "Review terminated without verdict. loop mode is still active; fix the issues and call submit_review again."
    | Rejected feedback ->
        "Review feedback:\n\n" + feedback
        + "\n\nAddress the feedback above. loop mode is still active — fix the issues and call submit_review again."
