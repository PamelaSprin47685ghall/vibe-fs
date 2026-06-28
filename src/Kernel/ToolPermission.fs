module Wanxiangshu.Kernel.ToolPermission

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.MessageTransformPolicy

type Agent = string
type Tool = string

type ToolSemantic =
    | AgentReport
    | BlockedShellTaskGrep
    | StealthBrowser
    | ReturnRoleEcho
    | TodoFamily
    | MethodologyFamily
    | Read
    | WritePatchFamily
    | FuzzyGrep
    | SubagentWebSkillOrSubmit
    | Other

let private knownAgentSet =
    Set.ofList [ "manager"; "investigator"; "coder"; "reviewer"; "browser"; "meditator"; "executor" ]

let private blockedShellTaskGrepSet =
    Set.ofList [ "bash"; "bash_run"; "task"; "grep"; "plan"; "memory"; "propose_plan"; "set_goal"; "get_goal"; "complete_goal" ]

let private todoFamilySet =
    Set.ofList [ "todowrite"; "todo_write"; "todo"; "manage_todo_list" ]

let private methodologyPrefix = "methodology_"

let private writePatchFamilySet =
    Set.ofList [ "write"; "edit"; "patch"; "apply_patch"; "ast_edit" ]

let private isStealthBrowserTool (t: string) = t.StartsWith "stealth-browser"

let private isDispatchOrWebSkillTool (t: string) =
    Set.contains t knownAgentSet
    || t = "websearch"
    || t = "webfetch"
    || t = "question"
    || t = "ask_user_question"
    || t = "skill"
    || t = "submit_review"

let classifyTool (host: Host) (tool: Tool) : ToolSemantic =
    let t = normalizeToolName host tool

    if t = "agent_report" then AgentReport
    elif Set.contains t blockedShellTaskGrepSet then BlockedShellTaskGrep
    elif isStealthBrowserTool t then StealthBrowser
    elif t.StartsWith "return_" then ReturnRoleEcho
    elif Set.contains t todoFamilySet then TodoFamily
    elif t.StartsWith methodologyPrefix then MethodologyFamily
    elif t = "read" then Read
    elif isDispatchOrWebSkillTool t then SubagentWebSkillOrSubmit
    elif Set.contains t writePatchFamilySet then WritePatchFamily
    elif t = "fuzzy_grep" then FuzzyGrep
    else Other

let canUseSemantic (agent: Agent) (semantic: ToolSemantic) (tool: Tool) : bool =

    match agent, semantic with
    | _, AgentReport -> true
    | _, BlockedShellTaskGrep -> false
    | _, StealthBrowser -> agent = "browser"
    | _, ReturnRoleEcho -> tool = sprintf "return_%s" agent
    | "meditator", Read -> true
    | "meditator", _
    | "executor", _ -> false
    | _, TodoFamily
    | _, MethodologyFamily -> not (shouldExcludeAgentFromProjection agent false)
    | _, Read -> true
    | "reviewer", _
    | "browser", _ -> false
    | "investigator", SubagentWebSkillOrSubmit when tool = "executor" -> true
    | "coder", SubagentWebSkillOrSubmit when tool = "investigator" -> true
    | _, SubagentWebSkillOrSubmit -> agent <> "investigator" && agent <> "coder"
    | _, WritePatchFamily -> agent <> "investigator" && agent <> "manager"
    | "manager", FuzzyGrep -> false
    | _, _ -> true

let canUseForHost (host: Host) (agent: Agent) (tool: Tool) : bool =
    let normalized = normalizeToolName host tool
    canUseSemantic agent (classifyTool host tool) normalized

let canUse (agent: Agent) (tool: Tool) : bool = canUseForHost opencode agent tool

let deniedToolsForHost (host: Host) (agent: Agent) (tools: Tool seq) : Tool list =
    tools |> Seq.filter (fun tool -> not (canUseForHost host agent tool)) |> Seq.toList

let deniedTools (agent: Agent) (tools: Tool seq) : Tool list = deniedToolsForHost opencode agent tools