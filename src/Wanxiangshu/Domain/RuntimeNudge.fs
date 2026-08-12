namespace Wanxiangshu.Domain

/// ARCH-010: runtime instruction-only payload semantic paths (PROMPT-019).
///
/// Prose lives in `resources/provider/runtime/...`. Call sites bind language via
/// `ProviderProse.documentFor` / `instructionLines`. Domain owns path constants only.
///
/// ── one text deliberately NOT here ─────────────────────────────────────────
///
/// `ReviewChallenge.Text` now carries a `# ` prefix before it is sent. The prefix does not break
/// REVIEW-003: `PerfectChallengeIssued` records the digest of the final sent bytes
/// (`ReviewChallenge.Prompt`) and the second run's input seal is searched for those same bytes, so
/// the record, the `verdict` tool result, and the nudge stay identical. A mismatch would still refuse
/// every confirmation while looking like correct fail-closed behaviour — the failure mode
/// `ReviewChallenge`'s own comment warns about.
///
/// ARCH-011: repair identity lives in typed `repairKind`, never in payload bytes.
[<RequireQualifiedAccess>]
module RuntimeNudge =

    /// FALLBACK: continuation after a provider failure inside one Logical Run.
    let ProviderRetry = "runtime/provider-retry"

    /// LOOP-006: continuation after a LOOP kill bridged into the same AABB path.
    let LoopContinue = "runtime/loop-continue"

    /// EXEC-016: join-capable role tried to finish while resources remain unjoined.
    let BackgroundJoin = "runtime/background-join"

    /// REVIEW-003: Reviewer produced prose where a structured verdict was required.
    let ReviewerVerdictRequired = "runtime/reviewer-verdict-required"

    /// Interaction repair after tool work with no final report.
    let MissingClosingReport = "runtime/missing-closing-report"

    /// Interaction repair that continues an in-progress turn.
    let InteractionContinue = "runtime/interaction-continue"
