module VibeFs.Opencode.KnowledgeGraphTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Mux.Wrappers

let knowledgeGraphFetchTool (kgRuntime: KnowledgeGraphRuntime) (ctx: obj) : obj =
    define fetchKnowledgeGraph
        (box {| entity = strReq "Knowledge graph entity from the session snapshot" |})
        (fun args context ->
            let sessionID = Dyn.str context "sessionID"
            let directory =
                let current = Dyn.str context "directory"
                if current = "" then Dyn.str ctx "directory" else current
            kgRuntime.FetchFromSessionSnapshot(sessionID, directory, Dyn.str args "entity"))

let returnBookkeeperTool (kgRuntime: KnowledgeGraphRuntime) : obj =
    define submitKnowledgeGraph
        (box {| entries = knowledgeGraphDraftEntriesReq "Knowledge graph draft entries" |})
        (fun args context ->
            match parseDraftArray (Dyn.get args "entries") with
            | Error message -> resolveStr message
            | Ok drafts -> kgRuntime.Submit(Dyn.str context "sessionID", drafts))
