module VibeFs.Kernel.ReviewVerdict

open VibeFs.Kernel.ReviewSession

/// The reviewer's intent, decoded once at the LLM boundary. An explicit enum
/// replaces the old nullable-feedback channel: a string "null" can no longer
/// masquerade as "accept", because the verdict no longer rides inside an
/// optional feedback field. Feedback is carried separately and is only
/// meaningful for `Reject`.
type Verdict =
    | Pass
    | Reject

/// Strict decoder for the `verdict` enum field. Anything that is not exactly
/// PASS/REJECT (case-insensitive, trimmed) is `None` — the caller surfaces that
/// as a re-prompt, never silently coercing an unknown token into a verdict.
let parseVerdict (raw: string) : Verdict option =
    match (if isNull raw then "" else raw).Trim().ToUpperInvariant() with
    | "PASS" -> Some Pass
    | "REJECT" -> Some Reject
    | _ -> None

/// The shared, pure review-submission policy. `doubleCheckDone` is the only
/// host-variable fact (OpenCode scans a history anchor; mux counts parent-side
/// rounds), so both hosts feed it in and share this one decision.
type ReviewDecision =
    | Finalize of ReviewResult
    | AskDoubleCheck

let decideReviewSubmission (verdict: Verdict) (feedback: string) (doubleCheckDone: bool) : ReviewDecision =
    match verdict with
    | Reject -> Finalize(Rejected feedback)
    | Pass when doubleCheckDone -> Finalize Accepted
    | Pass -> AskDoubleCheck

// ── mux reportMarkdown text codec ────────────────────────────────────────────
// mux carries the verdict back to the parent `submit_review` through the
// reviewer task's `reportMarkdown` (the single, native channel). These two
// functions are the encode/decode pair across that text boundary. They are not
// a strict bijection: an empty Reject feedback is normalized to a placeholder,
// so round-trip preserves the verdict dimension and non-empty feedback only.

let private rejectNoFeedback = "No feedback provided."

let formatReviewVerdictMarkdown (verdict: Verdict) (feedback: string) : string =
    match verdict with
    | Pass -> "PASS"
    | Reject ->
        let trimmed = (if isNull feedback then "" else feedback).Trim()
        if trimmed = "" then "REJECT: " + rejectNoFeedback else "REJECT: " + trimmed

let parseReviewReportMarkdown (markdown: string) : ReviewResult =
    let trimmed = (if isNull markdown then "" else markdown).Trim()
    if trimmed.ToUpperInvariant() = "PASS" then Accepted
    elif trimmed.ToUpperInvariant().StartsWith "REJECT" then
        let afterColon =
            match trimmed.IndexOf(':') with
            | i when i >= 0 -> trimmed.Substring(i + 1).Trim()
            | _ -> ""
        Rejected afterColon
    else Terminated
