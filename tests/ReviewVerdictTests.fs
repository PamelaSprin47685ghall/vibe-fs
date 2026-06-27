module Wanxiangshu.Tests.ReviewVerdictTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ReviewVerdict

let parseVerdictPass () =
    equal "PASS upper" (Some Pass) (parseVerdict "PASS")

let parseVerdictPassLower () =
    equal "pass lower" (Some Pass) (parseVerdict "pass")

let parseVerdictRejectPadded () =
    equal "reject padded" (Some Reject) (parseVerdict "  reject  ")

let parseVerdictNull () =
    equal "null -> None" None (parseVerdict null)

let parseVerdictUnknown () =
    equal "unknown -> None" None (parseVerdict "unknown")

let decideReject () =
    match decideReviewSubmission Reject "" false with
    | Finalize(Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Rejected fb) -> equal "reject feedback" "" fb
    | _ -> check "reject -> Finalize Rejected" false

let decidePassDoubleCheckDone () =
    match decideReviewSubmission Pass "ok" true with
    | Finalize(Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Accepted fb) -> equal "accepted feedback" "ok" fb
    | _ -> check "pass+done -> Finalize Accepted" false

let decidePassNotDoubleCheckDone () =
    match decideReviewSubmission Pass "" false with
    | AskDoubleCheck -> check "pass+not done -> AskDoubleCheck" true
    | _ -> check "pass+not done -> AskDoubleCheck" false

let formatPassEmpty () =
    equal "PASS empty" "PASS" (formatReviewVerdictMarkdown Pass "")

let formatPassWithFeedback () =
    equal "PASS feedback" "PASS: good" (formatReviewVerdictMarkdown Pass "good")

let formatRejectEmpty () =
    equal "REJECT empty" "REJECT: No feedback provided." (formatReviewVerdictMarkdown Reject "")

let formatRejectWithFeedback () =
    equal "REJECT feedback" "REJECT: bad" (formatReviewVerdictMarkdown Reject "bad")

let parseReportPass () =
    match parseReviewReportMarkdown "PASS" with
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Accepted fb -> equal "PASS empty fb" "" fb
    | _ -> check "PASS -> Accepted" false

let parseReportPassFeedback () =
    match parseReviewReportMarkdown "PASS: ok" with
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Accepted fb -> equal "PASS ok fb" "ok" fb
    | _ -> check "PASS: ok -> Accepted ok" false

let parseReportReject () =
    match parseReviewReportMarkdown "REJECT: bad" with
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Rejected fb -> equal "REJECT bad fb" "bad" fb
    | _ -> check "REJECT: bad -> Rejected bad" false

let parseReportRejectEmpty () =
    match parseReviewReportMarkdown "REJECT" with
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Rejected fb -> equal "REJECT empty fb" "" fb
    | _ -> check "REJECT -> Rejected empty" false

let parseReportUnknown () =
    match parseReviewReportMarkdown "unknown" with
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Terminated -> check "unknown -> Terminated" true
    | _ -> check "unknown -> Terminated" false

let parseReportNull () =
    match parseReviewReportMarkdown null with
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Terminated -> check "null -> Terminated" true
    | _ -> check "null -> Terminated" false

let run () : unit =
    parseVerdictPass ()
    parseVerdictPassLower ()
    parseVerdictRejectPadded ()
    parseVerdictNull ()
    parseVerdictUnknown ()
    decideReject ()
    decidePassDoubleCheckDone ()
    decidePassNotDoubleCheckDone ()
    formatPassEmpty ()
    formatPassWithFeedback ()
    formatRejectEmpty ()
    formatRejectWithFeedback ()
    parseReportPass ()
    parseReportPassFeedback ()
    parseReportReject ()
    parseReportRejectEmpty ()
    parseReportUnknown ()
    parseReportNull ()
