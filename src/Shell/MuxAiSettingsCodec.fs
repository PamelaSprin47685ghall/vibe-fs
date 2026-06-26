module Wanxiangshu.Shell.MuxAiSettingsCodec

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.ToolContext
open Wanxiangshu.Shell.DelegatedAiSettings
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.DynField
open Wanxiangshu.Shell.ToolContextCodec

module ConfigKeys =
    let subagentAiDefaults = "subagentAiDefaults"
    let agentAiDefaults = "agentAiDefaults"
    let aiSettingsByAgent = "aiSettingsByAgent"
    let workspace = "workspace"
    let ai = "ai"
    let muxEnv = "muxEnv"
    let runtime = "runtime"
    let cwd = "cwd"

type MuxDelegateAiConfig = {
    Execution: ToolExecutionContext
    Runtime: obj
    Cwd: string
}

type MuxParentRuntimeAiScalars = {
    ModelString: string option
    ThinkingLevel: string option
}

type AgentAiEntryScalars = {
    Model: string option
    ModelString: string option
    ThinkingLevel: string option
}

let normalizeTrimmedStr (v: obj) : string option =
    if Dyn.isNullish v then None
    else
        let s = (string v).Trim()
        if s = "" then None else Some s

let private thinkingLevelMap =
    [ "med", Some "medium"
      "off", Some "off"
      "low", Some "low"
      "medium", Some "medium"
      "high", Some "high"
      "xhigh", Some "xhigh"
      "max", Some "max" ]
    |> Map.ofList

let coerceThinkingLevel (value: string) : string option =
    Map.tryFind (value.Trim()) thinkingLevelMap |> Option.defaultValue None

let private delegateRuntimeAndCwd (config: obj) : obj * string =
    let runtime = Dyn.get config ConfigKeys.runtime
    let runtimeObj = if Dyn.isNullish runtime then null else runtime
    let cwd = defaultArg (strField config ConfigKeys.cwd) ""
    runtimeObj, cwd

let decodeMuxDelegateConfig (config: obj) : Result<MuxDelegateAiConfig, DomainError> =
    match decodeMuxConfig config with
    | Error e -> Error e
    | Ok ctx ->
        let runtimeObj, cwd = delegateRuntimeAndCwd config
        Ok { Execution = ctx; Runtime = runtimeObj; Cwd = cwd }

let decodeMuxDelegateConfigLenient (config: obj) : MuxDelegateAiConfig =
    let runtimeObj, cwd = delegateRuntimeAndCwd config
    { Execution = decodeMuxConfigLenient config
      Runtime = runtimeObj
      Cwd = cwd }

let decodeMuxParentRuntimeEnv (muxEnv: obj) : MuxParentRuntimeAiScalars =
    if Dyn.isNullish muxEnv then
        { ModelString = None; ThinkingLevel = None }
    else
        { ModelString = normalizeTrimmedStr (Dyn.get muxEnv "MUX_MODEL_STRING")
          ThinkingLevel = defaultArg (strField muxEnv "MUX_THINKING_LEVEL") "" |> coerceThinkingLevel }

let decodeAgentAiEntryScalars (entry: obj) : AgentAiEntryScalars =
    if Dyn.isNullish entry then
        { Model = None; ModelString = None; ThinkingLevel = None }
    else
        { Model = normalizeTrimmedStr (Dyn.get entry "model")
          ModelString = normalizeTrimmedStr (Dyn.get entry "modelString")
          ThinkingLevel = normalizeTrimmedStr (Dyn.get entry "thinkingLevel") }

let private namedSettingsFromRecord (source: obj) (agentId: string) : DelegatedAiSettings option =
    if Dyn.isNullish source then None
    else
        let entry = Dyn.get source agentId
        if Dyn.isNullish entry then None
        else
            let s = decodeAgentAiEntryScalars entry
            Some
                { modelString = s.Model |> Option.orElse s.ModelString
                  thinkingLevel = s.ThinkingLevel }

let readMuxConfigFileDefaults (configFile: obj) (agentId: string) : DelegatedAiSettings option list =
    if Dyn.isNullish configFile then [ None; None ]
    else
        [ namedSettingsFromRecord (Dyn.get configFile ConfigKeys.subagentAiDefaults) agentId
          namedSettingsFromRecord (Dyn.get configFile ConfigKeys.agentAiDefaults) agentId ]

let readWorkspaceAiSettingsByAgent (workspace: obj) (agentId: string) : DelegatedAiSettings option =
    if Dyn.isNullish workspace then None
    else namedSettingsFromRecord (Dyn.get workspace ConfigKeys.aiSettingsByAgent) agentId

// Null `fm` -> `emptySettings`; `AiSettings.resolveDelegatedAgentAiSettings` catches rejected `resolveAgentFrontmatter` promises (try/with) and uses the same fallback.
let readDescriptorAiFromFrontmatter (fm: obj) : DelegatedAiSettings =
    if Dyn.isNullish fm then emptySettings
    else
        let scalars = decodeAgentAiEntryScalars (Dyn.get fm ConfigKeys.ai)
        { modelString = scalars.Model |> Option.orElse scalars.ModelString
          thinkingLevel = scalars.ThinkingLevel }

let readWorkspaceFromFindResult (findResult: obj) : obj =
    if Dyn.isNullish findResult then null
    else Dyn.get findResult ConfigKeys.workspace

let readParentMuxEnv (config: obj) : MuxParentRuntimeAiScalars =
    let muxEnv = Dyn.get config ConfigKeys.muxEnv
    if Dyn.isNullish muxEnv then { ModelString = None; ThinkingLevel = None }
    else decodeMuxParentRuntimeEnv muxEnv