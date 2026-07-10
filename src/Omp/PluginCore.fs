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
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.SessionExecutor
open Wanxiangshu.Shell.TreeSitterShell
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Shell.FallbackConfigCodec
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Omp.FallbackHooks
open Wanxiangshu.Shell.ToolHookRuntime

type CoreServices =
    { ReviewStore: ReviewStore
      FinderCache: FinderCache
      Pi: obj
      FallbackHandler: (obj -> JS.Promise<FallbackHookResult>) option
      FallbackRuntime: FallbackRuntimeState
      FallbackConfig: FallbackConfig option }

let reviewStore: ReviewStore = createReviewStore ()

let private wrapRegisterToolForSchema (pi: obj) : unit =
    let original = Dyn.get pi "registerTool"

    if Dyn.isNullish original || not (Dyn.typeIs original "function") then
        ()
    else
        pi?registerTool <-
            box (fun (toolDef: obj) ->
                let name = Dyn.str toolDef "name"

                if name <> "" then
                    let parameters = Dyn.get toolDef "parameters"

                    if not (Dyn.isNullish parameters) then
                        registerSchemaTypes name parameters

                Dyn.call1 original toolDef |> ignore)

let private createCoreServices (pi: obj) : CoreServices =
    wrapRegisterToolForSchema pi
    let finderCache = FinderCache()

    let directory = Dyn.str pi "directory"
    let fallbackConfigOpt = loadFallbackConfig directory
    let fallbackRuntime = FallbackRuntimeState()

    let configLookup: ConfigLookup =
        match fallbackConfigOpt with
        | Some cfg -> (fun _ -> cfg)
        | None -> (fun _ -> emptyConfig)

    let sessionApi = Dyn.get pi "session"

    let fallbackHandler =
        Some(createOmpFallbackHandler fallbackRuntime configLookup sessionApi)

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
            let currentRaw: obj = Dyn.call0 getConfig
            let baseCfg = if Dyn.isNullish currentRaw then emptyObj () else currentRaw
            let next = applyAgentConfigFor baseCfg
            Dyn.call1 setConfig next |> ignore
        with _ ->
            ()

/// `session.abort` / `stream.abort` / `session.error` all collapse to the
/// same outcome: in-flight review state must clear. Without this hook,
/// review state survives host-driven aborts and leaks across sessions.
let private sessionEndEventTypes =
    Set
        [ "session.abort"
          "stream.abort"
          "session.error"
          "session.delete"
          "session.close"
          "session.remove"
          "session.deleted"
          "session.interrupted" ]

let registerAbortHandler
    (pi: obj)
    (reviewStore: ReviewStore)
    (fallbackRuntime: FallbackRuntimeState)
    (fallbackHandler: (obj -> JS.Promise<FallbackHookResult>) option)
    : unit =
    let fallbackEventTypes =
        Set [ "session.busy"; "session.idle"; "message.updated"; "session.updated" ]

    pi?on (
        "event",
        box (fun (event: obj) (ctx: obj) ->
            promise {
                let evtType = Dyn.str event "type"
                let sidOpt = getSessionIdFromContext ctx

                match sidOpt with
                | None -> ()
                | Some sid ->
                    if sessionEndEventTypes.Contains evtType then
                        match fallbackHandler with
                        | None ->
                            let root = Dyn.str ctx "cwd"

                            if root <> "" then
                                do! appendLoopCancelledOrFail root sid
                                do! syncReviewFromEventLogDedicated reviewStore root sid

                            Wanxiangshu.Omp.NudgeRuntime.markSessionForceStopped sid

                            Wanxiangshu.Shell.RunnerBackground.abortRunnerJobCore
                                Wanxiangshu.Omp.ExecutorTools.ompScope
                                sid
                        | Some handler ->
                            let rawEvent =
                                createObj [ "event", box event; "props", box (createObj [ "sessionID", box sid ]) ]

                            if sid <> "" then
                                fallbackRuntime.SetEventHandlingActive sid true

                            try
                                let! r = handler rawEvent

                                if not r.Consumed then
                                    let root = Dyn.str ctx "cwd"

                                    if root <> "" then
                                        do! appendLoopCancelledOrFail root sid
                                        do! syncReviewFromEventLogDedicated reviewStore root sid

                                    Wanxiangshu.Omp.NudgeRuntime.markSessionForceStopped sid

                                    Wanxiangshu.Shell.RunnerBackground.abortRunnerJobCore
                                        Wanxiangshu.Omp.ExecutorTools.ompScope
                                        sid
                            finally
                                if sid <> "" then
                                    fallbackRuntime.SetEventHandlingActive sid false
                    elif fallbackEventTypes.Contains evtType then
                        match fallbackHandler with
                        | Some handler ->
                            let rawEvent =
                                createObj [ "event", box event; "props", box (createObj [ "sessionID", box sid ]) ]

                            if sid <> "" then
                                fallbackRuntime.SetEventHandlingActive sid true

                            try
                                let! _ = handler rawEvent
                                ()
                            finally
                                if sid <> "" then
                                    fallbackRuntime.SetEventHandlingActive sid false
                        | None -> ()
            })
    )

let private registerHooks (pi: obj) (services: CoreServices) : unit =
    registerAllTools pi services.ReviewStore services.FallbackRuntime services.FallbackConfig
    registerInputHandler pi services.ReviewStore
    registerSessionLifecycle pi services.ReviewStore services.FallbackRuntime
    registerAbortHandler pi services.ReviewStore services.FallbackRuntime services.FallbackHandler

let pluginFor (pi: obj) : JS.Promise<unit> =
    promise {
        installTitleFetchGuard ()
        do! patchDisablePrune ()
        let services = createCoreServices pi
        applyAgentConfigIfSupported pi
        registerHooks pi services
    }
