module Wanxiangshu.Kernel.KnowledgeGraph.Job

open Fable.Core
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Kernel.Yaml
open Wanxiangshu.Kernel.KnowledgeGraph.Types

let private jobKindTag (kind: KnowledgeGraphJobKind) : string * string option =
    match kind with
    | AppendAfterWork -> "append", None
    | DailyRewrite date -> "daily", Some date

let private jobMarkerFields (ctx: KnowledgeGraphJobContext) : FrontMatterField list =
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
    let normalized = if isNull text then "" else text.Replace("\r\n", "\n").Replace("\r", "\n")
    if normalized = "" then
        renderJobMarker ctx
    else
        let marker = renderJobMarker ctx
        if normalized.StartsWith("---") then
            marker + "\n" + normalized
        else
            marker + "\n\n" + normalized

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
