module VibeFs.Kernel.KnowledgeGraph.Fetch

open System
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.KnowledgeGraph.Id

let fetchAnswer (projection: KnowledgeGraphProjection) (entityStr: string) : Result<string, string> =
    let query = entityStr.Trim()
    if query = "" then Error ($"Invalid knowledge graph entity: {entityStr}")
    else
        let tokens =
            query.Split([| ' '; '\t'; '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.toList
            |> List.distinct
        let entryMatches (e: KnowledgeGraphEntry) =
            List.contains query e.entity
            || List.exists (fun t -> List.contains t e.entity) tokens
        let matches =
            projection
            |> Map.toList
            |> List.filter (fun (_, e) -> entryMatches e)
            |> List.sortBy (fun (id, _) -> idValue id)
        if matches.IsEmpty then
            Error ($"Knowledge graph entity not found in this session snapshot: {entityStr}")
        else
            matches
            |> List.map (fun (_, e) -> e.fact)
            |> String.concat "\n\n"
            |> Ok
