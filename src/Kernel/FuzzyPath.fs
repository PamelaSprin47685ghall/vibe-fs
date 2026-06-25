module VibeFs.Kernel.FuzzyPath

open System.Text.RegularExpressions

let private normalizeSeparators (p: string) = p.Replace("\\", "/")
let private isPathAbsolute (p: string) = normalizeSeparators p |> fun n -> n.StartsWith("/")

let private normalizeSegments (segments: string list) : string list =
    ([], segments)
    ||> List.fold (fun acc seg ->
        match seg, acc with
        | ".", _ -> acc
        | "..", [] -> []
        | "..", _ :: tail -> tail
        | other, prefix -> other :: prefix)
    |> List.rev

let private resolveAgainst (basePath: string) (p: string) : string =
    let combined =
        if isPathAbsolute p then normalizeSeparators p
        else normalizeSeparators basePath + "/" + normalizeSeparators p
    let parts = combined.Split('/') |> Array.filter (fun s -> s <> "") |> List.ofArray
    "/" + String.concat "/" (normalizeSegments parts)

let private relativePath (fromPath: string) (toPath: string) : string =
    let splitAbs p = resolveAgainst "/" p |> fun s -> s.Split('/') |> Array.filter ((<>) "") |> List.ofArray
    let fromParts, toParts = splitAbs fromPath, splitAbs toPath
    let commonLen = Seq.zip fromParts toParts |> Seq.takeWhile (fun (a, b) -> a = b) |> Seq.length
    let remainingFrom = List.skip commonLen fromParts
    let remainingTo = List.skip commonLen toParts
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

let private (|RecursiveDirGlob|_|) (s: string) =
    match recursiveDirRe.Match s with
    | m when m.Success -> Some m.Groups.[1].Value
    | _ -> None

let private isOutside basePath targetPath =
    let rel = relativePath basePath targetPath
    rel = ".." || rel.StartsWith("../") || isPathAbsolute rel

type ResolvedFuzzySearchPath =
    { basePath: string
      pathConstraint: string option
      external: bool }

type ExternalBasePathTestResult =
    { basePath: string
      pathConstraint: string option }

let private normalizeTrimmed (trimmed: string) : string option =
    let stripLeadingDotSlash (t: string) =
        if t.StartsWith("./") then t.[2..] else t
    match trimmed with
    | "." | "./" -> None
    | _ ->
        let t = stripLeadingDotSlash trimmed
        match t with
        | RecursiveDirGlob dir when dir <> "" && not (hasGlobChars dir) -> Some $"{dir}/"
        | RecursiveDirGlob _ -> Some t
        | _ when t.StartsWith("/") || t.EndsWith("/") || hasGlobChars t -> Some t
        | _ ->
            let lastSegment = t.Split('/') |> Array.last
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
    let lastSegment = absPath.Split('/') |> Array.last
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

let resolveExternalBasePathForTest (absPath: string) : ExternalBasePathTestResult =
    let basePath, pathConstraint = resolveExternalBasePath absPath
    { basePath = basePath; pathConstraint = pathConstraint }
