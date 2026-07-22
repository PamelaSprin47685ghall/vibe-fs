module Wanxiangshu.Runtime.Subagent

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.SubagentPrompts

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
        output: string *
        language: string *
        program: string *
        dependencies: string list *
        timeoutType: string *
        whatToSummarize: string
    | WebsearchSummary of question: string * raw: string

let private agentReportTail =
    "\n\nWhen you have finished the task, you MUST call the agent_report tool. "
    + "Use structuredOutput with relatedFiles (and relatedCode where applicable) "
    + "so the caller can act on your findings."

let private withReportTail (host: Host) (body: string) : string =
    match host with
    | Opencode
    | Mux -> body
    | Mimocode ->
        if body.Contains "agent_report" then
            body
        else
            body + agentReportTail
    | Omp -> body

/// Produce one prompt per parallel intent for coder/inspector, exactly one
/// prompt for the singleton task kinds.  Host decides whether to append the
/// agent_report tail; otherwise prompt body is identical between hosts.
let formatPrompt (host: Host) (kind: SubagentTaskKind) : string list =
    match kind with
    | Coder intents -> intents |> List.map (fun i -> coderPromptWithHost i (Some host))
    | Inspector intents -> intents |> List.map (fun i -> inspectorPromptWithHost i (Some host))
    | Meditator intent -> [ withReportTail host intent ]
    | Browser intent -> [ browserPromptWithHost intent (Some host) ]
    | Continue(iterator, prompt) -> [ prompt ]
    | ExecutorSummary(output, language, program, dependencies, timeoutType, whatToSummarize) ->
        [ executorSummarizerPromptWithHost whatToSummarize output language program dependencies timeoutType (Some host) ]
    | WebsearchSummary(question, raw) -> [ websearchSummarizerPromptWithHost question raw (Some host) ]

let promptsForParallelIntents (host: Host) (constructor: 'a -> SubagentTaskKind) (intents: 'a) : string list =
    formatPrompt host (constructor intents)

let browserPromptText (host: Host) (intent: string) : string =
    formatPrompt host (Browser intent) |> List.head

let reportSeparator = "\n---\n"

/// Trim each report and join with the canonical separator.  Empty reports are
/// preserved as empty entries — callers decide whether to filter beforehand.
let joinReports (reports: string seq) : string =
    reports |> Seq.map (fun r -> r.Trim()) |> String.concat reportSeparator
