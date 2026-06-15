module VibeFs.Shell.Path

open Fable.Core

[<Import("resolve", "node:path")>]
let resolve (cwd: string) (filePath: string) : string = jsNative

[<Import("dirname", "node:path")>]
let dirname (filePath: string) : string = jsNative
