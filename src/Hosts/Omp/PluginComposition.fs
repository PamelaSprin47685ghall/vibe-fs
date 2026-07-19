module Wanxiangshu.Hosts.Omp.PluginComposition

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Hosts.Omp.AgentConfig
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.Tools
open Wanxiangshu.Hosts.Omp.PruneGuard
open Wanxiangshu.Hosts.Omp.ReviewToolsRegister
open Wanxiangshu.Hosts.Omp.SessionLifecycle
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.TitleFetchGuard
open Wanxiangshu.Hosts.Omp.MessageTransform
open Wanxiangshu.Runtime.OmpCaps
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.RunnerBackground
open Wanxiangshu.Runtime.SessionExecutor
open Wanxiangshu.Runtime.TreeSitterShell
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.FallbackConfigCodec
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Hosts.Omp.Fallback.Hook
open Wanxiangshu.Hosts.Omp.ChildSession
open Wanxiangshu.Runtime.ToolHookRuntime
open Wanxiangshu.Runtime.CommandProcessor
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.SubsessionActor
open Wanxiangshu.Kernel.HostCapability

type CoreServices =
    { ReviewStore: ReviewStore
      FinderCache: FinderCache
      Pi: obj
      FallbackHandler: (obj -> JS.Promise<FallbackHookResult>) option
      FallbackRuntime: FallbackRuntimeStore
      FallbackConfig: FallbackConfig option }

let reviewStore: ReviewStore = createReviewStore ()

let private wrapRegisterToolForSchema (pi: obj) : unit =
    let original = Dyn.get pi "registerTool"

    if Dyn.isNullish original || not (Dyn.typeIs original "function") then
        ()
    else
        let defs = ResizeArray<obj>()
        pi?toolDefinitions <- box defs

        pi?registerTool <-
            box (fun (toolDef: obj) ->
                let name = Dyn.str toolDef "name"

                if name <> "" then
                    let parameters = Dyn.get toolDef "parameters"

                    if not (Dyn.isNullish parameters) then
                        registerSchemaTypes name parameters

                    if not (Dyn.isNullish toolDef) then
                        defs.Add(toolDef)

                Dyn.callWithThis1 original pi toolDef)

let private createCoreServices (pi: obj) : CoreServices =
    wrapRegisterToolForSchema pi
    let finderCache = FinderCache()

    let directory = Dyn.str pi "directory"
    let fallbackConfigOpt = loadFallbackConfig directory
    let fallbackRuntime = FallbackRuntimeStore()

    let configLookup: ConfigLookup =
        match fallbackConfigOpt with
        | Some cfg -> (fun _ -> cfg)
        | None -> (fun _ -> emptyConfig)

    let sessionApi = Dyn.get pi "session"

    let fallbackHandler =
        Some(createOmpFallbackHandler fallbackRuntime configLookup sessionApi directory)

    { ReviewStore = reviewStore
      FinderCache = finderCache
      Pi = pi
      FallbackHandler = fallbackHandler
      FallbackRuntime = fallbackRuntime
      FallbackConfig = fallbackConfigOpt }

/// Apply AgentConfig to the host's config object if the host exposes both
/// `getConfig` and `setConfig`. Without `getConfig` we have no base to merge
/// against, so we skip the call rather than overwrite the host's user-defined
/// agents, permissions, and MCP wiring. AgentConfig is the same canonical
/// agent/permission matrix Opencode consumes, narrowed to Omp's host surface
/// (no client API exists, so MCP wiring is optional).
let private applyAgentConfigIfSupported (pi: obj) : unit =
    let getConfig = Dyn.get pi "getConfig"
    let setConfig = Dyn.get pi "setConfig"

    if Dyn.typeIs getConfig "function" && Dyn.typeIs setConfig "function" then
        try
            let currentRaw: obj = Dyn.callMethod0 pi "getConfig"
            let baseCfg = if Dyn.isNullish currentRaw then emptyObj () else currentRaw
            let next = applyAgentConfigFor baseCfg
            Dyn.callMethod1 pi "setConfig" next |> ignore
        with _ ->
            ()

let private registerHooks (pi: obj) (services: CoreServices) : unit =
    registerAllTools pi services.ReviewStore services.FallbackRuntime services.FallbackConfig
    registerInputHandler pi services.ReviewStore
    registerSessionLifecycle pi services.ReviewStore services.FallbackRuntime
    SessionAbortHandler.registerAbortHandler pi services.ReviewStore services.FallbackRuntime services.FallbackHandler

let pluginFor (pi: obj) : JS.Promise<unit> =
    promise {
        Wanxiangshu.Runtime.E2eSandbox.applyFromProcessEnv ()
        installTitleFetchGuard ()
        do! patchDisablePrune ()
        let services = createCoreServices pi
        applyAgentConfigIfSupported pi
        pi?capabilities <- box (toStringArray allFull)
        registerHooks pi services

        // Minimal safe restart: unfinished subsession runs become poisoned actors.
        let directory = Dyn.str pi "directory"

        let hostFactory (_sid: string) : ISubsessionHost =
            Wanxiangshu.Hosts.Omp.SubsessionHostAdapter.createHost null "" pi directory

        do!
            Wanxiangshu.Runtime.SubsessionReconcile.reconcileUnfinishedRuns directory (Some hostFactory)
            |> Promise.map ignore
    }
