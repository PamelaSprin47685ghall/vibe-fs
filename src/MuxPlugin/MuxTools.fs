module VibeFs.MuxPlugin.MuxTools

open VibeFs.Mux.Contract
open VibeFs.MuxPlugin.CallStore
open VibeFs.MuxPlugin.MuxTools.AgentTools
open VibeFs.MuxPlugin.MuxTools.IoTools
open VibeFs.MuxPlugin.MuxTools.SearchTools
open VibeFs.MuxPlugin.MuxTools.WebTools
open VibeFs.MuxPlugin.MuxTools.ReviewTool
open VibeFs.Shell.FuzzySearch

/// Tool names populated by `createToolCatalog` so that `experimentsFor` can
/// compute the disabled-tool list via `canUse`.  Set once at registration time.
let mutable registeredToolNames: string array = [||]

/// Build the full ordered tool list for createRegistration.
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
