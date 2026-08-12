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
/// ── one text deliberately NOT here ─────────────────────────────────────────
///
/// `ReviewChallenge.Text` now carries a `# ` prefix before it is sent. The prefix does not break
/// REVIEW-003: `PerfectChallengeIssued` records the digest of the final sent bytes
/// (`ReviewChallenge.Prompt`) and the second run's input seal is searched for those same bytes, so
/// the record, the `verdict` tool result, and the nudge stay identical. A mismatch would still refuse
/// every confirmation while looking like correct fail-closed behaviour — the failure mode
/// `ReviewChallenge`'s own comment warns about.
///
/// ARCH-011 (状态先于表示): the repair's identity lives in the typed `repairKind`
/// (FALLBACK-008 claim scope), never in the payload bytes. The old zero-width continuation
/// (`"\u200B"`) encoded "this is a transport poke" in the string itself and let the Companion
/// recover it by stripping U+200B and re-testing emptiness — reverse inference through
/// character features, which ARCH-011 forbids. Its consumer (`CompanionDelta`) is gone, and the
/// bytes were rejected as whitespace-only by Anthropic. `InteractionRepairContinue` renders the
/// same poke as a TOML comment: non-whitespace to every validator, instruction-only per
/// ARCH-010, and identity-free by construction.
[<RequireQualifiedAccess>]
module RuntimeNudge =

    /// FALLBACK: the continuation issued after a provider failure inside one Logical Run.
    ///
    /// Not a new task and not fallback bookkeeping — the run continues. The calibration the
    /// model most needs here: a physical execution failure is not evidence about the task,
    /// so the failed attempt must not quietly reprice the work.
    let ProviderRetryInstructions =
        [ "The previous attempt did not complete."
          ""
          "Continue the same work from the evidence already before you."
          ""
          "Do not treat the failed attempt itself as evidence about the task." ]

    /// LOOP-006: continuation after a LOOP kill was bridged into the same AABB path.
    ///
    /// Same ContinuationKind as provider failure (`ProviderRetryAttempt`). Distinct
    /// instruction so the model knows the stop was mid-stream degeneration, not a
    /// transport/provider error.
    let LoopContinueInstructions =
        [ "Continue from the interruption without repeating already produced content." ]

    let loopContinue = SyntheticToml.document LoopContinueInstructions []

    /// EXEC-016: join-capable role tried to finish while resources remain unjoined.
    ///
    /// The guard names the two verbs by their current contracts (`horizon` / `join`) and
    /// restores the anti-idleness calibration: something still being away is not, by itself,
    /// a reason to stop doing useful independent work.
    let BackgroundJoinGuardInstructions =
        [ "Background work remains away."
          ""
          "Receive the consequences that have become available before you finish."
          ""
          "If useful independent work remains, continue it instead of waiting merely because something else is still away."
          ""
          "Use horizon when orientation would change what you should do next."
          "Use join when receiving an arrived consequence is now useful." ]

    /// REVIEW-003: the Reviewer produced prose where a structured verdict was required.
    let ReviewerVerdictGuardInstructions =
        [ "Your previous response did not submit a verdict."
          "Continue the review and submit PERFECT or REVISE with the verdict tool." ]

    /// Interaction repair after tool work with no final report.
    ///
    /// Short because the semantics are simple, not because brevity is a virtue: the turn is
    /// unfinished until an ordinary closing report exists, and the report is natural prose —
    /// not a shape to fill, and not a reason to invent unrelated work.
    let MissingFinalReportInstructions =
        [ "Your work has not yet left an ordinary closing report."
          ""
          "Finish the current charge in natural prose."
          "Say what materially became true and what, if anything, remains unresolved."
          ""
          "Do not begin unrelated work merely to fill a report shape." ]

    /// Interaction repair that continues an in-progress turn (finish=tool-calls with no
    /// tool part, or a reasoning-only stop). ARCH-011: the typed `repairKind` is the
    /// identity; this text only asks the model to continue. Rendered as a comment per
    /// ARCH-010, and non-whitespace to every validator (the `"\u200B"` predecessor was
    /// rejected as whitespace-only by Anthropic).
    let InteractionRepairContinueInstructions = [ "Continue." ]

    let interactionRepairContinue =
        SyntheticToml.document InteractionRepairContinueInstructions []

    let providerRetry = SyntheticToml.document ProviderRetryInstructions []
    let backgroundJoinGuard = SyntheticToml.document BackgroundJoinGuardInstructions []

    let reviewerVerdictGuard =
        SyntheticToml.document ReviewerVerdictGuardInstructions []

    let missingFinalReport = SyntheticToml.document MissingFinalReportInstructions []
