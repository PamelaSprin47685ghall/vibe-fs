module Wanxiangshu.E2e.MimoTuiPluginTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.HarnessTypes

[<Import("start", "./harness.js")>]
let private startHarness: obj -> JS.Promise<obj> = jsNative

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let opts = createObj [ "plugin", box true; "variant", box "mimotui" ]
        let! apiObj = withTimeoutCustom 30000 (startHarness opts)
        let harness = unbox<Harness> apiObj
        let mutable ok = 0

        let chk l c =
            check l c

            if c then
                ok <- ok + 1

        let! createRes =
            withTimeout (
                harness.createSession
                    (createObj [ "model", createObj [ "id", box "test-model"; "providerID", box "test" ] ])
                    (createObj [])
            )

        let createData = unbox<obj> createRes
        chk "tui.session-create.ok" (createData?ok = true)
        let sessionID = string (createData?data?data?id)
        chk "tui.sessionId.valid" (sessionID <> "")

        harness.mockLLM.reset ()
        harness.mockLLM.expectText "ok"
        let! _ = withTimeout (harness.sendPrompt sessionID "hello" (createObj []))
        let! _ = withTimeout (harness.waitForCalls 1 60000)
        chk "tui.warmup.success" (harness.mockLLM.calls.Count > 0)

        do! withTimeoutCustom 4900 (harness.dispose ())
        printfn "\n✓ %d mimotui plugin e2e checks passed" ok
        return summary ()
    }
