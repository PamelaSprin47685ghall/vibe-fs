module Wanxiangshu.E2e.Tests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert

[<Import("start", "./harness.js")>]
let start: obj -> JS.Promise<obj> = jsNative

type MockLLM =
    abstract expectTool: string -> obj -> unit
    abstract reset: unit -> unit
    abstract calls: ResizeArray<obj>

type Harness =
    abstract mockLLM: MockLLM
    abstract createSession: obj -> obj -> JS.Promise<obj>
    abstract sendPrompt: string -> string -> obj -> JS.Promise<obj>
    abstract getMessages: string -> obj -> JS.Promise<obj>
    abstract dispose: unit -> JS.Promise<unit>

let private harnessFromObj (o: obj) : Harness = unbox o
let private emptyObj = createObj []

let runScenario () : JS.Promise<unit> =
  promise {
    try
            let! apiObj = start null
            let harness = harnessFromObj apiObj
            let args = box {| file_path = "README.md" |}
            harness.mockLLM.expectTool "read" args

            let! createRes = harness.createSession (createObj ["model", createObj ["id", box "test-model"; "providerID", box "test"]]) emptyObj
            let createData = unbox<obj> createRes
            check "createSession ok" (createData?ok = true)
            let sessionID = string (createData?data?data?id)
            check "session id" (sessionID.Length > 0)

            harness.mockLLM.reset()

            let! sendRes = harness.sendPrompt sessionID "read the README" emptyObj
            let sendData = unbox<obj> sendRes
            check "sendPrompt ok" (sendData?ok = true)
            check "mock LLM called" (harness.mockLLM.calls.Count >= 1)

            do! harness.dispose()
        with ex ->
            printfn "E2E scenario failed: %s" (string ex.Message)
            printfn "%s" (string ex.StackTrace)
            check "runScenario threw" false
    }

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let selectors = args |> Array.filter (fun a -> a <> "--verbose" && a <> "-v")
        let label = "e2e.read-tool-roundtrip"
        let selected =
            selectors.Length = 0
            || selectors
               |> Array.exists (fun s ->
                   let t = s.Trim()
                   t.Length > 0 && label.StartsWith t)
        if selected then
            do! timedAsyncSuite label runScenario
        return summary ()
    }
