module Wanxiangshu.Hosts.Opencode.Tools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Hosts.Opencode.SubagentTools
open Wanxiangshu.Hosts.Opencode.ExecutorTool
open Wanxiangshu.Hosts.Opencode.PtySpawn
open Wanxiangshu.Hosts.Opencode.PtyWriteTool
open Wanxiangshu.Hosts.Opencode.PtyReadTool
open Wanxiangshu.Hosts.Opencode.SearchTools
open Wanxiangshu.Hosts.Opencode.ReviewTools
open Wanxiangshu.Hosts.Opencode.MimoTodoTool
open Wanxiangshu.Hosts.Opencode.OpencodeTools
open Wanxiangshu.Hosts.Opencode.SwapTool
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.Fallback.RuntimeStore

let createTools
    (host: Host)
    (registry: ChildAgentRegistry)
    (finderCache: FinderCache)
    (ctx: obj)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (sessionScope: RuntimeScope)
    (fallbackRuntime: FallbackRuntimeStore)
    : obj =
    let iteratorStore = sessionScope.IteratorStore

    let tools =
        createObj
            [ yield "coder", box (coderTool host registry ctx fallbackRuntime sessionScope)
              yield "inspector", box (inspectorTool host registry ctx fallbackRuntime sessionScope)
              yield "browser", box (browserTool host registry ctx fallbackRuntime sessionScope)
              yield "continue", box (continueTool host registry ctx fallbackRuntime sessionScope)
              yield "executor", box (executorTool host registry ctx sessionScope fallbackRuntime)
              yield "pty_spawn", box (ptySpawnTool host)
              yield "pty_write", box (ptyWriteTool host)
              yield "pty_read", box (ptyReadTool host)
              yield "pty_list", box (ptyListTool host)
              yield "pty_kill", box (ptyKillTool host)
              yield "fuzzy_find", box (fuzzyFindTool finderCache iteratorStore)
              yield "fuzzy_grep", box (fuzzyGrepTool finderCache iteratorStore)
              yield "fuzzy_continue", box (fuzzyContinueTool finderCache iteratorStore)
              yield "web_search", box (websearchTool host registry ctx fallbackRuntime)
              yield "webfetch", box (webfetchTool ctx)
              yield "submit_review", box (submitReviewTool registry ctx reviewStore sessionScope)
              yield "return_reviewer", box (submitReviewResultTool ctx reviewStore sessionScope)
              yield "swap", box (swapTool ())
              if host = Mimocode then
                  yield todoWriteToolName host, box (mimoTodoTool ctx) ]

    registerMeditatorTools registry ctx host fallbackRuntime tools
    tools

// Test helper that supplies an empty fallback runtime.
let createToolsForTests
    (host: Host)
    (registry: ChildAgentRegistry)
    (finderCache: FinderCache)
    (ctx: obj)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (sessionScope: RuntimeScope)
    : obj =
    createTools host registry finderCache ctx reviewStore sessionScope (FallbackRuntimeStore())
