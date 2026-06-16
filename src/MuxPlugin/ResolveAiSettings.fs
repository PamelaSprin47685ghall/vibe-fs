module VibeFs.MuxPlugin.ResolveAiSettings

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn

type DelegatedAiSettings =
    { modelString: string option
      thinkingLevel: string option }

let emptySettings : DelegatedAiSettings =
    { modelString = None
      thinkingLevel = None }

let private loadConfigOrDefault (deps: obj) : obj = deps?loadConfigOrDefault()

let private findWorkspaceEntry (deps: obj) (configFile: obj) (workspaceId: string) : obj =
    deps?findWorkspaceEntry(configFile, workspaceId)

let private resolveAgentFrontmatter (deps: obj) (runtime: obj) (cwd: string) (agentId: string) : JS.Promise<obj> =
    unbox (deps?resolveAgentFrontmatter(runtime, cwd, agentId))

let private normalizeStr (v: obj) : string option =
    if Dyn.isNullish v then None
    else
        let s = (string v).Trim()
        if s = "" then None else Some s

/// Workspace `aiSettingsByAgent` uses `model`; config defaults use `modelString`.
let internal modelFromEntry (entry: obj) : string option =
    normalizeStr (Dyn.get entry "model")
    |> Option.orElseWith (fun () -> normalizeStr (Dyn.get entry "modelString"))

let private thinkingFromEntry (entry: obj) : string option =
    normalizeStr (Dyn.get entry "thinkingLevel")

let internal namedSettingsFromRecord (source: obj) (agentId: string) : DelegatedAiSettings option =
    if Dyn.isNullish source then None
    else
        let entry = Dyn.get source agentId
        if Dyn.isNullish entry then None
        else
            Some
                { modelString = modelFromEntry entry
                  thinkingLevel = thinkingFromEntry entry }

/// First non-blank value per field wins (same order as vibe-me-mux `mergeNamedSettings`).
let internal mergeNamedSettings (sources: DelegatedAiSettings option list) : DelegatedAiSettings =
    sources
    |> List.fold (fun acc source ->
        match source with
        | Some s ->
            { modelString = acc.modelString |> Option.orElse s.modelString
              thinkingLevel = acc.thinkingLevel |> Option.orElse s.thinkingLevel }
        | None -> acc) emptySettings

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
                    return
                        { modelString = normalizeStr (Dyn.get ai "model")
                          thinkingLevel = thinkingFromEntry ai }
                with _ ->
                    return emptySettings
            }

        let sources = [
            namedSettingsFromRecord byAgent agentId
            namedSettingsFromRecord (Dyn.get configFile "subagentAiDefaults") agentId
            namedSettingsFromRecord (Dyn.get configFile "agentAiDefaults") agentId
            Some descriptorSettings
            if agentId = "exec" then namedSettingsFromRecord byAgent "exec" else None
        ]

        return mergeNamedSettings sources
    } |> Async.StartAsPromise
