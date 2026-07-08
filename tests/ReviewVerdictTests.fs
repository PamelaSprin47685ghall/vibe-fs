module Wanxiangshu.Tests.ReviewVerdictTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ReviewVerdict

let parseVerdictPerfect () =
    equal "PERFECT upper" (Some Perfect) (parseVerdict "PERFECT")

let parseVerdictPerfectLower () =
    equal "perfect lower" (Some Perfect) (parseVerdict "perfect")

let parseVerdictRevisePadded () =
    equal "revise padded" (Some Revise) (parseVerdict "  revise  ")

let parseVerdictNull () =
    equal "null -> None" None (parseVerdict null)

let parseVerdictUnknown () =
    equal "unknown -> None" None (parseVerdict "unknown")

let parseVerdictUnknownToken () =
    equal "unknown token -> None" None (parseVerdict "FOO")

let decideRevise () =
    match decideReviewSubmission Revise "" false with
    | Finalize(Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.NeedsRevision fb) -> equal "revise feedback" "" fb
    | _ -> check "revise -> Finalize NeedsRevision" false

let decidePerfectDoubleCheckDone () =
    match decideReviewSubmission Perfect "ok" true with
    | Finalize(Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Accepted fb) -> equal "accepted feedback" "ok" fb
    | _ -> check "perfect+done -> Finalize Accepted" false

let decidePerfectNotDoubleCheckDone () =
    match decideReviewSubmission Perfect "" false with
    | AskDoubleCheck -> check "perfect+not done -> AskDoubleCheck" true
    | _ -> check "perfect+not done -> AskDoubleCheck" false

let formatPerfectEmpty () =
    equal "PERFECT empty" "PERFECT" (formatReviewVerdictMarkdown Perfect "")

let formatPerfectWithFeedback () =
    equal "PERFECT feedback" "PERFECT: good" (formatReviewVerdictMarkdown Perfect "good")

let formatReviseEmpty () =
    equal "REVISE empty" "REVISE: No feedback provided." (formatReviewVerdictMarkdown Revise "")

let formatReviseWithFeedback () =
    equal "REVISE feedback" "REVISE: bad" (formatReviewVerdictMarkdown Revise "bad")

let parseReportPerfect () =
    match parseReviewReportMarkdown "PERFECT" with
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Accepted fb -> equal "PERFECT empty fb" "" fb
    | _ -> check "PERFECT -> Accepted" false

let parseReportPerfectFeedback () =
    match parseReviewReportMarkdown "PERFECT: ok" with
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Accepted fb -> equal "PERFECT ok fb" "ok" fb
    | _ -> check "PERFECT: ok -> Accepted ok" false

let parseReportRevise () =
    match parseReviewReportMarkdown "REVISE: bad" with
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.NeedsRevision fb -> equal "REVISE bad fb" "bad" fb
    | _ -> check "REVISE: bad -> NeedsRevision bad" false

let parseReportUnrecognizedMarkdownTerminated () =
    match parseReviewReportMarkdown "MAYBE: bad" with
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Terminated -> check "unrecognized markdown -> Terminated" true
    | _ -> check "unrecognized markdown -> Terminated" false

let parseReportUnknown () =
    match parseReviewReportMarkdown "unknown" with
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Terminated -> check "unknown -> Terminated" true
    | _ -> check "unknown -> Terminated" false

let parseReportNull () =
    match parseReviewReportMarkdown null with
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Terminated -> check "null -> Terminated" true
    | _ -> check "null -> Terminated" false

let run () : unit =
    parseVerdictPerfect ()
    parseVerdictPerfectLower ()
    parseVerdictRevisePadded ()
    parseVerdictUnknownToken ()
    parseVerdictNull ()
    parseVerdictUnknown ()
    decideRevise ()
    decidePerfectDoubleCheckDone ()
    decidePerfectNotDoubleCheckDone ()
    formatPerfectEmpty ()
    formatPerfectWithFeedback ()
    formatReviseEmpty ()
    formatReviseWithFeedback ()
    parseReportPerfect ()
    parseReportPerfectFeedback ()
    parseReportRevise ()
    parseReportUnrecognizedMarkdownTerminated ()
    parseReportUnknown ()
    parseReportNull ()
