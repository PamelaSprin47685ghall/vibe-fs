module VibeFs.Omp.KnowledgeGraphTools

open Fable.Core.JsInterop
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.ToolCatalog
open VibeFs.Omp.Codec
open VibeFs.Omp.KnowledgeGraphRuntime
open VibeFs.Omp.Schema

module Dyn = VibeFs.Shell.Dyn
module Params = VibeFs.Kernel.ToolCatalog.Params

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
                    elif Dyn.isArray entityRaw then
                        let arrObj : obj = entityRaw
                        unbox<obj array> arrObj |> Array.map string |> List.ofArray
                    else [ string entityRaw ]
                validateDraft { id = id; entity = entities; fact = Dyn.str item "fact" }
        drafts
        |> Array.fold
            (fun acc item -> acc |> Result.bind (fun items -> parseDraft item |> Result.map (fun d -> d :: items)))
            (Ok [])
        |> Result.map List.rev

let private draftEntrySchema (tb: obj) : obj =
    objectOf
        [|
            ("id", optional (str "Existing entry id to update" tb) tb)
            ("entity", strArray "Knowledge graph entity" tb)
            ("fact", str "Knowledge graph fact" tb)
        |]
        tb

let registerKnowledgeGraphTools (pi: obj) (kgRuntime: OmpKnowledgeGraphRuntime) : unit =
    let tb = Dyn.get pi "typebox"
    let fetchDesc = description "knowledge_graph_fetch"

    pi?registerTool(
        createObj [
            "name", box "knowledge_graph_fetch"
            "label", box "Knowledge Graph Fetch"
            "description", box fetchDesc
            "parameters", objectOf [| ("entity", str Params.fetchKnowledgeGraphEntity tb) |] tb
            "execute",
                box(fun (_id: string) (params': obj) (_s: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        let sessionID = getSessionIdFromContext ctx |> Option.defaultValue ""
                        let directory = Dyn.str ctx "cwd"
                        let! answer = kgRuntime.FetchFromSessionSnapshot(sessionID, directory, Dyn.str params' "entity")
                        return textResult answer
                    })
        ])

    pi?registerTool(
        createObj [
            "name", box "return_bookkeeper"
            "label", box "Return Bookkeeper"
            "description", box (description "return_bookkeeper")
            "defaultInactive", box true
            "parameters",
                objectOf
                    [| ("entries", arrayOf (draftEntrySchema tb) Params.submitKnowledgeGraphEntries tb) |]
                    tb
            "execute",
                box(fun (_id: string) (params': obj) (_s: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        match parseDraftArray (Dyn.get params' "entries") with
                        | Error message -> return errorResult message
                        | Ok drafts ->
                            let sessionID = getSessionIdFromContext ctx |> Option.defaultValue ""
                            let directory = Dyn.str ctx "cwd"
                            let! msg = kgRuntime.Submit(sessionID, directory, drafts)
                            return textResult msg
                    })
        ])

let mutable private kgToolsRegistered = false

let ensureKnowledgeGraphTools (pi: obj) (kgRuntime: OmpKnowledgeGraphRuntime) (cwd: string) : unit =
    if not kgToolsRegistered && cwd <> "" && VibeFs.Shell.KnowledgeGraphFiles.knowledgeGraphDirExists cwd then
        registerKnowledgeGraphTools pi kgRuntime
        kgToolsRegistered <- true

let resetOmpKgToolsTestState () : unit = kgToolsRegistered <- false
