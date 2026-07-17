module Wanxiangshu.Hosts.Mux.PluginRegistration

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.MuxPluginCatalogShell
open Wanxiangshu.Hosts.Mux.PluginCatalog
open Wanxiangshu.Hosts.Mux.BacklogSession
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Mux.WrappersReview
open Wanxiangshu.Hosts.Mux.Wrappers
open Wanxiangshu.Hosts.Mux.EventHook
open Wanxiangshu.Hosts.Mux.SlashCommands
open Wanxiangshu.Hosts.Mux.CompactionTransform
open Wanxiangshu.Hosts.Mux.MessageTransform

let createWrapperExecution (toolsObj: obj) (hostReadExec: HostFunctionCapture) (scope: RuntimeScope) : obj =
    createAllWrappers toolsObj hostReadExec scope

let createMessageTransforms
    (deps: obj)
    (scope: RuntimeScope)
    (backlogSession: BacklogSession)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    : obj * obj =
    let messagesTransformFn =
        System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
            messagesTransform deps scope backlogSession reviewStore input output)

    let compactingTransformFn =
        System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
            compactingTransform deps scope backlogSession input output)

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

let assembleRegistrationObject
    (scope: RuntimeScope)
    (tools: ToolDefinition array)
    (wrappers: obj)
    (mcpServers: obj)
    (eventHook: obj)
    (slashCommands: obj)
    (messagesTransform: obj)
    (compactingTransform: obj)
    (getToolPolicy: obj)
    (reviewTestSurface: obj)
    : obj =
    createObj
        [ "__runtimeScope", box scope
          "toolNames", box muxToolNames
          "tools", box tools
          "wrappers", box wrappers
          "mcpServers", box mcpServers
          "eventHook", box eventHook
          "slashCommands", box slashCommands
          "messagesTransform", box messagesTransform
          "compactingTransform", box compactingTransform
          "getToolPolicy", box getToolPolicy
          "__reviewStore", box reviewTestSurface
          "tool.execute.after",
          box (
              System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
                  Wanxiangshu.Hosts.Mux.PluginCatalog.toolExecuteAfter scope input output)
          ) ]

let private createScope (deps: obj) =
    let scope = create ()
    let backlogSession = BacklogSession(scope)
    let reviewStore = Wanxiangshu.Runtime.ReviewRuntime.createReviewStore ()
    let hostReadExec = HostFunctionCapture()
    let finderCache = FinderCache()

    let tools =
        createToolCatalog deps muxToolNames reviewStore hostReadExec finderCache scope

    let toolsObj = toolsToObject tools
    (scope, backlogSession, reviewStore, hostReadExec, finderCache, tools, toolsObj)

let private registerTestHooks (registration: obj) (deps: obj) : unit =
    setKey
        registration
        "tool.execute.before"
        (box (System.Func<obj, obj, JS.Promise<unit>>(fun input output -> toolExecuteBefore input output)))

    setKey
        registration
        "systemTransform"
        (box (
            System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
                let directory = if Dyn.isNullish deps then "" else Dyn.str deps "directory"
                systemTransform directory input output)
        ))

let createRegistration (deps: obj) : obj =
    Wanxiangshu.Runtime.E2eSandbox.applyFromProcessEnv ()

    let (scope, backlogSession, reviewStore, hostReadExec, _, tools, toolsObj) =
        createScope deps

    let wrappers = createWrapperExecution toolsObj hostReadExec scope

    let mcpServers =
        box {| ``stealth-browser-mcp`` = getStealthBrowserMcpCommand (envVar "STEALTH_BROWSER_MCP_REF") |}

    let messagesTransform, compactingTransform =
        createMessageTransforms deps scope backlogSession reviewStore

    let eventHook, slashCommands, getToolPolicy =
        createEventHooksSlashAndPolicy deps scope reviewStore

    let registration =
        assembleRegistrationObject
            scope
            tools
            wrappers
            mcpServers
            eventHook
            slashCommands
            messagesTransform
            compactingTransform
            getToolPolicy
            (createReviewTestSurface reviewStore)

    registerTestHooks registration deps

    scope.OnInit <-
        Some(fun dir ->
            Wanxiangshu.Runtime.EventLogRuntime.syncAllSessionsFromEventLogDedicated
                Wanxiangshu.Kernel.HostTools.mux
                reviewStore
                scope
                dir)

    let directory = if Dyn.isNullish deps then "" else Dyn.str deps "directory"

    if directory <> "" then
        scope.TriggerInit(directory)

    box registration
