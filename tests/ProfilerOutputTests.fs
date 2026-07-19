module Wanxiangshu.Tests.ProfilerOutputTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureGatesFs

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let private sourcePath (cwd: string) (relativePath: string) = pathJoin cwd relativePath

let run () : unit =
    let cwd = unbox<string> (nodeProcess?cwd ())

    let profiler =
        readFileSync (sourcePath cwd "src/Runtime/Tooling/Profiler.fs") "utf8"

    check
        "profiler must not hardcode /tmp/wanxiangshu.cpuprofile"
        (not (profiler.Contains "/tmp/wanxiangshu.cpuprofile"))

    check
        "profiler must not hardcode /tmp/wanxiangshu.heapprofile"
        (not (profiler.Contains "/tmp/wanxiangshu.heapprofile"))

    let hasConfigurableOutputDir =
        profiler.Contains "WANXIANGSHU_PROFILER_DIR"
        || profiler.Contains "outputDir"
        || profiler.Contains "outputDirectory"

    check "profiler must support configurable output directory" hasConfigurableOutputDir

    let hasUniqueFileNameMechanism =
        (profiler.Contains "pid"
         || profiler.Contains "PID"
         || profiler.Contains "process.pid")
        && (profiler.Contains "Date.now"
            || profiler.Contains "timestamp"
            || profiler.Contains "random"
            || profiler.Contains "crypto")

    check "profiler must construct unique file names (PID + timestamp/random)" hasUniqueFileNameMechanism

    check
        "activeSession must remain private (not exposed as module-level public)"
        (profiler.Contains "let private activeSession")
