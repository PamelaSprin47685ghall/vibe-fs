module Wanxiangshu.Tests.FallbackConfigShapeTests

open Fable.Core
open Wanxiangshu.Tests.Assert

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

[<Import("cwd", "node:process")>]
let private cwd () : string = jsNative

let private sourceFile (relativePath: string) : string =
    let root = cwd ()
    readFileSync (System.IO.Path.Combine(root, relativePath)) "utf-8"

let run () : unit =
    let typeSource = sourceFile "src/Kernel/FallbackKernel/Types.fs"
    let codecSource = sourceFile "src/Runtime/Fallback/FallbackConfigCodec.fs"

    check "fallback domain type has no ghost zero-width flag" (not (typeSource.Contains "LegacyZeroWidthContinue"))
    check "fallback codec has no ghost zero-width flag" (not (codecSource.Contains "legacyZeroWidthContinue"))
