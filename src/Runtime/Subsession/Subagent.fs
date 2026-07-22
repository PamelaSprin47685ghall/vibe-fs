module Wanxiangshu.Runtime.Subagent

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Runtime.SubagentSummarizerPrompts
open Wanxiangshu.Runtime.Tooling.ToolOutputBatchToml

/// Every kind of subagent task that needs prompt construction. One union case
/// per logical task; coder/inspector carry many intents because each intent
/// becomes its own parallel prompt.
type SubagentTaskKind =
    | Coder of CoderIntent list
    | Inspector of InspectorIntent list
    | Meditator of intent: string
    | Browser of intent: string
    | Continue of iterator: string * prompt: string
    | ExecutorSummary of
        evidence: ExecutorOutputEvidence *
        language: string *
        program: string *
        dependencies: string list *
        timeoutKind: TimeoutKind *
        whatToSummarize: string
    | WebsearchSummary of question: string * results: WebSearchResultItem list

/// Produce one prompt per parallel intent for coder/inspector, exactly one
/// prompt for the singleton task kinds. Host-specific contracts (e.g. agent_report)
/// are projected inside SubagentPrompts before stringify — never appended after.
let formatPrompt (host: Host) (kind: SubagentTaskKind) : string list =
    match kind with
    | Coder intents -> intents |> List.map (fun i -> coderPromptWithHost i (Some host))
    | Inspector intents -> intents |> List.map (fun i -> inspectorPromptWithHost i (Some host))
    | Meditator intent -> [ intent ]
    | Browser intent -> [ browserPromptWithHost intent (Some host) ]
    | Continue(_, prompt) -> [ prompt ]
    | ExecutorSummary(evidence, language, program, dependencies, timeoutKind, whatToSummarize) ->
        [ executorSummarizerPromptWithHost
              whatToSummarize
              evidence
              language
              program
              dependencies
              timeoutKind
              (Some host) ]
    | WebsearchSummary(question, results) ->
        [ websearchSummarizerPromptWithHost question results (Some host) ]

let promptsForParallelIntents (host: Host) (constructor: 'a -> SubagentTaskKind) (intents: 'a) : string list =
    formatPrompt host (constructor intents)

let browserPromptText (host: Host) (intent: string) : string =
    formatPrompt host (Browser intent) |> List.head

open Wanxiangshu.Runtime.SubagentReportParse

/// Parse free-text / lightly-structured agent report into SubagentReport fields.
let reportFromText (text: string) : SubagentReport = parseSubagentReportText text

/// Join typed SubagentReport rows as BatchReport TOML (`[[reports]]`).
let joinReports (reports: SubagentReport seq) : string =
    let items =
        reports
        |> Seq.toList
        |> List.filter (fun r ->
            match r.summary, r.error with
            | Some s, _ when s.Trim() <> "" -> true
            | _, Some _ -> true
            | _ ->
                not (List.isEmpty r.findings)
                || not (List.isEmpty r.relatedFiles)
                || not (List.isEmpty r.relatedCode))

    match BatchReport.create items with
    | Some batch -> renderBatchReport batch
    | None -> ""
