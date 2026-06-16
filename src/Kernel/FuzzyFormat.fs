module VibeFs.Kernel.FuzzyFormat

let hotFrecency = 25
let warmFrecency = 20
let grepMaxLineLength = 500

type FileAnnotation =
    { gitStatus: string option
      totalFrecencyScore: int option
      accessFrecencyScore: int option }

let truncateLine (line: string) (max: int) : string =
    let trimmed = line.Trim()
    if trimmed.Length <= max then trimmed else trimmed.[.. max - 1] + "..."

/// A one-line annotation: git status, or a frecency heat label, or nothing.
let fileAnnotation (item: FileAnnotation option) : string =
    match item with
    | None -> ""
    | Some i ->
        match i.gitStatus with
        | Some g when g <> "clean" && g <> "unknown" && g <> "" -> $"  [{g} in git]"
        | _ ->
            let frecency = i.totalFrecencyScore |> Option.orElse i.accessFrecencyScore |> Option.defaultValue 0
            if frecency >= hotFrecency then "  [VERY often touched file]"
            elif frecency >= warmFrecency then "  [often touched file]"
            else ""

type GrepMatch =
    { relativePath: string; lineNumber: int; lineContent: string
      contextBefore: string list; contextAfter: string list
      annotation: FileAnnotation option }

/// `totalMatched` is an option so an explicit 0 is preserved (the TS `??`
/// fallback fires only for null/undefined, not for 0).
type GrepResult =
    { items: GrepMatch list; totalMatched: int option; regexFallbackError: string option }

/// Render a grep result as a human-readable report grouped by file.
let formatGrepOutput (result: GrepResult option) : string =
    match result with
    | None -> "No matches found"
    | Some r ->
        if r.items.IsEmpty then "No matches found"
        else
            let total = defaultArg r.totalMatched r.items.Length
            let plural = if total = 1 then "match" else "matches"
            let sb = System.Text.StringBuilder()
            sb.Append($"{total} {plural}\n\n") |> ignore
            let mutable currentFile = ""
            for m in r.items do
                if m.relativePath <> currentFile && currentFile <> "" then
                    sb.Append('\n') |> ignore
                if m.relativePath <> currentFile then
                    sb.Append($"{m.relativePath}{fileAnnotation m.annotation}\n") |> ignore
                    currentFile <- m.relativePath
                let ctxLen = m.contextBefore.Length
                m.contextBefore |> List.iteri (fun i line ->
                    let lineNum = m.lineNumber - ctxLen + i
                    sb.Append($" {lineNum}- {truncateLine line grepMaxLineLength}\n") |> ignore)
                sb.Append($" {m.lineNumber}: {truncateLine m.lineContent grepMaxLineLength}\n") |> ignore
                m.contextAfter |> List.iteri (fun i line ->
                    let lineNum = m.lineNumber + 1 + i
                    sb.Append($" {lineNum}- {truncateLine line grepMaxLineLength}\n") |> ignore)
            sb.ToString().TrimEnd('\n')

type FindMatch =
    { relativePath: string; annotation: FileAnnotation option }

/// `totalMatched` is an option so an explicit 0 is preserved (TS `??` only
/// fires for null/undefined).
type FindResult =
    { items: FindMatch list; totalMatched: int option; totalFiles: int }

/// Render a find result as a flat, annotated file list with a summary header.
let formatFindOutput (result: FindResult option) : string =
    match result with
    | None -> "No matching files found"
    | Some r ->
        if r.items.IsEmpty then "No matching files found"
        else
            let total = defaultArg r.totalMatched r.items.Length
            let plural = if total = 1 then "file" else "files"
            let header = [ $"{total} matching {plural} ({r.totalFiles} total indexed)"; "" ]
            let body = r.items |> List.map (fun m -> $"{m.relativePath}{fileAnnotation m.annotation}")
            String.concat "\n" (header @ body)
