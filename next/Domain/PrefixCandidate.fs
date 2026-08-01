namespace Wanxiangshu.Next.Domain

open Wanxiangshu.Next.Kernel.Identity

/// COMPANION-009: the frozen companion memory that replaces X's raw prefix, and
/// the proof it may.
///
/// `CutoffExclusive` is an index into X's provider-visible messages, so it is only
/// meaningful under the numbering that produced `CoveredPrefixDigest`. Both travel
/// together for that reason — a snapshot carrying one without the other could not
/// be re-verified before use (COMPANION-011).
///
/// Lives in Domain, not Journal: `AttemptExecutionProfile` carries a candidate
/// (PROMPT-008), the fold validates one (PERSIST-010), and the candidate selector
/// builds one (CTX-011). One type for all three is what keeps the profile's copy
/// and the committed copy comparable — CTX-012 requires the promoted snapshot to be
/// byte-identical to the one the successful request used.
type PrefixSnapshot =
    { FrozenBRef: BlobRef
      FrozenBDigest: BlobDigest
      CutoffExclusive: int
      CoveredPrefixDigest: string
      SealRoot: string
      SyntheticMessageId: string }

/// CTX-010: a candidate prefix, valid for ONE provider attempt.
///
/// Not session state. It exists only inside the immutable `AttemptExecutionProfile`
/// of the attempt that carries it, which is what makes a failed probe leave nothing
/// to roll back.
///
/// `BasedOnEpochId` is what the promotion is validated against: a probe built while
/// epoch 3 was in force may only promote to 4, so a probe that raced a concurrent
/// reanchor is refused rather than applied to the wrong base.
type PrefixProbe =
    { ProbeId: string
      BasedOnEpochId: PrefixEpochId
      Candidate: PrefixSnapshot }

/// CTX-010: which prefix this attempt sends.
///
/// A DU rather than a `PrefixProbe option`, because the two cases mean different
/// things to the promote gate: `UseCommittedEpoch` can never promote anything, and
/// its absence of a probe is a decision rather than missing data. An option would
/// make "no candidate was available" and "this slot was not armed" the same value.
[<RequireQualifiedAccess>]
type XProjectionChoice =
    | UseCommittedEpoch
    | UsePrefixProbe of PrefixProbe

/// PROMPT-008: which physical request this is.
///
/// Real request semantics, not a flow stage (ARCH-001). Each case answers a question
/// the caller cannot derive from anything else in the profile: which projection to
/// build, which instruction to send, and — via CTX-007 — what a success or failure
/// does to the cursor.
[<RequireQualifiedAccess>]
type ProviderRequestKind =
    /// The work session's own request. The only kind that may carry a prefix probe.
    | WorkMain
    /// A Companion entry request.
    | BloggerMain
    /// CTX-012's maintenance sub-request. FALLBACK-011: its success does not clear
    /// `ConsecutiveFailureCount`.
    | BloggerSquash
    /// FALLBACK-008's one repair for an unusable terminal.
    | InteractionRepair
    /// SSOT/16 LEARN-050: the Student's learning phase request (only `teacher`).
    | StudentLearn
    /// SSOT/16 LEARN-050: the Student's compile phase request (read/write/return).
    | StudentCompile

[<RequireQualifiedAccess>]
module ProviderRequestKind =

    let label (kind: ProviderRequestKind) =
        match kind with
        | ProviderRequestKind.WorkMain -> "work-main"
        | ProviderRequestKind.BloggerMain -> "blogger-main"
        | ProviderRequestKind.BloggerSquash -> "blogger-squash"
        | ProviderRequestKind.InteractionRepair -> "interaction-repair"
        | ProviderRequestKind.StudentLearn -> "student-learn"
        | ProviderRequestKind.StudentCompile -> "student-compile"

    /// CTX-008 / FALLBACK-011: does a success on this kind clear the consecutive
    /// failure count.
    ///
    /// Only a business main request does. A squash is maintenance — it produced a
    /// better representation, not a completed unit of the Logical Run's work — and a
    /// repair is salvage of an attempt that already failed to produce a usable
    /// terminal.
    let clearsFailureCountOnSuccess (kind: ProviderRequestKind) =
        match kind with
        | ProviderRequestKind.WorkMain
        | ProviderRequestKind.BloggerMain -> true
        | ProviderRequestKind.BloggerSquash
        | ProviderRequestKind.InteractionRepair
        | ProviderRequestKind.StudentLearn
        | ProviderRequestKind.StudentCompile -> false

    /// CTX-010: only the work session's main request substitutes a prefix.
    ///
    /// A Companion request has no prefix to probe — its history is the frame sequence
    /// — and a repair reuses whatever the attempt it repairs already sent.
    let mayCarryProbe (kind: ProviderRequestKind) =
        match kind with
        | ProviderRequestKind.WorkMain -> true
        | ProviderRequestKind.BloggerMain
        | ProviderRequestKind.BloggerSquash
        | ProviderRequestKind.InteractionRepair
        | ProviderRequestKind.StudentLearn
        | ProviderRequestKind.StudentCompile -> false
