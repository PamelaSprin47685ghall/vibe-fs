module Wanxiangshu.Tests.ReviewPromptsFormatTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.ReviewPrompts.Format
open Wanxiangshu.Kernel.ReviewSession.Types
open Fable.Core.JsInterop

// ── submitReviewIsWip ────────────────────────────────────────────────────────

let submitReviewIsWipNoneDefaultsTrue () =
    check "None defaults to true" (submitReviewIsWip None = true)

let submitReviewIsWipSomeTrue () =
    check "Some true returns true" (submitReviewIsWip (Some true))

let submitReviewIsWipSomeFalse () =
    check "Some false returns false" (not (submitReviewIsWip (Some false)))

// ── formatWipAcknowledgment structured SSOT ──────────────────────────────────

let submitReviewWipAcknowledgmentNonEmpty () =
    let msg = formatWipAcknowledgment "Progress recorded"
    check "acknowledgment is non-empty" (msg <> "")
    check "acknowledgment carries review_mode or progress" (msg.Contains "review_mode" || msg.Contains "review_progress")

// ── formatReviewResult ───────────────────────────────────────────────────────

let formatReviewResultAcceptedNoFeedback () =
    let result = Accepted []
    let text = formatReviewResult result
    check "accepted omits PERFECT" (not (text.Contains "PERFECT"))
    check "accepted omits Review passed" (not (text.Contains "Review passed"))
    check "accepted requires follow-through" (text.Contains "follow_through" || text.Contains "recommendations")
    check
        "accepted ends with-review mode"
        (text.Contains "review_mode" && (text.Contains "ended" || text.Contains "follow_through"))

let formatReviewResultAcceptedWithFeedback () =
    let feedback = "Good work, minor style nit."
    let result = Accepted [ feedback ]
    let text = formatReviewResult result
    check "accepted with feedback omits PERFECT" (not (text.Contains "PERFECT"))
    check "accepted with feedback embeds recommendations" (text.Contains feedback)
    check "accepted with feedback requires follow-through" (text.Contains "recommendations" || text.Contains "follow_through")
    check
        "accepted recommendations as outcome not background dump"
        (text.Contains "recommendation_" || text.Contains feedback)

let formatReviewResultNeedsRevision () =
    let feedback = "Fix the boundary checks."
    let result = NeedsRevision [ feedback ]
    let text = formatReviewResult result
    check "needs_revision contains verdict" (text.Contains "needs_revision")
    check "needs_revision contains feedback" (text.Contains feedback)
    check
        "needs_revision keeps With-Review Mode active"
        (text.Contains "review_mode" && text.Contains "active")
    check "needs_revision feedback is structured outcome" (text.Contains "feedback_" || text.Contains "feedback")

let formatReviewResultTerminated () =
    let result = Terminated
    let text = formatReviewResult result
    check "terminated contains verdict" (text.Contains "terminated")
    check
        "terminated keeps With-Review Mode active"
        (text.Contains "review_mode" && text.Contains "active")
    check "terminated has recovery rule" (text.Contains "Verify" || text.Contains "submit again" || text.Contains "rules")

// ── formatWipAcknowledgment ──────────────────────────────────────────────────

let formatWipAcknowledgmentProducesFrontMatter () =
    let task = "Implement the new feature: format wip acknowledgment."
    let text = formatWipAcknowledgment task
    check "output is non-empty" (text <> "")
    check "output contains objective" (text.Contains "objective =")
    check "output carries review_mode evidence" (text.Contains "review_mode" && text.Contains "active")
    check "output continue policy as rule" (text.Contains "Continue working" || text.Contains "continue")
    check "output task field contains input" (text.Contains task)
    check "output review_progress field recorded" (text.Contains "recorded")
    check "wip no markdown divider" (not (text.Contains "=== "))
    check "wip background not status dump" (not (text.Contains "Progress was saved"))

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
