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

let runToolScenario (label: string) (toolName: string) (toolArgs: obj) (promptText: string) : JS.Promise<unit> =
  promise {
    try
            let! apiObj = start null
            let harness = harnessFromObj apiObj
            harness.mockLLM.expectTool toolName toolArgs

            let! createRes = harness.createSession (createObj ["model", createObj ["id", box "test-model"; "providerID", box "test"]]) emptyObj
            let createData = unbox<obj> createRes
            check (label + ".createSession ok") (createData?ok = true)
            let sessionID = string (createData?data?data?id)
            check (label + ".session id") (sessionID.Length > 0)

            harness.mockLLM.reset()

            let! sendRes = harness.sendPrompt sessionID promptText emptyObj
            let sendData = unbox<obj> sendRes
            check (label + ".sendPrompt ok") (sendData?ok = true)
            check (label + ".mock LLM called") (harness.mockLLM.calls.Count >= 1)

            do! harness.dispose()
        with ex ->
            printfn "E2E scenario failed: %s" (string ex.Message)
            printfn "%s" (string ex.StackTrace)
            check (label + ". threw") false
    }

let readScenario = runToolScenario "e2e.read-tool-roundtrip" "read" (box {| file_path = "README.md" |}) "read the README"

let writeScenario = runToolScenario "e2e.write-tool-roundtrip" "write" (box {| file_path = "test.txt"; content = "hello" |}) "write hello to test.txt"

let executorScenario = runToolScenario "e2e.executor-tool-roundtrip" "executor" (box {| language = "shell"; program = "echo hi" |}) "run shell: echo hi"

let fuzzyFindScenario = runToolScenario "e2e.fuzzy-find-roundtrip" "fuzzy_find" (box {| pattern = "README" |}) "find README files"

let fuzzyGrepScenario = runToolScenario "e2e.fuzzy-grep-roundtrip" "fuzzy_grep" (box {| pattern = "test" |}) "grep for test"

let investigatorScenario = runToolScenario "e2e.investigator-roundtrip" "investigator" (box {| objective = "find README"; background = "looking for readme"; questions = ResizeArray(["where is README?"]) |}) "investigate README location"

let coderScenario = runToolScenario "e2e.coder-roundtrip" "coder" (box {| intents = ResizeArray([]); tdd = "green" |}) "run coder"

let meditatorScenario = runToolScenario "e2e.meditator-roundtrip" "meditator" (box {| intent = "analyze"; files = ResizeArray([]) |}) "run meditator"

let browserScenario = runToolScenario "e2e.browser-roundtrip" "browser" (box {| intent = "open page" |}) "open browser"

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let selectors = args |> Array.filter (fun a -> a <> "--verbose" && a <> "-v")
        let scenarios = [| readScenario; writeScenario; executorScenario; fuzzyFindScenario; fuzzyGrepScenario; investigatorScenario; coderScenario; meditatorScenario; browserScenario |]
        let labels = [| "e2e.read-tool-roundtrip"; "e2e.write-tool-roundtrip"; "e2e.executor-tool-roundtrip"; "e2e.fuzzy-find-roundtrip"; "e2e.fuzzy-grep-roundtrip"; "e2e.investigator-roundtrip"; "e2e.coder-roundtrip"; "e2e.meditator-roundtrip"; "e2e.browser-roundtrip" |]
        for i in 0 .. scenarios.Length - 1 do
            let selected =
                selectors.Length = 0
                || selectors
                   |> Array.exists (fun s ->
                       let t = s.Trim()
                       t.Length > 0 && labels.[i].StartsWith t)
            if selected then
                do! timedAsyncSuite labels.[i] (fun () -> scenarios.[i])
        return summary ()
    }
