module Wanxiangshu.Runtime.SubagentPrompts

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.PromptFrontMatter
open Wanxiangshu.Runtime.PromptFragments

let executorSummaryMaxBytes = 200_000

let private utf8CharWidth (text: string) (index: int) : int * int =
    let current = text[index]

    if
        Char.IsHighSurrogate current
        && index + 1 < text.Length
        && Char.IsLowSurrogate text[index + 1]
    then
        4, 2
    else
        let code = int current

        if code <= 0x7F then 1, 1
        elif code <= 0x7FF then 2, 1
        else 3, 1

let truncateUtf8ByBytes (text: string) (maxBytes: int) : string =
    if String.IsNullOrEmpty text || maxBytes <= 0 then
        ""
    else
        let mutable index = 0
        let mutable used = 0
        let mutable endIndex = 0

        while index < text.Length do
            let width, step = utf8CharWidth text index

            if used + width > maxBytes then
                index <- text.Length
            else
                used <- used + width
                index <- index + step
                endIndex <- index

        text.Substring(0, endIndex)

let capExecutorSummaryOutput (output: string) : string =
    let mutable index = 0
    let mutable total = 0

    while index < output.Length do
        let width, step = utf8CharWidth output index
        total <- total + width
        index <- index + step

    if total <= executorSummaryMaxBytes then
        output
    else
        truncateUtf8ByBytes output executorSummaryMaxBytes
        + "\n\n[Output truncated to 200000 bytes for summarization]"

let private coderTargetItem (t: CoderTarget) : obj =
    let fields =
        [ "file", box t.file; "guide", box t.guide ]
        @ (match t.draft with
           | Some draft when not (System.String.IsNullOrWhiteSpace draft) -> [ "draft", box draft ]
           | _ -> [])

    createObj fields

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
        [ yamlField "objective" intent.objective
          yamlField "background" intent.background
          yamlSeqField "targets" (intent.targets |> List.map coderTargetItem) ]
        @ (if intent.doNotTouch.Length = 0 then
               []
           else
               [ yamlStringSeqField "do_not_touch" (List.ofArray intent.doNotTouch) ])

    agentPrompt
        fields
        [ "You are an implementation agent. Read the listed files and related code, then edit or create files to satisfy the objective and each target guide."
          "Static verification only (read and think using logic). Do NOT run tests or execute code."
          "Return a detailed summary of changes and/or your difficulties." ]

let inspectorPrompt (intent: InspectorIntent) : string =
    agentPrompt
        [ yamlField "objective" intent.objective
          yamlField "background" intent.background
          yamlStringSeqField "questions" (List.ofArray intent.questions)
          yamlStringSeqField "entries" (List.ofArray intent.entries) ]
        [ "You are a codebase search agent. Explore the workspace and answer questions."
          "Use fuzzy_find, glob, fuzzy_grep, and read. Report concrete file paths and line-number references, and answer each question explicitly."
          "Return your report with relatedFiles and line ranges." ]

let browserPrompt (intent: string) : string =
    agentPrompt
        [ yamlField "task" intent ]
        [ "You are a browser automation agent. Use stealth-browser-mcp tools to interact with web pages. Return a detailed report." ]

let executorSummarizerPrompt
    (whatToSummarize: string)
    (output: string)
    (language: string)
    (program: string)
    (dependencies: string list)
    (timeoutType: string)
    (mode: string)
    : string =
    let capped = capExecutorSummaryOutput output

    let taskBody =
        let directive =
            "You are a filter for executor output. Preserve errors, stack traces, and key paths or values. Omit noise, repeated lines, and progress banners. Do not invent details that are not in the output.\nDo NOT lose any information."

        let trimmed = whatToSummarize.Trim()

        if trimmed = "" then
            directive
        else
            directive + "\n\n" + trimmed

    agentPrompt
        [ yamlField "language" language
          yamlField "program" program
          yamlStringSeqField "dependencies" dependencies
          yamlField "timeout_type" timeoutType
          yamlField "mode" mode
          yamlField "what_to_summarize" whatToSummarize ]
        [ capped; "# Task\n" + taskBody ]

let websearchSummarizerPrompt (whatToSummarize: string) (rawResults: string) : string =
    agentPrompt
        [ yamlField "question" whatToSummarize; yamlField "raw_results" rawResults ]
        [ "You are a filter for web search results. Focus on answering the question above using the raw results. Preserve concrete facts. Omit boilerplate and unrelated results. Do not invent details not present in the results."
          "Do NOT lose any information." ]
