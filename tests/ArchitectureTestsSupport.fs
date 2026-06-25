module VibeFs.Tests.ArchitectureTestsSupport

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert

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

let fsFiles (dir: string) : string array =
    check ("arch: dir exists " + dir) (existsSync dir)
    if existsSync dir then readdirSync dir |> Array.filter (fun f -> f.EndsWith ".fs")
    else [||]

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