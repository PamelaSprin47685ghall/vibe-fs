module Wanxiangshu.Runtime.SubagentPrompts

open System
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt

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
        + sprintf "\n\n[Output truncated to %d bytes for summarization]" executorSummaryMaxBytes

let private hostRules (host: Host option) : PromptRule list =
    match host with
    | Some Host.Mimocode ->
        [ PromptRule.Contract
              "When you have finished the task, you MUST call the agent_report tool. Use structuredOutput with relatedFiles (and relatedCode where applicable) so the caller can act on your findings." ]
    | _ -> []

let private renderOrFail (docView: PromptDocumentView) (label: string) : string =
    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create %s PromptDocument: %A" label errs

let coderPromptWithHost (intent: CoderIntent) (host: Host option) : string =
    let docView: PromptDocumentView =
        { objective = intent.objective
          background =
            if String.IsNullOrWhiteSpace intent.background then
                None
            else
                Some(intent.background.Trim())
          agentRole = AgentRole.Implementation
          targets =
            intent.targets
            |> List.map (fun t ->
                let draft =
                    match t.draft with
                    | Some d when not (String.IsNullOrWhiteSpace d) -> Some d
                    | _ -> None

                PromptTarget.FileTarget(t.file, t.guide, draft))
          boundaries =
            intent.doNotTouch
            |> Array.toList
            |> List.map (fun p -> PromptBoundary.DoNotTouch(BoundaryTarget.PathOrSymbol p))
          rules =
            [ PromptRule.Policy
                  "Read the listed files and related code, then edit or create files to satisfy the objective and each target guide."
              PromptRule.Constraint
                  "Static verification only (read and think using logic). Do NOT run tests or execute code."
              yield! hostRules host ]
          outcomes =
            [ { label = "report"
                text = "Return a detailed summary of changes and/or your difficulties." } ] }

    renderOrFail docView "Coder"

let coderPrompt (intent: CoderIntent) : string = coderPromptWithHost intent None

let inspectorPromptWithHost (intent: InspectorIntent) (host: Host option) : string =
    let docView: PromptDocumentView =
        { objective = intent.objective
          background =
            if String.IsNullOrWhiteSpace intent.background then
                None
            else
                Some(intent.background.Trim())
          agentRole = AgentRole.CodebaseSearch
          targets = intent.entries |> Array.toList |> List.map PromptTarget.EntryTarget
          boundaries = []
          rules =
            [ PromptRule.Policy
                  "Explore the workspace and answer questions. Use fuzzy_find, glob, fuzzy_grep, and read. Report concrete file paths and line-number references, and answer each question explicitly."
              yield! (intent.questions |> Array.toList |> List.map PromptRule.Question)
              yield! hostRules host ]
          outcomes =
            [ { label = "report"
                text = "Return your report with relatedFiles and line ranges." } ] }

    renderOrFail docView "Inspector"

let inspectorPrompt (intent: InspectorIntent) : string = inspectorPromptWithHost intent None

let browserPromptWithHost (intent: string) (host: Host option) : string =
    let docView: PromptDocumentView =
        { objective = intent
          background = None
          agentRole = AgentRole.BrowserAutomation
          targets = []
          boundaries = []
          rules =
            [ PromptRule.Policy "Use stealth-browser-mcp tools to interact with web pages."
              yield! hostRules host ]
          outcomes =
            [ { label = "report"
                text = "Return a detailed report." } ] }

    renderOrFail docView "Browser"

let browserPrompt (intent: string) : string = browserPromptWithHost intent None

let executorSummarizerPromptWithHost
    (whatToSummarize: string)
    (output: string)
    (language: string)
    (program: string)
    (dependencies: string list)
    (timeoutType: string)
    (host: Host option)
    : string =
    let capped = capExecutorSummaryOutput output

    let timeoutKind =
        match timeoutType.ToLowerInvariant() with
        | "short" -> TimeoutKind.Short
        | _ -> TimeoutKind.Long

    let objective =
        if String.IsNullOrWhiteSpace whatToSummarize then
            "Summarize the executor output while preserving stack traces"
        else
            whatToSummarize.Trim()

    let docView: PromptDocumentView =
        { objective = objective
          background = None
          agentRole = AgentRole.ExecutorSummarization
          targets =
            [ PromptTarget.CommandTarget(language, program, dependencies, timeoutKind)
              PromptTarget.EvidenceTarget("executor_output", capped) ]
          boundaries = []
          rules =
            [ PromptRule.Constraint
                  "Preserve errors, stack traces, and key paths or values. Omit noise, repeated lines, and progress banners. Do not invent details that are not in the output. Do NOT lose any information."
              yield! hostRules host ]
          outcomes =
            [ { label = "report"
                text = "Return a dense summary without inventing facts." } ] }

    renderOrFail docView "ExecutorSummarizer"

let executorSummarizerPrompt
    (whatToSummarize: string)
    (output: string)
    (language: string)
    (program: string)
    (dependencies: string list)
    (timeoutType: string)
    : string =
    executorSummarizerPromptWithHost whatToSummarize output language program dependencies timeoutType None

let websearchSummarizerPromptWithHost (whatToSummarize: string) (rawResults: string) (host: Host option) : string =
    let docView: PromptDocumentView =
        { objective = whatToSummarize
          background = None
          agentRole = AgentRole.WebSearchSummarization
          targets = [ PromptTarget.EvidenceTarget("websearch_results", rawResults) ]
          boundaries = []
          rules =
            [ PromptRule.Constraint
                  "Focus on answering the question using the raw results. Preserve concrete facts. Omit boilerplate and unrelated results. Do not invent details not present in the results. Do NOT lose any information."
              yield! hostRules host ]
          outcomes =
            [ { label = "report"
                text = "Answer the objective using only supplied evidence." } ] }

    renderOrFail docView "WebSearchSummarizer"

let websearchSummarizerPrompt (whatToSummarize: string) (rawResults: string) : string =
    websearchSummarizerPromptWithHost whatToSummarize rawResults None

let renderMeditatorIntent (entry: Wanxiangshu.Kernel.Methodology.Schema.MethodologyEntry) (intentText: string) (backgroundText: string) (noteText: string) : string =
    let docView = Wanxiangshu.Kernel.Methodology.Schema.renderMeditatorDocument entry intentText backgroundText noteText
    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create Meditator PromptDocument: %A" errs
