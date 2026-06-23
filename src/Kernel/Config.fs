module VibeFs.Kernel.Config

open VibeFs.Kernel.HostTools

let private repo = "https://github.com/vibheksoni/stealth-browser-mcp.git"

let stealthBrowserMcpRef (envValue: string) : string = if envValue = "" then "master" else envValue
let getStealthBrowserMcpCommand (envValue: string) : string = $"uvx --python 3.13 --from git+{repo}@{stealthBrowserMcpRef envValue} python -m server"
let getStealthBrowserMcpLocalConfig (envValue: string) : {| ``type``: string; command: string array |} = {| ``type`` = "local"; command = [| "uvx"; "--python"; "3.13"; "--from"; $"git+{repo}@{stealthBrowserMcpRef envValue}"; "python"; "-m"; "server" |] |}

type Agent = string
type Tool = string

let private knownAgents = [ "manager"; "investigator"; "coder"; "reviewer"; "browser"; "meditator"; "executor"; "bookkeeper" ]

/// The permission decision as an ordered pattern-match over (agent, tool).
/// First matching clause wins. The `when` guards express the substring rules
/// that host / MCP tool naming makes load-bearing (e.g. `stealth-browser-mcp_*`,
/// `bash_run`, `return_<role>`), so an exact-equality table cannot replace them
/// (REFACTOR.md §1 D8).
let canUseCanonical (agent: Agent) (tool: Tool) : bool =
    let toolMatches (subs: string list) = subs |> List.exists tool.Contains
    match agent, tool with
    | _, _ when toolMatches [ "agent_report" ] -> true
    | _, _ when toolMatches [ "bash"; "task" ] || tool = "grep" -> false
    | _, _ when toolMatches [ "stealth" ] -> agent = "browser"
    | _, _ when toolMatches [ "return" ] -> toolMatches [ agent ]
    | "bookkeeper", _ -> false
    | "meditator", _ | "executor", _ -> false
    | _, "read" -> true
    | _, "select_methodology" -> agent = "manager"
    | "reviewer", _ | "browser", _ -> false
    | "investigator", _ when toolMatches [ "executor" ] -> true
    | _, _ when toolMatches knownAgents
                     || toolMatches [ "todo"; "question"; "web"; "skill" ]
                     || tool = "submit_review" -> agent <> "investigator" && agent <> "coder"
    | _, _ when toolMatches [ "write"; "edit"; "patch" ] -> agent <> "investigator" && agent <> "manager"
    | "manager", _ -> tool <> "fuzzy_grep"
    | _, _ -> true

let canUseForHost (host: Host) (agent: Agent) (tool: Tool) : bool =
    canUseCanonical agent (normalizeToolName host tool)

let canUse (agent: Agent) (tool: Tool) : bool = canUseForHost opencode agent tool

let deniedToolsForHost (host: Host) (agent: Agent) (tools: Tool seq) : Tool list =
    tools |> Seq.filter (fun tool -> not (canUseForHost host agent tool)) |> Seq.toList

let deniedTools (agent: Agent) (tools: Tool seq) : Tool list = deniedToolsForHost opencode agent tools
