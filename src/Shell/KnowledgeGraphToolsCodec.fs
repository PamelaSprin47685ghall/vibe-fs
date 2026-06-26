module Wanxiangshu.Shell.KnowledgeGraphToolsCodec

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.DynField

let decodeFetchEntity (args: obj) : Result<string, DomainError> =
    match strField args "entity" with
    | None -> Error (InvalidIntent ("knowledge_graph_fetch", "entity", "must be a string"))
    | Some entity -> Ok entity

let decodeDraftEntries (value: obj) : Result<KnowledgeGraphDraft list, DomainError> =
    if Dyn.isNullish value || not (Dyn.isArray value) then
        Error (InvalidIntent ("return_bookkeeper", "entries", "must be an array"))
    else
        let drafts = value :?> obj array

        let parseDraft (item: obj) : Result<KnowledgeGraphDraft, DomainError> =
            if Dyn.isNullish item || not (Dyn.typeIs item "object") then
                Error (InvalidIntent ("return_bookkeeper", "entries", "must contain objects"))
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
                | Error message -> Error (InvalidIntent ("return_bookkeeper", "entries", message))

        drafts
        |> Array.fold
            (fun acc item ->
                acc
                |> Result.bind (fun items -> parseDraft item |> Result.map (fun draft -> draft :: items)))
            (Ok [])
        |> Result.map List.rev

let decodeReturnBookkeeperArgs (args: obj) : Result<KnowledgeGraphDraft list, DomainError> =
    decodeDraftEntries (Dyn.get args "entries")