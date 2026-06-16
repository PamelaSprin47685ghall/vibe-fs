module VibeFs.Shell.CapsShell

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.CapsFormat
open VibeFs.Shell.CapsFilter
open VibeFs.Shell.CapsBudget

[<Import("promises", "node:fs")>]
let private fsPromises : obj = jsNative

let private asPromise<'T> (o: obj) : JS.Promise<'T> = unbox<JS.Promise<'T>> o
let private readdir (dir: string) : JS.Promise<obj[]> =
    fsPromises?readdir(dir, {| withFileTypes = true |}) |> asPromise<obj[]>
let private stat (path: string) : JS.Promise<obj> = fsPromises?stat(path) |> asPromise<obj>
let private readFile (path: string) : JS.Promise<string> = fsPromises?readFile(path, "utf-8") |> asPromise<string>
let private realpath (path: string) : JS.Promise<string> = fsPromises?realpath(path) |> asPromise<string>
let private entryName (e: obj) : string = e?name
let private entryIsFile (e: obj) : bool = e?isFile ()
let private entryIsDirectory (e: obj) : bool = e?isDirectory ()
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
            let! s = stat filePath |> Async.AwaitPromise
            if not (statIsFile s) || statSize s > maxFileSize then return None
            else
                let! content = readFile filePath |> Async.AwaitPromise
                return if content.Trim() = "" then None else Some { filePath = filePath; label = label; content = content }
        with _ -> return None
    }

let private absorbFileAsync (filePath: string) (label: string) (budget: Budget) : Async<Budget> =
    async {
        if isFull budget then return budget
        else
            let! info = tryReadFileAsync filePath label
            match info with
            | None -> return budget
            | Some f -> return absorb f budget
    }

let rec private discoverFilesInDirAsync (dirPath: string) (depth: int) (visited: Set<string>)
                                        : Async<string list * Set<string>> =
    async {
        if depth >= maxDirDepth then return ([], visited)
        else
            try
                let! realPath = realpath dirPath |> Async.AwaitPromise
                if Set.contains realPath visited then return ([], visited)
                else
                    let! entries = readdir dirPath |> Async.AwaitPromise
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
                    if isFull b then return b
                    else
                        let! b' = absorbFileAsync filePath (pathRelative projectRoot filePath) b
                        return! loop rest b'
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
        let rec processEntry i b =
            async {
                if i >= entries.Length || isFull b then return b
                else
                    let entry = entries.[i]
                    let name = entryName entry
                    let full = pathJoin projectRoot name
                    if entryIsFile entry && isCapsFile name then
                        let! b' = absorbFileAsync full name b
                        return! processEntry (i + 1) b'
                    elif entryIsDirectory entry && isCapsDir name then
                        let! (sub, _) = discoverFilesInDirAsync full 0 Set.empty
                        let! b' = absorbFilesAsync sub projectRoot b
                        return! processEntry (i + 1) b'
                    else return! processEntry (i + 1) b
            }
        return! processEntry 0 budget
    }

let tryReadFile (filePath: string) (label: string) : JS.Promise<CapsFile option> =
    tryReadFileAsync filePath label |> Async.StartAsPromise

let discoverFilesInDir (dirPath: string) (depth: int) (visited: Set<string>)
                       : JS.Promise<string list * Set<string>> =
    discoverFilesInDirAsync dirPath depth visited |> Async.StartAsPromise

let findCapsFiles (projectRoot: string) : JS.Promise<CapsFile list> =
    async {
        let! final = discoverRootAsync projectRoot (fresh ())
        return final.results |> Seq.toList |> List.sortBy (fun f -> f.filePath)
    }
    |> Async.StartAsPromise
