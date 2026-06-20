module VibeFs.Opencode.Tools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.HostTools
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.WikiTools
open VibeFs.Opencode.SubagentTools
open VibeFs.Opencode.ExecutorTool
open VibeFs.Opencode.SearchTools
open VibeFs.Opencode.ReviewTools
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.FuzzyFinderShell

let createTools (registry: ChildAgentRegistry) (finderCache: FinderCache) (ctx: obj) (wikiRuntime: VibeFs.Opencode.WikiRuntime.WikiRuntime) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
    createObj [
        "coder", box (coderTool registry ctx)
        "investigator", box (investigatorTool registry ctx)
        "meditator", box (meditatorTool registry ctx)
        "browser", box (browserTool registry ctx)
        "executor", box (executorTool registry ctx)
        "fuzzy_find", box (fuzzyFindTool finderCache)
        "fuzzy_grep", box (fuzzyGrepTool finderCache)
        "websearch", box (websearchTool registry ctx)
        "webfetch", box (webfetchTool ())
        "fetch_wiki", box (fetchWikiTool wikiRuntime ctx)
        "return_bookkeeper", box (submitWikiTool wikiRuntime)
        "submit_review", box (submitReviewTool registry ctx reviewStore)
        "return_reviewer", box (submitReviewResultTool reviewStore)
    ]
