/// Shared assertion toolkit for the test suite.  A tiny problem should not pay
/// a framework tax — these mutable counters are the only cross-file state.
module Wanxiangshu.Tests.Assert

open Fable.Core

[<Emit("performance.now()")>]
let private now () : float = jsNative

[<Import("appendFileSync", "node:fs")>]
let private appendFile (path: string) (content: string) (encoding: string) : unit = jsNative

let mutable passed = 0
let mutable failed = 0
let private failures = ResizeArray<string>()
let private timings = ResizeArray<string * float>()
let mutable private verboseEnabled = false
let mutable private verboseLogPath: string option = None
let mutable private verboseChecksLogged = 0

let clearFailuresForRun () : unit =
    passed <- 0
    failed <- 0
    failures.Clear()
    timings.Clear()
    verboseChecksLogged <- 0

/// Enable per-check log append to `path`; pass `None` to disable. Tests entry
/// (`tests/Tests.fs:runAll`) calls this after reading the verbose switch
/// (`--verbose` CLI flag or `VIBE_FS_TEST_VERBOSE=1` env var).
let setVerbose (pathOpt: string option) : unit =
    verboseLogPath <- pathOpt
    verboseEnabled <- Option.isSome pathOpt

let verboseCheckCount () : int = verboseChecksLogged

let private logCheck (kind: string) (label: string) (passedNow: bool) (detail: string option) =
    match verboseLogPath with
    | Some path when verboseEnabled ->
        let status = if passedNow then "OK" else "FAIL"
        let suffix = detail |> Option.map (sprintf " | %s") |> Option.defaultValue ""
        let line = sprintf "[%s] %s: %s%s\n" status kind label suffix
        appendFile path line "utf8"
        verboseChecksLogged <- verboseChecksLogged + 1
    | _ -> ()

let check (label: string) (condition: bool) : unit =
    logCheck "check" label condition (if condition then None else Some "condition=false")

    if condition then
        passed <- passed + 1
    else
        failed <- failed + 1
        failures.Add label

let equal (label: string) (expected: 'a) (actual: 'a) : unit =
    let ok = actual = expected

    let detail =
        if ok then
            None
        else
            Some(sprintf "expected %A, got %A" expected actual)

    logCheck "equal" label ok detail

    if ok then
        passed <- passed + 1
    else
        failed <- failed + 1
        failures.Add(sprintf "%s | expected %A, got %A" label expected actual)

[<Emit("Promise.race([$0, new Promise((_, reject) => setTimeout(() => reject(new Error($1)), $2))])")>]
let private raceWithTimeout (p: JS.Promise<'a>) (msg: string) (ms: int) : JS.Promise<'a> = jsNative

/// Time a synchronous test body; catches exceptions so one throwing test does not abort the suite.
let timed (label: string) (f: unit -> unit) : unit =
    let start = now ()

    try
        f ()
        timings.Add(label, now () - start)
    with ex ->
        printfn "TEST %s THREW: %A" label ex
        failed <- failed + 1
        failures.Add(label + " [THREW]")
        timings.Add(label, now () - start)

/// Per-spec async ceiling (integration sub-specs use this inside their own loops).
let asyncSpecTimeoutMs = 5000

/// Suite-level async ceiling: one `*.run` that sequences many sub-specs must not
/// inherit the 1s per-spec budget (e.g. other Integration*.run suites).
let asyncSuiteTimeoutMs = 120_000

/// Time an asynchronous test body with a 1s hard timeout; return a unit promise.
/// Promise failure / timeout / throw are all converged into a single failure record.
let timedAsync (label: string) (f: unit -> JS.Promise<'a>) : JS.Promise<unit> =
    promise {
        let start = now ()

        try
            let! _ = raceWithTimeout (f ()) "TIMEOUT" asyncSpecTimeoutMs
            timings.Add(label, now () - start)
        with ex ->
            let msg = string ex.Message

            if msg.Contains "TIMEOUT" then
                failed <- failed + 1
                failures.Add(label + $" [TIMEOUT>{asyncSpecTimeoutMs}ms]")
            else
                printfn "TEST ASYNC %s THREW: %A" label ex
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
            let msg = string ex.Message

            if msg.Contains "TIMEOUT" then
                failed <- failed + 1
                failures.Add(label + $" [TIMEOUT>{asyncSuiteTimeoutMs}ms]")
            else
                printfn "TEST SUITE %s THREW: %A" label ex
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

    match verboseLogPath with
    | Some path when verboseEnabled ->
        let footer =
            sprintf "# summary: %d passed, %d failed, %d checks logged\n" passed failed verboseChecksLogged

        appendFile path footer "utf8"
        printfn "\nverbose log: %s" path
    | _ -> ()

    failed
