module Wanxiangshu.Kernel.Subagent

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.SubagentPrompts

/// Every kind of subagent task that needs prompt construction. One union case
/// per logical task; coder/investigator carry many intents because each intent
/// becomes its own parallel prompt.
type SubagentTaskKind =
    | Coder of CoderIntent list
    | Investigator of InvestigatorIntent list
    | Meditator of intent: string * sections: MeditatorFileSection list
    | Browser of intent: string
    | Continue of iterator: string * prompt: string
    | ExecutorSummary of
        output: string *
        language: string *
        program: string *
        dependencies: string list *
        timeoutType: string *
        mode: string *
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
    | Mimocode -> body + agentReportTail
    | Omp -> body

/// Produce one prompt per parallel intent for coder/investigator, exactly one
/// prompt for the singleton task kinds.  Host decides whether to append the
/// agent_report tail; otherwise prompt body is identical between hosts.
let formatPrompt (host: Host) (kind: SubagentTaskKind) : string list =
    let wrap = withReportTail host

    match kind with
    | Coder intents -> intents |> List.map (coderPrompt >> wrap)
    | Investigator intents -> intents |> List.map (investigatorPrompt >> wrap)
    | Meditator(intent, sections) -> [ meditatorPrompt sections intent |> wrap ]
    | Browser intent -> [ browserPrompt intent |> wrap ]
    | Continue(iterator, prompt) -> [ prompt ]
    | ExecutorSummary(output, language, program, dependencies, timeoutType, mode, whatToSummarize) ->
        [ executorSummarizerPrompt whatToSummarize output language program dependencies timeoutType mode
          |> wrap ]
    | WebsearchSummary(question, raw) -> [ websearchSummarizerPrompt question raw |> wrap ]

let promptsForParallelIntents (host: Host) (constructor: 'a -> SubagentTaskKind) (intents: 'a) : string list =
    formatPrompt host (constructor intents)

let browserPromptText (host: Host) (intent: string) : string =
    formatPrompt host (Browser intent) |> List.head

let meditatorPromptText (host: Host) (intent: string) (sections: MeditatorFileSection list) : string =
    formatPrompt host (Meditator(intent, sections)) |> List.head

let reportSeparator = "\n---\n"

/// Trim each report and join with the canonical separator.  Empty reports are
/// preserved as empty entries — callers decide whether to filter beforehand.
let joinReports (reports: string seq) : string =
    reports |> Seq.map (fun r -> r.Trim()) |> String.concat reportSeparator
