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

let private resolvePath (cwd: string) (rel: string) =
    let path1 = pathJoin cwd rel

    if existsSync path1 then path1
    else if existsSync rel then rel
    else failwithf "File not found: %s (cwd: %s)" rel cwd

let private assertNoDebugPrintf (label: string) (content: string) =
    if content.Contains("printfn") && content.Contains("DEBUG") then
        failwithf "Bare DEBUG printfn found in: %s" label

let private assertNoToken (token: string) (label: string) (content: string) =
    if content.Contains token then
        failwithf "Forbidden %s found in: %s" token label

let private checkExplicitFiles (cwd: string) =
    let files =
        [ "src/Runtime/Fallback/FallbackEventBridge.fs"
          "src/Runtime/Fallback/RuntimeStore.fs"
          "src/Runtime/ToolHookRuntime.fs"
          "src/Hosts/OpenCode/ProgressObserver.fs"
          "src/Hosts/OpenCode/PluginWanxiangzhenE2eMeta.fs" ]

    for rel in files do
        let content = readFileSync (resolvePath cwd rel) "utf8"
        assertNoDebugPrintf rel content
        assertNoToken "debug-mimocode.txt" rel content
        assertNoToken "DEBUG PROGRESS_OBSERVER" rel content
        assertNoToken "JS.console.log" rel content

let private scanProductionSources (cwd: string) =
    let allFsFiles = getFsFiles (resolvePath cwd "src")

    for filePath in allFsFiles do
        let isSembleFile = filePath.Contains("Semble") || filePath.Contains("semble")

        if not isSembleFile then
            let content = readFileSync filePath "utf8"
            assertNoToken "debug-mimocode.txt" filePath content
            assertNoToken "debug-wanxiangzhen.txt" filePath content
            assertNoToken "DEBUG PROGRESS_OBSERVER" filePath content
            assertNoToken "JS.console.log" filePath content
            assertNoDebugPrintf filePath content

let run () : unit =
    let cwd = unbox<string> (nodeProcess?cwd ())
    checkExplicitFiles cwd
    scanProductionSources cwd
    check "production sources contain no unguarded debug output" true
