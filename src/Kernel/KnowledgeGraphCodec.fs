module VibeFs.Kernel.KnowledgeGraphCodec

open Thoth.Json

open VibeFs.Kernel.KnowledgeGraph

let private versionDecoder : Decoder<int> =
    Decode.oneOf [ Decode.int; Decode.map int Decode.string ]

let private headerDecoder =
    Decode.object (fun get ->
        ( get.Required.Field "type" Decode.string
        , get.Required.Field "version" versionDecoder
        , get.Required.Field "kind" Decode.string
        , get.Required.Field "date" Decode.string
        , get.Required.Field "rewritten" Decode.bool )
    )

let parseHeaderLine (line: string) : Result<KnowledgeGraphHeader, string> =
    Decode.fromString headerDecoder line
    |> Result.bind (fun (t, version, kind, date, rewritten) ->
        if t <> "knowledge_graph_header" || version <> 1 || kind <> "day" then Error "bad header"
        else Ok(DayHeader(date, rewritten)))

let renderHeader (header: KnowledgeGraphHeader) : string =
    match header with
    | DayHeader(date, rewritten) ->
        Encode.object [
            "type", Encode.string "knowledge_graph_header"
            "version", Encode.int 1
            "kind", Encode.string "day"
            "date", Encode.string date
            "rewritten", Encode.bool rewritten
        ]
        |> Encode.toString 0

let private entryDecoder =
    Decode.object (fun get ->
        ( get.Required.Field "id" Decode.string
        , get.Required.Field "entity" (Decode.list Decode.string)
        , get.Required.Field "fact" Decode.string )
    )

let parseEntryLine (line: string) : Result<KnowledgeGraphEntry, string> =
    Decode.fromString entryDecoder line
    |> Result.bind (fun (idStr, entities, fact) ->
        match tryParseId idStr with
        | Some id -> Ok { id = id; entity = entities; fact = fact }
        | None -> Error "bad id")

let renderEntry (entry: KnowledgeGraphEntry) : string =
    Encode.object [
        "id", Encode.string (idValue entry.id)
        "entity", Encode.list (List.map Encode.string entry.entity)
        "fact", Encode.string entry.fact
    ]
    |> Encode.toString 0

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
