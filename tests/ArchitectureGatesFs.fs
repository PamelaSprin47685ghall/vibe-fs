module Wanxiangshu.Tests.ArchitectureGatesFs

open Fable.Core
open Fable.Core.JsInterop

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: string) (encoding: string) : string = jsNative

[<Import("existsSync", "node:fs")>]
let existsSync (path: string) : bool = jsNative

[<Import("readdirSync", "node:fs")>]
let readdirSync (path: string) : string array = jsNative

[<Import("statSync", "node:fs")>]
let statSync (path: string) : obj = jsNative

[<Import("join", "node:path")>]
let pathJoin (path: string) (seg: string) : string = jsNative

let isDirectory (path: string) : bool =
    let stat = statSync path
    stat?isDirectory ()

let rec collectFsFiles (dir: string) : string list =
    if not (existsSync dir) then
        []
    else
        [ for entry in readdirSync dir do
              let full = pathJoin dir entry

              if isDirectory full then
                  yield! collectFsFiles full
              elif full.EndsWith(".fs") then
                  yield full ]
