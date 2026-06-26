module VibeFs.Kernel.KnowledgeGraph.Job

open VibeFs.Kernel.PromptFrontMatter
open VibeFs.Kernel.KnowledgeGraph.Types

let private jobKindTag (kind: KnowledgeGraphJobKind) : string * string option =
    match kind with
    | AppendAfterWork -> "append", None
    | DailyRewrite date -> "daily", Some date

let private jobMarkerFields (ctx: KnowledgeGraphJobContext) : string list =
    let kind, value = jobKindTag ctx.kind
    [ yield yamlField "type" "vibe_knowledge_graph_job"
      yield yamlField "workspaceRoot" ctx.workspaceRoot
      yield yamlField "kind" kind
      match value with
      | Some date when kind = "daily" -> yield yamlField "date" date
      | _ -> () ]

let renderJobMarker (ctx: KnowledgeGraphJobContext) : string =
    frontMatter (jobMarkerFields ctx)

let prependJobMarker (ctx: KnowledgeGraphJobContext) (text: string) : string =
    let markerFields = jobMarkerFields ctx
    if System.String.IsNullOrEmpty text then
        frontMatter markerFields
    else
        let normalized = text.Replace("\r\n", "\n").Replace("\r", "\n")
        let lines = normalized.Split('\n')
        if lines.Length < 2 || lines.[0] <> "---" then
            frontMatterPrompt markerFields normalized
        else
            match lines.[1..] |> Array.tryFindIndex ((=) "---") with
            | None -> frontMatterPrompt markerFields normalized
            | Some relativeCloseIndex ->
                let closeIndex = relativeCloseIndex + 1
                let existingFields = lines.[1 .. closeIndex - 1] |> Array.toList
                let remainder =
                    if closeIndex + 1 >= lines.Length then ""
                    else lines.[closeIndex + 1 ..] |> String.concat "\n"
                let merged = frontMatter (markerFields @ existingFields)
                if remainder = "" then merged else merged + "\n" + remainder

let tryParseJobMarker (text: string) : KnowledgeGraphJobContext option =
    let fields = parseFrontMatterScalars text
    if Map.tryFind "type" fields <> Some "vibe_knowledge_graph_job" then None
    else
        match Map.tryFind "workspaceRoot" fields, Map.tryFind "kind" fields with
        | Some ws, Some k when ws.Trim() <> "" ->
            match k with
            | "append" -> Some { workspaceRoot = ws; kind = AppendAfterWork }
            | "daily" ->
                match Map.tryFind "date" fields with
                | Some d when d.Trim() <> "" -> Some { workspaceRoot = ws; kind = DailyRewrite d }
                | _ -> None
            | _ -> None
        | _ -> None
