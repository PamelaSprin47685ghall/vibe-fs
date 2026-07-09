module Wanxiangshu.E2e.OmpTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.OmpTestsSpecs

[<Import("start", "./omp-runner.js")>]
let start: obj -> JS.Promise<obj> = jsNative

let runAll (_args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let! apiObj = start (createObj [])
        let h = unbox<OmpHarness> apiObj

        let expected = 40
        let ok = ref 0

        do! testSpecs h ok
        printfn "\n✓ %d/%d omp e2e checks passed" ok.Value expected
        return summary ()
    }
