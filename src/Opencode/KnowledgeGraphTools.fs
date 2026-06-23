module VibeFs.Opencode.KnowledgeGraphTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Mux.Wrappers
open VibeFs.Shell
open VibeFs.Shell.Dyn

let parseDraftArray (value: obj) : Result<KnowledgeGraphDraft list, string> =
    if Dyn.isNullish value || not (Dyn.isArray value) then Error "entries must be an array"
    else
        let drafts = value :?> obj array
        let parseDraft (item: obj) : Result<KnowledgeGraphDraft, string> =
            if Dyn.isNullish item || not (Dyn.typeIs item "object") then Error "entries must contain objects"
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
                validateDraft { id = id; entity = entities; fact = Dyn.str item "fact" }
        drafts
        |> Array.fold
            (fun acc item ->
                acc
                |> Result.bind (fun items ->
                    parseDraft item |> Result.map (fun draft -> draft :: items)))
            (Ok [])
        |> Result.map List.rev

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
