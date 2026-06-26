module Wanxiangshu.Opencode.Tools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Opencode.ToolSchema
open Wanxiangshu.Opencode.KnowledgeGraphTools
open Wanxiangshu.Opencode.SubagentTools
open Wanxiangshu.Opencode.ExecutorTool
open Wanxiangshu.Opencode.SearchTools
open Wanxiangshu.Opencode.ReviewTools
open Wanxiangshu.Opencode.MimoTodoTool
open Wanxiangshu.Methodology.OpencodeTools
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.RuntimeScope

let createTools (host: Host) (registry: ChildAgentRegistry) (finderCache: FinderCache) (ctx: obj) (knowledgeGraphRuntime: Wanxiangshu.Opencode.KnowledgeGraphRuntime.KnowledgeGraphRuntime) (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) (knowledgeGraphEnabled: bool) (sessionScope: RuntimeScope) : obj =
    let iteratorStore = sessionScope.IteratorStore
    let tools =
        createObj [
            yield "coder", box (coderTool host registry ctx)
            yield "investigator", box (investigatorTool host registry ctx)
            yield "meditator", box (meditatorTool host registry ctx)
            yield "browser", box (browserTool host registry ctx)
            yield "executor", box (executorTool host registry ctx sessionScope)
            yield "fuzzy_find", box (fuzzyFindTool finderCache iteratorStore)
            yield "fuzzy_grep", box (fuzzyGrepTool finderCache iteratorStore)
            yield "websearch", box (websearchTool host registry ctx)
            yield "webfetch", box (webfetchTool ctx)
            if knowledgeGraphEnabled then
                yield "knowledge_graph_fetch", box (knowledgeGraphFetchTool knowledgeGraphRuntime ctx)
                yield "return_bookkeeper", box (returnBookkeeperTool knowledgeGraphRuntime ctx)
            yield "submit_review", box (submitReviewTool registry ctx reviewStore)
            yield "return_reviewer", box (submitReviewResultTool ctx reviewStore)
            if host = Mimocode then
                yield todoWriteToolName host, box (mimoTodoTool ctx)
        ]
    registerMethodologyTools registry ctx host tools
    tools