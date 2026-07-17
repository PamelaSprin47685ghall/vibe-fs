module Wanxiangshu.Tests.ReviewReportBufferTests

open Wanxiangshu.Tests.Assert

let private strContains (s: string) (sub: string) = s.Contains sub

let emptyBufferIsEmpty () =
    let b = Wanxiangshu.Kernel.Review.ReviewReportBuffer.empty
    equal "empty combined" "" b.CombinedText
    equal "empty count" 0 b.Count

let appendSingleReport () =
    let b =
        Wanxiangshu.Kernel.Review.ReviewReportBuffer.append
            "phase one"
            Wanxiangshu.Kernel.Review.ReviewReportBuffer.empty

    check "progress report 1" (strContains b.CombinedText "## Progress Report 1")
    check "phase one" (strContains b.CombinedText "phase one")
    equal "count" 1 b.Count

let appendMultipleReports () =
    let b =
        Wanxiangshu.Kernel.Review.ReviewReportBuffer.empty
        |> Wanxiangshu.Kernel.Review.ReviewReportBuffer.append "first"
        |> Wanxiangshu.Kernel.Review.ReviewReportBuffer.append "second"

    check "progress report 2" (strContains b.CombinedText "## Progress Report 2")
    equal "count" 2 b.Count

let appendEmptyIgnored () =
    let b =
        Wanxiangshu.Kernel.Review.ReviewReportBuffer.empty
        |> Wanxiangshu.Kernel.Review.ReviewReportBuffer.append ""
        |> Wanxiangshu.Kernel.Review.ReviewReportBuffer.append "  "
        |> Wanxiangshu.Kernel.Review.ReviewReportBuffer.append "real"

    check "real appended" (strContains b.CombinedText "real")
    equal "count" 1 b.Count

let withFinalReport () =
    let buf =
        Wanxiangshu.Kernel.Review.ReviewReportBuffer.empty
        |> Wanxiangshu.Kernel.Review.ReviewReportBuffer.append "progress"

    let combined =
        Wanxiangshu.Kernel.Review.ReviewReportBuffer.withFinalReport "final" buf

    check "final report" (strContains combined "## Final Report")

let withFinalReportNoWip () =
    let combined =
        Wanxiangshu.Kernel.Review.ReviewReportBuffer.withFinalReport
            "only final"
            Wanxiangshu.Kernel.Review.ReviewReportBuffer.empty

    check "final report" (strContains combined "## Final Report")
    check "no progress" (not (strContains combined "## Progress"))

let run () =
    emptyBufferIsEmpty ()
    appendSingleReport ()
    appendMultipleReports ()
    appendEmptyIgnored ()
    withFinalReport ()
    withFinalReportNoWip ()
