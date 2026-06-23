module VibeFs.Mux.KnowledgeGraphToolDefs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.ToolCatalog
open VibeFs.Mux.Wrappers
open VibeFs.Mux.KnowledgeGraphTools
open VibeFs.Shell.KnowledgeGraphFiles

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
              let sessionID = Dyn.str config "sessionID"
              let directory =
                  let current = Dyn.str config "directory"
                  if current = "" then defaultArg (strField config "cwd") "" else current
              kgRuntime.FetchFromSessionSnapshot(sessionID, directory, Dyn.str args "entity")
      condition = Some (fun pluginConfig -> knowledgeGraphDirExists (Dyn.str pluginConfig "cwd")) }

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
              let sessionID = Dyn.str config "sessionID"
              let directory =
                  let current = Dyn.str config "directory"
                  if current = "" then defaultArg (strField config "cwd") "" else current
              match parseDraftArray (Dyn.get args "entries") with
              | Error message -> resolveStr message
              | Ok drafts -> kgRuntime.Submit(sessionID, directory, drafts, config)
      condition = Some (fun pluginConfig -> knowledgeGraphDirExists (Dyn.str pluginConfig "cwd")) }