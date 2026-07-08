module Wanxiangshu.Shell.OpencodeAgentConfigWire

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.Dyn

let emptyObj () : obj = createObj []

let setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v

let mergeConfigObj (a: obj) (b: obj) : obj =
    let result = emptyObj ()
    Dyn.assignInto result a |> ignore
    Dyn.assignInto result b |> ignore
    result

let private emptyJsArray: obj = [||] :> obj

let private zeroedPushCaps: obj =
    createObj
        [ "tasks_ledger", box 0
          "focus_task", box 0
          "actor_ledger", box 0
          "memory_titles", box 0
          "global", box 0
          "checkpoint", box 0
          "memory", box 0
          "notes", box 0
          "design_decisions", box 0
          "open_notes", box 0 ]

let private disabledCheckpointSection: obj =
    createObj
        [ "thresholds", box emptyJsArray
          "push_caps", box zeroedPushCaps
          "memory_reconcile_on_search", box false ]

let private disabledAutoSection: obj = createObj [ "auto", box false ]

let private disabledMemorySection: obj = createObj [ "cc_index", box false ]

let private injectAgentDisables (agents: obj) : unit =
    for name in [| "dream"; "distill"; "checkpoint-writer" |] do
        let ua = Dyn.get agents name

        if Dyn.isNullish ua then
            setKey agents name (createObj [ "disable", box true ])
        elif Dyn.isNullish (Dyn.get ua "disable") then
            ua?disable <- box true

let disableMimoWorkflowToolsForAgents (agentMap: obj) : obj =
    for name in Dyn.keys agentMap do
        let agent = Dyn.get agentMap name
        let agentObj = if Dyn.isNullish agent then emptyObj () else agent

        let permissions =
            let current = Dyn.get agentObj "permission"

            mergeConfigObj
                (if Dyn.isNullish current then emptyObj () else current)
                (createObj [ "workflow", box "deny" ])

        let tools =
            let current = Dyn.get agentObj "tools"

            mergeConfigObj
                (if Dyn.isNullish current then emptyObj () else current)
                (createObj [ "workflow", box false ])

        setKey agentObj "permission" permissions
        setKey agentObj "tools" tools
        setKey agentMap name agentObj

    agentMap

let disableMimoMemoryAndCheckpoint (cfg: obj) : obj =
    let existingAgent = Dyn.get cfg "agent"

    let agentMap =
        if Dyn.isNullish existingAgent then
            emptyObj ()
        else
            existingAgent

    injectAgentDisables agentMap

    mergeConfigObj
        cfg
        (box (
            createObj
                [ "agent", box agentMap
                  "checkpoint", box disabledCheckpointSection
                  "dream", box disabledAutoSection
                  "distill", box disabledAutoSection
                  "memory", box disabledMemorySection ]
        ))

let applyAgentConfigFor
    (host: Host)
    (opencodeConfig: obj)
    (mcps: obj)
    (withRoleDefaultsFor: Host -> string -> obj -> obj)
    (builtinAgentNames: string list)
    : obj =
    let prepared = disableMimoMemoryAndCheckpoint opencodeConfig
    let userAgent = Dyn.get prepared "agent"
    let configMcp = Dyn.get prepared "mcp"

    let mergedMcp =
        if Dyn.isNullish configMcp then
            mcps
        else
            mergeConfigObj configMcp mcps

    let agents = mergeConfigObj userAgent (emptyObj ())

    for name in builtinAgentNames do
        if Dyn.isNullish (Dyn.get agents name) then
            setKey agents name (emptyObj ())

    let finalAgents =
        Dyn.keys agents
        |> Seq.map (fun name ->
            let ua = Dyn.get agents name
            let uaObj = if Dyn.isNullish ua then emptyObj () else ua
            name, withRoleDefaultsFor host name uaObj)
        |> createObj
        |> fun builtAgents ->
            match host with
            | Mimocode -> disableMimoWorkflowToolsForAgents builtAgents
            | Opencode
            | Mux
            | Omp -> builtAgents

    mergeConfigObj
        prepared
        (box
            {| agent = finalAgents
               mcp = mergedMcp |})
