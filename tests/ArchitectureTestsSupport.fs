module Wanxiangshu.Tests.ArchitectureTestsSupport

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: string) (encoding: string) : string = jsNative

[<Import("existsSync", "node:fs")>]
let existsSync (path: string) : bool = jsNative

[<Import("readdirSync", "node:fs")>]
let readdirSync (path: string) : string array = jsNative

let requireFile (path: string) : string =
    check ("arch: exists " + path) (existsSync path)
    if existsSync path then
        let content = readFileSync path "utf-8"
        check ("arch: non-empty " + path) (not (System.String.IsNullOrEmpty content))
        content
    else ""

let requireMuxHostTools () : string =
    requireFile "src/Mux/HostTools.fs" + "\n" + requireFile "src/Mux/HostToolsFuzzy.fs"

let fsFiles (dir: string) : string array =
    check ("arch: dir exists " + dir) (existsSync dir)
    if existsSync dir then readdirSync dir |> Array.filter (fun f -> f.EndsWith ".fs")
    else [||]

[<Import("statSync", "node:fs")>]
let private statSync (path: string) : obj = jsNative

let private isDirectory (path: string) : bool =
    let st = statSync path
    emitJsExpr st "!!$0 && $0.isDirectory()"

let rec fsFilesRecursive (dir: string) : string list =
    if not (existsSync dir) then []
    else
        readdirSync dir
        |> Array.collect (fun name ->
            let full = dir + "/" + name
            if isDirectory full then fsFilesRecursive full |> List.toArray
            elif name.EndsWith ".fs" then [| full |]
            else [||])
        |> Array.toList

/// Relative `.fs` paths under `dir`, recursive. Use when arch scans must include
/// modules that have been split into subdirectories (e.g.
/// `src/Kernel/KnowledgeGraph/Prompts.fs`); labels are forward-slash relative
/// paths so they match how callers build `dir + "/" + name` paths.
let rec fsFilesRelative (dir: string) : string list =
    if not (existsSync dir) then []
    else
        readdirSync dir
        |> Array.collect (fun name ->
            let full = dir + "/" + name
            if isDirectory full then
                fsFilesRecursive full |> List.toArray
                |> Array.map (fun p -> p.Substring(dir.Length + 1))
            elif name.EndsWith ".fs" then [| name |]
            else [||])
        |> Array.toList

let objTypeRe = System.Text.RegularExpressions.Regex(@":\s*obj\b")
let boxRe = System.Text.RegularExpressions.Regex(@"\bbox\b")
let emptyDefaultRe =
    System.Text.RegularExpressions.Regex("Option\\.defaultValue\\s*\"")

let reportFromFlatPartDefRe =
    System.Text.RegularExpressions.Regex(@"let\s+reportFromFlatPart(?!WithProjection)")

let nonCommentCode (content: string) : string =
    content.Split('\n')
    |> Array.choose (fun line ->
        let trimmed = line.TrimStart()
        if trimmed.StartsWith("//") then None else Some line)
    |> String.concat "\n"