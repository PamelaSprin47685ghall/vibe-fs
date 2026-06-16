module VibeFs.Opencode.AgentConfig

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.ToolPolicy

let private emptyObj () : obj = createObj []
let private setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v
let private mergeObj (a: obj) (b: obj) : obj =
    let result = emptyObj ()
    Dyn.assignInto result a |> ignore
    Dyn.assignInto result b |> ignore
    result

let private emptyMcps : obj = [||] :> obj

/// All tool names the plugin registers plus common host tool patterns that
/// canUse has opinions about — kept in sync with Tools.createTools.
let private allToolNames =
    [ "coder"; "reader"; "meditator"; "browser"; "executor"
      "fuzzy-find"; "fuzzy-grep"; "websearch"; "webfetch"
      "submit_review"; "return-reviewer"; "read"; "write"
      "bash"; "task"; "grep"; "edit"; "patch"; "apply_patch"
      "todowrite"; "todo_write"; "stealth-browser-mcp_*"
      "ask_user_question"; "agent_report" ]

/// Build {tool: bool} map from canUse so denied tools never appear available.
let private toolDefaults (agentName: string) : obj =
    let o = emptyObj ()
    for name in allToolNames do
        setKey o name (box (canUse agentName name))
    o

/// Build {tool: "allow"|"deny"} map from canUse.
let private permissionDefaults (agentName: string) : obj =
    let o = emptyObj ()
    for name in allToolNames do
        setKey o name (box (if canUse agentName name then "allow" else "deny"))
    o

let private systemPromptForBuiltin (name: string) : string =
    match name with
    | "reviewer" -> Prompts.reviewInstructions
    | _ -> ""

let private withRoleDefaults (name: string) (userAgent: obj) : obj =
    let userPrompt = Dyn.str userAgent "prompt"
    let prompt = if userPrompt <> "" then userPrompt else systemPromptForBuiltin name
    let defaultMode = if name = "manager" then "primary" else "subagent"
    let userMode = Dyn.str userAgent "mode"
    let mode = if userMode <> "" then userMode else defaultMode
    let userPerm = Dyn.get userAgent "permission"
    let userTools = Dyn.get userAgent "tools"
    let userMcps = Dyn.get userAgent "mcps"
    let mcps =
        match name with
        | "browser" -> if Dyn.isNullish userMcps then box [| "stealth-browser-mcp" |] else userMcps
        | _ -> if Dyn.isNullish userMcps then emptyMcps else userMcps

    let perm = mergeObj (permissionDefaults name) userPerm
    let tools = mergeObj (toolDefaults name) userTools
    let result = mergeObj (emptyObj ()) userAgent
    setKey result "prompt" (box prompt)
    setKey result "mode" (box mode)
    setKey result "permission" perm
    setKey result "tools" tools
    setKey result "mcps" mcps
    result

let private objectKeys (o: obj) : string array =
    JS.Constructors.Object.keys(o) |> Seq.toArray

let private builtinAgents = [ "coder"; "reader"; "meditator"; "reviewer"; "browser"; "executor" ]

let applyAgentConfig (opencodeConfig: obj) (mcps: obj) : obj =
    let userAgent = if Dyn.isNullish (Dyn.get opencodeConfig "agent") then emptyObj () else Dyn.get opencodeConfig "agent"
    let configMcp = Dyn.get opencodeConfig "mcp"
    let mergedMcp = if Dyn.isNullish configMcp then mcps else mergeObj configMcp mcps
    let agents = mergeObj userAgent (emptyObj ())
    for name in builtinAgents do
        if Dyn.isNullish (Dyn.get agents name) then setKey agents name (emptyObj ())
    if Dyn.isNullish (Dyn.get agents "manager") then setKey agents "manager" (emptyObj ())
    let finalAgents = emptyObj ()
    for name in objectKeys agents do
        if name = "basher" || name = "runner" then ()
        else
            let ua = Dyn.get agents name
            let uaObj = if Dyn.isNullish ua then emptyObj () else ua
            setKey finalAgents name (withRoleDefaults name uaObj)
    mergeObj opencodeConfig (box {| agent = finalAgents; mcp = mergedMcp |})
