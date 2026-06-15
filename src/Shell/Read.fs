module VibeFs.Shell.Read

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell.Path

[<Global("process")>]
let private nodeProcess : obj = jsNative

[<Import("promises", "node:fs")>]
let private fsPromises : obj = jsNative

let private asPromise<'T> (o: obj) : JS.Promise<'T> = unbox<JS.Promise<'T>> o

let private readFileAsync (p: string) : JS.Promise<string> =
    fsPromises?readFile(p, "utf-8") |> asPromise<string>

let private readdir (p: string) : JS.Promise<obj[]> =
    fsPromises?readdir(p, {| withFileTypes = true |}) |> asPromise<obj[]>

let private formatSize (bytes: int) : string =
    if bytes < 1024 then $"{bytes}B"
    elif bytes < 1024 * 1024 then sprintf "%.1fK" (float bytes / 1024.0)
    else sprintf "%.1fM" (float bytes / (1024.0 * 1024.0))

let private formatMtime (mtime: obj) : string =
    try
        let d = unbox<string> (mtime?ToISOString())
        d.Substring(0, 19).Replace("T", " ")
    with _ -> ""

let private listDirectoryEntries (dirPath: string) : Async<string> =
    async {
        let! entries = readdir dirPath |> Async.AwaitPromise
        let lines = ResizeArray<string>()
        lines.Add($"total {entries.Length}")
        for entry in entries do
            let name = unbox<string> (entry?name)
            let isDir = unbox<bool> (entry?isDirectory())
            let kind = if isDir then "d" else "-"
            let size = if isDir then 0 else unbox<int> (entry?size)
            let mtime = formatMtime (entry?mtime)
            lines.Add(sprintf "%s %8d %s %s" kind size mtime name)
        return String.concat "\n" lines
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

/// Read a file (with optional offset/limit) or format a directory listing for `path`.
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