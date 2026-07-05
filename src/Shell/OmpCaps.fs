module Wanxiangshu.Shell.OmpCaps

open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn

type OmpCapsFile = { filePath: string; label: string; content: string }

let private capsMarker = "<caps-context"

let private capsFileRe = Regex("^[A-Z][A-Z0-9_]*\\.md$")
let private capsDirRe = Regex("^[A-Z][A-Z0-9_]*$")

let private excludedFileNames = Set.ofList [ "AGENTS.md"; "CLAUDE.md"; "README.md" ]

let private excludedDirNames =
    Set.ofList [
        "AGENTS"; "CLAUDE"; "NODE_MODULES"; ".GIT"; "TARGET"; "DIST"; "OUT"
        ".VENV"; "VENV"; "__PYCACHE__"; ".CACHE"; ".NEXT"; ".TURBO"; ".PARCEL-CACHE"
    ]

let private maxFileSize = 1_048_576
let private maxTotalContextBytes = 8 * 1_048_576
let private maxCapsFiles = 200
let private maxDirDepth = 5

[<Import("promises", "node:fs")>]
let private fsPromises : obj = jsNative

[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative

[<Import("relative", "node:path")>]
let private pathRelative (root: string) (full: string) : string = jsNative

let private readdir (dir: string) : JS.Promise<obj[]> = fsPromises?readdir(dir, {| withFileTypes = true |})
let private stat (path: string) : JS.Promise<obj> = fsPromises?stat(path)
let private readFile (path: string) : JS.Promise<string> = fsPromises?readFile(path, "utf-8")
let private realpath (path: string) : JS.Promise<string> = fsPromises?realpath(path)

let private entryName (e: obj) : string = e?name
let private entryIsFile (e: obj) : bool = e?isFile ()
let private entryIsDirectory (e: obj) : bool = e?isDirectory ()
let private statIsFile (s: obj) : bool = s?isFile ()
let private statSize (s: obj) : int = s?size

let escapeXmlAttr (value: string) : string =
    value.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("'", "&apos;").Replace("<", "&lt;").Replace(">", "&gt;")

let private isExcludedDir (name: string) =
    let upper = name.ToUpperInvariant()
    excludedDirNames.Contains upper
    || (name.StartsWith "." && not (capsDirRe.IsMatch name))

let formatOmpCapsContext (files: OmpCapsFile list) : string =
    if isNull (box files) || List.isEmpty files then ""
    else
        files
        |> List.choose (fun f ->
            if Dyn.isNullish f || Dyn.isNullish (Dyn.get f "content") then None
            else Some $"<caps-context file=\"{escapeXmlAttr f.label}\">\n{f.content}\n</caps-context>")
        |> String.concat "\n\n"

let private tryReadOmpFileAsync (filePath: string) (label: string) : JS.Promise<OmpCapsFile option> =
    promise {
        try
            let! s = stat filePath
            if not (statIsFile s) || statSize s > maxFileSize then return None
            else
                let! content = readFile filePath
                if System.String.IsNullOrWhiteSpace content then return None
                else return Some { filePath = filePath; label = label; content = content }
        with _ -> return None
    }

let rec private discoverFilesInDirAsync (dirPath: string) (depth: int) (visited: Set<string>) : JS.Promise<string list> =
    promise {
        if depth >= maxDirDepth then return []
        else
            try
                let! real = realpath dirPath
                if visited.Contains real then return []
                else
                    let visited' = visited.Add real
                    let! entries = readdir dirPath
                    let mutable files = []
                    for entry in entries do
                        let name = entryName entry
                        let full = pathJoin dirPath name
                        if entryIsFile entry then files <- full :: files
                        elif entryIsDirectory entry && not (isExcludedDir name) then
                            let! nested = discoverFilesInDirAsync full (depth + 1) visited'
                            files <- List.append nested files
                    return List.rev files
            with _ -> return []
    }

let private foldAsync<'T, 'S> (folder: 'S -> 'T -> JS.Promise<'S>) (state: 'S) (items: 'T list) : JS.Promise<'S> =
    promise {
        let mutable s = state
        for item in items do
            let! next = folder s item
            s <- next
        return s
    }

type private ScanBudget = { files: OmpCapsFile list; totalBytes: int; count: int }

let private scanFull (budget: ScanBudget) = budget.count >= maxCapsFiles || budget.totalBytes >= maxTotalContextBytes

let private absorbOmpFile (budget: ScanBudget) (file: OmpCapsFile) : ScanBudget =
    let nextBytes = budget.totalBytes + file.content.Length
    if nextBytes > maxTotalContextBytes then budget
    else { files = file :: budget.files; totalBytes = nextBytes; count = budget.count + 1 }

let findOmpCapsFiles (projectRoot: string) : JS.Promise<OmpCapsFile list> =
    promise {
        let mutable budget = { files = []; totalBytes = 0; count = 0 }
        try
            let! rootEntries = readdir projectRoot
            for entry in rootEntries do
                if scanFull budget then ()
                else
                    let name = entryName entry
                    let fullPath = pathJoin projectRoot name
                    if entryIsFile entry && capsFileRe.IsMatch name && not (excludedFileNames.Contains name) then
                        let! info = tryReadOmpFileAsync fullPath name
                        match info with
                        | Some file -> budget <- absorbOmpFile budget file
                        | None -> ()
                    elif entryIsDirectory entry && capsDirRe.IsMatch name && not (isExcludedDir name) then
                        let! dirFiles = discoverFilesInDirAsync fullPath 0 Set.empty
                        let! budget' =
                            foldAsync
                                (fun b filePath ->
                                    promise {
                                        if scanFull b then return b
                                        else
                                            let! info = tryReadOmpFileAsync filePath (pathRelative projectRoot filePath)
                                            match info with
                                            | Some file -> return absorbOmpFile b file
                                            | None -> return b
                                    })
                                budget
                                dirFiles
                        budget <- budget'
            return budget.files |> List.sortBy (fun f -> f.filePath)
        with _ -> return []
    }

let buildCapsContextAsync (projectRoot: string) : JS.Promise<string> =
    promise {
        let! files = findOmpCapsFiles projectRoot
        return formatOmpCapsContext files
    }

let private normalizeSystemPrompt (systemPrompt: obj) : string array =
    if isNull systemPrompt then [||]
    elif Dyn.isArray systemPrompt then
        systemPrompt :?> obj array |> Array.map string
    else
        [| string systemPrompt |]

let private stripDirContextSegment (segment: string) : string option =
    let pattern = Regex("<dir-context>[\s\S]*?</dir-context>", RegexOptions.IgnoreCase)
    let stripped = pattern.Replace(segment, "").Trim()
    if stripped = "" then None else Some stripped

let stripHostAgentsPrompt (systemPrompt: obj) : string array =
    normalizeSystemPrompt systemPrompt
    |> Array.choose stripDirContextSegment

let private hasCapsContext (parts: string array) =
    parts |> Array.exists (fun s -> s.Contains capsMarker)

let appendCapsContext (systemPrompt: obj) (projectRoot: string) : JS.Promise<string array> =
    promise {
        let parts = normalizeSystemPrompt systemPrompt
        if hasCapsContext parts then return parts
        else
            let! caps = buildCapsContextAsync projectRoot
            if caps = "" then return parts
            else return Array.append [| caps |] parts
    }