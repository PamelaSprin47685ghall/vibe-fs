module VibeFs.Kernel.HostKernel

open VibeFs.Kernel.Prompts

let private stealthBrowserMcpRepo = "https://github.com/vibheksoni/stealth-browser-mcp.git"

let stealthBrowserMcpRef (envValue: string) : string =
    if envValue = "" then "master" else envValue

let getStealthBrowserMcpCommand (envValue: string) : string =
    $"uvx --python 3.13 --from git+{stealthBrowserMcpRepo}@{stealthBrowserMcpRef envValue} python -m server"

let getStealthBrowserMcpLocalConfig (envValue: string) : {| ``type``: string; command: string array |} =
    {| ``type`` = "local"
       command =
        [| "uvx"; "--python"; "3.13"; "--from"
           $"git+{stealthBrowserMcpRepo}@{stealthBrowserMcpRef envValue}"; "python"; "-m"; "server" |] |}

let subagentReportSeparator = "\n---\n"

/// One section of file content included in a meditator prompt.
type MeditatorFileSection = { file: string; content: string option }

type private PromptSection =
    | FileSection of fileName: string * body: string
    | InstructionSection of body: string

let private renderPromptSection = function
    | FileSection(fileName, body) -> $"=== {fileName} ===\n\n{body}"
    | InstructionSection body -> body

/// Build the quiet-room prompt for a meditator subagent from file sections.
let buildMeditatorPrompt (sections: MeditatorFileSection list) (intent: string) : string =
    let skipped = "(skipped)"
    let promptSections =
        sections
        |> List.map (fun section -> FileSection(section.file, Option.defaultValue skipped section.content))
    let files = sections |> List.map (fun s -> s.file)
    let allSections = promptSections @ [ InstructionSection(formatMeditatorUserPrompt intent files) ]
    allSections |> List.map renderPromptSection |> String.concat "\n\n"
