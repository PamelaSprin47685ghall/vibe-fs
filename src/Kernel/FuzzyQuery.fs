module VibeFs.Kernel.FuzzyQuery

open System.Text.RegularExpressions

// ── Pure path utilities (forward slash as canonical separator) ──────────────

let private normalizeSeparators (p: string) = p.Replace("\\", "/")

let private isPathAbsolute (p: string) =
    let n = normalizeSeparators p
    n.StartsWith("/")

/// Collapse `.` and `..` segments in a forward-slash path.
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

/// Resolve a possibly-relative path against a base directory.
let private resolveAgainst (basePath: string) (p: string) : string =
    let combined =
        if isPathAbsolute p then normalizeSeparators p
        else normalizeSeparators basePath + "/" + normalizeSeparators p
    let parts = combined.Split('/') |> Array.filter (fun s -> s <> "") |> List.ofArray
    "/" + String.concat "/" (normalizeSegments parts)

/// Relative path from `fromPath` to `toPath` (both absolute).
let private relativePath (fromPath: string) (toPath: string) : string =
    let fromParts = resolveAgainst "/" fromPath |> fun p -> p.Split('/') |> Array.filter ((<>) "") |> List.ofArray
    let toParts = resolveAgainst "/" toPath |> fun p -> p.Split('/') |> Array.filter ((<>) "") |> List.ofArray
    let rec commonPrefix a b =
        match a, b with
        | x :: xs, y :: ys when x = y -> commonPrefix xs ys
        | _ -> (a, b)
    let remainingFrom, remainingTo = commonPrefix fromParts toParts
    let ups = remainingFrom |> List.map (fun _ -> "..")
    let result = ups @ remainingTo
    if result.IsEmpty then "." else String.concat "/" result

let private dirname (p: string) : string =
    let n = normalizeSeparators p
    let idx = n.LastIndexOf('/')
    if idx <= 0 then "." else n.[..idx - 1]

let private toForwardSlashes (p: string) = p.Replace("\\", "/")

// ── Fuzzy query logic ───────────────────────────────────────────────────────

let private extensionRe = Regex(@"\.[a-zA-Z][a-zA-Z0-9]{0,9}$")
let private recursiveDirRe = Regex(@"^(.*)/\*\*(?:/\*)?$")
let private globCharsRe = Regex(@"[*?[{]")
let private hasExtension (segment: string) = extensionRe.IsMatch segment
let private hasGlobChars (s: string) = globCharsRe.IsMatch s
let private isOutside basePath targetPath =
    let rel = relativePath basePath targetPath
    rel = ".." || rel.StartsWith("../") || isPathAbsolute rel

type ResolvedFuzzySearchPath =
    { basePath: string; pathConstraint: string option; external: bool }

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

/// Normalise a raw path-constraint string into a search prefix (or None when it
/// collapses to the whole tree).
let normalizePathConstraint (pathConstraint: string) (cwd: string) : string option =
    let trimmed = pathConstraint.Trim()
    if trimmed = "" then None
    elif isPathAbsolute trimmed then
        match toForwardSlashes (relativePath cwd trimmed) with
        | "" -> None
        | rel when rel.StartsWith("../") || rel = ".." || isPathAbsolute rel -> None
        | rel -> normalizeTrimmed rel
    else normalizeTrimmed trimmed

/// Split on runs of commas/whitespace — matches the original TS `/[,\s]+/`.
let private splitExcludeTokens (s: string) : string list =
    Regex.Split(s, @"[,\s]+") |> List.ofArray |> List.map (fun w -> w.Trim()) |> List.filter ((<>) "")

/// Turn normalised exclude patterns into negation-prefixed search tokens.
let normalizeExcludes (exclude: string list) (cwd: string) : string list =
    let toPattern (raw: string) =
        let stripped = if raw.StartsWith("!") then raw.[1..] else raw
        match normalizePathConstraint stripped cwd with Some n -> [($"!{n}")] | None -> []
    exclude |> List.collect (splitExcludeTokens >> List.collect toPattern)

/// Compose the full query string: path constraint, excludes, then the pattern.
let buildQuery (fpath: string option) (pattern: string) (exclude: string list)
               (cwd: string) (allowExternal: bool) : string =
    let pathParts =
        match fpath with
        | None -> []
        | Some p ->
            if allowExternal && isPathAbsolute p then [ p ]
            else match normalizePathConstraint p cwd with Some c -> [ c ] | None -> []
    let excludeParts = normalizeExcludes exclude cwd
    (pathParts @ excludeParts @ [ pattern ]) |> String.concat " "

/// When an input path escapes the workspace, choose a base directory and an
/// optional last-segment constraint.
let private resolveExternalBasePath (absPath: string) =
    let lastSegment = absPath.Split('/') |> Array.tryLast |> Option.defaultValue ""
    if lastSegment.StartsWith(".") || hasExtension lastSegment then
        dirname absPath, Some lastSegment
    else absPath, None

/// Split an input path into a base directory, an optional constraint, and
/// whether it points outside the working directory.
let resolveFuzzySearchPath (inputPath: string option) (cwd: string) : ResolvedFuzzySearchPath =
    match inputPath |> Option.map (fun s -> s.Trim()) |> Option.filter (fun s -> s <> "") with
    | None -> { basePath = cwd; pathConstraint = None; external = false }
    | Some trimmed ->
        let resolved = resolveAgainst cwd trimmed
        if isPathAbsolute trimmed || isOutside cwd resolved then
            let externalBase, externalConstraint = resolveExternalBasePath resolved
            { basePath = externalBase; pathConstraint = externalConstraint; external = true }
        else
            let constraintStr = toForwardSlashes (relativePath cwd resolved)
            { basePath = cwd
              pathConstraint = if constraintStr = "" then None else Some constraintStr
              external = false }

let resolveExternalPath (inputPath: string option) (cwd: string)
                        : string option * string option =
    let resolved = resolveFuzzySearchPath inputPath cwd
    if not resolved.external then None, None
    else Some resolved.basePath, resolved.pathConstraint
