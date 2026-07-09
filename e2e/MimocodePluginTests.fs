module Wanxiangshu.E2e.MimocodePluginTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.MimocodePluginSpecsPart
open Wanxiangshu.E2e.MimocodePluginSpecs

[<Import("start", "./opencode-harness.js")>]
let private startHarness: obj -> JS.Promise<obj> = jsNative

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let! apiObj = startHarness (createObj [ "variant", box "mimocode" ])
        let harness = unbox<Harness> apiObj
        let ok = ref 0

        do! testSpecs harness ok
        printfn "\n✓ %d mimocode plugin e2e checks passed" ok.Value
        return summary ()
    }
