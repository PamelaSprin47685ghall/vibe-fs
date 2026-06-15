module VibeFs.Kernel.HostKernel

open VibeFs.Kernel.AgentPolicy
open VibeFs.Kernel.AgentRole
open VibeFs.Kernel.MuxPolicy
open VibeFs.Kernel.Prompts

/// The disabled-tool list a host must enforce when delegating to a role.
type SubagentToolPolicy = { disabledTools: string list }

let subagentToolPolicy (role: AgentRole) : SubagentToolPolicy =
    { disabledTools = (effectivePolicy role).deniedTools }

let resolveMuxToolPolicy (toolName: string) : SubagentToolPolicy =
    { disabledTools = expandPatterns (disabledToolsFor toolName) }

/// A subagent delegation request: pure data the host interprets.
type SubagentRequest =
    { role: AgentRole; prompt: string; title: string }

/// Format an editor intent together with the files it should touch.
let formatEditorIntent (intent: string) (affectedFiles: string list) : string =
    formatEditorUserPrompt intent affectedFiles

let subagentReportSeparator = "\n---\n"

/// One section of file content included in a reverie prompt.
type ReverieFileSection = { file: string; content: string option }

/// Build the quiet-room prompt for a reverie subagent from file sections.
let buildReveriePrompt (sections: ReverieFileSection list) (intent: string) : string =
    let skipped = "(skipped)"
    let rendered =
        sections
        |> List.map (fun s -> $"=== {s.file} ===\n\n{Option.defaultValue skipped s.content}")
    let body = rendered |> String.concat "\n\n"
    let files = sections |> List.map (fun s -> s.file)
    let basePrompt = formatReverieUserPrompt intent files
    if body = "" then basePrompt else $"{body}\n\n{basePrompt}"
