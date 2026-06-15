module VibeFs.Opencode.AgentConfig

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.AgentRole
open VibeFs.Kernel.Permission
open VibeFs.Kernel.AgentPolicy

[<Emit("{}")>]
let private emptyObj () : obj = jsNative
[<Emit("$0[$1] = $2")>]
let private setKey (o: obj) (k: string) (v: obj) : unit = jsNative
[<Emit("Object.assign({}, $0, $1)")>]
let private mergeObj (a: obj) (b: obj) : obj = jsNative

let private emptyMcps : obj = [||] :> obj

/// Build a plain-JS permission map {name: "allow"|"deny"} from a role's defaults.
let private permissionDefaults (role: AgentRole) : obj =
    let o = emptyObj ()
    defaultPermissions role |> Map.iter (fun name p ->
        setKey o name (box (match p with Allow -> "allow" | Deny -> "deny")))
    o

/// Build a plain-JS tool map {name: bool} from a role's tool policy.
let toolDefaults (role: AgentRole) : obj =
    let o = emptyObj ()
    toolMapFor role |> Map.iter (fun name p -> setKey o name (box (p = Allow)))
    o

/// Built-in subagent system prompts are intentionally empty; role instructions live in user/tool prompts.
let private systemPromptForBuiltin (name: string) : string =
    match name with
    | "editor" | "greper" | "reverie" | "browser" | "summarizer" | "orchestrator" -> ""
    | "reviewer" -> Prompts.reviewInstructions
    | _ -> ""

let private browserPermissionBase (o: obj) : unit =
    setKey o "read" (box "allow")
    setKey o "stealth-browser-mcp_*" (box "allow")
    setKey o "bash" (box "deny")
    setKey o "write" (box "deny")
    setKey o "edit" (box "deny")
    setKey o "glob" (box "deny")
    setKey o "grep" (box "deny")
    setKey o "fuzzy_find" (box "deny")
    setKey o "fuzzy_grep" (box "deny")
    setKey o "task" (box "deny")

let private browserToolsBase (o: obj) : unit =
    setKey o "read" (box true)
    setKey o "stealth-browser-mcp_*" (box true)

let private summarizerPermissionBase (o: obj) : unit =
    setKey o "edit" (box "deny")
    setKey o "write" (box "deny")
    setKey o "glob" (box "deny")
    setKey o "grep" (box "deny")
    setKey o "fuzzy_find" (box "deny")
    setKey o "fuzzy_grep" (box "deny")
    setKey o "task" (box "deny")
    setKey o "read" (box "deny")
    setKey o "executor" (box "deny")
    setKey o "webfetch" (box "deny")
    setKey o "websearch" (box "deny")
    setKey o "browser" (box "deny")
    setKey o "bash" (box "deny")

let private summarizerToolsBase (o: obj) : unit =
    setKey o "agent_report" (box true)
    setKey o "read" (box false)
    setKey o "write" (box false)
    setKey o "edit" (box false)
    setKey o "glob" (box false)
    setKey o "grep" (box false)
    setKey o "fuzzy_find" (box false)
    setKey o "fuzzy_grep" (box false)
    setKey o "webfetch" (box false)
    setKey o "websearch" (box false)
    setKey o "browser" (box false)
    setKey o "executor" (box false)
    setKey o "task" (box false)

let private withBase (init: obj -> unit) : obj =
    let o = emptyObj ()
    init o
    o

/// Apply role permission/tool defaults to a single agent entry, preserving any
/// user-provided fields (disable, model, etc.).  User values override defaults.
let private withRoleDefaults (name: string) (userAgent: obj) : obj =
    let roleResult = AgentRole.ofString name
    let role = match roleResult with Ok r -> r | Error _ -> Reverie
    let userPrompt = Dyn.str userAgent "prompt"
    let prompt = if userPrompt <> "" then userPrompt else systemPromptForBuiltin name
    let defaultMode = if name = "orchestrator" then "primary" else "subagent"
    let userMode = Dyn.str userAgent "mode"
    let mode = if userMode <> "" then userMode else defaultMode
    let userPerm = Dyn.get userAgent "permission"
    let userTools = Dyn.get userAgent "tools"
    let userMcps = Dyn.get userAgent "mcps"

    let perm, tools, mcps =
        match name with
        | "browser" ->
            let p = mergeObj (permissionDefaults role) (mergeObj (withBase browserPermissionBase) userPerm)
            let t = mergeObj (toolDefaults role) (mergeObj (withBase browserToolsBase) userTools)
            let m = if Dyn.isNullish userMcps then box [| "stealth-browser-mcp" |] else userMcps
            p, t, m
        | "summarizer" ->
            let p = mergeObj (permissionDefaults role) (mergeObj (withBase summarizerPermissionBase) userPerm)
            let t = mergeObj (toolDefaults role) (mergeObj (withBase summarizerToolsBase) userTools)
            let m = if Dyn.isNullish userMcps then emptyMcps else userMcps
            p, t, m
        | _ ->
            let tools =
                match roleResult with
                | Ok _ -> mergeObj (toolDefaults role) userTools
                | Error _ -> if Dyn.isNullish userTools then emptyObj () else userTools
            mergeObj (permissionDefaults role) userPerm,
            tools,
            if Dyn.isNullish userMcps then emptyMcps else userMcps

    let result = mergeObj (emptyObj ()) userAgent
    setKey result "prompt" (box prompt)
    setKey result "mode" (box mode)
    setKey result "permission" perm
    setKey result "tools" tools
    setKey result "mcps" mcps
    result

[<Emit("Object.keys($0)")>]
let private objectKeys (o: obj) : string array = jsNative

let private builtinAgents = [ "editor"; "greper"; "reverie"; "reviewer"; "browser"; "summarizer" ]

/// Apply agent config: merge permission/tool defaults for every agent, add
/// built-in subagents, preserve all user-defined agents, merge mcp servers.
/// Returns the updated config object.
let applyAgentConfig (opencodeConfig: obj) (mcps: obj) : obj =
    let userAgent = if Dyn.isNullish (Dyn.get opencodeConfig "agent") then emptyObj () else Dyn.get opencodeConfig "agent"
    let configMcp = Dyn.get opencodeConfig "mcp"
    let mergedMcp = if Dyn.isNullish configMcp then mcps else mergeObj configMcp mcps
    // Start from the user's existing agents so non-builtin entries (plan, title,
    // etc.) are preserved, then ensure every agent gets role defaults applied.
    let agents = mergeObj userAgent (emptyObj ())
    for name in builtinAgents do
        if Dyn.isNullish (Dyn.get agents name) then setKey agents name (emptyObj ())
    if Dyn.isNullish (Dyn.get agents "orchestrator") then setKey agents "orchestrator" (emptyObj ())
    let finalAgents = emptyObj ()
    for name in objectKeys agents do
        if name = "basher" || name = "runner" then ()
        else
            let ua = Dyn.get agents name
            let uaObj = if Dyn.isNullish ua then emptyObj () else ua
            setKey finalAgents name (withRoleDefaults name uaObj)
    mergeObj opencodeConfig (box {| agent = finalAgents; mcp = mergedMcp |})
