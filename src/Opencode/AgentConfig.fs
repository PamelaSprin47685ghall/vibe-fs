module Wanxiangshu.Opencode.AgentConfig

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeAgentConfigCodec
open Wanxiangshu.Shell.OpencodeAgentConfigWire

[<Global("process")>]
let private nodeProcess : obj = jsNative

let envVar (name: string) : string =
    let v = nodeProcess?env?(name)
    if isNull v then "" else string v

let emptyObj () : obj = createObj []
let setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v

type private BuiltinAgentSpec =
    { name: string
      defaultMode: string
      systemPrompt: string
      defaultMcps: string array }

let private defaultPrimaryAliases = Set [ "manager"; "build"; "plan" ]

let private builtinAgentSpecs =
    [ { name = "manager"; defaultMode = "primary"; systemPrompt = managerSystemPrompt; defaultMcps = [||] }
      { name = "build"; defaultMode = "primary"; systemPrompt = ""; defaultMcps = [||] }
      { name = "plan"; defaultMode = "primary"; systemPrompt = ""; defaultMcps = [||] }
      { name = "coder"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "investigator"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "meditator"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "reviewer"; defaultMode = "subagent"; systemPrompt = reviewInstructions; defaultMcps = [||] }
      { name = "browser"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [| "stealth-browser-mcp" |] }
      { name = "executor"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] } ]

let private tryFindBuiltinAgent name =
    builtinAgentSpecs |> List.tryFind (fun spec -> spec.name = name)

let private toolDefaultsFor (host: Host) (agentName: string) : OpencodeAgentConfigCodec.ToolsOverrides =
    Array.append (allToolNames host) [| "methodology" |]
    |> Seq.fold (fun acc name -> Map.add name (canUseForHost host agentName name) acc) Map.empty

let private permissionDefaultsFor (host: Host) (agentName: string) : OpencodeAgentConfigCodec.PermissionOverrides =
    Array.append (allToolNames host) [| "methodology" |]
    |> Seq.fold
        (fun acc name ->
            let value = if canUseForHost host agentName name then "allow" else "deny"
            Map.add name value acc)
        Map.empty

let private withRoleDefaultsFor (host: Host) (name: string) (userAgent: obj) : obj =
    let spec = tryFindBuiltinAgent name
    let scalars = decodeUserAgentScalars userAgent
    let prompt =
        if scalars.Prompt <> "" then scalars.Prompt
        else spec |> Option.map (fun value -> value.systemPrompt) |> Option.defaultValue ""
    let mode =
        if scalars.Mode <> "" then scalars.Mode
        else spec |> Option.map (fun value -> value.defaultMode) |> Option.defaultValue "subagent"
    let primaryDefaultMode = if defaultPrimaryAliases |> Set.contains name then "primary" else "subagent"
    let effectiveMode = if mode <> "" then mode else primaryDefaultMode
    let userPerm = scalars.Permission
    let userTools = scalars.Tools

    let encodedScalars =
        encodeAgentScalarsRecord
            { Prompt = prompt
              Mode = effectiveMode
              Permission =
                  Some (
                      OpencodeAgentConfigCodec.mergePermissionOverrides (permissionDefaultsFor host name) userPerm)
              Tools =
                  Some (OpencodeAgentConfigCodec.mergeToolsOverrides (toolDefaultsFor host name) userTools)
              Mcps =
                  match scalars.Mcps with
                  | Some userArr when userArr.Length > 0 -> Some userArr
                  | Some _ -> Some [||]
                  | None ->
                      spec
                      |> Option.map (fun value -> value.defaultMcps)
                      |> Option.defaultValue [||]
                      |> Some }
    mergeConfigObj userAgent encodedScalars

let disableMimoMemoryAndCheckpoint (cfg: obj) : obj =
    OpencodeAgentConfigWire.disableMimoMemoryAndCheckpoint cfg

let applyAgentConfigFor (host: Host) (opencodeConfig: obj) (mcps: obj) : obj =
    let names = builtinAgentSpecs |> List.map (fun spec -> spec.name)
    OpencodeAgentConfigWire.applyAgentConfigFor host opencodeConfig mcps withRoleDefaultsFor names
