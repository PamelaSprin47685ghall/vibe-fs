module Wanxiangshu.Kernel.FuzzyFormat

type FileAnnotation =
    { gitStatus: string option
      totalFrecencyScore: int option
      accessFrecencyScore: int option }

let hotFrecency = 25
let warmFrecency = 20
let grepMaxLineLength = 500
let private skipGitStatuses = Set [ "clean"; "unknown"; "" ]

let truncateLine (line: string) (max: int) : string =
    let trimmed = line.Trim()
    if trimmed.Length <= max then trimmed else trimmed.[.. max - 1] + "..."

let fileAnnotation (item: FileAnnotation option) : string =
    match item with
    | None -> ""
    | Some i ->
        match i.gitStatus with
        | Some g when not (Set.contains g skipGitStatuses) -> $"  [{g} in git]"
        | _ ->
            let frecency = i.totalFrecencyScore |> Option.orElse i.accessFrecencyScore |> Option.defaultValue 0
            match frecency with
            | f when f >= hotFrecency -> "  [VERY often touched file]"
            | f when f >= warmFrecency -> "  [often touched file]"
            | _ -> ""

type GrepMatch =
    { relativePath: string
      lineNumber: int
      lineContent: string
      contextBefore: string list
      contextAfter: string list
      annotation: FileAnnotation option }

type GrepResult =
    { items: GrepMatch list
      totalMatched: int option
      regexFallbackError: string option }

let private grepMatchLines (m: GrepMatch) : string list =
    let contextOffset = m.contextBefore.Length
    let before =
        m.contextBefore
        |> List.mapi (fun i line -> $" {m.lineNumber - contextOffset + i}- {truncateLine line grepMaxLineLength}")
    let matchLine = $" {m.lineNumber}: {truncateLine m.lineContent grepMaxLineLength}"
    let after =
        m.contextAfter
        |> List.mapi (fun i line -> $" {m.lineNumber + 1 + i}- {truncateLine line grepMaxLineLength}")
    before @ [ matchLine ] @ after

let formatGrepOutput (result: GrepResult option) : string =
    match result with
    | None -> "No matches found"
    | Some r ->
        if r.items.IsEmpty then
            "No matches found"
        else
            let total = defaultArg r.totalMatched r.items.Length
            let plural = if total = 1 then "match" else "matches"
            let lines, _ =
                r.items
                |> List.fold
                    (fun (lines, currentFile) m ->
                        let lines =
                            if m.relativePath <> currentFile && currentFile <> "" then lines @ [ "" ]
                            else lines
                        let lines, currentFile =
                            if m.relativePath <> currentFile then
                                lines @ [ $"{m.relativePath}{fileAnnotation m.annotation}" ], m.relativePath
                            else
                                lines, currentFile
                        lines @ grepMatchLines m, currentFile)
                    ([ $"{total} {plural}"; "" ], "")
            (lines |> String.concat "\n").TrimEnd('\n')

type FindMatch = { relativePath: string; annotation: FileAnnotation option }

type FindResult =
    { items: FindMatch list
      totalMatched: int option
      totalFiles: int }

let formatFindOutput (result: FindResult option) : string =
    match result with
    | None -> "No matching files found"
    | Some r ->
        if r.items.IsEmpty then
            "No matching files found"
        else
            let total = defaultArg r.totalMatched r.items.Length
            let plural = if total = 1 then "file" else "files"
            String.concat "\n" ([ $"{total} matching {plural} ({r.totalFiles} total indexed)"; "" ] @ (r.items |> List.map (fun m -> $"{m.relativePath}{fileAnnotation m.annotation}")))

let formatMultiPatternFindOutput (results: (string * FindResult option) list) : string =
    results
    |> List.map (fun (pat, result) ->
        let body = formatFindOutput result
        $"## pattern: \"{pat}\"\n{body}")
    |> String.concat "\n\n"

let formatMultiPatternGrepOutput (results: (string * GrepResult option * string option) list) : string =
    results
    |> List.map (fun (pat, result, regexErr) ->
        let body = formatGrepOutput result
        let body' =
            match regexErr with
            | Some e -> body + "\n\nInvalid regex: " + e + ", used literal match"
            | None -> body
        $"## pattern: \"{pat}\"\n{body'}")
    |> String.concat "\n\n"
