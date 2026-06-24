module VibeFs.Mux.KnowledgeGraphToolDefs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Shell

open VibeFs.Kernel.ToolCatalog
open VibeFs.Mux.Wrappers
open VibeFs.Mux.KnowledgeGraphTools
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.KnowledgeGraphToolsCodec
open VibeFs.Shell.ToolRuntimeContext
open VibeFs.Shell.ToolContextCodec

let private knowledgeGraphDraftEntrySchema : obj =
    createObj
        [ "type", box "object"
          "properties",
          box
              (createObj
                  [ "id", box (createObj [ "type", box "string"; "description", box "Existing entry id to update" ])
                    "entity",
                    box
                        (createObj
                            [ "type", box "array"
                              "items", box (createObj [ "type", box "string" ])
                              "description", box "Knowledge graph entity" ])
                    "fact", box (createObj [ "type", box "string"; "description", box "Knowledge graph fact" ]) ])
          "required", box [| "entity"; "fact" |]
          "additionalProperties", box false ]

let knowledgeGraphFetchTool (kgRuntime: MuxKnowledgeGraphRuntime) : ToolDefinition =
    { name = "knowledge_graph_fetch"
      description = description "knowledge_graph_fetch"
      parameters = mkSchema (createObj [ "entity", box (strProp Params.fetchKnowledgeGraphEntity) ]) [| "entity" |]
      execute =
          fun config args ->
              match decodeFetchEntity args with
              | Error e -> resolveStr (formatDomainError e)
              | Ok entity ->
                  match fromMuxConfig config with
                  | Error e -> resolveStr (formatDomainError e)
                  | Ok runtime ->
                      kgRuntime.FetchFromSessionSnapshot(
                          runtime.Execution.SessionId,
                          runtime.Execution.Directory,
                          entity)
      condition =
          Some (fun pluginConfig -> knowledgeGraphDirExists (muxConfigDirectoryFallback pluginConfig)) }

let returnBookkeeperTool (kgRuntime: MuxKnowledgeGraphRuntime) : ToolDefinition =
    { name = "return_bookkeeper"
      description = description "return_bookkeeper"
      parameters =
          mkSchema
              (createObj
                  [ "entries",
                    box
                        (createObj
                            [ "type", box "array"
                              "items", box knowledgeGraphDraftEntrySchema
                              "description", box Params.submitKnowledgeGraphEntries ]) ])
              [| "entries" |]
      execute =
          fun config args ->
              match fromMuxConfig config with
              | Error e -> resolveStr (formatDomainError e)
              | Ok runtime ->
                  match decodeReturnBookkeeperArgs args with
                  | Error e -> resolveStr (formatDomainError e)
                  | Ok drafts ->
                      kgRuntime.Submit(
                          runtime.Execution.SessionId,
                          runtime.Execution.Directory,
                          drafts,
                          config)
      condition =
          Some (fun pluginConfig -> knowledgeGraphDirExists (muxConfigDirectoryFallback pluginConfig)) }
