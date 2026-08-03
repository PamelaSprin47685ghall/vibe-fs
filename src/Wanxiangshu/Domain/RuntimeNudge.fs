namespace Wanxiangshu.Domain

/// ARCH-010: the runtime's own instruction-only payloads.
///
/// Each of these is a case the clause calls instruction-only — the runtime is telling the model what
/// to do and carries no data alongside it, so 「不要求增加虚假的 data 字段」 applies and the payload is
/// a comment header with no body.
///
/// They live together for the reason `CompanionPrompt` gives: literal text in one place, with no
/// format holes. A `sprintf` here would be how a session id or a token count gets into a prompt that
/// must not carry one.
///
/// ── two texts deliberately NOT here ─────────────────────────────────────────
///
/// `ReviewChallenge.Text` now carries a `# ` prefix before it is sent. The prefix does not break
/// REVIEW-003: `PerfectChallengeIssued` records the digest of the final sent bytes
/// (`ReviewChallenge.Prompt`) and the second run's input seal is searched for those same bytes, so
/// the record, the `verdict` tool result, and the nudge stay identical. A mismatch would still refuse
/// every confirmation while looking like correct fail-closed behaviour — the failure mode
/// `ReviewChallenge`'s own comment warns about.
///
/// The zero-width continuation (`"\u200B"`, `TurnCompletionProgram.fs:215`) stays raw because its
/// emptiness IS its meaning. It is transport rather than semantic delta; a `# ` prefix would make it
/// non-empty and promote a transport nudge into the Companion's semantic history.
///
/// Both exclusions are ARCH-010's own: a payload whose bytes carry a domain contract is not a
/// rendering choice, and the clause governs LLM-facing notation rather than transport markers.
[<RequireQualifiedAccess>]
module RuntimeNudge =

    /// FALLBACK: the continuation issued after a provider failure inside one Logical Run.
    ///
    /// Not a new task and not fallback bookkeeping — the run continues, and the model needs to know
    /// only that it should carry on.
    let ProviderRetryInstructions = [ "Continue after provider failure." ]

    /// LOOP-006: continuation after a LOOP kill was bridged into the same AABB path.
    ///
    /// Same ContinuationKind as provider failure (`ProviderRetryAttempt`). Distinct
    /// instruction so the model knows the stop was mid-stream degeneration, not a
    /// transport/provider error.
    let LoopContinueInstructions =
        [ "Continue from the interruption without repeating already produced content." ]

    let loopContinue = SyntheticToml.document LoopContinueInstructions []

    /// REVIEW-003: the Manager tried to finish without a confirmed double PERFECT.
    let ManagerReviewGuardInstructions =
        [ "Review is required before completion."
          "Fork or nudge a Reviewer until the current Git tree has two distinct PERFECT verdicts." ]

    /// REVIEW-003: the Reviewer produced prose where a structured verdict was required.
    let ReviewerVerdictGuardInstructions =
        [ "Submit a structured verdict with the verdict tool: PERFECT or REVISE."
          "Do not put a verdict in prose." ]

    /// Interaction repair after tool work with no final report.
    ///
    /// Bare `#` only. The model already knows the report shape
    /// (`ForkChildPayload.BaseInstructions`); this is just the poke that the turn is unfinished.
    /// `SyntheticToml.comment ""` renders as `#`.
    // ponytail: one-byte nudge; expand only if bare `#` stops eliciting the report.
    let MissingFinalReportInstructions = [ "" ]

    let providerRetry = SyntheticToml.document ProviderRetryInstructions []
    let managerReviewGuard = SyntheticToml.document ManagerReviewGuardInstructions []

    let reviewerVerdictGuard =
        SyntheticToml.document ReviewerVerdictGuardInstructions []

    let missingFinalReport = SyntheticToml.document MissingFinalReportInstructions []
