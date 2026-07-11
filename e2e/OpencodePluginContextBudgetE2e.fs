module Wanxiangshu.E2e.OpencodePluginContextBudgetE2e

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.OpencodePluginContextBudgetTests

[<Import("start", "./harness.js")>]
let private startHarness: obj -> JS.Promise<obj> = jsNative

let private createEmpty () = createObj []

let runAll (_args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let mutable failCount = 0

        let chk label cond =
            check label cond

            if not cond then
                failCount <- failCount + 1

        let! _ = OpencodePluginContextBudgetTests.run (unbox null) chk startHarness createEmpty

        let failed = summary ()
        return failed
    }
