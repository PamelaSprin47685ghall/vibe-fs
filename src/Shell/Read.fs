module VibeFs.Shell.Read

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell.Path

[<Emit("process.cwd()")>]
let private processCwd () : string = jsNative

[<Emit("import('node:fs/promises')")>]
let private importFsPromises () : JS.Promise<obj> = jsNative

let private fsApi () = importFsPromises () |> Async.AwaitPromise

[<Emit("$0.readFile($1, 'utf-8')")>]
let private readFileAsync (fs': obj) (p: string) : JS.Promise<string> = jsNative

[<Emit("$0.readdir($1, { withFileTypes: true })")>]
let private readdir (fs': obj) (p: string) : JS.Promise<obj[]> = jsNative

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
        let! api = fsApi ()
        let! entries = readdir api dirPath |> Async.AwaitPromise
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
        let! api = fsApi ()
        let! raw = readFileAsync api filePath |> Async.AwaitPromise
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
        let cwd' = defaultArg cwd (processCwd ())
        let resolved = resolve cwd' path
        let! api = fsApi ()
        let statFn = Dyn.get api "stat"
        let! st = Dyn.call1 statFn resolved |> unbox<JS.Promise<obj>> |> Async.AwaitPromise
        let isDir = unbox<bool> (st?isDirectory())
        if isDir then
            return! listDirectoryEntries resolved
        else
            return! readFileWithLineNumbers resolved offset limit
    }