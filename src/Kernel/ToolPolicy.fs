module VibeFs.Kernel.ToolPolicy

type Agent = string
type Tool = string

let private knownAgents =
    [ "manager"; "reader"; "coder"; "reviewer"; "browser"; "meditator"; "executor" ]

/// The single truth function: given an agent name and a tool name (both raw
/// host-level strings, no normalization), decide whether the agent may use
/// the tool.  Unknown agents behave like `build` — equivalent to
/// `manager + coder + reader`.
let canUse (agent: Agent) (tool: Tool) : bool =
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
    | _ when toolHas knownAgents || toolHas [ "todo"; "question"; "web" ] ->
        agent <> "reader" && agent <> "coder"
    | _ when toolHas [ "write"; "edit"; "patch" ] ->
        agent <> "reader" && agent <> "manager"
    | "manager" -> tool <> "fuzzy-grep"
    | _ -> true

/// Return the subset of `tools` that `agent` is NOT allowed to use.
let deniedTools (agent: Agent) (tools: Tool seq) : Tool list =
    tools |> Seq.filter (fun t -> not (canUse agent t)) |> Seq.toList
