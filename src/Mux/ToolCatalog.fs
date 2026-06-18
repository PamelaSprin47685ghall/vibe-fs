module VibeFs.Mux.ToolCatalog

open VibeFs.Mux.Contract
open VibeFs.Mux.CallStore
open VibeFs.Mux.AgentTools
open VibeFs.Mux.IoTools
open VibeFs.Mux.WebSearchTools
open VibeFs.Mux.ReviewTool
open VibeFs.Shell.FuzzyFinderShell

let createToolCatalog
    (deps: obj)
    (callStore: CallStore)
    (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
    (hostReadExec: HostReadExec)
    (finderCache: FinderCache)
    : ToolDefinition array =
    let tools =
        [| coderTool deps
           readerTool deps
           meditatorTool deps
           browserTool deps
           executorTool deps
           submitReviewTool deps callStore reviewStore
           websearchTool deps
           webfetchTool
           fuzzyGrepTool finderCache
           fuzzyFindTool finderCache
           writeTool deps
           readTool deps hostReadExec |]
    registeredToolNames <- tools |> Array.map (fun t -> t.name)
    tools
