module VibeFs.Kernel.HostKernel

open VibeFs.Kernel.Prompts

let subagentReportSeparator = "\n---\n"

/// One section of file content included in a meditator prompt.
type MeditatorFileSection = { file: string; content: string option }

/// Build the quiet-room prompt for a meditator subagent from file sections.
let buildMeditatorPrompt (sections: MeditatorFileSection list) (intent: string) : string =
    let skipped = "(skipped)"
    let rendered =
        sections
        |> List.map (fun s -> $"=== {s.file} ===\n\n{Option.defaultValue skipped s.content}")
    let body = rendered |> String.concat "\n\n"
    let files = sections |> List.map (fun s -> s.file)
    let basePrompt = formatMeditatorUserPrompt intent files
    if body = "" then basePrompt else $"{body}\n\n{basePrompt}"
