module VibeFs.MuxPlugin.MuxTools

open VibeFs.Mux.Contract
open VibeFs.MuxPlugin.MuxTools.AgentTools
open VibeFs.MuxPlugin.MuxTools.IoTools
open VibeFs.MuxPlugin.MuxTools.SearchTools
open VibeFs.MuxPlugin.MuxTools.WebTools
open VibeFs.MuxPlugin.MuxTools.ReviewTool

/// Build the full ordered tool list for createRegistration.
let createToolCatalog (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) : ToolDefinition array =
    [| editorTool; greperTool; reverieTool; browserTool; executorTool
       submitReviewTool reviewStore; websearchTool; webfetchTool
       fuzzyGrepTool; fuzzyFindTool; writeTool |]
