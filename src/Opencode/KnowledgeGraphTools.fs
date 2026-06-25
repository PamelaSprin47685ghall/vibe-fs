module VibeFs.Opencode.KnowledgeGraphTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.ToolCatalog
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Shell.PromiseStr
open VibeFs.Shell.ToolRuntimeContext
open VibeFs.Shell.KnowledgeGraphToolsCodec
open VibeFs.Shell.ToolExecute

let knowledgeGraphFetchTool (kgRuntime: KnowledgeGraphRuntime) (ctx: obj) : obj =
    define fetchKnowledgeGraph
        (box {| entity = strReq Params.fetchKnowledgeGraphEntity |})
        (fun args context ->
            match decodeFetchEntity args with
            | Error e -> resolveStr (wireDecodeFailure "knowledge_graph_fetch" e)
            | Ok entity ->
                let pluginDirectory = pluginDirectoryFromCtx ctx
                let runtime = fromOpencode context pluginDirectory
                kgRuntime.FetchFromSessionSnapshot(VibeFs.Kernel.Domain.Id.sessionIdValue runtime.Execution.SessionId, runtime.Execution.Directory, entity))

let returnBookkeeperTool (kgRuntime: KnowledgeGraphRuntime) (ctx: obj) : obj =
    define submitKnowledgeGraph
        (box {| entries = knowledgeGraphDraftEntriesReq Params.submitKnowledgeGraphEntries |})
        (fun args context ->
            let pluginDirectory = pluginDirectoryFromCtx ctx
            let runtime = fromOpencode context pluginDirectory
            match decodeReturnBookkeeperArgs args with
            | Error e -> resolveStr (wireDecodeFailure "return_bookkeeper" e)
            | Ok drafts -> kgRuntime.Submit(VibeFs.Kernel.Domain.Id.sessionIdValue runtime.Execution.SessionId, drafts))
