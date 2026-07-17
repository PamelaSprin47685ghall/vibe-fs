module Wanxiangshu.Runtime.MuxAiSettingsCodec

open Fable.Core
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.ToolContext
open Wanxiangshu.Runtime.DelegatedAiSettings
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.DynField
open Wanxiangshu.Runtime.ToolContextCodec

[<Erase>]
type IAgentAiEntryScalars =
    abstract model: string
    abstract modelString: string
    abstract thinkingLevel: string

[<Erase>]
type IDelegatedAiSettingsRecord =
    [<Emit("$0[$1]")>]
    abstract getAgentSettings: agentId: string -> IAgentAiEntryScalars option

[<Erase>]
type IMuxWorkspace =
    abstract aiSettingsByAgent: IDelegatedAiSettingsRecord

[<Erase>]
type IMuxAiConfig =
    abstract subagentAiDefaults: IDelegatedAiSettingsRecord
    abstract agentAiDefaults: IDelegatedAiSettingsRecord

[<Erase>]
type IMuxEnv =
    abstract MUX_MODEL_STRING: string
    abstract MUX_THINKING_LEVEL: string

[<Erase>]
type IMuxAiObj =
    abstract ai: IAgentAiEntryScalars

[<Erase>]
type IMuxDelegateConfig =
    inherit IMuxToolContext
    abstract runtime: obj
    abstract muxEnv: IMuxEnv

type MuxDelegateAiConfig =
    { Execution: ToolExecutionContext
      Runtime: obj
      Cwd: string }

type MuxParentRuntimeAiScalars =
    { ModelString: string option
      ThinkingLevel: string option }

type AgentAiEntryScalars =
    { Model: string option
      ModelString: string option
      ThinkingLevel: string option }

let normalizeTrimmedStr (v: obj) : string option =
    if Dyn.isNullish v then
        None
    else
        let s = (string v).Trim()
        if s = "" then None else Some s

let normalizeOpt (v: string) : string option =
    if Dyn.isNullish (box v) then
        None
    else
        let t = v.Trim()
        if t = "" then None else Some t

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

let private delegateRuntimeAndCwd (config: IMuxDelegateConfig) : obj * string =
    let runtime = config.runtime
    let runtimeObj = if Dyn.isNullish runtime then null else runtime
    let cwd = if Dyn.isNullish (box config.cwd) then "" else config.cwd
    runtimeObj, cwd

let decodeMuxDelegateConfig (configObj: obj) : Result<MuxDelegateAiConfig, DomainError> =
    if Dyn.isNullish configObj then
        Error(InvalidIntent("mux-delegate", "config", "nullish"))
    else
        let config = unbox<IMuxDelegateConfig> configObj

        match decodeMuxConfig config with
        | Error e -> Error e
        | Ok ctx ->
            let runtimeObj, cwd = delegateRuntimeAndCwd config

            Ok
                { Execution = ctx
                  Runtime = runtimeObj
                  Cwd = cwd }

let decodeMuxDelegateConfigLenient (configObj: obj) : MuxDelegateAiConfig =
    let config = unbox<IMuxDelegateConfig> configObj
    let runtimeObj, cwd = delegateRuntimeAndCwd config

    { Execution = decodeMuxConfigLenient config
      Runtime = runtimeObj
      Cwd = cwd }

let decodeMuxParentRuntimeEnv (muxEnv: IMuxEnv) : MuxParentRuntimeAiScalars =
    if Dyn.isNullish (box muxEnv) then
        { ModelString = None
          ThinkingLevel = None }
    else
        { ModelString = normalizeTrimmedStr (box muxEnv.MUX_MODEL_STRING)
          ThinkingLevel = normalizeOpt muxEnv.MUX_THINKING_LEVEL |> Option.bind coerceThinkingLevel }

let decodeAgentAiEntryScalars (entry: IAgentAiEntryScalars) : AgentAiEntryScalars =
    if Dyn.isNullish (box entry) then
        { Model = None
          ModelString = None
          ThinkingLevel = None }
    else
        { Model = normalizeOpt entry.model
          ModelString = normalizeOpt entry.modelString
          ThinkingLevel = normalizeOpt entry.thinkingLevel }

let private namedSettingsFromRecord
    (source: IDelegatedAiSettingsRecord)
    (agentId: string)
    : DelegatedAiSettings option =
    if Dyn.isNullish (box source) then
        None
    else
        match source.getAgentSettings agentId with
        | None -> None
        | Some entry ->
            let s = decodeAgentAiEntryScalars entry

            Some
                { modelString = s.Model |> Option.orElse s.ModelString
                  thinkingLevel = s.ThinkingLevel }

let readMuxConfigFileDefaults (configFileObj: obj) (agentId: string) : DelegatedAiSettings option list =
    if Dyn.isNullish configFileObj then
        [ None; None ]
    else
        let configFile = unbox<IMuxAiConfig> configFileObj

        [ namedSettingsFromRecord configFile.subagentAiDefaults agentId
          namedSettingsFromRecord configFile.agentAiDefaults agentId ]

let readWorkspaceAiSettingsByAgent (workspaceObj: obj) (agentId: string) : DelegatedAiSettings option =
    if Dyn.isNullish workspaceObj then
        None
    else
        let workspace = unbox<IMuxWorkspace> workspaceObj
        namedSettingsFromRecord workspace.aiSettingsByAgent agentId

let readDescriptorAiFromFrontmatter (fmObj: obj) : DelegatedAiSettings =
    if Dyn.isNullish fmObj then
        emptySettings
    else
        let fm = unbox<IMuxAiObj> fmObj
        let aiScalars = fm.ai

        if Dyn.isNullish (box aiScalars) then
            emptySettings
        else
            let scalars = decodeAgentAiEntryScalars aiScalars

            { modelString = scalars.Model |> Option.orElse scalars.ModelString
              thinkingLevel = scalars.ThinkingLevel }

let readWorkspaceFromFindResult (findResult: obj) : obj =
    if Dyn.isNullish findResult then
        null
    else
        Dyn.get findResult "workspace"

let readParentMuxEnv (configObj: obj) : MuxParentRuntimeAiScalars =
    if Dyn.isNullish configObj then
        { ModelString = None
          ThinkingLevel = None }
    else
        let config = unbox<IMuxDelegateConfig> configObj
        let muxEnv = config.muxEnv

        if Dyn.isNullish (box muxEnv) then
            { ModelString = None
              ThinkingLevel = None }
        else
            decodeMuxParentRuntimeEnv muxEnv
