namespace Wanxiangshu.Context.Prefix

open Wanxiangshu.Foundation.Identity

/// COMPANION-009: the frozen companion memory that replaces X's raw prefix, and
/// the proof it may.
///
/// `CutoffExclusive` is a canonical current-generation XTrace semantic-turn
/// boundary. It is meaningful only under the XTrace materialization that produced
/// `CoveredPrefixDigest`; Host write-back maps that boundary through stable message
/// provenance rather than treating it as a request-local provider-array index.
/// Both values travel together so a snapshot cannot be used without re-verifying
/// the exact canonical history it claims to replace (COMPANION-011).
///
/// Lives in Domain, not Journal: `AttemptExecutionProfile` carries a candidate
/// (PROMPT-008), the fold validates one (PERSIST-010), and the candidate selector
/// builds one (CTX-011). One type for all three is what keeps the profile's copy
/// and the committed copy comparable — CTX-012 requires the promoted snapshot to be
/// byte-identical to the one the successful request used.
type PrefixSnapshot =
    { FrozenRecordPrefixRef: BlobRef
      FrozenRecordPrefixDigest: BlobDigest
      CutoffExclusive: int
      CoveredPrefixDigest: string
      SealRoot: string
      SyntheticMessageId: string }

[<RequireQualifiedAccess>]
module PrefixSnapshot =

    /// CTX-011 snapshot identity: cutoff, covered-prefix digest, FrozenRecordPrefix digest.
    /// SealRoot and SyntheticMessageId are derived from these fields and are not
    /// independent identity inputs.
    let sameIdentity (a: PrefixSnapshot) (b: PrefixSnapshot) =
        a.CutoffExclusive = b.CutoffExclusive
        && a.CoveredPrefixDigest = b.CoveredPrefixDigest
        && a.FrozenRecordPrefixDigest = b.FrozenRecordPrefixDigest

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
