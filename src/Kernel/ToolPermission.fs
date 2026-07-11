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
    | SearchFamily
    | SubagentWebSkillOrSubmit
    | PtyFamily
    | Other

let private knownAgentSet =
    Set.ofList
        [ "manager"
          "investigator"
          "coder"
          "reviewer"
          "browser"
          "meditator"
          "executor" ]

let private blockedShellTaskGrepSet =
    Set.ofList
        [ "bash"
          "bash_run"
          "task"
          "grep"
          "plan"
          "memory"
          "propose_plan"
          "set_goal"
          "get_goal"
          "complete_goal" ]

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
    || t = "continue"

let classifyTool (host: Host) (tool: Tool) : ToolSemantic =
    let t = normalizeToolName host tool

    if t = "agent_report" then
        AgentReport
    elif Set.contains t blockedShellTaskGrepSet then
        BlockedShellTaskGrep
    elif isStealthBrowserTool t then
        StealthBrowser
    elif t.StartsWith "return_" then
        ReturnRoleEcho
    elif Set.contains t todoFamilySet then
        TodoFamily
    elif t.StartsWith methodologyPrefix || t = "methodology" then
        MethodologyFamily
    elif t = "read" then
        Read
    elif isDispatchOrWebSkillTool t then
        SubagentWebSkillOrSubmit
    elif Set.contains t writePatchFamilySet then
        WritePatchFamily
    elif t = "fuzzy_grep" then
        FuzzyGrep
    elif t = "fuzzy_find" || t = "fuzzy_continue" || t = "glob" || t = "grep_x" then
        SearchFamily
    elif t.StartsWith "pty_" then
        PtyFamily
    else
        Other

let canUseSemantic (agent: Agent) (semantic: ToolSemantic) (tool: Tool) : bool =

    match agent, semantic with
    | _, AgentReport -> true
    | _, BlockedShellTaskGrep -> false
    | _, StealthBrowser -> agent = "browser"
    | _, ReturnRoleEcho -> tool = sprintf "return_%s" agent
    | _, PtyFamily ->
        not (Set.contains agent knownAgentSet)
        || agent = "investigator"
        || agent = "manager"
    | "meditator", Read -> true
    | "meditator", SubagentWebSkillOrSubmit when tool = "investigator" -> true
    | "meditator", _
    | "executor", _ -> false
    | _, TodoFamily
    | _, MethodologyFamily -> not (shouldExcludeAgentFromProjection agent false)
    | _, Read -> true
    | "browser", _ -> false
    | "reviewer", SubagentWebSkillOrSubmit when tool = "investigator" -> true
    | "reviewer", SubagentWebSkillOrSubmit -> false
    | "reviewer", WritePatchFamily -> false
    | "reviewer", _ -> true
    | "investigator", SubagentWebSkillOrSubmit when tool = "executor" -> true
    | "coder", SubagentWebSkillOrSubmit when tool = "investigator" -> true
    | _, SubagentWebSkillOrSubmit -> agent <> "investigator" && agent <> "coder" && agent <> "reviewer"
    | _, WritePatchFamily -> agent <> "investigator" && agent <> "manager" && agent <> "reviewer"
    | "manager", FuzzyGrep -> false
    | _, FuzzyGrep -> agent <> "manager"
    | _, SearchFamily -> agent <> "browser" && agent <> "meditator" && agent <> "executor"
    | _, Other -> false

let canUseForHost (host: Host) (agent: Agent) (tool: Tool) : bool =
    let normalized = normalizeToolName host tool
    canUseSemantic agent (classifyTool host tool) normalized

let canUse (agent: Agent) (tool: Tool) : bool = canUseForHost opencode agent tool

let deniedToolsForHost (host: Host) (agent: Agent) (tools: Tool seq) : Tool list =
    tools
    |> Seq.filter (fun tool -> not (canUseForHost host agent tool))
    |> Seq.toList

let deniedTools (agent: Agent) (tools: Tool seq) : Tool list = deniedToolsForHost opencode agent tools
