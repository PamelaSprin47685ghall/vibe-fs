module VibeFs.MuxPlugin.MuxTools

open VibeFs.Mux.Contract
open VibeFs.MuxPlugin.PlanTools
open VibeFs.MuxPlugin.MuxTools.AgentTools
open VibeFs.MuxPlugin.MuxTools.IoTools
open VibeFs.MuxPlugin.MuxTools.SearchTools
open VibeFs.MuxPlugin.MuxTools.WebTools
open VibeFs.MuxPlugin.MuxTools.ReviewTool

/// Build the full ordered tool list for createRegistration.
let createToolCatalog (deps: obj) (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) : ToolDefinition array =
    [| yield! allPlanTools
       editorTool deps
       greperTool deps
       reverieTool deps
       browserTool deps
       executorTool deps
       submitReviewTool deps reviewStore
       websearchTool
       webfetchTool
       fuzzyGrepTool
       fuzzyFindTool
       writeTool deps
       readTool deps |]
