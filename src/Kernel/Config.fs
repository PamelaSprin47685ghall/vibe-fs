module VibeFs.Kernel.Config

open VibeFs.Kernel.HostTools

let private repo = "https://github.com/vibheksoni/stealth-browser-mcp.git"

let stealthBrowserMcpRef (envValue: string) : string = if envValue = "" then "master" else envValue
let getStealthBrowserMcpCommand (envValue: string) : string = $"uvx --python 3.13 --from git+{repo}@{stealthBrowserMcpRef envValue} python -m server"
let getStealthBrowserMcpLocalConfig (envValue: string) : {| ``type``: string; command: string array |} = {| ``type`` = "local"; command = [| "uvx"; "--python"; "3.13"; "--from"; $"git+{repo}@{stealthBrowserMcpRef envValue}"; "python"; "-m"; "server" |] |}

type Agent = string
type Tool = string

let private knownAgents = [ "manager"; "investigator"; "coder"; "reviewer"; "browser"; "meditator"; "executor"; "bookkeeper" ]

/// `true` when `tool` contains any of `subs` as a substring. The substring
/// matching is load-bearing: host / MCP tool names vary by prefix and suffix
/// (e.g. `stealth-browser-mcp_navigate`, `bash_run`, `return_reviewer`,
/// `ast_edit`), so an exact-equality table cannot replace it.
let private toolContainsAny (tool: Tool) (subs: string list) : bool =
    subs |> List.exists tool.Contains

/// The permission decision as an ordered rule list. Precedence is the domain
/// knowledge — the first matching rule wins — and each rule is named so a
/// reader can follow "why may agent X use tool Y" without mentally simulating
/// nested `when` guards (REFACTOR.md §1 D8).
let canUseCanonical (agent: Agent) (tool: Tool) : bool =
    if tool = "fetch_wiki" then agent = "manager"
    elif tool = "submit_wiki" then agent = "bookkeeper"
    elif toolContainsAny tool [ "agent_report" ] then true
    elif agent = "bookkeeper" then false
    elif toolContainsAny tool [ "bash"; "task" ] || tool = "grep" then false
    elif toolContainsAny tool [ "stealth" ] then agent = "browser"
    elif toolContainsAny tool [ "return" ] then toolContainsAny tool [ agent ]
    elif agent = "meditator" || agent = "executor" then false
    elif tool = "read" then true
    elif agent = "reviewer" || agent = "browser" then false
    elif agent = "investigator" && toolContainsAny tool [ "executor" ] then true
    elif toolContainsAny tool knownAgents
         || toolContainsAny tool [ "todo"; "question"; "web"; "skill" ]
         || tool = "submit_review" then agent <> "investigator" && agent <> "coder"
    elif toolContainsAny tool [ "write"; "edit"; "patch" ] then agent <> "investigator" && agent <> "manager"
    elif agent = "manager" then tool <> "fuzzy_grep"
    else true

let canUseForHost (host: Host) (agent: Agent) (tool: Tool) : bool =
    canUseCanonical agent (normalizeToolName host tool)

let canUse (agent: Agent) (tool: Tool) : bool = canUseForHost opencode agent tool

let deniedToolsForHost (host: Host) (agent: Agent) (tools: Tool seq) : Tool list =
    tools |> Seq.filter (fun tool -> not (canUseForHost host agent tool)) |> Seq.toList

let deniedTools (agent: Agent) (tools: Tool seq) : Tool list = deniedToolsForHost opencode agent tools
