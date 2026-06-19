module VibeFs.Shell.FileSys

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Shell.TreeSitterShell

[<Import("createHash", "node:crypto")>]
let private createHash (algorithm: string) : obj = jsNative

let sha256HexTruncated (input: string) : string =
    let hash = createHash "sha256"
    hash?update(input) |> ignore
    hash?digest("hex")?slice(0, 16)

[<Import("resolve", "node:path")>]
let resolve (cwd: string) (filePath: string) : string = jsNative

[<Import("basename", "node:path")>]
let basename (filePath: string) : string = jsNative

[<Import("extname", "node:path")>]
let extname (filePath: string) : string = jsNative

[<Import("dirname", "node:path")>]
let dirname (filePath: string) : string = jsNative

[<Global("process")>]
let private nodeProcess : obj = jsNative

[<Import("promises", "node:fs")>]
let private fsPromises : obj = jsNative

let private asPromise<'T> (o: obj) : JS.Promise<'T> = unbox<JS.Promise<'T>> o

let private readFileAsync (p: string) : JS.Promise<string> =
    fsPromises?readFile(p, "utf-8") |> asPromise<string>

let private readdir (p: string) : JS.Promise<obj[]> =
    fsPromises?readdir(p, {| withFileTypes = true |}) |> asPromise<obj[]>

let private formatMtime (mtime: obj) : string =
    try
        let d = unbox<string> (mtime?ToISOString())
        d.Substring(0, 19).Replace("T", " ")
    with _ -> ""

let private listDirectoryEntries (dirPath: string) : Async<string> =
    async {
        let! entries = readdir dirPath |> Async.AwaitPromise
        let header = $"total {entries.Length}"
        let lines =
            entries
            |> Array.map (fun entry ->
                let name = unbox<string> (entry?name)
                let isDir = unbox<bool> (entry?isDirectory())
                let kind = if isDir then "d" else "-"
                let size = if isDir then 0 else unbox<int> (entry?size)
                let mtime = formatMtime (entry?mtime)
                sprintf "%s %8d %s %s" kind size mtime name)
        return String.concat "\n" (header :: Array.toList lines)
    }

let private readFileWithLineNumbers (filePath: string) (offset: int option) (limit: int option) : Async<string> =
    async {
        let! raw = readFileAsync filePath |> Async.AwaitPromise
        let lines = raw.Split('\n')
        let startLine = defaultArg offset 1
        let startIdx = max 0 (startLine - 1)
        let slice =
            match limit with
            | Some lim -> lines.[startIdx .. min (startIdx + lim - 1) (lines.Length - 1)]
            | None -> lines.[startIdx ..]
        let numbered =
            slice
            |> Array.mapi (fun i line -> sprintf "%6d|%s" (startLine + i) (line.TrimEnd('\r')))
        return String.concat "\n" numbered
    }

let read (cwd: string option) (path: string) (offset: int option) (limit: int option) : Async<string> =
    async {
        let cwd' = defaultArg cwd (nodeProcess?cwd())
        let resolved = resolve cwd' path
        let! st = fsPromises?stat(resolved) |> unbox<JS.Promise<obj>> |> Async.AwaitPromise
        let isDir = unbox<bool> (st?isDirectory())
        if isDir then
            return! listDirectoryEntries resolved
        else
            return! readFileWithLineNumbers resolved offset limit
    }

let private mkdir (p: string) : JS.Promise<unit> =
    fsPromises?mkdir(p, {| recursive = true |}) |> asPromise<unit>

let private writeFile (p: string) (content: string) : JS.Promise<unit> =
    fsPromises?writeFile(p, content, "utf-8") |> asPromise<unit>

[<Import("access", "node:fs/promises")>]
let private accessAsync (p: string) (mode: int) : JS.Promise<unit> = jsNative

let private fileExistsAsync (p: string) : Async<bool> =
    async {
        try
            do! accessAsync p 0 |> Async.AwaitPromise
            return true
        with _ ->
            return false
    }

let private formatSyntaxDiagnostics (filePath: string) (result: SyntaxCheckResult) : string =
    match result with
    | Ok (lang, [||]) ->
        $"Successfully wrote to {filePath}"
    | Ok (lang, errors) ->
        let header = $"[syntax-check] Syntax check failed for {filePath} ({lang}):"
        let body =
            errors
            |> Array.map (fun e -> $"  line {e.line}, col {e.column}: {e.message}")
            |> String.concat "\n"
        header + "\n" + body
    | Failed (lang, reason) ->
        $"[syntax-check] Syntax check failed for {filePath} ({lang}): {reason}"

let write (cwd: string option) (file_path: string) (content: string) : Async<string> =
    async {
        let cwd' = defaultArg cwd (nodeProcess?cwd())
        let resolved = resolve cwd' file_path
        let parent = dirname resolved
        if not (System.String.IsNullOrEmpty parent) then
            do! mkdir parent |> Async.AwaitPromise
        do! writeFile resolved content |> Async.AwaitPromise
        let! syntax = checkSyntax content resolved |> Async.AwaitPromise
        return formatSyntaxDiagnostics resolved syntax
    }
