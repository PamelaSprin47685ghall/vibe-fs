module VibeFs.Mux.KnowledgeGraphToolDefs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Shell

open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.ToolCatalog
open VibeFs.Mux.Wrappers
open VibeFs.Mux.KnowledgeGraphTools
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.Dyn

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

let private errorMessage (error: DomainError) : string =
    match error with
    | ParseError (_, detail) -> detail
    | e -> string e

let parseDraftArray (value: obj) : Result<KnowledgeGraphDraft list, DomainError> =
    if Dyn.isNullish value || not (Dyn.isArray value) then
        Error (ParseError ("knowledge graph draft", "entries must be an array"))
    else
        let drafts = value :?> obj array
        let parseDraft (item: obj) : Result<KnowledgeGraphDraft, DomainError> =
            if Dyn.isNullish item || not (Dyn.typeIs item "object") then
                Error (ParseError ("knowledge graph draft", "entries must contain objects"))
            else
                let id =
                    match Dyn.opt item "id" with
                    | Some rawId ->
                        let trimmed = (string rawId).Trim()
                        if trimmed = "" then None else Some trimmed
                    | None -> None
                let entityRaw = Dyn.get item "entity"
                let entities =
                    if Dyn.isNullish entityRaw then []
                    elif Dyn.isArray entityRaw then (entityRaw :?> obj array) |> Array.map string |> Array.toList
                    else [ string entityRaw ]
                match validateDraft { id = id; entity = entities; fact = Dyn.str item "fact" } with
                | Ok draft -> Ok draft
                | Error message -> Error (ParseError ("knowledge graph draft", message))
        drafts
        |> Array.fold
            (fun acc item ->
                acc
                |> Result.bind (fun items ->
                    parseDraft item |> Result.map (fun draft -> draft :: items)))
            (Ok [])
        |> Result.map List.rev

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
              | Error e -> resolveStr (errorMessage e)
              | Ok drafts -> kgRuntime.Submit(sessionID, directory, drafts, config)
      condition = Some (fun pluginConfig -> knowledgeGraphDirExists (Dyn.str pluginConfig "cwd")) }
