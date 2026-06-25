module VibeFs.Mux.AiSettings
open VibeFs.Kernel.Domain

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell
open VibeFs.Shell.DelegatedAiSettings
open VibeFs.Shell.Dyn
open VibeFs.Shell.MuxAiSettingsCodec

type DelegatedAiSettings = VibeFs.Shell.DelegatedAiSettings.DelegatedAiSettings
let emptySettings = VibeFs.Shell.DelegatedAiSettings.emptySettings

let private loadConfigOrDefault (deps: obj) : obj = deps?loadConfigOrDefault()

let private findWorkspaceEntry (deps: obj) (configFile: obj) (workspaceId: string) : obj =
    deps?findWorkspaceEntry(configFile, workspaceId)

let private resolveAgentFrontmatter (deps: obj) (runtime: obj) (cwd: string) (agentId: string) : JS.Promise<obj> =
    unbox (deps?resolveAgentFrontmatter(runtime, cwd, agentId))

let mergeNamedSettings (sources: DelegatedAiSettings option list) : DelegatedAiSettings =
    sources
    |> List.fold (fun acc source ->
        match source with
        | Some s ->
            { modelString = acc.modelString |> Option.orElse s.modelString
              thinkingLevel = acc.thinkingLevel |> Option.orElse s.thinkingLevel }
        | None -> acc) emptySettings

let resolveDelegatedAgentAiSettings (deps: obj) (config: obj) (agentId: string) : JS.Promise<DelegatedAiSettings> =
    promise {
        let d = decodeMuxDelegateConfigLenient config
        let workspaceId = d.Execution.WorkspaceId |> Option.map Id.workspaceIdValue |> Option.defaultValue ""
        let runtime = d.Runtime
        let cwd = d.Cwd
        let configFile = loadConfigOrDefault deps
        let workspace =
            if workspaceId = "" then null
            else readWorkspaceFromFindResult (findWorkspaceEntry deps configFile workspaceId)
        let! descriptorSettings =
            promise {
                try
                    let! fm = resolveAgentFrontmatter deps runtime cwd agentId
                    return readDescriptorAiFromFrontmatter fm
                with _ -> return emptySettings
            }
        return
            mergeNamedSettings (
                [ readWorkspaceAiSettingsByAgent workspace agentId ]
                @ readMuxConfigFileDefaults configFile agentId
                @ [ Some descriptorSettings ])
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
    if scalars.ModelString.IsNone && scalars.ThinkingLevel.IsNone then null
    else
        toRuntimeAiSettingsObj
            { modelString = scalars.ModelString
              thinkingLevel = scalars.ThinkingLevel }