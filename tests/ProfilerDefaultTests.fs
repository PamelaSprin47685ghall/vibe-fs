module Wanxiangshu.Tests.ProfilerDefaultTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureGatesFs

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let private sourcePath (cwd: string) (relativePath: string) = pathJoin cwd relativePath

let run () : unit =
    let cwd = unbox<string> (nodeProcess?cwd ())

    let runtimeScope =
        readFileSync (sourcePath cwd "src/Runtime/Workspace/RuntimeScope.fs") "utf8"

    let profiler =
        readFileSync (sourcePath cwd "src/Runtime/Tooling/Profiler.fs") "utf8"

    check "RuntimeScope does not initialize the profiler" (not (runtimeScope.Contains "Profiler.initGlobal"))
    check "profiler has no implicit initialization entry" (not (profiler.Contains "let initGlobal"))
    check "profiler keeps an explicit start entry" (profiler.Contains "let start")
