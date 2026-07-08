module Wanxiangshu.Tests.Wanxiangzhen.E2eBehaviorCoverageTests

open Wanxiangshu.Tests
open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat
open Wanxiangshu.Tests.Wanxiangzhen.MockE2eTests
open Wanxiangshu.Tests.Wanxiangzhen.OpencodePluginE2eTests
open Wanxiangshu.Tests.Wanxiangzhen.ExtendedMockE2eTests

/// Registry test: aggregate entry-list lengths from the three e2e mock suites so
/// the full behavior surface is documented and CI fails if a suite regresses.
let entries () : (string * (unit -> unit)) list = [
    ("e2e_behavior_coverage.registry", fun () ->
        let mockLen   = Wanxiangshu.Tests.Wanxiangzhen.MockE2eTests.entriesAsync () |> List.length
        let openLen   = Wanxiangshu.Tests.Wanxiangzhen.OpencodePluginE2eTests.entriesAsync () |> List.length
        let extLen    = Wanxiangshu.Tests.Wanxiangzhen.ExtendedMockE2eTests.entriesAsync () |> List.length
        chk "coverage.mock_e2e_ge_6"   (mockLen >= 6)
        chk "coverage.opencode_e2e_ge_10" (openLen >= 10)
        chk "coverage.ext_mock_e2e_ge_25" (extLen >= 25)
        if not Assert.silentEnabled then
            printfn "  e2e coverage: mock=%d opencode=%d ext=%d" mockLen openLen extLen
            printfn "  gap doc: see E2eBehaviorGapTests.entries () for behavior coverage registry")
]
