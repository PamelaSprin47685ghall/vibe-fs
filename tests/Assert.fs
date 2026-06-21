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

let check (label: string) (condition: bool) : unit =
    if condition then passed <- passed + 1
    else failed <- failed + 1; failures.Add label

let equal (label: string) (expected: 'a) (actual: 'a) : unit =
    check label (actual = expected)

/// Time a synchronous test body.
let timed (label: string) (f: unit -> 'a) : 'a =
    let start = now ()
    let r = f ()
    timings.Add(label, now () - start)
    r

/// Time an asynchronous test body; return a unit promise.
let timedAsync (label: string) (f: unit -> JS.Promise<'a>) : JS.Promise<unit> =
    promise {
        let start = now ()
        let! _ = f ()
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
