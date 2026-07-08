module Wanxiangshu.Tests.DedupRegressionTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Dedup

/// Regression: DedupState caps rawOutputs at 100 entries.
/// When the 101st unique output is added, the oldest (first inserted) is dropped.
///
/// Fixture invariant: plain strings like "output_N" contain no line-number prefix
/// (e.g. "   1|") or caps layout markers, so `readFingerprint` returns None for them.
/// This guarantees the test exercises the rawOutputs (fallback) cap logic,
/// not the fingerprints (Set) path — making the test stable against future
/// changes to fingerprint heuristics.
let testDedupStateCapRegression () =
    let mutable state = emptyState
    // Insert 101 unique outputs "output_0" .. "output_100"
    for i in 0 .. 100 do
        let result = deduplicate state (sprintf "output_%d" i)
        state <- result.state
    // State should cap at 100: output_0 dropped, output_1..output_100 retained
    equal "dedup cap at 100" 100 (List.length state.rawOutputs)
    // rawOutputs is stored in reverse insertion order (newest first)
    // So head = output_100 (newest), last = output_1 (oldest retained)
    let head = List.head state.rawOutputs
    let last = List.last state.rawOutputs
    equal "dedup cap head = newest" "output_100" head
    equal "dedup cap last = oldest retained" "output_1" last
    // output_0 must have been evicted
    check "dedup cap evicts oldest" (not (List.contains "output_0" state.rawOutputs))

let run () =
    testDedupStateCapRegression ()
