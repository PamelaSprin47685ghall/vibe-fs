module VibeFs.Kernel.Config

open VibeFs.Kernel.HostTools

let private repo = "https://github.com/vibheksoni/stealth-browser-mcp.git"

let stealthBrowserMcpRef (envValue: string) : string = if envValue = "" then "master" else envValue
let getStealthBrowserMcpCommand (envValue: string) : string = $"uvx --python 3.13 --from git+{repo}@{stealthBrowserMcpRef envValue} python -m server"
let getStealthBrowserMcpLocalConfig (envValue: string) : {| ``type``: string; command: string array |} = {| ``type`` = "local"; command = [| "uvx"; "--python"; "3.13"; "--from"; $"git+{repo}@{stealthBrowserMcpRef envValue}"; "python"; "-m"; "server" |] |}

type Agent = string
type Tool = string

let private knownAgents = [ "manager"; "reader"; "coder"; "reviewer"; "browser"; "meditator"; "executor" ]

let private canUseCanonical (agent: Agent) (tool: Tool) : bool =
    let toolHas (subs: string list) = subs |> List.exists tool.Contains
    match agent with
    | _ when toolHas [ "agent_report" ] -> true
    | _ when toolHas [ "bash"; "task" ] || tool = "grep" -> false
    | _ when toolHas [ "stealth" ] -> agent = "browser"
    | _ when toolHas [ "return" ] -> toolHas [ agent ]
    | "meditator" | "executor" -> false
    | _ when tool = "read" -> true
    | "reviewer" | "browser" -> false
    | "reader" when toolHas [ "executor" ] -> true
    | _ when toolHas knownAgents || toolHas [ "todo"; "question"; "web"; "skill" ] || tool = "submit_review" -> agent <> "reader" && agent <> "coder"
    | _ when toolHas [ "write"; "edit"; "patch" ] -> agent <> "reader" && agent <> "manager"
    | "manager" -> tool <> "fuzzy_grep"
    | _ -> true

let canUseForHost (host: Host) (agent: Agent) (tool: Tool) : bool =
    canUseCanonical agent (normalizeToolName host tool)

let canUse (agent: Agent) (tool: Tool) : bool = canUseForHost opencode agent tool

let deniedToolsForHost (host: Host) (agent: Agent) (tools: Tool seq) : Tool list =
    tools |> Seq.filter (fun tool -> not (canUseForHost host agent tool)) |> Seq.toList

let deniedTools (agent: Agent) (tools: Tool seq) : Tool list = deniedToolsForHost opencode agent tools
