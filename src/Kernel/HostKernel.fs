module VibeFs.Kernel.HostKernel

open VibeFs.Kernel.AgentPolicy
open VibeFs.Kernel.AgentRole

/// The disabled-tool list a host must enforce when delegating to a role.
type SubagentToolPolicy = { disabledTools: string list }

let subagentToolPolicy (role: AgentRole) : SubagentToolPolicy =
    { disabledTools = (effectivePolicy role).deniedTools }

/// A subagent delegation request: pure data the host interprets.
type SubagentRequest =
    { role: AgentRole; prompt: string; title: string }

/// Format an editor intent together with the files it should touch.
let formatEditorIntent (intent: string) (affectedFiles: string list) : string =
    let fileList = affectedFiles |> List.map (fun f -> $"- {f}") |> String.concat "\n"
    $"Intent: {intent}\n\nAffected files:\n{fileList}"

let subagentReportSeparator = "\n---\n"

/// Build the quiet-room prompt for a reverie subagent from file sections.
type ReverieFileSection = { file: string; content: string option }

let buildReveriePrompt (sections: ReverieFileSection list) (intent: string) : string =
    let skipped = "(skipped)"
    let rendered =
        sections
        |> List.map (fun s -> $"=== {s.file} ===\n\n{Option.defaultValue skipped s.content}")
    let body = rendered |> String.concat "\n"
    $"{body}\nQuestion:\n{intent}"
