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

let private defaultPrimaryAliases = [ "manager"; "build"; "plan" ]

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

let private objectKeys (o: obj) : string array =
    JS.Constructors.Object.keys(o) |> Seq.toArray

let private toolDefaultsFor (host: Host) (agentName: string) : obj =
    allToolNames host
    |> Seq.map (fun name -> name, box (canUseForHost host agentName name))
    |> createObj

let private permissionDefaultsFor (host: Host) (agentName: string) : obj =
    allToolNames host
    |> Seq.map (fun name -> name, box (if canUseForHost host agentName name then "allow" else "deny"))
    |> createObj

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

    let perm = mergeObj (permissionDefaultsFor host name) userPerm
    let tools = mergeObj (toolDefaultsFor host name) userTools
    let result = mergeObj (emptyObj ()) userAgent
    setKey result "prompt" (box prompt)
    setKey result "mode" (box effectiveMode)
    setKey result "permission" perm
    setKey result "tools" tools
    setKey result "mcps" mcps
    result

let applyAgentConfigFor (host: Host) (opencodeConfig: obj) (mcps: obj) : obj =
    let userAgent = if Dyn.isNullish (Dyn.get opencodeConfig "agent") then emptyObj () else Dyn.get opencodeConfig "agent"
    let configMcp = Dyn.get opencodeConfig "mcp"
    let mergedMcp = if Dyn.isNullish configMcp then mcps else mergeObj configMcp mcps
    let agents = mergeObj userAgent (emptyObj ())
    for name in builtinAgentSpecs |> List.map (fun spec -> spec.name) do
        if Dyn.isNullish (Dyn.get agents name) then setKey agents name (emptyObj ())
    let finalAgents =
        objectKeys agents
        |> Seq.map (fun name ->
            let ua = Dyn.get agents name
            let uaObj = if Dyn.isNullish ua then emptyObj () else ua
            name, withRoleDefaultsFor host name uaObj)
        |> createObj
    mergeObj opencodeConfig (box {| agent = finalAgents; mcp = mergedMcp |})
