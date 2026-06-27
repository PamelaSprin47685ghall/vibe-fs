module Wanxiangshu.Omp.AgentConfig

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn

let emptyObj () : obj = createObj []
let setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v

type private BuiltinAgentSpec =
    { name: string
      defaultMode: string
      systemPrompt: string
      defaultMcps: string array }

let private ompBuiltinAgentSpecs =
    [ { name = "manager"; defaultMode = "primary"; systemPrompt = managerSystemPromptFor omp; defaultMcps = [||] }
      { name = "build"; defaultMode = "primary"; systemPrompt = ""; defaultMcps = [||] }
      { name = "plan"; defaultMode = "primary"; systemPrompt = ""; defaultMcps = [||] }
      { name = "coder"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "investigator"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "meditator"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "bookkeeper"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "reviewer"; defaultMode = "subagent"; systemPrompt = reviewInstructions; defaultMcps = [||] }
      { name = "browser"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [| "stealth-browser-mcp" |] }
      { name = "executor"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] } ]

let private mergeObj (a: obj) (b: obj) : obj =
    let result = emptyObj ()
    Dyn.assignInto result a |> ignore
    Dyn.assignInto result b |> ignore
    result

let private mapToolNames (f: string -> 'a) : obj =
    Array.append (allToolNames omp) methodologyToolNames
    |> Seq.map (fun name -> name, box (f name))
    |> createObj

let private toolDefaultsFor (agentName: string) : obj =
    mapToolNames (fun name -> canUseForHost omp agentName name)

let private permissionDefaultsFor (agentName: string) : obj =
    mapToolNames (fun name -> if canUseForHost omp agentName name then "allow" else "deny")

let private primaryAliases = Set [ "manager"; "build"; "plan" ]

let private withRoleDefaultsFor (name: string) (userAgent: obj) : obj =
    let spec = ompBuiltinAgentSpecs |> List.tryFind (fun s -> s.name = name)
    let userPrompt = Dyn.str userAgent "prompt"
    let prompt =
        if userPrompt <> ""
        then userPrompt
        else spec |> Option.map (fun s -> s.systemPrompt) |> Option.defaultValue ""
    let userMode = Dyn.str userAgent "mode"
    let mode =
        if userMode <> ""
        then userMode
        else spec |> Option.map (fun s -> s.defaultMode) |> Option.defaultValue "subagent"
    let primaryDefaultMode = if primaryAliases |> Set.contains name then "primary" else "subagent"
    let effectiveMode = if mode <> "" then mode else primaryDefaultMode
    let userPerm = Dyn.get userAgent "permission"
    let userTools = Dyn.get userAgent "tools"
    let userMcps = Dyn.get userAgent "mcps"
    let emptyJsArray : obj = [||] :> obj
    let mcps =
        if Dyn.isNullish userMcps then
            spec
            |> Option.map (fun s -> if s.defaultMcps.Length = 0 then emptyJsArray else box s.defaultMcps)
            |> Option.defaultValue emptyJsArray
        else userMcps
    let perm = mergeObj (permissionDefaultsFor name) userPerm
    let tools = mergeObj (toolDefaultsFor name) userTools
    let result = mergeObj (emptyObj ()) userAgent
    setKey result "prompt" (box prompt)
    setKey result "mode" (box effectiveMode)
    setKey result "permission" perm
    setKey result "tools" tools
    setKey result "mcps" mcps
    result

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

let private disabledCheckpointSection : obj =
    createObj [
        "thresholds", box ([||] :> obj)
        "push_caps", box zeroedPushCaps
        "memory_reconcile_on_search", box false
    ]

let private disabledAutoSection : obj = createObj [ "auto", box false ]

let private disabledMemorySection : obj = createObj [ "cc_index", box false ]

let private injectAgentDisables (agents: obj) : unit =
    for name in [|"dream"; "distill"; "checkpoint-writer"|] do
        let ua = Dyn.get agents name
        if Dyn.isNullish ua then setKey agents name (createObj [ "disable", box true ])
        elif Dyn.isNullish (Dyn.get ua "disable") then ua?disable <- box true

let disableNativeAgents (cfg: obj) : obj =
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

let applyAgentConfigFor (ompConfig: obj) : obj =
    let prepared = disableNativeAgents ompConfig
    let userAgent = Dyn.get prepared "agent"
    let agents = mergeObj userAgent (emptyObj ())
    for name in ompBuiltinAgentSpecs |> List.map (fun s -> s.name) do
        if Dyn.isNullish (Dyn.get agents name) then setKey agents name (emptyObj ())
    let finalAgents =
        Dyn.keys agents
        |> Seq.map (fun name ->
            let ua = Dyn.get agents name
            let uaObj = if Dyn.isNullish ua then emptyObj () else ua
            name, withRoleDefaultsFor name uaObj)
        |> createObj
    mergeObj prepared (box {| agent = finalAgents |})