namespace Wanxiangshu.Next.Domain

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
/// `ReviewChallenge.Text` stays raw. REVIEW-003 records its digest in `PerfectChallengeIssued` and
/// searches the second run's input seal for that same value, so the bytes are a domain fact rather
/// than a rendering. Wrapping it would change the digest and refuse every confirmation while looking
/// like correct fail-closed behaviour — the failure mode `ReviewChallenge`'s own comment warns about.
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

    /// REVIEW-003: the Manager tried to finish without a confirmed double PERFECT.
    let ManagerReviewGuardInstructions =
        [ "Review is required before completion."
          "Fork or nudge a Reviewer until the current Git tree has two distinct PERFECT verdicts." ]

    /// REVIEW-003: the Reviewer produced prose where a structured verdict was required.
    let ReviewerVerdictGuardInstructions =
        [ "Submit a structured verdict with the verdict tool: PERFECT or REVISE."
          "Do not put a verdict in prose." ]

    /// The interaction repair for a turn that ran tools and never reported.
    ///
    /// The field list is one line rather than a Markdown bullet list, matching
    /// `ForkChildPayload.BaseInstructions`. Two reasons: a bullet list inside a comment header renders
    /// as `# - result`, which reads as a comment about a list rather than a list; and the two prompts
    /// ask for the same report, so they should ask for it the same way.
    let MissingFinalReportInstructions =
        [ "Your tool work is complete, but no final task report was produced."
          "Return a concise final report with exactly these fields: result, evidence, files changed, tests run, remaining risks or blockers."
          "Do not call another tool unless necessary." ]

    let providerRetry = SyntheticToml.document ProviderRetryInstructions []
    let managerReviewGuard = SyntheticToml.document ManagerReviewGuardInstructions []

    let reviewerVerdictGuard =
        SyntheticToml.document ReviewerVerdictGuardInstructions []

    let missingFinalReport = SyntheticToml.document MissingFinalReportInstructions []
