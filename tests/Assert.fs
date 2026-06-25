/// Shared assertion toolkit for the test suite.  A tiny problem should not pay
/// a framework tax — these mutable counters are the only cross-file state.
module VibeFs.Tests.Assert

open Fable.Core

[<Emit("performance.now()")>]
let private now () : float = jsNative

let mutable passed = 0
let mutable failed = 0
let private failures = ResizeArray<string>()
let private timings = ResizeArray<string * float>()

let clearFailuresForRun () : unit =
    passed <- 0
    failed <- 0
    failures.Clear ()
    timings.Clear ()

let check (label: string) (condition: bool) : unit =
    if condition then passed <- passed + 1
    else failed <- failed + 1; failures.Add label

let equal (label: string) (expected: 'a) (actual: 'a) : unit =
    check label (actual = expected)

[<Emit("Promise.race([$0, new Promise((_, reject) => setTimeout(() => reject(new Error($1)), $2))])")>]
let private raceWithTimeout (p: JS.Promise<'a>) (msg: string) (ms: int) : JS.Promise<'a> = jsNative

/// Time a synchronous test body; catches exceptions so one throwing test does not abort the suite.
let timed (label: string) (f: unit -> unit) : unit =
    let start = now ()
    try
        f ()
        timings.Add(label, now () - start)
    with ex ->
        printfn "ERROR in %s: %A" label ex
        failed <- failed + 1
        failures.Add(label + " [THREW]")
        timings.Add(label, now () - start)

/// Per-spec async ceiling (integration sub-specs use this inside their own loops).
let asyncSpecTimeoutMs = 1000

/// Suite-level async ceiling: one `*.run` that sequences many sub-specs must not
/// inherit the 1s per-spec budget (e.g. IntegrationToolTests ~110 cases ≈ 1s+ total).
let asyncSuiteTimeoutMs = 120_000

/// Time an asynchronous test body with a 1s hard timeout; return a unit promise.
/// Reject / timeout / throw are all converged into a single failure record.
let timedAsync (label: string) (f: unit -> JS.Promise<'a>) : JS.Promise<unit> =
    promise {
        let start = now ()
        try
            let! _ = raceWithTimeout (f ()) "TIMEOUT" asyncSpecTimeoutMs
            timings.Add(label, now () - start)
        with ex ->
            let msg =
                match ex with
                | :? System.Exception as e -> string e.Message
                | _ -> string ex
            if msg.Contains "TIMEOUT" then
                failed <- failed + 1
                failures.Add(label + $" [TIMEOUT>{asyncSpecTimeoutMs}ms]")
            else
                printfn "ERROR in %s: %A" label ex
                failed <- failed + 1
                failures.Add(label + " [THREW]")
            timings.Add(label, now () - start)
    }

let timedAsyncSuite (label: string) (f: unit -> JS.Promise<'a>) : JS.Promise<unit> =
    promise {
        let start = now ()
        try
            let! _ = raceWithTimeout (f ()) "TIMEOUT" asyncSuiteTimeoutMs
            timings.Add(label, now () - start)
        with ex ->
            let msg =
                match ex with
                | :? System.Exception as e -> string e.Message
                | _ -> string ex
            if msg.Contains "TIMEOUT" then
                failed <- failed + 1
                failures.Add(label + $" [TIMEOUT>{asyncSuiteTimeoutMs}ms]")
            else
                printfn "ERROR in %s: %A" label ex
                failed <- failed + 1
                failures.Add(label + " [THREW]")
            timings.Add(label, now () - start)
    }

/// Print the pass/fail summary and the slowest tests, return the failure count.
let summary () : int =
    printfn "\n==== %d passed, %d failed ====" passed failed
    if failures.Count > 0 then
        printfn "FAILURES:"
        failures |> Seq.iteri (fun i f -> printfn "  %d. %s" (i + 1) f)
    if timings.Count > 0 then
        let total = timings |> Seq.sumBy snd
        printfn "\nTIMINGS (top 25 of %d, total %.0f ms):" timings.Count total
        timings
        |> Seq.sortByDescending snd
        |> Seq.truncate 25
        |> Seq.iteri (fun i (label, ms) -> printfn "  %2d. %7.1f ms  %s" (i + 1) ms label)
    failed
