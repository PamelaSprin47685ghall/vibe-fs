module Wanxiangshu.Kernel.KnowledgeGraph.Draft

open System
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Kernel.KnowledgeGraph.Id

let private normalizeEntities (entities: string list) : string list =
    entities
    |> List.map (fun s -> s.Trim())
    |> List.filter (fun s -> s <> "")
    |> List.distinct

let validateDraft (draft: KnowledgeGraphDraft) : Result<KnowledgeGraphDraft, string> =
    match draft.id with
    | Some id when not (System.Text.RegularExpressions.Regex("^[0-9a-f]{4}$").IsMatch id) -> Error "invalid id"
    | _ ->
        let normalized = normalizeEntities draft.entity
        if normalized.IsEmpty then Error "entity required"
        elif String.IsNullOrWhiteSpace draft.fact then Error "fact required"
        else Ok { draft with entity = normalized }

let applyDrafts (allocate: Set<string> -> Result<string, string>) (projection: KnowledgeGraphProjection) (drafts: KnowledgeGraphDraft list)
                : Result<KnowledgeGraphEntry list, string> =
    let initialKnown =
        projection |> Map.toList |> List.map (fun (id, _) -> idValue id) |> Set.ofList
    let reuseExisting (idStr: string) : KnowledgeGraphId option =
        match tryParseId idStr with
        | Some wid when Map.containsKey wid projection -> Some wid
        | _ -> None
    let entry wid (d: KnowledgeGraphDraft) : KnowledgeGraphEntry =
        { id = wid; entity = d.entity; fact = d.fact }
    let step state draft =
        state
        |> Result.bind (fun (known, acc) ->
            match validateDraft draft with
            | Error e -> Error e
            | Ok d ->
                match d.id |> Option.bind reuseExisting with
                | Some wid -> Ok(known, entry wid d :: acc)
                | None ->
                    match allocate known with
                    | Error e -> Error e
                    | Ok newId ->
                        match tryParseId newId with
                        | Some wid -> Ok(Set.add newId known, entry wid d :: acc)
                        | None -> Error "allocated id invalid")
    drafts
    |> List.fold step (Ok(initialKnown, []))
    |> Result.map (snd >> List.rev)
