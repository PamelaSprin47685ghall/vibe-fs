module VibeFs.Kernel.Fuzzy

open System.Text.RegularExpressions
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Dyn

type FileAnnotation =
    { gitStatus: string option
      totalFrecencyScore: int option
      accessFrecencyScore: int option }

let hotFrecency = 25
let warmFrecency = 20
let grepMaxLineLength = 500

let truncateLine (line: string) (max: int) : string =
    let trimmed = line.Trim()
    if trimmed.Length <= max then trimmed else trimmed.[.. max - 1] + "..."

let fileAnnotation (item: FileAnnotation option) : string =
    match item with
    | None -> ""
    | Some i ->
        match i.gitStatus with
        | Some g when g <> "clean" && g <> "unknown" && g <> "" -> $"  [{g} in git]"
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

let private escapeRegex (pattern: string) : string =
    let escapeChar (m: Match) = "\\" + m.Value
    Regex.Replace(pattern, @"[.*+?^${}()|[\]\\]", escapeChar)

let private isValidRegex (pattern: string) : bool =
    try
        Regex(pattern) |> ignore
        true
    with _ -> false

let detectGrepMode (pattern: string) : string =
    let escaped = escapeRegex pattern
    if pattern = escaped then "plain"
    elif isValidRegex pattern then "regex"
    else "plain"

let checkWildcardOnly (pattern: string) (mode: string) : bool =
    if mode = "plain" then false
    else Regex.IsMatch(pattern.Trim(), @"^(?:[.^$]*(?:[.][*+?]|\*|\+)[.^$]*|[.^$\s]*|\.\*\??|\.*[+?]?|\.+\??|\.|\*|\?)$")

let private normalizeSeparators (p: string) = p.Replace("\\", "/")
let private isPathAbsolute (p: string) = normalizeSeparators p |> fun n -> n.StartsWith("/")

let private normalizeSegments (segments: string list) : string list =
    let rec fold acc = function
        | [] -> List.rev acc
        | "." :: rest -> fold acc rest
        | ".." :: rest ->
            match acc with
            | [] -> fold acc rest
            | _ :: tail -> fold tail rest
        | seg :: rest -> fold (seg :: acc) rest
    fold [] segments

let private resolveAgainst (basePath: string) (p: string) : string =
    let combined =
        if isPathAbsolute p then normalizeSeparators p
        else normalizeSeparators basePath + "/" + normalizeSeparators p
    let parts = combined.Split('/') |> Array.filter (fun s -> s <> "") |> List.ofArray
    "/" + String.concat "/" (normalizeSegments parts)

let private relativePath (fromPath: string) (toPath: string) : string =
    let fromParts = resolveAgainst "/" fromPath |> fun p -> p.Split('/') |> Array.filter ((<>) "") |> List.ofArray
    let toParts = resolveAgainst "/" toPath |> fun p -> p.Split('/') |> Array.filter ((<>) "") |> List.ofArray
    let rec commonPrefix a b =
        match a, b with
        | x :: xs, y :: ys when x = y -> commonPrefix xs ys
        | _ -> (a, b)
    let remainingFrom, remainingTo = commonPrefix fromParts toParts
    let result = (remainingFrom |> List.map (fun _ -> "..")) @ remainingTo
    if result.IsEmpty then "." else String.concat "/" result

let private dirname (p: string) : string =
    let n = normalizeSeparators p
    let idx = n.LastIndexOf('/')
    if idx <= 0 then "." else n.[..idx - 1]

let private toForwardSlashes (p: string) = p.Replace("\\", "/")

let private extensionRe = Regex(@"\.[a-zA-Z][a-zA-Z0-9]{0,9}$")
let private recursiveDirRe = Regex(@"^(.*)/\*\*(?:/\*)?$")
let private globCharsRe = Regex(@"[\*\?\[\{]")
let private hasExtension (segment: string) = extensionRe.IsMatch segment
let private hasGlobChars (s: string) = globCharsRe.IsMatch s
let private isOutside basePath targetPath =
    let rel = relativePath basePath targetPath
    rel = ".." || rel.StartsWith("../") || isPathAbsolute rel

type ResolvedFuzzySearchPath =
    { basePath: string
      pathConstraint: string option
      external: bool }

let private normalizeTrimmed (trimmed: string) : string option =
    if trimmed = "." || trimmed = "./" then None
    else
        let t = if trimmed.StartsWith("./") then trimmed.[2..] else trimmed
        let recursive = recursiveDirRe.Match t
        if recursive.Success then
            let dir = recursive.Groups.[1].Value
            if dir <> "" && not (hasGlobChars dir) then Some $"{dir}/" else Some t
        elif t.StartsWith("/") || t.EndsWith("/") then Some t
        elif hasGlobChars t then Some t
        else
            let lastSegment = t.Split('/') |> Array.tryLast |> Option.defaultValue ""
            if hasExtension lastSegment then Some t else Some $"{t}/"

