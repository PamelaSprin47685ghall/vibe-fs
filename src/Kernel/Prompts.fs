module VibeFs.Kernel.Prompts

open VibeFs.Kernel.Dyn
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.SubagentIntents
open VibeFs.Kernel.ReviewSession
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.PromptFrontMatter

type SearchResult =
    { title: string
      url: string
      content: string }

let parseSearchResults (results: obj) : SearchResult list =
    if Dyn.isNullish results || not (Dyn.isArray results) then
        []
    else
        (results :?> obj array)
        |> Array.map (fun r ->
            { title = Dyn.str r "title"
              url = Dyn.str r "url"
              content = Dyn.str r "content" })
        |> List.ofArray

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
    + "You must NOT run commands or call "
    + todoWritePromptName host
    + " or any mutating tool. "
    + "You must NOT change workspace state. Output reports only."

let readOnlyRules = readOnlyRulesFor opencode

let loopNudgePrompt =
    "You are in With-Review Mode. You must call the submit_review tool to\n"
    + "submit your detailed report and list of modified files for review\n"
    + "before finishing. Do not end the conversation without calling submit_review."

let managerSystemPromptFor (host: Host) =
    let todoLine =
        "For multi-step work, keep "
        + todoWriteToolName host
        + " current. Every "
        + todoWriteToolName host
        + " call must provide the full todos list plus a detailed completedWorkReport that can survive context folding."

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
    + "- feedback: detailed, actionable feedback when rejecting; empty string when passing\n"
    + "- callId: the callId supplied in this prompt\n\n"
    + "Do not output free-form text as your final answer; the tool call is required."

let private agentReportVerdictInstructions (passMeaning: string) =
    "Call the agent_report tool to submit your verdict. Use exactly these fields:\n"
    + "- verdict: \"PASS\" if "
    + passMeaning
    + ", \"REJECT\" otherwise\n"
    + "- feedback: detailed, actionable feedback when rejecting; empty string when passing\n"
    + "- callId: the callId supplied in this prompt\n\n"
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
    + agentReportVerdictInstructions "the reported changes satisfy the original task"

let private preReviewVerdictBody =
    readOnlyWorkspaceConstraint
    + "\n\n"
    + "You are a code reviewer evaluating whether the proposed task is clear and actionable enough to begin work.\n\n"
    + reviewCriteria
    + "\n\nBased on the task above, judge whether the requirement is specific, coherent, and implementable without guesswork. "
    + "Reject when the task is ambiguous, underspecified, or self-contradictory.\n\n"
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

/// Placeholder rendered for a meditator file section whose content could not be
/// read. Single SSOT — consumed by the Subagent prompt builder.
let meditatorSkippedSection = "(skipped)"

type MeditatorFileSection =
    { file: string; content: string option }

let private coderTargetItem (t: CoderTarget) : string =
    let guideLines = t.guide.Split('\n') |> Array.map (fun line -> "      " + line)

    let draftLines =
        match t.draft with
        | Some draft when not (System.String.IsNullOrWhiteSpace draft) ->
            Array.append [| "    draft: |" |] (draft.Split('\n') |> Array.map (fun line -> "      " + line))
        | _ -> [||]

    Array.concat
        [ [| "  - file: " + yamlScalar t.file; "    guide: |" |]
          guideLines
          draftLines ]
    |> String.concat "\n"

let private agentPrompt fields lines =
    let actualLines =
        if
            lines
            |> List.exists (fun (l: string) -> l.StartsWith("You are an implementation agent"))
        then
            lines
        else
            readOnlyRules :: lines

    frontMatterPrompt fields (String.concat "\n\n" actualLines)

let coderPrompt (intent: CoderIntent) : string =
    let fields =
        [ yamlBlockField "objective" intent.objective
          yamlBlockField "background" intent.background
          yamlSeqField "targets" (intent.targets |> List.map coderTargetItem) ]
        @ (if intent.doNotTouch.Length = 0 then
               []
           else
               [ yamlStringSeqField "do_not_touch" (List.ofArray intent.doNotTouch) ])

    agentPrompt
        fields
        [ "You are an implementation agent. Read the listed files and related code, then edit or create files to satisfy the objective and each target guide."
          "Static verification only (read, inspect, type-check). Do NOT run tests or execute code."
          "Return a concise summary of changes and/or your difficulties." ]

let investigatorPrompt (intent: InvestigatorIntent) : string =
    agentPrompt
        [ yamlBlockField "objective" intent.objective
          yamlBlockField "background" intent.background
          yamlStringSeqField "questions" (List.ofArray intent.questions)
          yamlStringSeqField "entries" (List.ofArray intent.entries) ]
        [ "You are a codebase search agent. Explore the workspace and answer every question in `questions`."
          "Use fuzzy_find, glob, fuzzy_grep, and read. Report concrete file paths and line-number references, and answer each question explicitly."
          "Return a structured report with relatedFiles and line ranges." ]

