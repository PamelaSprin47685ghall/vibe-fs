module Wanxiangshu.Mux.AiSettings

open Wanxiangshu.Kernel.Domain

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell
open Wanxiangshu.Shell.DelegatedAiSettings
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.MuxAiSettingsCodec
open Wanxiangshu.Shell.FallbackConfigCodec
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.Recovery

type DelegatedAiSettings = Wanxiangshu.Shell.DelegatedAiSettings.DelegatedAiSettings
let emptySettings = Wanxiangshu.Shell.DelegatedAiSettings.emptySettings

let private loadConfigOrDefault (deps: obj) : obj = deps?loadConfigOrDefault ()

let private findWorkspaceEntry (deps: obj) (configFile: obj) (workspaceId: string) : obj =
    deps?findWorkspaceEntry (configFile, workspaceId)

let private resolveAgentFrontmatter (deps: obj) (runtime: obj) (cwd: string) (agentId: string) : JS.Promise<obj> =
    unbox (deps?resolveAgentFrontmatter (runtime, cwd, agentId))

let mergeNamedSettings (sources: DelegatedAiSettings option list) : DelegatedAiSettings =
    sources
    |> List.fold
        (fun acc source ->
            match source with
            | Some s ->
                { modelString = acc.modelString |> Option.orElse s.modelString
                  thinkingLevel = acc.thinkingLevel |> Option.orElse s.thinkingLevel }
            | None -> acc)
        emptySettings

let private modelStringFromFallbackModel (m: FallbackModel) : string =
    match m.Variant with
    | Some v -> sprintf "%s/%s:%s" m.ProviderID m.ModelID v
    | None -> sprintf "%s/%s" m.ProviderID m.ModelID

let private readDescriptorModelsFromFrontmatter (fm: obj) (agentId: string) : DelegatedAiSettings =
    match extractFallbackConfig fm with
    | None -> emptySettings
    | Some cfg ->
        let normId = normalizeAgentName agentId

        let chain =
            Map.tryFind normId cfg.AgentChains
            |> Option.orElse (Some cfg.DefaultChain)
            |> Option.defaultValue []

        { modelString = chain |> List.tryHead |> Option.map modelStringFromFallbackModel
          thinkingLevel = None }

let resolveDelegatedAgentAiSettings (deps: obj) (config: obj) (agentId: string) : JS.Promise<DelegatedAiSettings> =
    promise {
        let d = decodeMuxDelegateConfigLenient config

        let workspaceId =
            d.Execution.WorkspaceId
            |> Option.map Id.workspaceIdValue
            |> Option.defaultValue ""

        let runtime = d.Runtime
        let cwd = d.Cwd
        let configFile = loadConfigOrDefault deps

        let workspace =
            if workspaceId = "" then
                null
            else
                readWorkspaceFromFindResult (findWorkspaceEntry deps configFile workspaceId)

        let! fm =
            promise {
                try
                    let! fm = resolveAgentFrontmatter deps runtime cwd agentId
                    return fm
                with _ ->
                    return null
            }

        let descriptorSettings = readDescriptorAiFromFrontmatter fm
        let modelsSettings = readDescriptorModelsFromFrontmatter fm agentId

        return
            mergeNamedSettings (
                [ readWorkspaceAiSettingsByAgent workspace agentId ]
                @ readMuxConfigFileDefaults configFile agentId
                @ [ Some descriptorSettings; Some modelsSettings ]
            )
    }

type ParentRuntimeAiSettings =
    { modelString: string option
      thinkingLevel: string option }

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
    let scalars = readParentMuxEnv config

    if scalars.ModelString.IsNone && scalars.ThinkingLevel.IsNone then
        null
    else
        toRuntimeAiSettingsObj
            { modelString = scalars.ModelString
              thinkingLevel = scalars.ThinkingLevel }
