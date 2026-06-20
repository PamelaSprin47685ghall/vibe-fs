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
open VibeFs.Opencode.NudgeHook
open VibeFs.Opencode.WikiRuntime
open VibeFs.Opencode.MagicTodo
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.ChildAgentRegistry

let private twoArgHook (f: obj -> obj -> JS.Promise<unit>) = box (System.Func<obj, obj, JS.Promise<unit>>(f))

let pluginFor (host: Host) (ctx: obj) : JS.Promise<obj> =
    promise {
        installTitleFetchGuard ()
        let reviewStore = VibeFs.Shell.ReviewRuntime.createReviewStore ()
        let childAgentRegistry = ChildAgentRegistry.Create()
        let finderCache = FinderCache()
        let nudgeHook = createNudgeHook host ctx reviewStore childAgentRegistry
        let directory = Dyn.str ctx "directory"
        let nowUtc () =
            let nowMs = Dyn.get ctx "nowMs"
            if Dyn.isNullish nowMs then System.DateTime.UtcNow
            else System.DateTimeOffset.FromUnixTimeMilliseconds(int64 (unbox<float> nowMs)).UtcDateTime
        let wikiRuntime = WikiRuntime(Dyn.get ctx "client", directory, nowUtc, childAgentRegistry)
        let magicSession = MagicSession host
        let tools = createTools childAgentRegistry finderCache ctx wikiRuntime reviewStore
        let mcps = box {| ``type`` = "local"; command = VibeFs.Kernel.Config.getStealthBrowserMcpLocalConfig(envVar "STEALTH_BROWSER_MCP_REF").command |}
        let mcpMap = box {| ``stealth-browser-mcp`` = mcps |}
        let result = emptyObj ()
        setKey result "id" (box "kunwei")
        setKey result "name" (box "kunwei")
        setKey result "mcp" mcpMap
        setKey result "tool" tools
        setKey
            result
            "__wikiRuntime"
            (box (
                createObj [
                    "rawInstance", box wikiRuntime
                    "registerJobForTesting",
                    box (System.Func<string, string, string, obj, unit>(fun sessionID workspaceRoot kindTag payload ->
                        wikiRuntime.RegisterJobForTesting(sessionID, workspaceRoot, kindTag, payload)))
                    "takeBookkeeperLaunchesForTesting",
                    box (System.Func<obj array>(fun () -> wikiRuntime.TakeBookkeeperLaunchesForTesting()))
                    "waitForBackgroundJobsForTesting",
                    box (System.Func<JS.Promise<unit>>(fun () -> wikiRuntime.WaitForBackgroundJobsForTesting()))
                ]))
        setKey result "config" (box (fun (cfg: obj) ->
            promise {
                let next = applyAgentConfigFor host cfg mcpMap
                registerCommands cfg
                return assignInto cfg next
            }))
        setKey result "chat.message" (twoArgHook (fun input output -> chatMessageFor host childAgentRegistry nudgeHook input output))
        setKey result "tool.definition" (twoArgHook (fun input output -> toolDefinitionFor host input output))
        setKey result "tool.execute.before" (twoArgHook (fun input output -> toolExecuteBeforeFor host input output))
        setKey result "tool.execute.after" (twoArgHook (fun input output -> toolExecuteAfterFor host directory nudgeHook wikiRuntime childAgentRegistry input output))
        setKey result "experimental.chat.messages.transform" (twoArgHook (fun input output -> messagesTransform childAgentRegistry directory magicSession wikiRuntime reviewStore input output))
        setKey result "command.execute.before" (twoArgHook (fun input output ->
            promise {
                do! nudgeHook.handleCommandExecuteBefore input output
                do! commandExecuteBefore childAgentRegistry ctx reviewStore input output
            }))
        setKey result "event" (box (fun (input: obj) ->
            promise {
                do! eventHandler reviewStore input
                cleanUpJobContextIfAbortedOrDeleted wikiRuntime input
                do! nudgeHook.handleEvent input
            }))
        setKey result "experimental.session.compacting" (twoArgHook (fun input output -> compactingHandlerFor host magicSession input output))
        return result
    }
