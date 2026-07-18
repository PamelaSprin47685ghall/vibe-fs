module Wanxiangshu.Tests.ContinuationCleanupTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureGatesFs

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let run () : unit =
    let cwd = unbox<string> (nodeProcess?cwd ())

    let removedFiles =
        [ "src/Kernel/Fallback/ContinuationEventParse.fs"
          "src/Kernel/Fallback/ContinuationProjection.fs"
          "src/Kernel/Fallback/ContinuationDecision.fs"
          "src/Runtime/Fallback/ContinuationEventCodec.fs"
          "src/Runtime/Fallback/ContinuationHost.fs"
          "src/Runtime/Fallback/ContinuationCommandProcessor.fs"
          "src/Runtime/Fallback/ContinuationSupervisor.fs"
          "src/Hosts/OpenCode/Fallback/ContinuationHost.fs" ]

    for relativePath in removedFiles do
        check
            (sprintf "uncomposed continuation file is removed: %s" relativePath)
            (not (existsSync (pathJoin cwd relativePath)))
