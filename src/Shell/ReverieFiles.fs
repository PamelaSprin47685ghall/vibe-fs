module VibeFs.Shell.ReverieFiles

open Fable.Core
open Fable.Core.JsInterop

let maxReverieFileBytes = 1_048_576

/// Outcome of attempting to read one reverie file.
type ReverieFileResult =
    { filePath: string
      content: string option
      skipReason: string option }

[<Import("promises", "node:fs")>]
let private fsPromises : obj = jsNative

let private asPromise<'T> (o: obj) : JS.Promise<'T> = unbox<JS.Promise<'T>> o

let private stat (path: string) : JS.Promise<obj> =
    fsPromises?stat(path) |> asPromise<obj>

let private readFile (path: string) : JS.Promise<string> =
    fsPromises?readFile(path, "utf-8") |> asPromise<string>
[<Import("resolve", "node:path")>]
let private pathResolve (cwd: string) (file: string) : string = jsNative
let private statSize (s: obj) : int = s?size
let private statIsFile (s: obj) : bool = s?isFile ()

/// Read one file for the reverie prompt, guarding size and readability.
let readOne (cwd: string) (file: string) : JS.Promise<ReverieFileResult> =
    async {
        let absolute = pathResolve cwd file
        try
            let! s = stat absolute |> Async.AwaitPromise
            if not (statIsFile s) then
                return { filePath = file; content = None; skipReason = Some "not-file" }
            elif statSize s > maxReverieFileBytes then
                return { filePath = file; content = None; skipReason = Some "too-large" }
            else
                let! content = readFile absolute |> Async.AwaitPromise
                return { filePath = absolute; content = Some content; skipReason = None }
        with _ ->
            return { filePath = file; content = None; skipReason = Some "unreadable" }
    }
    |> Async.StartAsPromise

/// Read every file; each that fails yields a typed skip reason.  Sequential so
/// the dynamic module import is reused cleanly across reads.
let readReverieFiles (cwd: string) (files: string list) : JS.Promise<ReverieFileResult list> =
    let rec loop remaining acc =
        async {
            match remaining with
            | [] -> return List.rev acc
            | file :: rest ->
                let! r = readOne cwd file |> Async.AwaitPromise
                return! loop rest (r :: acc)
        }
    loop files [] |> Async.StartAsPromise
