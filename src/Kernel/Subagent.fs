module VibeFs.Kernel.Subagent

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.SubagentIntents
open VibeFs.Kernel.Prompts

/// Every kind of subagent task that needs prompt construction. One union case
/// per logical task; coder/investigator carry many intents because each intent
/// becomes its own parallel prompt.
type SubagentTaskKind =
    | Coder of CoderIntent list
    | Investigator of InvestigatorIntent list
    | Meditator of intent: string * sections: MeditatorFileSection list
    | Browser of intent: string
    | ExecutorSummary of output: string
    | WebsearchSummary of question: string * raw: string

let private agentReportTail =
    "\n\nWhen you have finished the task, you MUST call the agent_report tool. "
    + "Use structuredOutput with relatedFiles (and relatedCode where applicable) "
    + "so the caller can act on your findings."

let private withReportTail (host: Host) (body: string) : string =
    match host with
    | Opencode -> body
    | Mimocode -> body + agentReportTail

let private coderBody (intent: CoderIntent) : string =
    coderPromptBody intent + "4. Return a concise summary of changes and verification results.\n\n"

let private investigatorBody (intent: InvestigatorIntent) : string =
    investigatorPromptBody intent + "4. Return a structured report with relatedFiles and relatedCode.\n\n"

let private meditatorBody (intent: string) (files: string list) : string =
    meditatorPromptBody intent files + "3. Return a structured report with relatedFiles and relatedCode.\n\n"

let private browserBody (intent: string) : string =
    browserPromptBody intent + "3. Return a clear summary of what you found or did.\n\n"

let private executorBody (output: string) : string =
    executorSummarizerPromptBody output + "\n4. Return a concise, actionable summary.\n\n"

let private websearchBody (question: string) (raw: string) : string =
    websearchSummarizerPromptBody question raw + "\n5. Return a focused, ready-to-use answer.\n\n"

/// Produce one prompt per parallel intent for coder/investigator, exactly one
/// prompt for the singleton task kinds.  Host decides whether to append the
/// agent_report tail; otherwise prompt body is identical between hosts.
let formatPrompt (host: Host) (kind: SubagentTaskKind) : string list =
    match kind with
    | Coder intents -> intents |> List.map (coderBody >> withReportTail host)
    | Investigator intents -> intents |> List.map (investigatorBody >> withReportTail host)
    | Meditator(intent, sections) ->
        let files = sections |> List.map (fun s -> s.file)
        let prelude =
            sections
            |> List.map (fun s ->
                let body = Option.defaultValue "(skipped)" s.content
                $"=== {s.file} ===\n\n{body}")
            |> String.concat "\n\n"
        let instructions = withReportTail host (meditatorBody intent files)
        let combined = if prelude = "" then instructions else $"{prelude}\n\n{instructions}"
        [ combined ]
    | Browser intent -> [ withReportTail host (browserBody intent) ]
    | ExecutorSummary output -> [ withReportTail host (executorBody output) ]
    | WebsearchSummary(question, raw) -> [ withReportTail host (websearchBody question raw) ]

let reportSeparator = "\n---\n"

/// Trim each report and join with the canonical separator.  Empty reports are
/// preserved as empty entries — callers decide whether to filter beforehand.
let joinReports (reports: string seq) : string =
    reports |> Seq.map (fun r -> r.Trim()) |> String.concat reportSeparator
