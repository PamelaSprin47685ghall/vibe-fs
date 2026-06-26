module VibeFs.Kernel.KnowledgeGraph.JobTesting

open System
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types

let private readRequiredField (readField: string -> string) (fieldName: string) : string =
    let value = readField fieldName
    if value.Trim() = "" then failwith $"Knowledge graph job payload missing required field '{fieldName}'"
    else value.Trim()

/// Parses integration-test job kind tags (`append`, `daily`) into a typed kind.
/// `readField` supplies payload field values without tying the kernel to host `obj` decoding.
let parseTestingJobKind (kindTag: string) (readField: string -> string) : KnowledgeGraphJobKind =
    let normalizedTag = kindTag.Trim().ToLowerInvariant()
    match normalizedTag with
    | "append" -> AppendAfterWork
    | "daily" -> DailyRewrite(readRequiredField readField "date")
    | _ -> failwith $"Unknown knowledge graph job kind: {normalizedTag}"

let buildTestingJobContext (workspaceRoot: string) (kindTag: string) (readField: string -> string) : KnowledgeGraphJobContext =
    { workspaceRoot = workspaceRoot; kind = parseTestingJobKind kindTag readField }