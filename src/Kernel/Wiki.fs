module VibeFs.Kernel.Wiki

open System
open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn

let private idRe = Regex("^[0-9a-f]{4}$")

let private jsonParse (text: string) : obj = emitJsExpr (text) "JSON.parse($0)"
let private jsonStringify (value: obj) : string = emitJsExpr (value) "JSON.stringify($0)"
let private truncateTo (s: string) (max: int) = if s.Length > max then s.[.. max - 1] + "..." else s

type WikiId = private WikiId of string

type WikiEntry = { id: WikiId; q: string; a: string }

type WikiDraft = { id: string option; q: string; a: string }

type WikiHeader =
    | DayHeader of date: string * rewritten: bool
    | SnapshotHeader of through: string option

type WikiFile = { header: WikiHeader; entries: WikiEntry list }

type WikiProjection = Map<WikiId, WikiEntry>

// Lives in Kernel.Wiki (not the sibling WikiRuntimeState module) because F#
// union cases / records are not re-exported transitively by `open`: existing
// callers reach `AppendAfterWork` and the `WikiJobContext` record through
// `open VibeFs.Kernel.Wiki`, and the pure state module references them too.
type WikiJobKind =
    | AppendAfterWork
    | DailyRewrite of date: string
    | WeeklyRewrite of throughDate: string

type WikiJobContext =
    { workspaceRoot: string
      kind: WikiJobKind }

let tryParseId (s: string) : WikiId option =
    if idRe.IsMatch s then Some(WikiId s) else None

let idValue (WikiId s) : string = s

let parseHeaderLine (line: string) : Result<WikiHeader, string> =
    try
        let o = jsonParse line
        if Dyn.isNullish o || not (Dyn.typeIs o "object") then Error "bad header"
        else
            let t = Dyn.str o "type"
            let k = Dyn.str o "kind"
            if t <> "wiki_header" || string (Dyn.get o "version") <> "1" then Error "bad header"
            elif k = "day" then Ok(DayHeader(Dyn.str o "date", unbox<bool> (Dyn.get o "rewritten")))
            elif k = "snapshot" then
                let thr = Dyn.str o "through"
                if thr = "" then Error "snapshot missing through" else Ok(SnapshotHeader(Some thr))
            else Error "bad header kind"
    with _ -> Error "header parse failed"

let renderHeader (header: WikiHeader) : string =
    match header with
    | DayHeader(date, rewritten) ->
        jsonStringify (createObj [
            "type", box "wiki_header"
            "version", box 1
            "kind", box "day"
            "date", box date
            "rewritten", box rewritten ])
    | SnapshotHeader(Some through) ->
        jsonStringify (createObj [
            "type", box "wiki_header"
            "version", box 1
            "kind", box "snapshot"
            "through", box through ])
    | SnapshotHeader None ->
        jsonStringify (createObj [
            "type", box "wiki_header"
            "version", box 1
            "kind", box "snapshot" ])

let parseEntryLine (line: string) : Result<WikiEntry, string> =
    try
        let o = jsonParse line
        if Dyn.isNullish o || not (Dyn.typeIs o "object") then Error "bad entry"
        else
            let idStr = Dyn.str o "id"
            let q = Dyn.get o "q"
            let a = Dyn.get o "a"
            if not (idRe.IsMatch idStr) then Error "bad id"
            elif Dyn.isNullish q || Dyn.isNullish a then Error "missing q or a"
            else Ok { id = WikiId idStr; q = string q; a = string a }
    with _ -> Error "entry parse failed"

let renderEntry (entry: WikiEntry) : string =
    jsonStringify (createObj [
        "id", box (idValue entry.id)
        "q", box entry.q
        "a", box entry.a ])

let parseNdjson (fileName: string) (text: string) : Result<WikiFile, string> =
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

let renderNdjson (header: WikiHeader) (entries: WikiEntry list) : string =
    let h = renderHeader header
    if entries.IsEmpty then h + "\n"
    else h + "\n" + (entries |> List.map renderEntry |> String.concat "\n") + "\n"

let projectLatestWins (files: WikiFile list) : WikiProjection =
    files
    |> List.collect (fun f -> f.entries)
    |> List.fold (fun m e -> Map.add e.id e m) Map.empty

let preludeLines (projection: WikiProjection) : string list =
    projection
    |> Map.toList
    |> List.sortBy (fun (id, _) -> idValue id)
    |> List.map (fun (id, e) -> "- " + idValue id + " " + truncateTo e.q 160)

let buildPreludeSection (projection: WikiProjection) : string option =
    if Map.isEmpty projection then None
    else
        Some(
            "[项目背景和历史]" + "\n\n"
            + String.concat "\n" (preludeLines projection)
            + "\n\n" + "需要某条历史答案时，调用 fetch_wiki(id)。")

let validateDraft (draft: WikiDraft) : Result<WikiDraft, string> =
    match draft.id with
    | Some id when not (idRe.IsMatch id) -> Error "invalid id"
    | _ ->
        if String.IsNullOrWhiteSpace draft.q then Error "q required"
        elif String.IsNullOrWhiteSpace draft.a then Error "a required"
        else Ok draft

let fetchAnswer (projection: WikiProjection) (idStr: string) : Result<string, string> =
    match tryParseId idStr with
    | None -> Error ($"Invalid wiki id: {idStr}")
    | Some id ->
        match Map.tryFind id projection with
        | Some entry -> Ok entry.a
        | None -> Error ($"Wiki entry not found in this session snapshot: {idStr}")

let parseDraftArray (value: obj) : Result<WikiDraft list, string> =
    if Dyn.isNullish value || not (Dyn.isArray value) then Error "entries must be an array"
    else
        let drafts = value :?> obj array
        let parseDraft (item: obj) : Result<WikiDraft, string> =
            if Dyn.isNullish item || not (Dyn.typeIs item "object") then Error "entries must contain objects"
            else
                let id =
                    match Dyn.opt item "id" with
                    | Some rawId ->
                        let trimmed = (string rawId).Trim()
                        if trimmed = "" then None else Some trimmed
                    | None -> None
                validateDraft
                    { id = id
                      q = Dyn.str item "q"
                      a = Dyn.str item "a" }

        drafts
        |> Array.fold
            (fun acc item ->
                acc
                |> Result.bind (fun items ->
                    parseDraft item |> Result.map (fun draft -> draft :: items)))
            (Ok [])
        |> Result.map List.rev

let applyDrafts (allocate: Set<string> -> string) (projection: WikiProjection) (drafts: WikiDraft list)
                : Result<WikiEntry list, string> =
    let initialKnown =
        projection |> Map.toList |> List.map (fun (id, _) -> idValue id) |> Set.ofList
    let reuseExisting (idStr: string) : WikiId option =
        match tryParseId idStr with
        | Some wid when Map.containsKey wid projection -> Some wid
        | _ -> None
    let rec loop known revResults remaining =
        match remaining with
        | [] -> Ok(List.rev revResults)
        | draft :: rest ->
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
                loop nextKnown (({ id = targetId; q = d.q; a = d.a } : WikiEntry) :: revResults) rest
    loop initialKnown [] drafts

let allocateRandomHexId (randomInt: unit -> int) (existingIds: Set<string>) : Result<string, string> =
    let rec loop attempts =
        if attempts > 65536 then Error "wiki id space exhausted"
        else
            let candidate = sprintf "%04x" (randomInt() % 65536)
            if not (Set.contains candidate existingIds) then Ok candidate
            else loop (attempts + 1)
    loop 0
