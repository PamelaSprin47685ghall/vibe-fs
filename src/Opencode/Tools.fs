module VibeFs.Opencode.Tools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.HostTools
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.KnowledgeGraphTools
open VibeFs.Opencode.SubagentTools
open VibeFs.Opencode.ExecutorTool
open VibeFs.Opencode.SearchTools
open VibeFs.Opencode.ReviewTools
open VibeFs.Opencode.MethodologyTool
open VibeFs.Opencode.MimoTodoTool
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.FuzzyFinderShell

let createTools (host: Host) (registry: ChildAgentRegistry) (finderCache: FinderCache) (ctx: obj) (knowledgeGraphRuntime: VibeFs.Opencode.KnowledgeGraphRuntime.KnowledgeGraphRuntime) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (knowledgeGraphEnabled: bool) : obj =
    createObj [
        yield "coder", box (coderTool registry ctx)
        yield "investigator", box (investigatorTool registry ctx)
        yield "meditator", box (meditatorTool registry ctx)
        yield "browser", box (browserTool registry ctx)
        yield "executor", box (executorTool registry ctx)
        yield "fuzzy_find", box (fuzzyFindTool finderCache)
        yield "fuzzy_grep", box (fuzzyGrepTool finderCache)
        yield "websearch", box (websearchTool registry ctx)
        yield "webfetch", box (webfetchTool ())
        yield "select_methodology", box (selectMethodologyTool ())
        if knowledgeGraphEnabled then
            yield "knowledge_graph_fetch", box (knowledgeGraphFetchTool knowledgeGraphRuntime ctx)
            yield "return_bookkeeper", box (returnBookkeeperTool knowledgeGraphRuntime)
        yield "submit_review", box (submitReviewTool registry ctx reviewStore)
        yield "return_reviewer", box (submitReviewResultTool ctx reviewStore)
        if host = Mimocode then
            yield todoWriteToolName host, box (mimoTodoTool ctx)
    ]
