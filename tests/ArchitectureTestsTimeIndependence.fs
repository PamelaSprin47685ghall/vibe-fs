module Wanxiangshu.Tests.ArchitectureTestsTimeIndependence

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

/// Files exempt from the time-independence check:
/// - Assert.fs: test harness infrastructure (performance.now for timing,
///   setTimeout for raceWithTimeout hard ceiling)
/// - ArchitectureTestsFallback.fs: contract test that scans source for setTimeout
/// - E2eHarnessContractTests.fs: contract test that scans harness for setTimeout
let private exemptFiles =
    Set.ofList
        [ "tests/Assert.fs"
          "tests/ArchitectureTestsFallback.fs"
          "tests/ArchitectureTestsFoundation.fs"
          "tests/ArchitectureTestsTimeIndependence.fs"
          "tests/Wanxiangzhen/E2eHarnessContractTests.fs" ]

/// Patterns that indicate real-time dependence in test logic.
/// Each entry is (pattern, human-readable label).
let private timePatterns =
    [ "setTimeout", "setTimeout (timer)"
      "setInterval", "setInterval (timer)"
      "Date.now", "Date.now (wall clock)"
      "new Date(", "new Date (wall clock)"
      "performance.now", "performance.now (high-res timer)"
      "Promise.sleep", "Promise.sleep (timer-based delay)"
      "DateTimeOffset", "DateTimeOffset (wall clock)"
      "UtcNow", "UtcNow (wall clock)" ]

/// All test .fs files must be time-independent: no setTimeout, setInterval,
/// Date.now, performance.now, Promise.sleep, or wall-clock types in non-comment
/// code.  Tests must use event-driven primitives (yieldMicrotask, spinUntil,
/// OnStateChanged callbacks) instead of real-time delays.
///
/// Exempt: Assert.fs (harness), ArchitectureTestsFallback.fs and
/// E2eHarnessContractTests.fs (contract tests that reference patterns in strings).
let testFilesAreTimeIndependent () =
    let files = fsFilesRecursive "tests"

    for file in files do
        if Set.contains file exemptFiles then
            ()
        else
            let code = requireFile file |> nonCommentCode

            for (pattern, label) in timePatterns do
                check ("arch: " + file + " no " + label) (not (code.Contains pattern))
