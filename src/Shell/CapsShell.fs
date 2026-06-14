module VibeFs.Shell.CapsShell

open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.CapsFormat

[<Emit("import('node:fs/promises')")>]
let private fsAsync () : JS.Promise<obj> = jsNative
[<Emit("$0.readdir($1, { withFileTypes: true })")>]
let private readdir (fs': obj) (dir: string) : JS.Promise<obj[]> = jsNative
[<Emit("$0.stat($1)")>]
let private stat (fs': obj) (path: string) : JS.Promise<obj> = jsNative
[<Emit("$0.readFile($1, 'utf-8')")>]
let private readFile (fs': obj) (path: string) : JS.Promise<string> = jsNative
[<Emit("$0.realpath($1)")>]
let private realpath (fs': obj) (path: string) : JS.Promise<string> = jsNative
let private entryName (e: obj) : string = e?name
let private entryIsFile (e: obj) : bool = e?isFile ()
let private entryIsDirectory (e: obj) : bool = e?isDirectory ()
let private statSize (s: obj) : int = s?size
let private statIsFile (s: obj) : bool = s?isFile ()
[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative
[<Import("relative", "node:path")>]
let private pathRelative (a: string) (b: string) : string = jsNative

let capsFileRe = Regex(@"^[A-Z][A-Z0-9_]*\.md$")
let capsDirRe = Regex(@"^[A-Z][A-Z0-9_]*$")
let capsDotDirRe = Regex(@"^\.[A-Z][A-Z0-9_]*$")
let private excludedFileNames = set [ "AGENTS.md"; "CLAUDE.md"; "README.md" ]
let private excludedDirNames =
    set [ "AGENTS"; "CLAUDE"; "NODE_MODULES"; ".GIT"; "TARGET"; "DIST"; "OUT"
          ".VENV"; "VENV"; "__PYCACHE__"; ".CACHE"; ".NEXT"; ".TURBO"; ".PARCEL-CACHE" ]

let maxFileSize = 1_048_576
let maxTotalContextBytes = 8 * 1_048_576
let maxCapsFiles = 2000
let maxDirDepth = 5

let private isExcludedDir (name: string) : bool =
    Set.contains (name.ToUpperInvariant ()) excludedDirNames
    || (name.StartsWith(".") && not (capsDotDirRe.IsMatch name))

/// Mutable budget threaded through sequential discovery.
type private Budget = { results: CapsFile ResizeArray; totalBytes: int; count: int }
let private budgetFull (b: Budget) = b.count >= maxCapsFiles || b.totalBytes >= maxTotalContextBytes
let private freshBudget () : Budget = { results = ResizeArray (); totalBytes = 0; count = 0 }

let private tryReadFileAsync (filePath: string) (label: string) : Async<CapsFile option> =
    async {
        let! api = fsAsync () |> Async.AwaitPromise
        try
            let! s = stat api filePath |> Async.AwaitPromise
            if not (statIsFile s) || statSize s > maxFileSize then return None
            else
                let! content = readFile api filePath |> Async.AwaitPromise
                return if content.Trim() = "" then None else Some { filePath = filePath; label = label; content = content }
        with _ -> return None
    }

/// Fold one file into the budget, respecting size and count caps.
let private absorbFileAsync (filePath: string) (label: string) (budget: Budget) : Async<Budget> =
    async {
        if budgetFull budget then return budget
        else
            let! info = tryReadFileAsync filePath label
            match info with
            | None -> return budget
            | Some f ->
                let nextTotal = budget.totalBytes + f.content.Length
                if nextTotal > maxTotalContextBytes then return budget
                else
                    budget.results.Add f
                    return { results = budget.results; totalBytes = nextTotal; count = budget.count + 1 }
    }

/// Recursively gather file paths under an uppercase-named directory.
let rec private discoverFilesInDirAsync (dirPath: string) (depth: int) (visited: Set<string>)
                                        : Async<string list * Set<string>> =
    async {
        if depth >= maxDirDepth then return ([], visited)
        else
            let! api = fsAsync () |> Async.AwaitPromise
            try
                let! realPath = realpath api dirPath |> Async.AwaitPromise
                if Set.contains realPath visited then return ([], visited)
                else
                    let! entries = readdir api dirPath |> Async.AwaitPromise
                    let visited' = Set.add realPath visited
                    let rec processEntry i acc vis =
                        async {
                            if i >= entries.Length then return (acc, vis)
                            else
                                let entry = entries.[i]
                                let name = entryName entry
                                let full = pathJoin dirPath name
                                if entryIsFile entry then
                                    return! processEntry (i + 1) (full :: acc) vis
                                elif entryIsDirectory entry && not (isExcludedDir name) then
                                    let! (sub, vis') = discoverFilesInDirAsync full (depth + 1) vis
                                    return! processEntry (i + 1) (acc @ sub) vis'
                                else return! processEntry (i + 1) acc vis
                        }
                    return! processEntry 0 [] visited'
            with _ -> return ([], visited)
    }

let private absorbFilesAsync (files: string list) (projectRoot: string) (budget: Budget) : Async<Budget> =
    async {
        let rec loop remaining b =
            async {
                match remaining with
                | [] -> return b
                | filePath :: rest ->
                    if budgetFull b then return b
                    else
                        let! b' = absorbFileAsync filePath (pathRelative projectRoot filePath) b
                        return! loop rest b'
            }
        return! loop files budget
    }

/// Walk the project root, absorbing caps files and recursing into caps dirs.
let private discoverRootAsync (api: obj) (projectRoot: string) (budget: Budget) : Async<Budget> =
    async {
        let! entries =
            async {
                try return! readdir api projectRoot |> Async.AwaitPromise
                with _ -> return [||]
            }
        let rec processEntry i b =
            async {
                if i >= entries.Length || budgetFull b then return b
                else
                    let entry = entries.[i]
                    let name = entryName entry
                    let full = pathJoin projectRoot name
                    if entryIsFile entry && capsFileRe.IsMatch name
                       && not (Set.contains name excludedFileNames) then
                        let! b' = absorbFileAsync full name b
                        return! processEntry (i + 1) b'
                    elif entryIsDirectory entry && capsDirRe.IsMatch name
                         && not (isExcludedDir name) then
                        let! (sub, _) = discoverFilesInDirAsync full 0 Set.empty
                        let! b' = absorbFilesAsync sub projectRoot b
                        return! processEntry (i + 1) b'
                    else return! processEntry (i + 1) b
            }
        return! processEntry 0 budget
    }

// ── Public JS.Promise API ────────────────────────────────────────────────────

let tryReadFile (filePath: string) (label: string) : JS.Promise<CapsFile option> =
    tryReadFileAsync filePath label |> Async.StartAsPromise

let discoverFilesInDir (dirPath: string) (depth: int) (visited: Set<string>)
                       : JS.Promise<string list * Set<string>> =
    discoverFilesInDirAsync dirPath depth visited |> Async.StartAsPromise

/// Discover all capability files rooted at `projectRoot`, respecting budgets.
let findCapsFiles (projectRoot: string) : JS.Promise<CapsFile list> =
    async {
        let! api = fsAsync () |> Async.AwaitPromise
        let! final = discoverRootAsync api projectRoot (freshBudget ())
        return final.results |> Seq.toList |> List.sortBy (fun f -> f.filePath)
    }
    |> Async.StartAsPromise
