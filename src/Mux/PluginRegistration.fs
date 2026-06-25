module VibeFs.Mux.PluginRegistration

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Config
open VibeFs.Shell
open VibeFs.Mux.PluginCatalog
open VibeFs.Mux.PluginRegistrationParts
open VibeFs.Mux.BacklogSession
open VibeFs.Mux.KnowledgeGraphRuntimeMux
open VibeFs.Shell.RuntimeScope
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Mux.WrappersReview

let private createScope (deps: obj) =
    let scope = create ()
    let backlogSession = BacklogSession(scope)
    let reviewStore = VibeFs.Shell.ReviewRuntime.createReviewStore ()
    let hostReadExec = HostFunctionCapture()
    let finderCache = FinderCache()
    let knowledgeGraphRuntime = MuxKnowledgeGraphRuntime(deps)
    let tools = createToolCatalog deps muxToolNames reviewStore hostReadExec finderCache knowledgeGraphRuntime scope
    let toolsObj = toolsToObject tools
    (scope, backlogSession, reviewStore, hostReadExec, finderCache, knowledgeGraphRuntime, tools, toolsObj)

let private registerTestHooks
    (registration: obj)
    (knowledgeGraphRuntime: MuxKnowledgeGraphRuntime)
    (deps: obj)
    : unit =
    setKey registration "tool.execute.after" (box (System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
        toolExecuteAfter knowledgeGraphRuntime deps input output)))
    setKey registration "tool.execute.before" (box (System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
        toolExecuteBefore input output)))
    setKey registration "systemTransform" (box (System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
        systemTransform input output)))

let createRegistration (deps: obj) : obj =
    let (scope, backlogSession, reviewStore, hostReadExec, _, knowledgeGraphRuntime, tools, toolsObj) =
        createScope deps
    let wrappers = createWrapperExecution toolsObj hostReadExec scope
    let mcpServers = box {| ``stealth-browser-mcp`` = getStealthBrowserMcpCommand (envVar "STEALTH_BROWSER_MCP_REF") |}
    let messagesTransform, compactingTransform =
        createMessageTransforms deps scope backlogSession knowledgeGraphRuntime reviewStore
    let eventHook, slashCommands, getToolPolicy =
        createEventHooksSlashAndPolicy deps reviewStore knowledgeGraphRuntime
    let registration =
        assembleRegistrationObject scope tools wrappers mcpServers (createContextInjector ())
            eventHook slashCommands messagesTransform compactingTransform getToolPolicy
            (createKnowledgeGraphTestSurface knowledgeGraphRuntime) (createReviewTestSurface reviewStore)
    registerTestHooks registration knowledgeGraphRuntime deps
    box registration