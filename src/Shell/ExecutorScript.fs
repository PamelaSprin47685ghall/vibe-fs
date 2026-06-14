module VibeFs.Shell.ExecutorScript

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Shell.ExecutorPaths

[<Import("writeFileSync", "node:fs")>]
let private writeFileSync (path: string) (content: string) (encoding: string) : unit = jsNative
[<Import("chmodSync", "node:fs")>]
let private chmodSync (path: string) (mode: int) : unit = jsNative
[<Emit("process.platform")>]
let private platform : string = jsNative

/// Write a program to a temp script path, making it executable on Unix.
let createTempScript (scriptPath: string) (program: string) : string =
    writeFileSync scriptPath program "utf-8"
    if platform <> "win32" then chmodSync scriptPath 0o755
    scriptPath

/// Per-session temp script path for a given language extension.
let getTempScriptPath (sessionId: string) (extension: string) : string =
    getExecutorTempScriptPath sessionId extension
