module VibeFs.Opencode.PluginCore

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open VibeFs.Kernel
open VibeFs.Kernel.Config
open VibeFs.Kernel.HostTools
open VibeFs.Opencode.AgentConfig
open VibeFs.Opencode.CommandHooks
open VibeFs.Opencode.ChatHooks
open VibeFs.Opencode.MessageTransform
open VibeFs.Opencode.ToolDefinitionHooks
open VibeFs.Opencode.EventHooks
open VibeFs.Opencode.Tools
open VibeFs.Opencode.HookExecute
open VibeFs.Opencode.TitleFetchGuard
open VibeFs.Opencode.SessionLifecycleObserver
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Opencode.KnowledgeGraphTestHooks
open VibeFs.Opencode.BacklogSession
open VibeFs.Shell.RuntimeScope
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell
open VibeFs.Shell.ToolRuntimeContext
open VibeFs.Shell.Dyn

let private twoArgHook (f: obj -> obj -> JS.Promise<unit>) = box (System.Func<obj, obj, JS.Promise<unit>>(f))

type private CoreServices = {
    ReviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore
    ChildAgentRegistry: ChildAgentRegistry
    SessionLifecycleObserver: SessionLifecycleObserver
    Directory: string
    RuntimeScope: RuntimeScope
    KnowledgeGraphRuntime: KnowledgeGraphRuntime
    BacklogSession: BacklogSession
    Tools: obj
    McpMap: obj
}

let private createCoreServices (host: Host) (ctx: obj) =
    let reviewStore = VibeFs.Shell.ReviewRuntime.createReviewStore ()
    let childAgentRegistry = ChildAgentRegistry.Create()
    let finderCache = FinderCache()
    let lifecycleObserver = createSessionLifecycleObserver host ctx reviewStore childAgentRegistry
    let directory = pluginDirectoryFromCtx ctx
    let nowUtc () =
        let nowMs = Dyn.get ctx "nowMs"
        if Dyn.isNullish nowMs then System.DateTime.UtcNow
        else System.DateTimeOffset.FromUnixTimeMilliseconds(int64 (unbox<float> nowMs)).UtcDateTime
    let knowledgeGraphEnabled = knowledgeGraphDirExists directory
    let knowledgeGraphRuntime = KnowledgeGraphRuntime(Dyn.get ctx "client", directory, nowUtc, childAgentRegistry, 30000L, 1000)
    let scope = create ()
    let backlogSession = BacklogSession(host, scope)
    let tools = createTools host childAgentRegistry finderCache ctx knowledgeGraphRuntime reviewStore knowledgeGraphEnabled scope
    let mcps = box {| ``type`` = "local"; command = VibeFs.Kernel.Config.getStealthBrowserMcpLocalConfig(envVar "STEALTH_BROWSER_MCP_REF").command |}
    let mcpMap = box {| ``stealth-browser-mcp`` = mcps |}
    {
        ReviewStore = reviewStore
        ChildAgentRegistry = childAgentRegistry
        SessionLifecycleObserver = lifecycleObserver
        Directory = directory
        RuntimeScope = scope
        KnowledgeGraphRuntime = knowledgeGraphRuntime
        BacklogSession = backlogSession
        Tools = tools
        McpMap = mcpMap
    }

let private registerHooks (result: obj) (host: Host) (ctx: obj) (services: CoreServices) =
    setKey result "chat.message" (twoArgHook (fun input output -> chatMessageFor host services.ChildAgentRegistry services.SessionLifecycleObserver input output))
    setKey result "tool.definition" (twoArgHook (fun input output -> toolDefinitionFor host input output))
    setKey result "tool.execute.before" (twoArgHook (fun input output -> toolExecuteBeforeFor host input output))
    setKey result "tool.execute.after" (twoArgHook (fun input output -> toolExecuteAfterFor host services.Directory services.SessionLifecycleObserver services.KnowledgeGraphRuntime services.ChildAgentRegistry input output))
    setKey result "experimental.chat.messages.transform" (twoArgHook (fun input output -> messagesTransform services.ChildAgentRegistry services.Directory services.RuntimeScope services.BacklogSession services.KnowledgeGraphRuntime services.ReviewStore input output))
    setKey result "command.execute.before" (twoArgHook (fun input output ->
        promise {
            do! services.SessionLifecycleObserver.handleCommandExecuteBefore input output
            do! commandExecuteBefore services.ChildAgentRegistry ctx services.ReviewStore input output
        }))
    setKey result "event" (box (fun (input: obj) ->
        promise {
            do! eventHandler services.ReviewStore input
            cleanUpJobContextIfAbortedOrDeleted services.KnowledgeGraphRuntime input
            do! services.SessionLifecycleObserver.handleEvent input
        }))
    setKey result "experimental.session.compacting" (twoArgHook (fun input output -> compactingHandlerFor host services.BacklogSession input output))
    setKey result "experimental.chat.system.transform" (twoArgHook (fun input output -> HookTransform.systemTransform input output))

let pluginFor (host: Host) (ctx: obj) : JS.Promise<obj> =
    promise {
        installTitleFetchGuard ()
        let services = createCoreServices host ctx
        let result = emptyObj ()
        setKey result "id" (box "kunwei")
        setKey result "name" (box "kunwei")
        setKey result "mcp" services.McpMap
        setKey result "tool" services.Tools
        setKey
            result
            "__knowledgeGraphRuntime"
            (box (
                let hooks = services.KnowledgeGraphRuntime.TestHooks
                createObj [
                    "rawInstance", box services.KnowledgeGraphRuntime
                    "registerJobForTesting",
                    box (System.Func<string, string, string, obj, unit>(fun sessionID workspaceRoot kindTag payload ->
                        hooks.RegisterJob(sessionID, workspaceRoot, kindTag, payload)))
                    "takeBookkeeperLaunchesForTesting",
                    box (System.Func<obj array>(fun () -> hooks.TakeLaunches()))
                    "waitForBackgroundJobsForTesting",
                    box (System.Func<JS.Promise<unit>>(fun () -> hooks.WaitJobs()))
                ]))
        setKey result "config" (box (fun (cfg: obj) ->
            promise {
                let next = applyAgentConfigFor host cfg services.McpMap
                registerCommands cfg
                return assignInto cfg next
            }))
        registerHooks result host ctx services
        return result
    }