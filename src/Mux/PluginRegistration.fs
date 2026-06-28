module Wanxiangshu.Mux.PluginRegistration

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Shell
open Wanxiangshu.Mux.PluginCatalog
open Wanxiangshu.Mux.PluginRegistrationParts
open Wanxiangshu.Mux.BacklogSession
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Mux.WrappersReview

let private createScope (deps: obj) =
    let scope = create ()
    let backlogSession = BacklogSession(scope)
    let reviewStore = Wanxiangshu.Shell.ReviewRuntime.createReviewStore ()
    let hostReadExec = HostFunctionCapture()
    let finderCache = FinderCache()
    let tools = createToolCatalog deps muxToolNames reviewStore hostReadExec finderCache scope
    let toolsObj = toolsToObject tools
    (scope, backlogSession, reviewStore, hostReadExec, finderCache, tools, toolsObj)

let private registerTestHooks
    (registration: obj)
    (deps: obj)
    : unit =
    setKey registration "tool.execute.before" (box (System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
        toolExecuteBefore input output)))
    setKey registration "systemTransform" (box (System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
        let directory = if Dyn.isNullish deps then "" else Dyn.str deps "directory"
        systemTransform directory input output)))

let createRegistration (deps: obj) : obj =
    let (scope, backlogSession, reviewStore, hostReadExec, _, tools, toolsObj) =
        createScope deps
    let wrappers = createWrapperExecution toolsObj hostReadExec scope
    let mcpServers = box {| ``stealth-browser-mcp`` = getStealthBrowserMcpCommand (envVar "STEALTH_BROWSER_MCP_REF") |}
    let messagesTransform, compactingTransform =
        createMessageTransforms deps scope backlogSession reviewStore
    let eventHook, slashCommands, getToolPolicy =
        createEventHooksSlashAndPolicy deps reviewStore
    let registration =
        assembleRegistrationObject scope tools wrappers mcpServers (createContextInjector ())
            eventHook slashCommands messagesTransform compactingTransform getToolPolicy
            (createReviewTestSurface reviewStore)
    registerTestHooks registration deps
    box registration