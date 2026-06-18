module VibeFs.Shell.Caps

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions
open VibeFs.Kernel.CapsFormat

let maxFileSize = 4 * 1_048_576
let maxTotalContextBytes = 8 * 1_048_576
let maxCapsFiles = 2000

type Budget = { results: CapsFile ResizeArray; totalBytes: int; count: int }

let fresh () : Budget = { results = ResizeArray (); totalBytes = 0; count = 0 }

let isFull (budget: Budget) : bool = budget.count >= maxCapsFiles || budget.totalBytes >= maxTotalContextBytes

let absorb (file: CapsFile) (budget: Budget) : Budget =
    let nextTotal = budget.totalBytes + file.content.Length
    if nextTotal > maxTotalContextBytes then budget
    else
        budget.results.Add file
        { results = budget.results; totalBytes = nextTotal; count = budget.count + 1 }

let capsFileRe = Regex(@"^[A-Z][A-Z0-9_]*\.md$")
let capsDirRe = Regex(@"^[A-Z][A-Z0-9_]*$")
let capsDotDirRe = Regex(@"^\.[A-Z][A-Z0-9_]*$")

let excludedFileNames = set [ "AGENTS.md"; "CLAUDE.md"; "README.md" ]
let excludedDirNames =
    set [ "AGENTS"; "CLAUDE"; "NODE_MODULES"; ".GIT"; "TARGET"; "DIST"; "OUT"; ".VENV"; "VENV"; "__PYCACHE__"; ".CACHE"; ".NEXT"; ".TURBO"; ".PARCEL-CACHE" ]

let isExcludedDir (name: string) : bool =
    Set.contains (name.ToUpperInvariant ()) excludedDirNames || (name.StartsWith "." && not (capsDotDirRe.IsMatch name))

let isCapsFile (name: string) : bool = capsFileRe.IsMatch name && not (Set.contains name excludedFileNames)

let isCapsDir (name: string) : bool = not (isExcludedDir name) && (capsDirRe.IsMatch name || capsDotDirRe.IsMatch name)

[<Import("promises", "node:fs")>]
let private fsPromises : obj = jsNative

let private asPromise<'T> (value: obj) : JS.Promise<'T> = unbox<JS.Promise<'T>> value
let private readdir (dir: string) : JS.Promise<obj[]> = fsPromises?readdir(dir, {| withFileTypes = true |}) |> asPromise<obj[]>
let private stat (path: string) : JS.Promise<obj> = fsPromises?stat(path) |> asPromise<obj>
let private readFile (path: string) : JS.Promise<string> = fsPromises?readFile(path, "utf-8") |> asPromise<string>
let private realpath (path: string) : JS.Promise<string> = fsPromises?realpath(path) |> asPromise<string>
let private entryName (entry: obj) : string = entry?name
let private entryIsFile (entry: obj) : bool = entry?isFile ()
let private entryIsDirectory (entry: obj) : bool = entry?isDirectory ()
let private statSize (s: obj) : int = s?size
let private statIsFile (s: obj) : bool = s?isFile ()

[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative
[<Import("relative", "node:path")>]
let private pathRelative (a: string) (b: string) : string = jsNative

let maxDirDepth = 5

let private tryReadFileAsync (filePath: string) (label: string) : Async<CapsFile option> =
    async {
        try
            let! fileStat = stat filePath |> Async.AwaitPromise
            if not (statIsFile fileStat) || statSize fileStat > maxFileSize then return None
            else
                let! content = readFile filePath |> Async.AwaitPromise
                return if content.Trim() = "" then None else Some { filePath = filePath; label = label; content = content }
        with _ -> return None
    }

let private absorbFileAsync (filePath: string) (label: string) (budget: Budget) : Async<Budget> =
    async {
        if isFull budget then return budget
        else
            let! fileInfo = tryReadFileAsync filePath label
            match fileInfo with
            | None -> return budget
            | Some file -> return absorb file budget
    }

let rec private discoverFilesInDirAsync (dirPath: string) (depth: int) (visited: Set<string>) : Async<string list * Set<string>> =
    async {
        if depth >= maxDirDepth then return ([], visited)
        else
            try
                let! realPath = realpath dirPath |> Async.AwaitPromise
                if Set.contains realPath visited then return ([], visited)
                else
                    let! entries = readdir dirPath |> Async.AwaitPromise
                    let visited' = Set.add realPath visited
                    let rec processEntry index acc currentVisited =
                        async {
                            if index >= entries.Length then return (acc, currentVisited)
                            else
                                let entry = entries.[index]
                                let name = entryName entry
                                let fullPath = pathJoin dirPath name
                                if entryIsFile entry then
                                    return! processEntry (index + 1) (fullPath :: acc) currentVisited
                                elif entryIsDirectory entry && not (isExcludedDir name) then
                                    let! (subFiles, visited'') = discoverFilesInDirAsync fullPath (depth + 1) currentVisited
                                    return! processEntry (index + 1) (acc @ subFiles) visited''
                                else
                                    return! processEntry (index + 1) acc currentVisited
                        }
                    return! processEntry 0 [] visited'
            with _ -> return ([], visited)
    }

let private absorbFilesAsync (files: string list) (projectRoot: string) (budget: Budget) : Async<Budget> =
    async {
        let rec loop remaining currentBudget =
            async {
                match remaining with
                | [] -> return currentBudget
                | filePath :: rest ->
                    if isFull currentBudget then return currentBudget
                    else
                        let! nextBudget = absorbFileAsync filePath (pathRelative projectRoot filePath) currentBudget
                        return! loop rest nextBudget
            }
        return! loop files budget
    }

let private discoverRootAsync (projectRoot: string) (budget: Budget) : Async<Budget> =
    async {
        let! entries =
            async {
                try return! readdir projectRoot |> Async.AwaitPromise
                with _ -> return [||]
            }
        let rec processEntry index currentBudget =
            async {
                if index >= entries.Length || isFull currentBudget then return currentBudget
                else
                    let entry = entries.[index]
                    let name = entryName entry
                    let fullPath = pathJoin projectRoot name
                    if entryIsFile entry && isCapsFile name then
                        let! nextBudget = absorbFileAsync fullPath name currentBudget
                        return! processEntry (index + 1) nextBudget
                    elif entryIsDirectory entry && isCapsDir name then
                        let! (subFiles, _) = discoverFilesInDirAsync fullPath 0 Set.empty
                        let! nextBudget = absorbFilesAsync subFiles projectRoot currentBudget
                        return! processEntry (index + 1) nextBudget
                    else
                        return! processEntry (index + 1) currentBudget
            }
        return! processEntry 0 budget
    }

let tryReadFile (filePath: string) (label: string) : JS.Promise<CapsFile option> = tryReadFileAsync filePath label |> Async.StartAsPromise

let discoverFilesInDir (dirPath: string) (depth: int) (visited: Set<string>) : JS.Promise<string list * Set<string>> =
    discoverFilesInDirAsync dirPath depth visited |> Async.StartAsPromise

let findCapsFiles (projectRoot: string) : JS.Promise<CapsFile list> =
    async {
        let! finalBudget = discoverRootAsync projectRoot (fresh ())
        return finalBudget.results |> Seq.toList |> List.sortBy (fun file -> file.filePath)
    }
    |> Async.StartAsPromise
