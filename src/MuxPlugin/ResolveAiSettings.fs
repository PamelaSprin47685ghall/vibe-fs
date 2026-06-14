module VibeFs.MuxPlugin.ResolveAiSettings

open Fable.Core
open VibeFs.Kernel
open VibeFs.Kernel.Dyn

type DelegatedAiSettings =
    { modelString: string option
      thinkingLevel: string option }

let emptySettings : DelegatedAiSettings =
    { modelString = None
      thinkingLevel = None }

[<Emit("$0.loadConfigOrDefault()")>]
let private loadConfigOrDefault (deps: obj) : obj = jsNative

[<Emit("$0.findWorkspaceEntry($1, $2)")>]
let private findWorkspaceEntry (deps: obj) (configFile: obj) (workspaceId: string) : obj = jsNative

[<Emit("$0.resolveAgentFrontmatter($1, $2, $3)")>]
let private resolveAgentFrontmatter (deps: obj) (runtime: obj) (cwd: string) (agentId: string) : JS.Promise<obj> = jsNative

let private optStr (o: obj) (key: string) : string option =
    let v = Dyn.get o key
    if Dyn.isNullish v then None else Some (string v)

let private agentSettings (source: obj) (agentId: string) : DelegatedAiSettings option =
    let entry = Dyn.get source agentId
    if Dyn.isNullish entry then None
    else Some { modelString = optStr entry "model"; thinkingLevel = optStr entry "thinkingLevel" }

let private merge (sources: DelegatedAiSettings option list) : DelegatedAiSettings =
    let pick current fallback =
        { modelString = current.modelString |> Option.orElse fallback.modelString
          thinkingLevel = current.thinkingLevel |> Option.orElse fallback.thinkingLevel }
    sources |> List.choose id |> List.fold pick emptySettings

let resolveDelegatedAgentAiSettings (deps: obj) (config: obj) (agentId: string) : JS.Promise<DelegatedAiSettings> =
    async {
        let configFile = loadConfigOrDefault deps

        let workspaceId = Dyn.str config "workspaceId"
        let workspace =
            if workspaceId = "" then null
            else
                let result = findWorkspaceEntry deps configFile workspaceId
                Dyn.get result "workspace"

        let byAgent = Dyn.get workspace "aiSettingsByAgent"

        let! descriptorSettings =
            async {
                try
                    let runtimeObj = Dyn.get config "runtime"
                    let runtime = if Dyn.isNullish runtimeObj then null else runtimeObj
                    let cwd = Dyn.str config "cwd"
                    let! fm = resolveAgentFrontmatter deps runtime cwd agentId |> Async.AwaitPromise
                    let ai = Dyn.get fm "ai"
                    return { modelString = optStr ai "model"; thinkingLevel = optStr ai "thinkingLevel" }
                with _ ->
                    return emptySettings
            }

        let sources = [
            agentSettings byAgent agentId
            agentSettings (Dyn.get configFile "subagentAiDefaults") agentId
            agentSettings (Dyn.get configFile "agentAiDefaults") agentId
            Some descriptorSettings
            if agentId = "exec" then agentSettings byAgent "exec" else None
        ]

        return merge sources
    } |> Async.StartAsPromise
