module Wanxiangshu.Tests.ReviewReportBufferTests

open Wanxiangshu.Tests.Assert

let private R = Wanxiangshu.Kernel.Review.ReviewReportBuffer

let private strContains (s: string) (sub: string) = s.Contains sub

let emptyBufferIsEmpty () =
    equal "empty combined" "" R.empty.CombinedText
    equal "empty count" 0 R.empty.Count

let appendSingleReport () =
    let b = R.append "phase one" R.empty
    check "progress report 1" (strContains b.CombinedText "## Progress Report 1")
    equal "count" 1 b.Count

let appendMultipleReports () =
    let b = R.append "first" R.empty |> R.append "second"
    check "progress report 2" (strContains b.CombinedText "## Progress Report 2")
    equal "count" 2 b.Count

let appendEmptyIgnored () =
    let b = R.empty |> R.append "" |> R.append "  " |> R.append "real"
    check "real appended" (strContains b.CombinedText "real")
    equal "count" 1 b.Count

let withFinalReport () =
    let combined = R.withFinalReport "final" (R.append "progress" R.empty)
    check "final report" (strContains combined "## Final Report")

let withFinalReportNoWip () =
    let combined = R.withFinalReport "only final" R.empty
    check "final report" (strContains combined "## Final Report")
    check "no progress" (not (strContains combined "## Progress"))

let run () =
    emptyBufferIsEmpty ()
    appendSingleReport ()
    appendMultipleReports ()
    appendEmptyIgnored ()
    withFinalReport ()
    withFinalReportNoWip ()