let meditatorPrompt (sections: MeditatorFileSection list) (intent: string) : string =
    let fileItem (s: MeditatorFileSection) : string =
        let body = Option.defaultValue meditatorSkippedSection s.content
        let contentLines = body.Split('\n') |> Array.map (fun line -> "      " + line)

        Array.concat [ [| "  - path: " + yamlScalar s.file; "    content: |" |]; contentLines ]
        |> String.concat "\n"

    agentPrompt
        [ yamlSeqField "files" (sections |> List.map fileItem)
          yamlBlockField "question" intent ]
        [ "You are a deep-reasoning agent. The file contents are provided above; analyze every listed file carefully."
          "Produce a thorough analysis covering tradeoffs, risks, and concrete recommendations."
          "Return a conclusive report with reasoning." ]

let browserPrompt (intent: string) : string =
    agentPrompt
        [ yamlBlockField "task" intent ]
        [ "You are a browser automation agent. Use only stealth-browser-mcp tools to interact with web pages. Do not write files or run shell commands."
          "Return a clear summary of what you found or did." ]

let executorSummarizerPrompt
    (output: string)
    (language: string)
    (program: string)
    (dependencies: string list)
    (timeoutType: string)
    (mode: string)
    : string =
    agentPrompt
        [ yamlScalarField "language" language
          yamlBlockField "program" program
          yamlStringSeqField "dependencies" dependencies
          yamlScalarField "timeout_type" timeoutType
          yamlScalarField "mode" mode
          yamlBlockField "raw_output" output ]
        [ "You are a filter for executor (shell) output. Preserve errors, non-zero exit status, and key paths or values. Omit noise, repeated lines, and progress banners. Do not invent details that are not in the output."
          "Do NOT lose any information." ]

let websearchSummarizerPrompt (whatToSummarize: string) (rawResults: string) : string =
    agentPrompt
        [ yamlBlockField "question" whatToSummarize
          yamlBlockField "raw_results" rawResults ]
        [ "You are a filter for web search results. Focus on answering the question above using the raw results. Preserve concrete facts. Omit boilerplate and unrelated results. Do not invent details not present in the results."
          "Do NOT lose any information." ]

let agentReportReviewInstructions =
    readOnlyWorkspaceConstraint
    + "\n\n"
    + "You are a code reviewer performing a rigorous review of submitted work.\n\n"
    + reviewCriteria
    + "\n\nBased on the original task, change report, and affected files above, read and inspect the actual file contents before making your judgment. The original task is the authoritative requirement — verify that the implementation satisfies it, not just that it matches the self-reported change report.\n\n# Submitting Your Verdict\n\n"
    + "When you have finished the task, you MUST call the agent_report tool. Use structuredOutput with relatedFiles (and relatedCode where applicable). The reportMarkdown must be exactly one of:\n\nPASS\n\nor\n\nREJECT: <detailed, actionable feedback>\n\n"
    + "IMPORTANT: If you accept, reportMarkdown MUST be exactly \"PASS\". Do not write ACCEPT, praise, JSON, or any other text — it will be misinterpreted as rejection feedback."

let formatSearchResults (results: SearchResult list) : string =
    if results.IsEmpty then
        "No results found."
    else
        let items =
            results
            |> List.map (fun r ->
                let contentBlock = yamlBlockField "content" r.content

                let indentedContentBlock =
                    contentBlock.Split('\n')
                    |> Array.map (fun line -> "    " + line)
                    |> String.concat "\n"

                "  - title: "
                + yamlScalar r.title
                + "\n    url: "
                + yamlScalar r.url
                + "\n"
                + indentedContentBlock)

        frontMatter [ yamlSeqField "results" items ]

let formatFetchResponse (data: FetchResponse) : string =
    let nonEmpty (s: string) = not (System.String.IsNullOrEmpty s)

    let scalarIf (key: string) =
        function
        | Some v when nonEmpty v -> [ yamlScalarField key v ]
        | _ -> []

    let title = scalarIf "title" data.title
    let byline = scalarIf "byline" data.byline

    let length =
        match data.length with
        | Some l -> [ yamlScalarField "length" (string l) ]
        | None -> []

    let content =
        match data.content with
        | Some c when nonEmpty c -> [ yamlBlockField "content" c ]
        | _ -> []

    frontMatter (title @ byline @ length @ content)

module ReviewerVerdictPrompts =
    let reviewerVerdictInstructions =
        reviewerVerdictPrologue "whether the reported changes satisfy the original task"

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
