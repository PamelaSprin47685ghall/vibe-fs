module Wanxiangshu.Omp.PluginCore

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Omp.AgentConfig
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.Tools
open Wanxiangshu.Omp.PruneGuard
open Wanxiangshu.Omp.ReviewTools
open Wanxiangshu.Omp.SessionLifecycle
open Wanxiangshu.Shell.TitleFetchGuardCommon
open Wanxiangshu.Omp.MessageTransform
open Wanxiangshu.Shell.OmpCaps
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.SessionExecutor
open Wanxiangshu.Shell.TreeSitterShell
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Shell.FallbackConfigCodec
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Omp.FallbackHooks

type CoreServices =
    { ReviewStore: ReviewStore
      FinderCache: FinderCache
      Pi: obj
      FallbackHandler: (obj -> JS.Promise<FallbackHookResult>) option
      FallbackRuntime: FallbackRuntimeState
      FallbackConfig: FallbackConfig option }

let reviewStore : ReviewStore = createReviewStore ()

let private createCoreServices (pi: obj) : CoreServices =
    let finderCache = FinderCache()

    let directory = Dyn.str pi "directory"
    let fallbackConfigOpt = loadFallbackConfig directory
    let fallbackRuntime = FallbackRuntimeState()
    let configLookup : ConfigLookup =
        match fallbackConfigOpt with
        | Some cfg -> (fun _ -> cfg)
        | None -> (fun _ -> emptyConfig)
    let sessionApi = Dyn.get pi "session"
    let fallbackHandler =
        match fallbackConfigOpt with
        | Some _ -> Some (createOmpFallbackHandler fallbackRuntime configLookup sessionApi)
        | None -> None

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
            let currentRaw : obj = Dyn.call0 getConfig
            let baseCfg = if Dyn.isNullish currentRaw then emptyObj () else currentRaw
            let next = applyAgentConfigFor baseCfg
            Dyn.call1 setConfig next |> ignore
        with _ -> ()

/// `session.abort` / `stream.abort` / `session.error` all collapse to the
/// same outcome: in-flight review state must clear. Without this hook,
/// review state survives host-driven aborts and leaks across sessions.
let private sessionEndEventTypes =
    Set [ "session.abort"; "stream.abort"; "session.error"; "session.delete"; "session.close"; "session.remove"; "session.deleted" ]

let registerAbortHandler (pi: obj) (reviewStore: ReviewStore)
    (fallbackHandler: (obj -> JS.Promise<FallbackHookResult>) option) : unit =
    let fallbackEventTypes = Set [ "session.busy"; "session.idle"; "message.updated"; "session.updated" ]
    pi?on(
        "event",
        box(fun (event: obj) (ctx: obj) ->
            promise {
                let evtType = Dyn.str event "type"
                let sidOpt = getSessionIdFromContext ctx
                match sidOpt with
                | None -> ()
                | Some sid ->
                    if sessionEndEventTypes.Contains evtType then
                        match fallbackHandler with
                        | None ->
                            reviewStore.deactivateReview sid
                            Wanxiangshu.Omp.NudgeRuntime.clearNudgeSession sid
                        | Some handler ->
                            let rawEvent = createObj [ "event", box event; "props", box (createObj [ "sessionID", box sid ]) ]
                            let! r = handler rawEvent
                            if not r.Consumed then
                                reviewStore.deactivateReview sid
                                Wanxiangshu.Omp.NudgeRuntime.clearNudgeSession sid
                    elif fallbackEventTypes.Contains evtType then
                        match fallbackHandler with
                        | Some handler ->
                            let rawEvent = createObj [ "event", box event; "props", box (createObj [ "sessionID", box sid ]) ]
                            let! _ = handler rawEvent
                            ()
                        | None -> ()
            }))

let private registerHooks (pi: obj) (services: CoreServices) : unit =
    registerAllTools pi services.ReviewStore services.FallbackRuntime services.FallbackConfig
    registerInputHandler pi services.ReviewStore
    registerSessionLifecycle pi services.ReviewStore
    registerAbortHandler pi services.ReviewStore services.FallbackHandler

let pluginFor (pi: obj) : JS.Promise<unit> =
    promise {
        installTitleFetchGuard ()
        do! patchDisablePrune ()
        let services = createCoreServices pi
        applyAgentConfigIfSupported pi
        registerHooks pi services
    }
