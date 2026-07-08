module Wanxiangshu.Tests.OmpPluginCoreIntegrationTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Omp
open Wanxiangshu.Omp.Plugin
open Wanxiangshu.Omp.PluginCore
open Wanxiangshu.Shell

module Dyn = Wanxiangshu.Shell.Dyn

let private reviewStore = PluginCore.reviewStore

type private PiHarness =
    { hookStore: obj
      tools: ResizeArray<obj>
      commands: ResizeArray<obj>
      messages: ResizeArray<obj> }

let private createHarness () : PiHarness =
    let tools = ResizeArray<obj>()
    let commands = ResizeArray<obj>()
    let messages = ResizeArray<obj>()

    let hookStore =
        createObj
            [ "tools", box tools
              "commands", box commands
              "messages", box messages
              "events", box (createObj [])
              "activeTools",
              box
                  [| "read"
                     "edit"
                     "write"
                     "find"
                     "fuzzy_find"
                     "fuzzy_grep"
                     "lsp"
                     "browser"
                     "search"
                     "glob"
                     "bash"
                     "coder"
                     "investigator"
                     "meditator"
                     "browser"
                     "executor"
                     "executor_wait"
                     "executor_abort"
                     "submit_review"
                     "return_reviewer"
                     "websearch"
                     "webfetch"
                     "todowrite" |] ]

    { hookStore = hookStore
      tools = tools
      commands = commands
      messages = messages }

let private piObject (h: PiHarness) : obj =
    let tb =
        createObj
            [ "Type",
              box (
                  createObj
                      [ "Object", box (fun (p: obj) -> createObj [ "type", box "object"; "properties", box p ])
                        "String", box (fun (_: obj) -> createObj [ "type", box "string" ])
                        "Number", box (fun (_: obj) -> createObj [ "type", box "number" ])
                        "Boolean", box (fun (_: obj) -> createObj [ "type", box "boolean" ])
                        "Null", box (fun (_: obj) -> createObj [ "type", box "null" ])
                        "Union", box (fun (_: obj) -> createObj [ "anyOf", box [||] ])
                        "Enum", box (fun (_: obj) (_: obj) -> createObj [ "type", box "enum" ])
                        "Array", box (fun (_: obj) -> createObj [ "type", box "array" ])
                        "Optional", box (fun (s: obj) -> s) ]
              ) ]

    let pi =
        emitJsExpr
            h.hookStore
            """((hs) => ({
            on(event, handler) {
                if (!hs.events[event]) hs.events[event] = [];
                hs.events[event].push(handler);
            },
            registerTool(tool) { hs.tools.push(tool); },
            registerCommand(name, config) { hs.commands.push({ name, config }); },
            sendMessage(message, options) { hs.messages.push({ message, options }); },
            getActiveTools() { return hs.activeTools; },
            setActiveTools(names) { hs.activeTools = names; return Promise.resolve(); }
        }))($0)"""
        |> unbox<obj>

    pi?("typebox") <- tb
    pi

/// `wanxiangshuExtension` must be idempotent at the tool-registration level.
let extensionIsIdempotent () =
    promise {
        RunnerBackground.clearRunnerLogsForTest ExecutorTools.ompScope
        reviewStore.clearReviewSessions ()
        let h = createHarness ()
        let pi = piObject h
        do! Plugin.wanxiangshuExtension pi
        let count1 = h.tools.Count
        do! Plugin.wanxiangshuExtension pi
        check "second registration did not add tools" (h.tools.Count = count1)
    }

/// `wanxiangshuExtension` registers the lifecycle hooks `before_agent_start`,
/// `tool_result`, `agent_end`, `session_start`, `session_shutdown`, and
/// `input` — all of these handlers should be present in the harness.
let extensionRegistersLifecycleHooks () =
    promise {
        RunnerBackground.clearRunnerLogsForTest ExecutorTools.ompScope
        reviewStore.clearReviewSessions ()
        let h = createHarness ()
        let pi = piObject h
        do! Plugin.wanxiangshuExtension pi
        let events = Dyn.get h.hookStore "events"

        let has name =
            Dyn.truthy (Dyn.get events name)
            && (unbox<obj array> (Dyn.get events name)).Length > 0

        check "before_agent_start hook" (has "before_agent_start")
        check "tool_result hook" (has "tool_result")
        check "agent_end hook" (has "agent_end")
        check "session_start hook" (has "session_start")
        check "session_shutdown hook" (has "session_shutdown")
        check "input hook" (has "input")
        check "event hook" (has "event")
        check "context hook" (has "context")
    }

/// After `wanxiangshuExtension`, the shared `reviewStore` is wired such that
/// pre-activation through the exposed handle is visible to a tool.
let reviewStoreSharedWithTools () =
    promise {
        RunnerBackground.clearRunnerLogsForTest ExecutorTools.ompScope
        reviewStore.clearReviewSessions ()
        let h = createHarness ()
        let pi = piObject h
        do! Plugin.wanxiangshuExtension pi
        let sessionId = "shared-store-1"
        reviewStore.activateReview (sessionId, "t", 0L)
        check "plugin pre-activation visible" (reviewStore.getReviewTask sessionId = Some "t")
        reviewStore.deactivateReview sessionId
        check "deactivation observed" (reviewStore.getReviewState sessionId |> Option.isNone)
    }
