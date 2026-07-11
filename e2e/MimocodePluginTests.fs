module Wanxiangshu.E2e.MimocodePluginTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.HarnessTypes

[<Import("start", "./harness.js")>]
let private startHarness: obj -> JS.Promise<obj> = jsNative

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let opts = createObj [ "plugin", box true; "variant", box "mimocode" ]
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
        check "mimo.session-create.ok" (createData?ok = true)
        let sessionID = string (createData?data?data?id)

        let emptyObj = createObj []
        let jsonStringify (o: obj) : string = JS.JSON.stringify (o)

        let bodies (h: Harness) =
            h.mockLLM.calls
            |> Seq.cast<obj>
            |> Seq.map (fun c -> jsonStringify (c?body))
            |> String.concat "\n"

        let containsTool (h: Harness) (toolName: string) =
            let t = bodies h

            t.Contains(sprintf "\"name\":\"%s\"" toolName)
            || t.Contains(sprintf "\"name\": \"%s\"" toolName)

        let toolRoundWithCalls (h: Harness) (sess: string) (tool: string) (args: obj) (prompt: string) (expected: int) =
            promise {
                h.mockLLM.reset ()
                h.mockLLM.expectTool tool args

                for _ in 1 .. (expected - 1) do
                    h.mockLLM.expectText "ok"

                let! _ = withTimeout (h.sendPrompt sess prompt emptyObj)
                let! _ = withTimeout (h.waitForCalls expected 60000)
                return ()
            }

        let toolRound (h: Harness) (sess: string) (tool: string) (args: obj) (prompt: string) =
            toolRoundWithCalls h sess tool args prompt 1

        let textRound (h: Harness) (sess: string) (prompt: string) =
            promise {
                h.mockLLM.reset ()
                h.mockLLM.expectText "ok"
                let! _ = withTimeout (h.sendPrompt sess prompt emptyObj)
                let! _ = withTimeout (h.waitForCalls 1 60000)
                return ()
            }

        do!
            Wanxiangshu.E2e.TestsServePlugin.runServePluginChecks
                harness
                sessionID
                chk
                toolRound
                toolRoundWithCalls
                textRound
                containsTool
                bodies
                emptyObj

        do! withTimeoutCustom 4900 (harness.dispose ())
        printfn "\n✓ %d mimocode plugin e2e checks passed" ok
        return summary ()
    }
