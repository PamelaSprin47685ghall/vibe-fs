module Wanxiangshu.Kernel.ReviewVerdict

open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types

/// The reviewer's intent, decoded once at the LLM boundary. An explicit enum
/// replaces the old nullable-feedback channel: a string "null" can no longer
/// masquerade as "accept", because the verdict no longer rides inside an
/// optional feedback field. Feedback is carried separately and is only
/// meaningful for `Revise`.
type Verdict =
    | Perfect
    | Revise

/// Strict decoder for the `verdict` enum field. Anything that is not exactly
/// PERFECT/REVISE (case-insensitive, trimmed) is `None` — the caller surfaces that
/// as a re-prompt, never silently coercing an unknown token into a verdict.
let parseVerdict (raw: string) : Verdict option =
    match (if isNull raw then "" else raw).Trim().ToUpperInvariant() with
    | "PERFECT" -> Some Perfect
    | "REVISE" -> Some Revise
    | _ -> None

/// The shared, pure review-submission policy. `doubleCheckDone` is the only
/// host-variable fact (OpenCode scans a history anchor; mux counts parent-side
/// rounds), so both hosts feed it in and share this one decision.
type ReviewDecision =
    | Finalize of ReviewResult
    | AskDoubleCheck

let decideReviewSubmission (verdict: Verdict) (feedback: string) (doubleCheckDone: bool) : ReviewDecision =
    match verdict with
    | Revise -> Finalize(ReviewResult.NeedsRevision feedback)
    | Perfect when doubleCheckDone -> Finalize(ReviewResult.Accepted feedback)
    | Perfect -> AskDoubleCheck

// ── mux reportMarkdown text codec ────────────────────────────────────────────
// mux carries the verdict back to the parent `submit_review` through the
// reviewer task's `reportMarkdown` (the single, native channel). These two
// functions are the encode/decode pair across that text boundary. They are not
// a strict bijection: an empty Revise feedback is normalized to a placeholder,
// so round-trip preserves the verdict dimension and non-empty feedback only.

let private reviseNoFeedback = "No feedback provided."

let formatReviewVerdictMarkdown (verdict: Verdict) (feedback: string) : string =
    let trimmedFeedback (f: string) = (if isNull f then "" else f).Trim()
    match verdict with
    | Perfect ->
        let f = trimmedFeedback feedback
        if f = "" then "PERFECT" else "PERFECT: " + f
    | Revise ->
        let f = trimmedFeedback feedback
        if f = "" then "REVISE: " + reviseNoFeedback else "REVISE: " + f

let parseReviewReportMarkdown (markdown: string) : ReviewResult =
    let trimmed = (if isNull markdown then "" else markdown).Trim()
    let upper = trimmed.ToUpperInvariant()
    let extractAfterColon () =
        match trimmed.IndexOf(':') with
        | i when i >= 0 -> trimmed.Substring(i + 1).Trim()
        | _ -> ""
    if upper = "PERFECT" then ReviewResult.Accepted ""
    elif upper.StartsWith "PERFECT" then ReviewResult.Accepted(extractAfterColon ())
    elif upper.StartsWith "REVISE" then ReviewResult.NeedsRevision(extractAfterColon ())
    else ReviewResult.Terminated