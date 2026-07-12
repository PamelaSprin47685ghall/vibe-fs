module Wanxiangshu.Shell.FileSys

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Shell.TreeSitterShell

[<Emit("$0[$1]()")>]
let private callMethod0 (o: obj) (name: string) : 'T = jsNative

[<Emit("$0[$1]($2)")>]
let private callMethod1 (o: obj) (name: string) (arg: 'A) : 'T = jsNative

[<Emit("$0[$1]($2, $3)")>]
let private callMethod2 (o: obj) (name: string) (arg1: 'A) (arg2: 'B) : 'T = jsNative

[<Emit("$0[$1]($2, $3, $4)")>]
let private callMethod3 (o: obj) (name: string) (arg1: 'A) (arg2: 'B) (arg3: 'C) : 'T = jsNative

[<Import("createHash", "node:crypto")>]
let private createHash (algorithm: string) : obj = jsNative

let sha256HexTruncated (input: string) : string =
    let hash = createHash "sha256"
    callMethod1 hash "update" input |> ignore
    let digested = callMethod1 hash "digest" "hex"
    callMethod2 digested "slice" 0 16

[<Import("resolve", "node:path")>]
let resolve (cwd: string) (filePath: string) : string = jsNative

[<Import("basename", "node:path")>]
let basename (filePath: string) : string = jsNative

[<Import("extname", "node:path")>]
let extname (filePath: string) : string = jsNative

[<Import("dirname", "node:path")>]
let dirname (filePath: string) : string = jsNative

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

[<Import("promises", "node:fs")>]
let private fsPromises: obj = jsNative

let private readFileAsync (p: string) : JS.Promise<string> =
    callMethod2 fsPromises "readFile" p "utf-8"

let private readdir (p: string) : JS.Promise<obj[]> =
    callMethod2 fsPromises "readdir" p {| withFileTypes = true |}

let private statAsync (p: string) : JS.Promise<obj> = callMethod1 fsPromises "stat" p

let private formatMtime (mtime: obj) : string =
    try
        let d: string = callMethod0 mtime "toISOString"
        d.Substring(0, 19).Replace("T", " ")
    with _ ->
        ""

let private listDirectoryEntries (dirPath: string) : JS.Promise<string> =
    promise {
        let! entries = readdir dirPath
        let header = $"total {entries.Length}"

        let! lines =
            entries
            |> Array.map (fun entry ->
                promise {
                    let name = unbox<string> (entry?name)
                    let fullPath = resolve dirPath name
                    let! details = statAsync fullPath
                    let isDir: bool = callMethod0 details "isDirectory"
                    let kind = if isDir then "d" else "-"
                    let size = if isDir then 0 else unbox<int> (details?size)
                    let mtime = formatMtime (details?mtime)
                    return sprintf "%s %8d %s %s" kind size mtime name
                })
            |> Promise.all

        return String.concat "\n" (header :: Array.toList lines)
    }

let private readFileWithLineNumbers (filePath: string) (offset: int option) (limit: int option) : JS.Promise<string> =
    promise {
        let! raw = readFileAsync filePath
        let lines = raw.Split('\n')
        let startLine = defaultArg offset 1
        let startIdx = max 0 (startLine - 1)

        let slice =
            match limit with
            | Some lim -> lines.[startIdx .. min (startIdx + lim - 1) (lines.Length - 1)]
            | None -> lines.[startIdx..]

        let numbered =
            slice
            |> Array.mapi (fun i line -> sprintf "%6d|%s" (startLine + i) (line.TrimEnd('\r')))

        return String.concat "\n" numbered
    }

let read (cwd: string option) (path: string) (offset: int option) (limit: int option) : JS.Promise<string> =
    promise {
        let cwd' = defaultArg cwd (callMethod0 nodeProcess "cwd")
        let resolved = resolve cwd' path
        let! (st: obj) = callMethod1 fsPromises "stat" resolved
        let isDir: bool = callMethod0 st "isDirectory"

        if isDir then
            return! listDirectoryEntries resolved
        else
            return! readFileWithLineNumbers resolved offset limit
    }

let private mkdir (p: string) : JS.Promise<unit> =
    callMethod2 fsPromises "mkdir" p {| recursive = true |}

let private writeFile (p: string) (content: string) : JS.Promise<unit> =
    callMethod3 fsPromises "writeFile" p content "utf-8"

[<Import("access", "node:fs/promises")>]
let private accessAsync (p: string) (mode: int) : JS.Promise<unit> = jsNative

let private fileExistsAsync (p: string) : JS.Promise<bool> =
    promise {
        try
            do! accessAsync p 0
            return true
        with _ ->
            return false
    }

let write (cwd: string option) (file_path: string) (content: string) : JS.Promise<Result<string, DomainError>> =
    promise {
        let cwd' = defaultArg cwd (nodeProcess?cwd ())
        let resolved = resolve cwd' file_path
        let parent = dirname resolved

        try
            if not (System.String.IsNullOrEmpty parent) then
                do! mkdir parent

            do! writeFile resolved content
            let! syntax = checkSyntax content resolved
            return Result.Ok(formatWriteSyntaxResult resolved syntax)
        with ex ->
            return Error(UnknownJsError ex.Message)
    }
