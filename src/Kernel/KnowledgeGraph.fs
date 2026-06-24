module VibeFs.Kernel.KnowledgeGraph

open System
open System.Text.RegularExpressions

open VibeFs.Kernel.Messaging
open VibeFs.Kernel.PromptFrontMatter

let private idRe = Regex("^[0-9a-f]{4}$")

let private truncateTo (s: string) (max: int) = if s.Length > max then s.[.. max - 1] + "..." else s

type KnowledgeGraphId = private KnowledgeGraphId of string

type KnowledgeGraphEntry = { id: KnowledgeGraphId; entity: string list; fact: string }

type KnowledgeGraphDraft = { id: string option; entity: string list; fact: string }

type KnowledgeGraphHeader =
    | DayHeader of date: string * rewritten: bool

type KnowledgeGraphFile = { header: KnowledgeGraphHeader; entries: KnowledgeGraphEntry list }

type KnowledgeGraphProjection = Map<KnowledgeGraphId, KnowledgeGraphEntry>

type KnowledgeGraphJobKind =
    | AppendAfterWork
    | DailyRewrite of date: string

type KnowledgeGraphJobContext =
    { workspaceRoot: string
      kind: KnowledgeGraphJobKind }

let private jobKindTag (kind: KnowledgeGraphJobKind) : string * string option =
    match kind with
    | AppendAfterWork -> "append", None
    | DailyRewrite date -> "daily", Some date

let private jobMarkerFields (ctx: KnowledgeGraphJobContext) : string list =
    let kind, value = jobKindTag ctx.kind
    [ yield yamlScalarField "type" "vibe_knowledge_graph_job"
      yield yamlScalarField "workspaceRoot" ctx.workspaceRoot
      yield yamlScalarField "kind" kind
      match value with
      | Some date when kind = "daily" -> yield yamlScalarField "date" date
      | _ -> () ]

let renderJobMarker (ctx: KnowledgeGraphJobContext) : string =
    frontMatter (jobMarkerFields ctx)

let prependJobMarker (ctx: KnowledgeGraphJobContext) (text: string) : string =
    let markerFields = jobMarkerFields ctx
    if String.IsNullOrEmpty text then
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
                    else String.concat "\n" lines.[closeIndex + 1 ..]
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

let tryParseId (s: string) : KnowledgeGraphId option =
    if idRe.IsMatch s then Some(KnowledgeGraphId s) else None

let idValue (KnowledgeGraphId s) : string = s

let projectLatestWins (files: KnowledgeGraphFile list) : KnowledgeGraphProjection =
    files
    |> List.collect (fun f -> f.entries)
    |> List.fold (fun m e -> Map.add e.id e m) Map.empty

let private normalizeEntities (entities: string list) : string list =
    entities
    |> List.map (fun s -> s.Trim())
    |> List.filter (fun s -> s <> "")
    |> List.distinct

let buildPreludeSection (projection: KnowledgeGraphProjection) : string option =
    if Map.isEmpty projection then None
    else
        let entities =
            projection
            |> Map.toList
            |> List.collect (fun (_, e) -> e.entity)
            |> normalizeEntities
            |> List.sort
            |> List.map (fun e -> "  - " + yamlScalar (truncateTo e 160))
        Some(
            frontMatterPrompt
                [ yamlSeqField "knowledge_graph" entities ]
                "Call knowledge_graph_fetch(entity) to expand related facts.")

let validateDraft (draft: KnowledgeGraphDraft) : Result<KnowledgeGraphDraft, string> =
    match draft.id with
    | Some id when not (idRe.IsMatch id) -> Error "invalid id"
    | _ ->
        let normalized = normalizeEntities draft.entity
        if normalized.IsEmpty then Error "entity required"
        elif String.IsNullOrWhiteSpace draft.fact then Error "fact required"
        else Ok { draft with entity = normalized }

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

let allocateRandomHexId (randomInt: unit -> int) (existingIds: Set<string>) : Result<string, string> =
    let rec loop attempts =
        if attempts > 65536 then Error "knowledge graph id space exhausted"
        else
            let candidate = sprintf "%04x" (randomInt() % 65536)
            if not (Set.contains candidate existingIds) then Ok candidate
            else loop (attempts + 1)
    loop 0

// --- Submit idempotency: reject second return_bookkeeper from history ---

let returnBookkeeperToolName = "return_bookkeeper"

let historyHasCompletedReturnBookkeeper (messages: Message<'raw> list) : bool =
    messages
    |> List.exists (fun msg ->
        msg.parts
        |> List.exists (fun part ->
            match part with
            | ToolPart(toolName, _, Some state, _) ->
                toolName = returnBookkeeperToolName && state.status = "completed"
            | _ -> false))

let rejectSecondReturnBookkeeperMessage =
    "This session already completed return_bookkeeper. The knowledge graph job was submitted and persisted. "
    + "Do not call return_bookkeeper again. A second call performs no writes and only wastes context. "
    + "You were instructed to submit exactly one return_bookkeeper call. Follow the prompt."
