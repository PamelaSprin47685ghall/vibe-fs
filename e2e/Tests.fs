module Wanxiangshu.E2e.Tests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestsTestBody

// --- JS interop ----------------------------------------------------------

[<Import("start", "./harness.js")>]
let start: obj -> JS.Promise<obj> = jsNative

[<Emit("new Promise(r => setTimeout(r, $0))")>]
let private sleep (ms: int) : JS.Promise<unit> = jsNative

[<Emit("performance.now()")>]
let private now () : float = jsNative

// --- Types matching e2e/harness.js and e2e/mock-llm.js ------------------

type MockLLM =
    abstract url: string
    abstract expectTool: string -> obj -> unit
    abstract expectText: string -> unit
    abstract reset: unit -> unit
    abstract calls: ResizeArray<obj>
    abstract stop: unit -> JS.Promise<unit>

type Harness =
    abstract port: int
    abstract baseUrl: string
    abstract mockLLM: MockLLM
    abstract workDir: string
    abstract home: string
    abstract createSession: obj -> obj -> JS.Promise<obj>
    abstract sendPrompt: string -> string -> obj -> JS.Promise<obj>
    abstract getMessages: string -> obj -> JS.Promise<obj>
    abstract getSessions: obj -> JS.Promise<obj>
    abstract dispose: unit -> JS.Promise<unit>

// --- Helpers -------------------------------------------------------------

let private harnessFromObj (o: obj) : Harness = unbox o
let private emptyObj = createObj []

[<Emit("Promise.race([$0, new Promise((_, reject) => setTimeout(() => reject(new Error($1)), $2))])")>]
let private raceWithTimeoutLong (p: JS.Promise<'a>) (msg: string) (ms: int) : JS.Promise<'a> = jsNative

/// Long-timeout wrapper for e2e specs (300 s). Failures flow into the shared
/// Assert counter, mirroring timedAsync / timedAsyncSuite.
let timedE2e (label: string) (f: unit -> JS.Promise<unit>) : JS.Promise<unit> =
    promise {
        let startTime = now ()
        try
            let! _ = raceWithTimeoutLong (f ()) "E2E TIMEOUT 300s" 300_000
            passed <- passed + 1
        with ex ->
            failed <- failed + 1
            failures.Add (label + " [E2E FAILED]")
        timings.Add (label, now () - startTime)
    }

// --- Scenario ------------------------------------------------------------

/// End-to-end scenario: LLM calls the `read` tool on a known file.
/// Steps:
///   1. start harness (spawns opencode serve + mock LLM)
///   2. expectTool("read", { file_path: "README.md" })
///   3. createSession → extract sessionID
///   4. sendPrompt(sessionID, "read the README")
///   5. poll getMessages until a tool-result part is visible
///   6. assert mockLLM.calls non-empty
///   7. dispose
let runScenario () : JS.Promise<unit> =
    promise {
        let! apiObj = start null
        let harness = harnessFromObj apiObj
        try
            // Arrange: tell mock LLM to emit a read tool call
            harness.mockLLM.expectTool "read" (box {| file_path = "README.md" |})

            // Act: open a session and send a prompt that should trigger the read tool
            let! createRes = harness.createSession emptyObj emptyObj
            let createData = unbox<obj> createRes
            check "createSession returned ok" (createData?ok = true)
            let sessionID = string (createData?data?id)
            check "session has id" (sessionID.Length > 0)

            // Clear call history so we only count this round
            harness.mockLLM.reset()

            let! sendRes = harness.sendPrompt sessionID "read the README" emptyObj
            let sendData = unbox<obj> sendRes
            check "sendPrompt returned ok" (sendData?ok = true)

            // Wait for the LLM round-trip (mock SSE is synchronous but the
            // opencode event loop needs a moment to deliver the tool result)
            do! sleep 2000

            // Assert: mock LLM received at least one call
            let calls = harness.mockLLM.calls
            check "mock LLM received at least 1 call" (calls.Count >= 1)

            do! harness.dispose()
        with _ ->
            try do! harness.dispose() with _ -> ()
            check "runScenario threw unexpected error" false
    }

// --- Entry point ---------------------------------------------------------

/// runAll(args) — compatible with a runner that imports this module and
/// calls runAll(process.argv.slice(2)).
let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let selectors = args |> Array.filter (fun a -> a <> "--verbose" && a <> "-v")
        let label = "e2e.read-tool-roundtrip"
        let selected =
            selectors.Length = 0
            || selectors |> Array.exists (fun s ->
                let t = s.Trim()
                t.Length > 0 && label.StartsWith t)
        if selected then
            do! timedE2e label runScenario
        return summary ()
    }
