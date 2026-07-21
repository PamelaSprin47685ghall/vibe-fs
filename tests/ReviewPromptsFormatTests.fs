module Wanxiangshu.Tests.ReviewPromptsFormatTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.ReviewPrompts.Format
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Runtime.PromptHeader
open Fable.Core.JsInterop

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

    check "accepted with feedback contains accepted" (text.Contains "accepted")
    check "accepted with feedback contains feedback text" (text.Contains feedback)

let formatReviewResultNeedsRevision () =
    let feedback = "Fix the boundary checks."
    let result = NeedsRevision feedback
    let text = formatReviewResult result
    check "needs_revision contains verdict" (text.Contains "needs_revision")
    check "needs_revision contains feedback" (text.Contains feedback)
    check "needs_revision keeps With-Review Mode active" (text.Contains "With-Review Mode remains active")

let formatReviewResultTerminated () =
    let result = Terminated
    let text = formatReviewResult result
    check "terminated contains verdict" (text.Contains "terminated")
    check "terminated no verdict label in prose" (text.Contains "ended without a verdict")
    check "terminated keeps With-Review Mode active" (text.Contains "With-Review Mode remains active")

// ── formatWipAcknowledgment ──────────────────────────────────────────────────

let formatWipAcknowledgmentProducesFrontMatter () =
    let task = "Implement the new feature: format wip acknowledgment."
    let text = formatWipAcknowledgment task
    check "output is non-empty" (text <> "")
    check "output contains frontmatter delimiter" (text.Contains "---")
    check "output mentions With-Review Mode" (text.Contains "With-Review Mode")
    check "output mentions Continue working" (text.Contains "Continue working")
    check "output task field contains input" (text.Contains task)
    check "output review_progress field recorded" (text.Contains "recorded")

let run () =
    submitReviewIsWipNoneDefaultsTrue ()
    submitReviewIsWipSomeTrue ()
    submitReviewIsWipSomeFalse ()
    submitReviewWipAcknowledgmentNonEmpty ()
    formatReviewResultAcceptedNoFeedback ()
    formatReviewResultAcceptedWithFeedback ()
    formatReviewResultNeedsRevision ()
    formatReviewResultTerminated ()
    formatWipAcknowledgmentProducesFrontMatter ()
