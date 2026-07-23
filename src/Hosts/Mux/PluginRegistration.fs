module Wanxiangshu.Hosts.Mux.PluginRegistration

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.MuxPluginCatalogShell
open Wanxiangshu.Hosts.Mux.PluginCatalog
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Mux.WrappersReview
open Wanxiangshu.Hosts.Mux.Wrappers
open Wanxiangshu.Hosts.Mux.EventHook
open Wanxiangshu.Hosts.Mux.SlashCommands
open Wanxiangshu.Hosts.Mux.MessageTransform
open Wanxiangshu.Kernel.HostCapability
open Wanxiangshu.Hosts.Mux.PluginRegistrationAssembly
open Wanxiangshu.Runtime.EventLogRuntimeRecovery

let createWrapperExecution (toolsObj: obj) (hostReadExec: HostFunctionCapture) (scope: RuntimeScope) : obj =
    createAllWrappers toolsObj hostReadExec scope

let createMessageTransforms
    (deps: obj)
    (scope: RuntimeScope)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    : obj * obj =
    let messagesTransformFn =
        System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
            messagesTransform deps scope reviewStore input output)

    let compactingTransformFn =
        System.Func<obj, obj, JS.Promise<unit>>(fun input _output ->
            promise {
                let sid =
                    let s1 = Dyn.str input "sessionID"

                    if s1 <> "" then
                        s1
                    else
                        let s2 = Dyn.str input "sessionId"
                        if s2 <> "" then s2 else Dyn.str input "session_id"

                if sid <> "" then
                    Wanxiangshu.Runtime.MessageTransform.CapsStage.invalidateCapsAfterCompaction scope sid
            })

    (box messagesTransformFn, box compactingTransformFn)

let createEventHooksSlashAndPolicy
    (deps: obj)
    (scope: RuntimeScope)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    : obj * obj * obj =
    let eventHook = createEventHook deps reviewStore scope

    let slashCommands = createSlashCommands scope deps muxToolNames reviewStore

    let getToolPolicy =
        System.Func<string, obj, obj>(fun (_agentId: string) (role: obj) -> buildToolPolicy muxToolNames role)

    (box eventHook, box slashCommands, box getToolPolicy)

let createReviewTestSurface (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore) : obj =
    createObj
        [ "applyReviewTaskProjection",
          box (
              System.Func<string, string option, unit>(fun sessionID task ->
                  reviewStore.applyReviewTaskProjection (sessionID, task))
          )
          "getReviewTask",
          box (System.Func<string, string option>(fun sessionID -> reviewStore.getReviewTask sessionID))
          "tryLockReview", box (System.Func<string, bool>(fun sessionID -> reviewStore.tryLockReview sessionID))
          "unlockReview", box (System.Func<string, unit>(fun sessionID -> reviewStore.unlockReview sessionID)) ]

let private createScope (deps: obj) =
    let scope = create ()
    let reviewStore = Wanxiangshu.Runtime.ReviewRuntime.createReviewStore ()
    let hostReadExec = HostFunctionCapture()
    let finderCache = FinderCache()

    let tools =
        createToolCatalog deps muxToolNames reviewStore hostReadExec finderCache scope

    let toolsObj = toolsToObject tools
    (scope, reviewStore, hostReadExec, finderCache, tools, toolsObj)

let private buildInitHandler
    (scope: RuntimeScope)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    : (string -> JS.Promise<unit>) =
    fun dir ->
        promise {
            // Mux lacks SubsessionHostAdapter; use explicit no-op reconcile host
            // that rejects all operations with typed errors instead of silent no-op.
            let reconcileHostFactory _ =
                Wanxiangshu.Runtime.SubsessionReconcile.createReconcileHost ()

            do!
                Wanxiangshu.Runtime.SubsessionReconcile.reconcileUnfinishedRuns dir (Some reconcileHostFactory)
                |> Promise.map ignore

            do!
                Wanxiangshu.Runtime.EventLogRuntimeSync.syncAllSessionsFromEventLogDedicated
                    Wanxiangshu.Kernel.HostTools.mux
                    reviewStore
                    scope
                    dir

            do! recoverRequestedFallbackLeases scope dir
        }

let private registerLifecycleHandlers
    (scope: RuntimeScope)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    : unit =
    scope.OnInit <- Some(buildInitHandler scope reviewStore)

    SubsessionActorRegistry.SubsessionActorRegistry.RegisterGlobalCleanup(fun workspaceRoot sessionId ->
        if workspaceRoot <> "" && sessionId <> "" then
            let ws =
                Wanxiangshu.Kernel.Primitives.Identity.Id.workspaceIdQuick ("mux:" + workspaceRoot)

            Wanxiangshu.Runtime.RuntimeScopeForgetSession.forgetSession scope sessionId
            Wanxiangshu.Runtime.RunnerBackground.abortRunnerJobCore scope sessionId
            reviewStore.CleanupSession sessionId
            Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance sessionId
            Wanxiangshu.Runtime.ToolHookRuntime.closeSession sessionId
            Wanxiangshu.Runtime.SubsessionPendingEvidence.SubsessionPendingEvidence.ForgetSession sessionId

            Wanxiangshu.Runtime.Dispatch.DispatchRegistryInstance.sharedDispatchRegistry.NotifySessionClosed
                ws
                sessionId)

let createRegistrationWithSeams
    (deps: obj)
    : {| Registration: obj
         Scope: RuntimeScope
         ReviewStore: obj |}
    =
    Wanxiangshu.Runtime.E2eSandbox.applyFromProcessEnv ()

    let (scope, reviewStore, hostReadExec, _, tools, toolsObj) = createScope deps

    let wrappers = createWrapperExecution toolsObj hostReadExec scope

    let mcpServers =
        box {| ``stealth-browser-mcp`` = getStealthBrowserMcpCommand (envVar "STEALTH_BROWSER_MCP_REF") |}

    let messagesTransform, compactingTransform =
        createMessageTransforms deps scope reviewStore

    let eventHook, slashCommands, getToolPolicy =
        createEventHooksSlashAndPolicy deps scope reviewStore

    let registration =
        PluginRegistrationAssembly.assembleRegistrationObject
            deps
            scope
            tools
            wrappers
            mcpServers
            eventHook
            slashCommands
            messagesTransform
            compactingTransform
            getToolPolicy

    registerLifecycleHandlers scope reviewStore

    let directory = if Dyn.isNullish deps then "" else Dyn.str deps "directory"

    if directory <> "" then
        scope.TriggerInit(directory)

    {| Registration = registration
       Scope = scope
       ReviewStore = createReviewTestSurface reviewStore |}

let createRegistration (deps: obj) : obj =
    (createRegistrationWithSeams deps).Registration
