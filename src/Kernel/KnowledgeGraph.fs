module VibeFs.Kernel.KnowledgeGraph

open System
open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.PromptFrontMatter

let private idRe = Regex("^[0-9a-f]{4}$")

let private jsonParse (text: string) : obj = emitJsExpr (text) "JSON.parse($0)"
let private jsonStringify (value: obj) : string = emitJsExpr (value) "JSON.stringify($0)"
let private truncateTo (s: string) (max: int) = if s.Length > max then s.[.. max - 1] + "..." else s

type KnowledgeGraphId = private KnowledgeGraphId of string

type KnowledgeGraphEntry = { id: KnowledgeGraphId; entity: string list; fact: string }

type KnowledgeGraphDraft = { id: string option; entity: string list; fact: string }

type KnowledgeGraphHeader =
    | DayHeader of date: string * rewritten: bool

type KnowledgeGraphFile = { header: KnowledgeGraphHeader; entries: KnowledgeGraphEntry list }

type KnowledgeGraphProjection = Map<KnowledgeGraphId, KnowledgeGraphEntry>

// Lives in Kernel.KnowledgeGraph (not the sibling KnowledgeGraphRuntimeState module) because F#
// union cases / records are not re-exported transitively by `open`: existing
// callers reach `AppendAfterWork` and the `KnowledgeGraphJobContext` record through
// `open VibeFs.Kernel.KnowledgeGraph`, and the pure state module references them too.
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
        let workspaceRoot = Map.tryFind "workspaceRoot" fields |> Option.defaultValue ""
        let kind = Map.tryFind "kind" fields |> Option.defaultValue ""
        if workspaceRoot.Trim() = "" then None
        else
            match kind with
            | "append" -> Some { workspaceRoot = workspaceRoot; kind = AppendAfterWork }
            | "daily" ->
                let date = Map.tryFind "date" fields |> Option.defaultValue ""
                if date.Trim() = "" then None else Some { workspaceRoot = workspaceRoot; kind = DailyRewrite date }
            | _ -> None

let tryParseId (s: string) : KnowledgeGraphId option =
    if idRe.IsMatch s then Some(KnowledgeGraphId s) else None

let idValue (KnowledgeGraphId s) : string = s

let parseHeaderLine (line: string) : Result<KnowledgeGraphHeader, string> =
    try
        let o = jsonParse line
        if Dyn.isNullish o || not (Dyn.typeIs o "object") then Error "bad header"
        else
            let t = Dyn.str o "type"
            let k = Dyn.str o "kind"
            if t <> "knowledge_graph_header" || string (Dyn.get o "version") <> "1" then Error "bad header"
            elif k = "day" then Ok(DayHeader(Dyn.str o "date", unbox<bool> (Dyn.get o "rewritten")))
            else Error "bad header kind"
    with _ -> Error "header parse failed"

let renderHeader (header: KnowledgeGraphHeader) : string =
    match header with
    | DayHeader(date, rewritten) ->
        jsonStringify (createObj [
            "type", box "knowledge_graph_header"
            "version", box 1
            "kind", box "day"
            "date", box date
            "rewritten", box rewritten ])

let parseEntryLine (line: string) : Result<KnowledgeGraphEntry, string> =
    try
        let o = jsonParse line
        if Dyn.isNullish o || not (Dyn.typeIs o "object") then Error "bad entry"
        else
            let idStr = Dyn.str o "id"
            let entityRaw = Dyn.get o "entity"
            let fact = Dyn.get o "fact"
            if not (idRe.IsMatch idStr) then Error "bad id"
            elif Dyn.isNullish entityRaw || not (Dyn.isArray entityRaw) then Error "missing entity"
            elif Dyn.isNullish fact then Error "missing fact"
            else
                let entities = (entityRaw :?> obj array) |> Array.map string |> Array.toList
                Ok { id = KnowledgeGraphId idStr; entity = entities; fact = string fact }
    with _ -> Error "entry parse failed"

let renderEntry (entry: KnowledgeGraphEntry) : string =
    jsonStringify (createObj [
        "id", box (idValue entry.id)
        "entity", box (Array.ofList entry.entity)
        "fact", box entry.fact ])

let parseNdjson (fileName: string) (text: string) : Result<KnowledgeGraphFile, string> =
    try
        let lines =
            text.Split('\n')
            |> Array.toList
            |> List.map (fun l -> l.Trim())
            |> List.filter ((<>) "")
        match lines with
        | [] -> Error(fileName + ": empty file")
        | headerLine :: entryLines ->
            match parseHeaderLine headerLine with
            | Error e -> Error e
            | Ok header ->
                let rec collect acc remaining =
                    match remaining with
                    | [] -> Ok(List.rev acc)
                    | line :: rest ->
                        match parseEntryLine line with
                        | Ok e -> collect (e :: acc) rest
                        | Error _ -> Ok(List.rev acc)
                match collect [] entryLines with
                | Ok entries -> Ok { header = header; entries = entries }
                | Error e -> Error e
    with _ -> Error "ndjson parse failed"

let renderNdjson (header: KnowledgeGraphHeader) (entries: KnowledgeGraphEntry list) : string =
    let h = renderHeader header
    if entries.IsEmpty then h + "\n"
    else h + "\n" + (entries |> List.map renderEntry |> String.concat "\n") + "\n"

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
                    elif Dyn.isArray entityRaw then (entityRaw :?> obj array) |> Array.map string |> Array.toList
                    else [ string entityRaw ]
                validateDraft
                    { id = id
                      entity = entities
                      fact = Dyn.str item "fact" }

        drafts
        |> Array.fold
            (fun acc item ->
                acc
                |> Result.bind (fun items ->
                    parseDraft item |> Result.map (fun draft -> draft :: items)))
            (Ok [])
        |> Result.map List.rev

let applyDrafts (allocate: Set<string> -> string) (projection: KnowledgeGraphProjection) (drafts: KnowledgeGraphDraft list)
                : Result<KnowledgeGraphEntry list, string> =
    let initialKnown =
        projection |> Map.toList |> List.map (fun (id, _) -> idValue id) |> Set.ofList
    let reuseExisting (idStr: string) : KnowledgeGraphId option =
        match tryParseId idStr with
        | Some wid when Map.containsKey wid projection -> Some wid
        | _ -> None
    let step state draft =
        state
        |> Result.bind (fun (known, acc) ->
            match validateDraft draft with
            | Error e -> Error e
            | Ok d ->
                let targetId, nextKnown =
                    match d.id |> Option.bind reuseExisting with
                    | Some wid -> wid, known
                    | None ->
                        let newId = allocate known
                        match tryParseId newId with
                        | Some wid -> wid, Set.add newId known
                        | None -> failwith "allocated id invalid"
                Ok(nextKnown, ({ id = targetId; entity = d.entity; fact = d.fact } : KnowledgeGraphEntry) :: acc))
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
