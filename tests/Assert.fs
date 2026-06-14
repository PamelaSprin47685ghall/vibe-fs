/// Shared assertion toolkit for the test suite.  A tiny problem should not pay
/// a framework tax — these mutable counters are the only cross-file state.
module VibeFs.Tests.Assert

let mutable passed = 0
let mutable failed = 0
let private failures = ResizeArray<string>()

let check (label: string) (condition: bool) : unit =
    if condition then passed <- passed + 1
    else failed <- failed + 1; failures.Add label

let equal (label: string) (expected: 'a) (actual: 'a) : unit =
    check label (actual = expected)

/// Print the pass/fail summary and return the failure count (process exit code).
let summary () : int =
    printfn "\n==== %d passed, %d failed ====" passed failed
    if failures.Count > 0 then
        printfn "FAILURES:"
        failures |> Seq.iteri (fun i f -> printfn "  %d. %s" (i + 1) f)
    failed
