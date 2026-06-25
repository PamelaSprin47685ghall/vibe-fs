module VibeFs.Omp.PluginCore

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.HostTools
open VibeFs.Shell
open VibeFs.Shell.Dyn
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Omp.AgentConfig
open VibeFs.Omp.Codec
open VibeFs.Omp.KnowledgeGraph.Runtime
open VibeFs.Omp.Tools
open VibeFs.Omp.PruneGuard
open VibeFs.Omp.ReviewTools
open VibeFs.Omp.SessionLifecycle
open VibeFs.Shell.TitleFetchGuardCommon
open VibeFs.Omp.MessageTransform
open VibeFs.Shell.OmpCaps
open VibeFs.Shell.ReviewRuntime
open VibeFs.Shell.RunnerBackground
open VibeFs.Shell.SessionExecutor
open VibeFs.Shell.TreeSitterShell

type CoreServices =
    { ReviewStore: ReviewStore
      KnowledgeGraphRuntime: OmpKnowledgeGraphRuntime
      FinderCache: FinderCache
      Pi: obj }

let reviewStore : ReviewStore = createReviewStore ()

let private createCoreServices (pi: obj) : CoreServices =
    let kgRuntime = OmpKnowledgeGraphRuntime(pi)
    let finderCache = FinderCache()
    { ReviewStore = reviewStore
      KnowledgeGraphRuntime = kgRuntime
      FinderCache = finderCache
      Pi = pi }

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

let registerAbortHandler (pi: obj) (reviewStore: ReviewStore) (kgRuntime: OmpKnowledgeGraphRuntime) : unit =
    pi?on(
        "event",
        box(fun (event: obj) (ctx: obj) ->
            promise {
                let evtType = Dyn.str event "type"
                if sessionEndEventTypes.Contains evtType then
                    match getSessionIdFromContext ctx with
                    | Some sid ->
                        reviewStore.deactivateReview sid
                        VibeFs.Omp.NudgeRuntime.clearNudgeSession sid
                        kgRuntime.DeleteJob sid
                    | None -> ()
            }))

let private registerHooks (pi: obj) (services: CoreServices) : unit =
    registerAllTools pi services.ReviewStore services.KnowledgeGraphRuntime
    registerInputHandler pi services.ReviewStore
    registerSessionLifecycle pi services.ReviewStore services.KnowledgeGraphRuntime
    registerAbortHandler pi services.ReviewStore services.KnowledgeGraphRuntime

let pluginFor (pi: obj) : JS.Promise<unit> =
    promise {
        installTitleFetchGuard ()
        do! patchDisablePrune ()
        let services = createCoreServices pi
        applyAgentConfigIfSupported pi
        registerHooks pi services
    }