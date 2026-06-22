module VibeFs.Opencode.AgentConfig

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Config
open VibeFs.Kernel.Prompts

[<Global("process")>]
let private nodeProcess : obj = jsNative

let envVar (name: string) : string =
    let v = nodeProcess?env?(name)
    if isNull v then "" else string v

let emptyObj () : obj = createObj []
let setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v
let assignInto (target: obj) (source: obj) : obj = Dyn.assignInto target source

let private emptyMcps : obj = [||] :> obj

type private BuiltinAgentSpec =
    { name: string
      defaultMode: string
      systemPrompt: string
      defaultMcps: string array }

let private defaultPrimaryAliases = Set [ "manager"; "build"; "plan" ]

let private builtinAgentSpecs =
    [ { name = "manager"; defaultMode = "primary"; systemPrompt = Prompts.managerSystemPrompt; defaultMcps = [||] }
      { name = "build"; defaultMode = "primary"; systemPrompt = ""; defaultMcps = [||] }
      { name = "plan"; defaultMode = "primary"; systemPrompt = ""; defaultMcps = [||] }
      { name = "coder"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "investigator"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "meditator"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "bookkeeper"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "reviewer"; defaultMode = "subagent"; systemPrompt = Prompts.reviewInstructions; defaultMcps = [||] }
      { name = "browser"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [| "stealth-browser-mcp" |] }
      { name = "executor"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] } ]

let private tryFindBuiltinAgent name =
    builtinAgentSpecs |> List.tryFind (fun spec -> spec.name = name)

let private mergeObj (a: obj) (b: obj) : obj =
    let result = emptyObj ()
    Dyn.assignInto result a |> ignore
    Dyn.assignInto result b |> ignore
    result

let private mapToolNames (host: Host) (f: string -> 'a) : obj =
    allToolNames host
    |> Seq.map (fun name -> name, box (f name))
    |> createObj

let private toolDefaultsFor (host: Host) (agentName: string) : obj =
    mapToolNames host (canUseForHost host agentName)

let private permissionDefaultsFor (host: Host) (agentName: string) : obj =
    mapToolNames host (fun name -> if canUseForHost host agentName name then "allow" else "deny")

let private withRoleDefaultsFor (host: Host) (name: string) (userAgent: obj) : obj =
    let spec = tryFindBuiltinAgent name
    let userPrompt = Dyn.str userAgent "prompt"
    let prompt =
        if userPrompt <> "" then userPrompt
        else spec |> Option.map (fun value -> value.systemPrompt) |> Option.defaultValue ""
    let userMode = Dyn.str userAgent "mode"
    let mode =
        if userMode <> "" then userMode
        else spec |> Option.map (fun value -> value.defaultMode) |> Option.defaultValue "subagent"
    let primaryDefaultMode = if defaultPrimaryAliases |> Set.contains name then "primary" else "subagent"
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

    let perm = mergeObj (permissionDefaultsFor host name) userPerm
    let tools = mergeObj (toolDefaultsFor host name) userTools
    let result = mergeObj (emptyObj ()) userAgent
    setKey result "prompt" (box prompt)
    setKey result "mode" (box effectiveMode)
    setKey result "permission" perm
    setKey result "tools" tools
    setKey result "mcps" mcps
    result

let private emptyJsArray : obj = [||] :> obj

let private zeroedPushCaps : obj =
    createObj [
        "tasks_ledger", box 0
        "focus_task", box 0
        "actor_ledger", box 0
        "memory_titles", box 0
        "global", box 0
        "checkpoint", box 0
        "memory", box 0
        "notes", box 0
        "design_decisions", box 0
        "open_notes", box 0
    ]

let private nativeAgentDisableOverrides : obj =
    createObj [
        "dream", box {| disable = true |}
        "distill", box {| disable = true |}
        "checkpoint-writer", box {| disable = true |}
    ]

let private disabledCheckpointSection : obj =
    createObj [
        "thresholds", box emptyJsArray
        "push_caps", box zeroedPushCaps
        "memory_reconcile_on_search", box false
    ]

let private disabledAutoSection : obj = createObj [ "auto", box false ]

let private disabledMemorySection : obj = createObj [ "cc_index", box false ]

let private injectAgentDisables (agents: obj) : unit =
    for name in [|"dream"; "distill"; "checkpoint-writer"|] do
        let ua = Dyn.get agents name
        if Dyn.isNullish ua then setKey agents name (createObj [ "disable", box true ])
        elif Dyn.isNullish (Dyn.get ua "disable") then
            ua?disable <- box true

let disableMimoMemoryAndCheckpoint (cfg: obj) : obj =
    let existingAgent = Dyn.get cfg "agent"
    let agentMap = if Dyn.isNullish existingAgent then emptyObj () else existingAgent
    injectAgentDisables agentMap
    mergeObj cfg
        (box (createObj [
            "agent", box agentMap
            "checkpoint", box disabledCheckpointSection
            "dream", box disabledAutoSection
            "distill", box disabledAutoSection
            "memory", box disabledMemorySection
        ]))

let applyAgentConfigFor (host: Host) (opencodeConfig: obj) (mcps: obj) : obj =
    let prepared = disableMimoMemoryAndCheckpoint opencodeConfig
    let userAgent = Dyn.get prepared "agent"
    let configMcp = Dyn.get prepared "mcp"
    let mergedMcp = if Dyn.isNullish configMcp then mcps else mergeObj configMcp mcps
    let agents = mergeObj userAgent (emptyObj ())
    for name in builtinAgentSpecs |> List.map (fun spec -> spec.name) do
        if Dyn.isNullish (Dyn.get agents name) then setKey agents name (emptyObj ())
    let finalAgents =
        Dyn.keys agents
        |> Seq.map (fun name ->
            let ua = Dyn.get agents name
            let uaObj = if Dyn.isNullish ua then emptyObj () else ua
            name, withRoleDefaultsFor host name uaObj)
        |> createObj
    mergeObj prepared (box {| agent = finalAgents; mcp = mergedMcp |})
