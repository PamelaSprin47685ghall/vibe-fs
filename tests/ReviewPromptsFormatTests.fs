module Wanxiangshu.Tests.ReviewPromptsFormatTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ReviewPrompts.Format
open Wanxiangshu.Kernel.ReviewSession.Types

// ── submitReviewIsWip ────────────────────────────────────────────────────────

let submitReviewIsWipNoneDefaultsTrue () =
    check "None defaults to true" (submitReviewIsWip None = true)

let submitReviewIsWipSomeTrue () =
    check "Some true returns true" (submitReviewIsWip (Some true))

let submitReviewIsWipSomeFalse () =
    check "Some false returns false" (not (submitReviewIsWip (Some false)))

// ── submitReviewWipAcknowledgment ────────────────────────────────────────────

let submitReviewWipAcknowledgmentNonEmpty () =
    let msg = submitReviewWipAcknowledgment
    check "acknowledgment is non-empty" (msg <> "")
    check "acknowledgment mentions With-Review Mode" (msg.Contains "With-Review Mode")

// ── formatReviewResult ───────────────────────────────────────────────────────

let formatReviewResultAcceptedNoFeedback () =
    let result = Accepted ""
    let text = formatReviewResult result
    check "accepted no feedback contains accepted verdict" (text.Contains "accepted")
    check "accepted no feedback contains Review passed" (text.Contains "Review passed")
    check "accepted no feedback contains With-Review Mode has ended" (text.Contains "With-Review Mode has ended")

let formatReviewResultAcceptedWithFeedback () =
    let feedback = "Good work, minor style nit."
    let result = Accepted feedback
    let text = formatReviewResult result
    check "accepted with feedback contains verdict" (text.Contains "accepted")
    check "accepted with feedback contains feedback text" (text.Contains feedback)
    check "accepted with feedback contains Review passed with feedback" (text.Contains "Review passed with the following feedback")

let formatReviewResultRejected () =
    let feedback = "Fix the boundary checks."
    let result = Rejected feedback
    let text = formatReviewResult result
    check "rejected contains verdict" (text.Contains "rejected")
    check "rejected contains feedback" (text.Contains feedback)
    check "rejected keeps With-Review Mode active" (text.Contains "With-Review Mode is still active")

let formatReviewResultTerminated () =
    let result = Terminated
    let text = formatReviewResult result
    check "terminated contains verdict" (text.Contains "terminated")
    check "terminated no verdict label in prose" (text.Contains "Review terminated without verdict")
    check "terminated keeps With-Review Mode active" (text.Contains "With-Review Mode is still active")

let run () =
    submitReviewIsWipNoneDefaultsTrue ()
    submitReviewIsWipSomeTrue ()
    submitReviewIsWipSomeFalse ()
    submitReviewWipAcknowledgmentNonEmpty ()
    formatReviewResultAcceptedNoFeedback ()
    formatReviewResultAcceptedWithFeedback ()
    formatReviewResultRejected ()
    formatReviewResultTerminated ()
