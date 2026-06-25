module VibeFs.Omp.KnowledgeGraphRuntimeIO

open Fable.Core
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.KnowledgeGraphPortLock
open VibeFs.Shell.Dyn

module Dyn = VibeFs.Shell.Dyn

let buildEntries (root: string) (drafts: KnowledgeGraphDraft list) : JS.Promise<Result<KnowledgeGraphEntry list, string>> =
    promise {
        let! projection = readProjection root
        let normalizedDrafts = normalizeDraftIds projection drafts
        let random = System.Random()
        return applyDrafts (allocateRandomHexId (fun () -> random.Next(0, 65536))) projection normalizedDrafts
    }

let extractTexts (entry: obj) : string list =
    if Dyn.typeIs entry "string" then [ string entry ]
    else
        let texts = ResizeArray<string>()
        let partsObj : obj = Dyn.get entry "parts"
        if not (Dyn.isNullish partsObj) && Dyn.isArray partsObj then
            let partsArr = unbox<obj array> partsObj
            for p in partsArr do
                if Dyn.str p "type" = "text" then
                    let t = Dyn.str p "text"
                    if t <> "" then texts.Add t
        let m = Dyn.get entry "message"
        if not (Dyn.isNullish m) then
            let contentObj : obj = Dyn.get m "content"
            if not (Dyn.isNullish contentObj) && Dyn.isArray contentObj then
                let contentArr = unbox<obj array> contentObj
                for p in contentArr do
                    let t = Dyn.str p "text"
                    if t <> "" then texts.Add t
        List.ofSeq texts

let tryResolveJobContext (getEntries: (unit -> obj array) option) (sessionID: string) (_directory: string)
    : JS.Promise<KnowledgeGraphJobContext option> =
    promise {
        if System.String.IsNullOrWhiteSpace sessionID then return None
        else
            match getEntries with
            | None -> return None
            | Some load ->
                try
                    let history = load ()
                    let texts = history |> Array.toList |> List.collect extractTexts
                    return texts |> List.tryPick tryParseJobMarker
                with _ ->
                    return None
    }

let submitForKind (root: string) (todayStr: string) (entries: KnowledgeGraphEntry list) (kind: KnowledgeGraphJobKind)
    : JS.Promise<string> =
    promise {
        let! result = withKnowledgeGraphPortLock 30000L 1000 root (fun () ->
            promise {
                match kind with
                | AppendAfterWork ->
                    do! appendEntries root todayStr entries
                    return $"Appended {entries.Length} knowledge graph entries."
                | DailyRewrite date ->
                    do! rewriteDay root date entries
                    return $"Rewrote knowledge graph day {date}."
            })
        match result with
        | Error e -> return e
        | Ok msg -> return msg
    }