module Wanxiangshu.Tests.ProductionDebugOutputTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

[<Import("existsSync", "node:fs")>]
let private existsSync (path: string) : bool = jsNative

[<Import("readdirSync", "node:fs")>]
let private readdirSync (path: string) : string array = jsNative

[<Import("statSync", "node:fs")>]
let private statSync (path: string) : obj = jsNative

[<Import("join", "node:path")>]
let private pathJoin (path: string) (seg: string) : string = jsNative

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let private isDirectory (path: string) : bool =
    let stat = statSync path
    stat?isDirectory ()

let rec private getFsFiles (dir: string) : string list =
    if not (existsSync dir) then
        []
    else
        let entries = readdirSync dir

        [ for entry in entries do
              let fullPath = pathJoin dir entry

              if isDirectory fullPath then
                  yield! getFsFiles fullPath
              elif fullPath.EndsWith(".fs") then
                  yield fullPath ]

let run () : unit =
    let cwd = unbox<string> (nodeProcess?cwd ())

    let resolvePath (rel: string) =
        let path1 = pathJoin cwd rel

        if existsSync path1 then path1
        else if existsSync rel then rel
        else failwithf "File not found: %s (cwd: %s)" rel cwd

    let fileFallbackEventBridge = "src/Shell/FallbackEventBridge.fs"
    let fileFallbackRuntimeState = "src/Shell/FallbackRuntimeState.fs"
    let fileToolHookRuntime = "src/Shell/ToolHookRuntime.fs"
    let fileProgressObserver = "src/Opencode/ProgressObserver.fs"
    let filePluginWanxiangzhen = "src/Opencode/PluginWanxiangzhenE2eMeta.fs"

    // 1. Explicit check for FallbackEventBridge.fs
    let pathBridge = resolvePath fileFallbackEventBridge
    let contentBridge = readFileSync pathBridge "utf8"

    if contentBridge.Contains("printfn") && contentBridge.Contains("DEBUG") then
        failwithf "Bare DEBUG printfn found in: %s" fileFallbackEventBridge

    // 2. Explicit check for FallbackRuntimeState.fs
    let pathState = resolvePath fileFallbackRuntimeState
    let contentState = readFileSync pathState "utf8"

    if contentState.Contains("printfn") && contentState.Contains("DEBUG") then
        failwithf "Bare DEBUG printfn found in: %s" fileFallbackRuntimeState

    // 3. Explicit check for ToolHookRuntime.fs
    let pathToolHook = resolvePath fileToolHookRuntime
    let contentToolHook = readFileSync pathToolHook "utf8"

    if contentToolHook.Contains("printfn") && contentToolHook.Contains("DEBUG") then
        failwithf "Bare DEBUG printfn found in: %s" fileToolHookRuntime

    // 4. Explicit check for ProgressObserver.fs
    let pathObserver = resolvePath fileProgressObserver
    let contentObserver = readFileSync pathObserver "utf8"

    if contentObserver.Contains("debug-mimocode.txt") then
        failwithf "Forbidden debug-mimocode.txt found in: %s" fileProgressObserver

    if contentObserver.Contains("DEBUG PROGRESS_OBSERVER") then
        failwithf "Forbidden DEBUG PROGRESS_OBSERVER found in: %s" fileProgressObserver

    // 5. Explicit check for PluginWanxiangzhenE2eMeta.fs
    let pathMeta = resolvePath filePluginWanxiangzhen
    let contentMeta = readFileSync pathMeta "utf8"

    if contentMeta.Contains("JS.console.log") then
        failwithf "Forbidden JS.console.log found in: %s" filePluginWanxiangzhen

    // 6. Generic scan of src/**/*.fs excluding Semble/semble files
    let srcDir = resolvePath "src"
    let allFsFiles = getFsFiles srcDir

    for filePath in allFsFiles do
        let isSembleFile = filePath.Contains("Semble") || filePath.Contains("semble")

        if not isSembleFile then
            let content = readFileSync filePath "utf8"

            if content.Contains("debug-mimocode.txt") then
                failwithf "Forbidden debug-mimocode.txt found in scanned file: %s" filePath

            if content.Contains("debug-wanxiangzhen.txt") then
                failwithf "Forbidden debug-wanxiangzhen.txt found in scanned file: %s" filePath

            if content.Contains("DEBUG PROGRESS_OBSERVER") then
                failwithf "Forbidden DEBUG PROGRESS_OBSERVER found in scanned file: %s" filePath

            if content.Contains("JS.console.log") then
                failwithf "Forbidden JS.console.log found in scanned file: %s" filePath

            if content.Contains("printfn") && content.Contains("DEBUG") then
                failwithf "Bare DEBUG printfn found in scanned file: %s" filePath

    check "production sources contain no unguarded debug output" true
