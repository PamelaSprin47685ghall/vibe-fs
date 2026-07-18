module Wanxiangshu.Runtime.WorkspacePathResolution

open Fable.Core
open Fable.Core.JsInterop

[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative

[<Import("relative", "node:path")>]
let private pathRelative (a: string) (b: string) : string = jsNative

[<Import("resolve", "node:path")>]
let private pathResolve (cwd: string) (file: string) : string = jsNative

let resolveAbsPath (cwd: string) (file: string) : string = pathResolve cwd file

let relativeToRoot (projectRoot: string) (fullPath: string) : string = pathRelative projectRoot fullPath

let joinPath (dir: string) (name: string) : string = pathJoin dir name
