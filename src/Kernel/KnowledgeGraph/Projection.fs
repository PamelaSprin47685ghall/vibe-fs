module VibeFs.Kernel.KnowledgeGraph.Projection

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.PromptFrontMatter
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.KnowledgeGraph.Id

let private truncateTo (s: string) (max: int) = if s.Length > max then s.[.. max - 1] + "..." else s

let private normalizeEntities (entities: string list) : string list =
    entities
    |> List.map (fun s -> s.Trim())
    |> List.filter (fun s -> s <> "")
    |> List.distinct

let projectLatestWins (files: KnowledgeGraphFile list) : KnowledgeGraphProjection =
    files
    |> List.collect (fun f -> f.entries)
    |> List.fold (fun m e -> Map.add e.id e m) Map.empty

let buildPreludeSection (projection: KnowledgeGraphProjection) : string option =
    if Map.isEmpty projection then None
    else
        let entities =
            projection
            |> Map.toList
            |> List.collect (fun (_, e) -> e.entity)
            |> normalizeEntities
            |> List.sort
            |> List.map (fun e -> box (truncateTo e 160))
        Some(
            frontMatterPrompt
                [ yamlSeqField "knowledge_graph" entities ]
                "Call knowledge_graph_fetch(entity) to expand related facts.")
