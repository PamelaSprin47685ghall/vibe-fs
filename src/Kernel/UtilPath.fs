module VibeFs.Kernel.UtilPath

open Fable.Core
open Fable.Core.JsInterop

[<Import("relative", "node:path")>]
let private relative (fromPath: string) (toPath: string) : string = jsNative
[<Import("isAbsolute", "node:path")>]
let private isAbsolute (p: string) : bool = jsNative

/// True when `child` lives inside `parent` (no `..` climb, not absolute-relative).
let isWithinDirectory (child: string) (parentDir: string) : bool =
    let rel = relative parentDir child
    not (rel.StartsWith("..")) && not (isAbsolute rel)
