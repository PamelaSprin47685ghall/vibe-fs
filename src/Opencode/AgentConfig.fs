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

type private BuiltinAgentSpec =
    { name: string
      defaultMode: string
      systemPrompt: string
      defaultMcps: string array }

let private defaultPrimaryAliases = [ "manager"; "build"; "plan" ]

let private builtinAgentSpecs =
    [ { name = "manager"; defaultMode = "primary"; systemPrompt = Prompts.managerSystemPrompt; defaultMcps = [||] }
      { name = "build"; defaultMode = "primary"; systemPrompt = ""; defaultMcps = [||] }
      { name = "plan"; defaultMode = "primary"; systemPrompt = ""; defaultMcps = [||] }
      { name = "coder"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "reader"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "meditator"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "reviewer"; defaultMode = "subagent"; systemPrompt = Prompts.reviewInstructions; defaultMcps = [||] }
      { name = "browser"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [| "stealth-browser-mcp" |] }
      { name = "executor"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] } ]

let private builtinAgentByName = builtinAgentSpecs |> List.map (fun spec -> spec.name, spec) |> Map.ofList

/// All tool names the plugin registers plus common host tool patterns that
/// canUse has opinions about — kept in sync with Tools.createTools.
let private allToolNames =
    [ "coder"; "reader"; "meditator"; "browser"; "executor"
      "fuzzy-find"; "fuzzy-grep"; "websearch"; "webfetch"
      "submit_review"; "return-reviewer"; "read"; "write"
      "bash"; "task"; "grep"; "edit"; "patch"; "apply_patch"
      "todowrite"; "todo_write"; "stealth-browser-mcp_*"
      "question"; "ask_user_question"; "agent_report" ]

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

let private withRoleDefaults (name: string) (userAgent: obj) : obj =
    let spec = Map.tryFind name builtinAgentByName
    let userPrompt = Dyn.str userAgent "prompt"
    let prompt =
        if userPrompt <> "" then userPrompt
        else spec |> Option.map (fun value -> value.systemPrompt) |> Option.defaultValue ""
    let userMode = Dyn.str userAgent "mode"
    let mode =
        if userMode <> "" then userMode
        else spec |> Option.map (fun value -> value.defaultMode) |> Option.defaultValue "subagent"
    let primaryDefaultMode = if defaultPrimaryAliases |> List.contains name then "primary" else "subagent"
    let effectiveMode = if mode <> "" then mode else primaryDefaultMode
    let userPerm = Dyn.get userAgent "permission"
    let userTools = Dyn.get userAgent "tools"
    let userMcps = Dyn.get userAgent "mcps"
    let mcps =
        if Dyn.isNullish userMcps then
            spec
            |> Option.map (fun value -> if value.defaultMcps.Length = 0 then emptyMcps else box value.defaultMcps)
            |> Option.defaultValue emptyMcps
        else
            userMcps

    let perm = mergeObj (permissionDefaults name) userPerm
    let tools = mergeObj (toolDefaults name) userTools
    let result = mergeObj (emptyObj ()) userAgent
    setKey result "prompt" (box prompt)
    setKey result "mode" (box effectiveMode)
    setKey result "permission" perm
    setKey result "tools" tools
    setKey result "mcps" mcps
    result

let private objectKeys (o: obj) : string array =
    JS.Constructors.Object.keys(o) |> Seq.toArray

let applyAgentConfig (opencodeConfig: obj) (mcps: obj) : obj =
    let userAgent = if Dyn.isNullish (Dyn.get opencodeConfig "agent") then emptyObj () else Dyn.get opencodeConfig "agent"
    let configMcp = Dyn.get opencodeConfig "mcp"
    let mergedMcp = if Dyn.isNullish configMcp then mcps else mergeObj configMcp mcps
    let agents = mergeObj userAgent (emptyObj ())
    for name in builtinAgentSpecs |> List.map (fun spec -> spec.name) do
        if Dyn.isNullish (Dyn.get agents name) then setKey agents name (emptyObj ())
    let finalAgents = emptyObj ()
    for name in objectKeys agents do
        let ua = Dyn.get agents name
        let uaObj = if Dyn.isNullish ua then emptyObj () else ua
        setKey finalAgents name (withRoleDefaults name uaObj)
    mergeObj opencodeConfig (box {| agent = finalAgents; mcp = mergedMcp |})
