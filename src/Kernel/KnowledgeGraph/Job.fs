module VibeFs.Kernel.KnowledgeGraph.Job

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.PromptFrontMatter
open VibeFs.Kernel.Yaml
open VibeFs.Kernel.KnowledgeGraph.Types

let private jobKindTag (kind: KnowledgeGraphJobKind) : string * string option =
    match kind with
    | AppendAfterWork -> "append", None
    | DailyRewrite date -> "daily", Some date

[<Emit("Object.assign({}, $0, $1)")>]
let private assignObjects (baseObj: obj) (overrideObj: obj) : obj = jsNative

let private markerObject (ctx: KnowledgeGraphJobContext) : obj =
    let kind, value = jobKindTag ctx.kind
    let fields =
        [ "type", box "vibe_knowledge_graph_job"
          "workspaceRoot", box ctx.workspaceRoot
          "kind", box kind ]
        @ (match value with Some date when kind = "daily" -> [ "date", box date ] | _ -> [])
    createObj fields

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
        frontMatter (jobMarkerFields ctx)
    else
        let parsed = parseFrontMatter normalized
        if isNull parsed then
            frontMatterPrompt (jobMarkerFields ctx) normalized
        else
            let body = bodyAfterFrontMatter normalized
            let merged = assignObjects parsed (markerObject ctx)
            let yamlStr = stringify merged
            let fm = "---\n" + yamlStr.TrimEnd('\n') + "\n---"
            match body with "" -> fm | _ -> fm + "\n" + body

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