let normalizePathConstraint (pathConstraint: string) (cwd: string) : string option =
    let trimmed = pathConstraint.Trim()
    if trimmed = "" then None
    elif isPathAbsolute trimmed then
        match toForwardSlashes (relativePath cwd trimmed) with
        | "" -> None
        | rel when rel.StartsWith("../") || rel = ".." || isPathAbsolute rel -> None
        | rel -> normalizeTrimmed rel
    else
        normalizeTrimmed trimmed

let private splitExcludeTokens (s: string) : string list =
    Regex.Split(s, @"[,\s]+") |> List.ofArray |> List.map (fun w -> w.Trim()) |> List.filter ((<>) "")

let normalizeExcludes (exclude: string list) (cwd: string) : string list =
    let toPattern (raw: string) =
        let stripped = if raw.StartsWith("!") then raw.[1..] else raw
        match normalizePathConstraint stripped cwd with
        | Some n -> [ $"!{n}" ]
        | None -> []
    exclude |> List.collect (splitExcludeTokens >> List.collect toPattern)

let buildQuery (fpath: string option) (pattern: string) (exclude: string list) (cwd: string) (allowExternal: bool) : string =
    let pathParts =
        match fpath with
        | None -> []
        | Some p ->
            if allowExternal && isPathAbsolute p then [ p ]
            else
                match normalizePathConstraint p cwd with
                | Some c -> [ c ]
                | None -> []
    (pathParts @ normalizeExcludes exclude cwd @ [ pattern ]) |> String.concat " "

let private resolveExternalBasePath (absPath: string) =
    let lastSegment = absPath.Split('/') |> Array.tryLast |> Option.defaultValue ""
    if lastSegment.StartsWith(".") || hasExtension lastSegment then
        dirname absPath, Some lastSegment
    else
        absPath, None

let resolveFuzzySearchPath (inputPath: string option) (cwd: string) : ResolvedFuzzySearchPath =
    match inputPath |> Option.map (fun s -> s.Trim()) |> Option.filter (fun s -> s <> "") with
    | None -> { basePath = cwd; pathConstraint = None; external = false }
    | Some trimmed ->
        let resolved = resolveAgainst cwd trimmed
        if isPathAbsolute trimmed || isOutside cwd resolved then
            let externalBase, externalConstraint = resolveExternalBasePath resolved
            { basePath = externalBase
              pathConstraint = externalConstraint
              external = true }
        else
            let constraintStr = toForwardSlashes (relativePath cwd resolved)
            { basePath = cwd
              pathConstraint = if constraintStr = "" then None else Some constraintStr
              external = false }

let resolveExternalPath (inputPath: string option) (cwd: string) : string option * string option =
    let resolved = resolveFuzzySearchPath inputPath cwd
    if not resolved.external then None, None
    else Some resolved.basePath, resolved.pathConstraint

type FuzzyFindParams =
    { pattern: string option
      path: string option
      limit: int option
      iterator: string option }

type FuzzyGrepParams =
    { pattern: string option
      path: string option
      exclude: string list
      caseSensitive: bool option
      context: int option
      limit: int option
      iterator: string option }

type FuzzyFindState = { query: string; pageSize: int; pageIndex: int; externalBasePath: string option }

type FuzzyGrepState =
    { query: string
      mode: string
      smartCase: bool
      beforeContext: int
      afterContext: int
      pageSize: int
      externalBasePath: string option
      cursor: obj option }

type SearchOutcome = { output: string; isError: bool }

type ResolvedGrep = { matches: GrepMatch list; total: int option; regexError: string option; cursor: obj }

let parseExcludeField (args: obj) : string list =
    let v = Dyn.get args "exclude"
    if Dyn.isNullish v then []
    elif Dyn.isArray v then v :?> obj array |> Array.map string |> List.ofArray
    else [ string v ]

let buildGrepOutput (body: string) (regexError: string option) (nextIterator: string) : string =
    let regexNotice = regexError |> Option.map (fun error -> sprintf "Invalid regex: %s, used literal match" error)
    let iteratorNotice = sprintf "iterator=\"%s\"" nextIterator
    let notices = (regexNotice |> Option.toList) @ [ iteratorNotice ]
    sprintf "%s\n\n[%s]" body (String.concat ". " notices)
