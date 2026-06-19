module VibeFs.Mux.AiSettings

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel

type DelegatedAiSettings =
    { modelString: string option
      thinkingLevel: string option }

let emptySettings : DelegatedAiSettings =
    { modelString = None
      thinkingLevel = None }

type AiConfigRecord =
    { workspaceId: string
      runtime: obj
      cwd: string }

let private decodeAiConfig (config: obj) : AiConfigRecord =
    { workspaceId = Dyn.str config "workspaceId"
      runtime = let r = Dyn.get config "runtime" in if Dyn.isNullish r then null else r
      cwd = Dyn.str config "cwd" }

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

let modelFromEntry (entry: obj) : string option =
    normalizeStr (Dyn.get entry "model")
    |> Option.orElseWith (fun () -> normalizeStr (Dyn.get entry "modelString"))

let private thinkingFromEntry (entry: obj) : string option =
    normalizeStr (Dyn.get entry "thinkingLevel")

let namedSettingsFromRecord (source: obj) (agentId: string) : DelegatedAiSettings option =
    if Dyn.isNullish source then None
    else
        let entry = Dyn.get source agentId
        if Dyn.isNullish entry then None
        else Some { modelString = modelFromEntry entry; thinkingLevel = thinkingFromEntry entry }

let mergeNamedSettings (sources: DelegatedAiSettings option list) : DelegatedAiSettings =
    sources
    |> List.fold (fun acc source ->
        match source with
        | Some s ->
            { modelString = acc.modelString |> Option.orElse s.modelString
              thinkingLevel = acc.thinkingLevel |> Option.orElse s.thinkingLevel }
        | None -> acc) emptySettings

let resolveDelegatedAgentAiSettings (deps: obj) (config: obj) (agentId: string) : JS.Promise<DelegatedAiSettings> =
    async {
        let cfg = decodeAiConfig config
        let configFile = loadConfigOrDefault deps
        let workspace =
            if cfg.workspaceId = "" then null
            else
                let result = findWorkspaceEntry deps configFile cfg.workspaceId
                Dyn.get result "workspace"
        let byAgent = Dyn.get workspace "aiSettingsByAgent"
        let! descriptorSettings =
            async {
                try
                    let! fm = resolveAgentFrontmatter deps cfg.runtime cfg.cwd agentId |> Async.AwaitPromise
                    let ai = Dyn.get fm "ai"
                    return { modelString = normalizeStr (Dyn.get ai "model"); thinkingLevel = thinkingFromEntry ai }
                with _ -> return emptySettings
            }
        return
            mergeNamedSettings [
                namedSettingsFromRecord byAgent agentId
                namedSettingsFromRecord (Dyn.get configFile "subagentAiDefaults") agentId
                namedSettingsFromRecord (Dyn.get configFile "agentAiDefaults") agentId
                Some descriptorSettings
                if agentId = "exec" then namedSettingsFromRecord byAgent "exec" else None
            ]
    } |> Async.StartAsPromise

let internal coerceThinkingLevel (value: string) : string option =
    let trimmed = value.Trim()
    match trimmed with
    | "med" -> Some "medium"
    | "off" | "low" | "medium" | "high" | "xhigh" | "max" -> Some trimmed
    | "" -> None
    | _ -> None

type ParentRuntimeAiSettings =
    { modelString: string option
      thinkingLevel: string option }

let private trimToOption (value: string) =
    let trimmed = value.Trim()
    if trimmed = "" then None else Some trimmed

let private readMuxEnvSettings (muxEnv: obj) : ParentRuntimeAiSettings =
    { modelString = Dyn.str muxEnv "MUX_MODEL_STRING" |> trimToOption
      thinkingLevel = Dyn.str muxEnv "MUX_THINKING_LEVEL" |> coerceThinkingLevel }

let private toRuntimeAiSettingsObj (settings: ParentRuntimeAiSettings) : obj =
    let fields =
        [ match settings.modelString with
          | Some v -> yield ("modelString", box v)
          | None -> ()
          match settings.thinkingLevel with
          | Some v -> yield ("thinkingLevel", box v)
          | None -> () ]
    match fields with
    | [] -> null
    | _ -> createObj fields

let buildParentRuntimeAiSettings (config: obj) : obj =
    let muxEnv = Dyn.get config "muxEnv"
    if Dyn.isNullish muxEnv then null
    else muxEnv |> readMuxEnvSettings |> toRuntimeAiSettingsObj
