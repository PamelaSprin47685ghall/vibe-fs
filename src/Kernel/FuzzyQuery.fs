module VibeFs.Kernel.FuzzyQuery

open System.Text.RegularExpressions
open Fable.Core

[<Import("resolve", "node:path")>]
let private resolve (p: string) : string = jsNative
[<Import("resolve", "node:path")>]
let private resolve2 (baseDir: string) (p: string) : string = jsNative
[<Import("relative", "node:path")>]
let private relative (fromPath: string) (toPath: string) : string = jsNative
[<Import("isAbsolute", "node:path")>]
let private isAbsolute (p: string) : bool = jsNative
[<Import("dirname", "node:path")>]
let private dirname (p: string) : string = jsNative
[<Import("sep", "node:path")>]
let private sep : string = jsNative
[<Emit("process.cwd()")>]
let private processCwd () : string = jsNative

let private toForwardSlashes (p: string) = p.Replace(sep, "/")
let private cwd () = resolve (processCwd ())
let private extensionRe = Regex(@"\.[a-zA-Z][a-zA-Z0-9]{0,9}$")
let private recursiveDirRe = Regex(@"^(.*)/\*\*(?:/\*)?$")
let private globCharsRe = Regex(@"[*?[{]")
let private hasExtension (segment: string) = extensionRe.IsMatch segment
let private hasGlobChars (s: string) = globCharsRe.IsMatch s
let private isOutside basePath targetPath =
    let rel = relative basePath targetPath
    rel = ".." || rel.StartsWith($"..{sep}") || isAbsolute rel

type ResolvedFuzzySearchPath =
    { basePath: string; pathConstraint: string option; external: bool }

/// Core normaliser for a path constraint already stripped of leading "./".
/// Directories gain a trailing slash; glob patterns and file-like segments are
/// returned untouched; recursive globs collapse to their directory.
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
let normalizePathConstraint (pathConstraint: string) (cwdOverride: string option) : string option =
    let work = defaultArg cwdOverride (cwd ())
    let trimmed = pathConstraint.Trim()
    if trimmed = "" then None
    elif isAbsolute trimmed then
        match toForwardSlashes (relative work trimmed) with
        | "" -> None
        | rel when rel.StartsWith("../") || rel = ".." || isAbsolute rel -> None
        | rel -> normalizeTrimmed rel
    else normalizeTrimmed trimmed

/// Split on runs of commas/whitespace — matches the original TS `/[,\s]+/`.
let private splitExcludeTokens (s: string) : string list =
    Regex.Split(s, @"[,\s]+") |> List.ofArray |> List.map (fun w -> w.Trim()) |> List.filter ((<>) "")

/// Turn exclude flags — a string ("!src,node_modules") OR a string array — into
/// normalised negation patterns.  Mirrors `exclude?: string | string[]`.
let normalizeExcludes (exclude: obj option) (cwdOverride: string option) : string list =
    let toPattern (raw: string) =
        let stripped = if raw.StartsWith("!") then raw.[1..] else raw
        match normalizePathConstraint stripped cwdOverride with Some n -> [($"!{n}")] | None -> []
    match exclude with
    | None -> []
    | Some raw when Dyn.isArray raw ->
        (raw :?> obj array) |> Array.toList |> List.collect (fun v -> splitExcludeTokens (string v) |> List.collect toPattern)
    | Some raw -> splitExcludeTokens (string raw) |> List.collect toPattern

/// Compose the full query string: path constraint, excludes, then the pattern.
let buildQuery (fpath: string option) (pattern: string) (exclude: obj option)
               (cwdOverride: string option) (allowExternal: bool) : string =
    let pathParts =
        match fpath with
        | None -> []
        | Some p ->
            if allowExternal && isAbsolute p then [ p ]
            else match normalizePathConstraint p cwdOverride with Some c -> [ c ] | None -> []
    let excludeParts = normalizeExcludes exclude cwdOverride
    (pathParts @ excludeParts @ [ pattern ]) |> String.concat " "

/// When an input path escapes the workspace, choose a base directory and an
/// optional last-segment constraint.
let private resolveExternalBasePath (absPath: string) =
    let normalized = resolve absPath
    let lastSegment = normalized.Split(sep) |> Array.tryLast |> Option.defaultValue ""
    if lastSegment.StartsWith(".") || hasExtension lastSegment then
        dirname normalized, Some lastSegment
    else normalized, None

/// Split an input path into a base directory, an optional constraint, and
/// whether it points outside the working directory.
let resolveFuzzySearchPath (inputPath: string option) (cwdOverride: string option) : ResolvedFuzzySearchPath =
    let basePath = resolve (defaultArg cwdOverride (cwd ()))
    match inputPath |> Option.map (fun s -> s.Trim()) |> Option.filter (fun s -> s <> "") with
    | None -> { basePath = basePath; pathConstraint = None; external = false }
    | Some trimmed ->
        let resolved = resolve2 basePath trimmed
        if isAbsolute trimmed || isOutside basePath resolved then
            let externalBase, externalConstraint = resolveExternalBasePath resolved
            { basePath = externalBase; pathConstraint = externalConstraint; external = true }
        else
            let constraintStr = toForwardSlashes (relative basePath resolved)
            { basePath = basePath
              pathConstraint = if constraintStr = "" then None else Some constraintStr
              external = false }

let resolveExternalPath (inputPath: string option) (cwdOverride: string option)
                        : string option * string option =
    let resolved = resolveFuzzySearchPath inputPath cwdOverride
    if not resolved.external then None, None
    else Some resolved.basePath, resolved.pathConstraint
