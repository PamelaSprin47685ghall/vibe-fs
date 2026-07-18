module Wanxiangshu.E2e.MimocodePluginSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.MimocodePluginTaskAndArgsTests
open Wanxiangshu.E2e.MimocodePluginSpecSections

let testSpecs (harness: Harness) (ok: int ref) : JS.Promise<unit> =
    promise {
        let chk label cond =
            check label cond

            if cond then
                ok.Value <- ok.Value + 1

        do runMimoPluginIdentity harness chk
        runMimoToolPresence harness chk

        do! runMimoTaskSchema harness chk
        do! runMimoTaskMissingAha harness chk
        do! runMimoTaskShortAha harness chk
        do! runMimoTaskSuccess harness chk
        do! runMimoTaskNoMethodology harness chk
        do! runMimoTaskEmptyTodo harness chk

        do! runMimoCoderSchema harness chk
        do! runMimoExecutor harness chk
        do! runMimoFuzzyFind harness chk
        do! runMimoLoopCommand harness chk
        do! runMimoStreamAbort harness chk
        do! runMimoMessageTransform harness chk
        do! runMimoSystemTransform harness chk
        do! runMimoConfigAndSessionHooks harness chk
        do! runMimoSessionDeletedEvent harness chk

        do! withTimeout (harness.dispose ())
    }
