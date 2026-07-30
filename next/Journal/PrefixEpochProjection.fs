namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Identity

/// COMPANION-009: the frozen companion memory that replaces X's raw prefix, and
/// the proof it may.
///
/// `CutoffExclusive` is an index into X's provider-visible messages, so it is only
/// meaningful under the numbering that produced `CoveredPrefixDigest`. Both travel
/// together for that reason — a snapshot carrying one without the other could not
/// be re-verified before use (COMPANION-011).
type PrefixSnapshot =
    { FrozenBRef: BlobRef
      FrozenBDigest: BlobDigest
      CutoffExclusive: int
      CoveredPrefixDigest: string
      SealRoot: string
      SyntheticMessageId: string }

/// COMPANION-009: which prefix generation is in force.
///
/// `Snapshot = None` is the honest representation of two different histories that
/// call for identical behaviour: nothing has been promoted yet, and a Host
/// compaction retired what had been promoted (HOST-006). Both mean "send raw
/// history", so they are one state, not two.
type ActivePrefixEpoch =
    { EpochId: PrefixEpochId
      Snapshot: PrefixSnapshot option }

/// Why a prefix-epoch line was refused. One case per PERSIST-010 rule.
[<RequireQualifiedAccess>]
type PrefixFoldRejection =
    /// The line was written against a different epoch than the one in force.
    | StalePrefixEpoch of expected: PrefixEpochId * actual: PrefixEpochId
    /// `NextEpochId` was not the successor of `PreviousEpochId`.
    | NonSequentialPrefixEpoch
    /// CTX-011: a promoted cutoff may not be earlier than the committed one.
    | CutoffRetreated of committed: int * proposed: int
    /// CTX-011: the candidate is indistinguishable from what is already committed,
    /// so promoting it would burn an epoch and a cold boundary for no change.
    | CandidateNotNew

// A duplicate reanchor needs no case of its own. Every reanchor advances the
// epoch, so a replayed line carries a `PreviousEpochId` the projection has already
// left behind and lands on `StalePrefixEpoch`. Idempotency is a consequence of the
// epoch check rather than a second mechanism that could disagree with it.

module PrefixEpochProjection =

    let empty =
        { EpochId = PrefixEpochId.initial
          Snapshot = None }

    /// CTX-011 snapshot identity: cutoff, prefix digest, FrozenB digest.
    ///
    /// SealRoot and SyntheticMessageId are excluded because COMPANION-013 derives
    /// both from these three; including them would make the comparison circular.
    let private sameCandidate (a: PrefixSnapshot) (b: PrefixSnapshot) =
        a.CutoffExclusive = b.CutoffExclusive
        && a.CoveredPrefixDigest = b.CoveredPrefixDigest
        && a.FrozenBDigest = b.FrozenBDigest

    /// PERSIST-010 `PrefixRebaseCommitted`: a probe produced a valid terminal, so
    /// its candidate becomes the committed epoch.
    ///
    /// The candidate arrives whole rather than field by field. CTX-012 requires the
    /// promoted snapshot to be byte-identical to the one the successful request
    /// used — in particular its SealRoot, so the next request continues the same
    /// prefix instead of paying a second cold boundary. Rebuilding it here would be
    /// a second construction site that could disagree.
    let applyRebase
        (previousEpoch: PrefixEpochId)
        (nextEpoch: PrefixEpochId)
        (candidate: PrefixSnapshot)
        (state: ActivePrefixEpoch)
        : Result<ActivePrefixEpoch, PrefixFoldRejection> =
        if previousEpoch <> state.EpochId then
            Error(PrefixFoldRejection.StalePrefixEpoch(state.EpochId, previousEpoch))
        elif nextEpoch <> PrefixEpochId.next previousEpoch then
            Error PrefixFoldRejection.NonSequentialPrefixEpoch
        else
            match state.Snapshot with
            | Some committed when candidate.CutoffExclusive < committed.CutoffExclusive ->
                Error(PrefixFoldRejection.CutoffRetreated(committed.CutoffExclusive, candidate.CutoffExclusive))
            | Some committed when sameCandidate candidate committed -> Error PrefixFoldRejection.CandidateNotNew
            | _ ->
                Ok
                    { EpochId = nextEpoch
                      Snapshot = Some candidate }

    /// PERSIST-010 `ContextReanchored`: HOST-006 containment.
    ///
    /// Retirement, not replacement. The projection cannot repoint the snapshot at a
    /// position after the Host summary: `CutoffExclusive` is an index in the voided
    /// numbering, and the Companion may have been behind the Host when compaction
    /// happened, so any new index would be a claim the journal cannot support.
    ///
    /// The epoch still advances. This is a real cold boundary — the provider-visible
    /// prefix changed and the seal barrier broke — and COMPANION-009's byte-stability
    /// guarantee is scoped to one epoch, so staying on the same number would state
    /// something false.
    let applyReanchor
        (previousEpoch: PrefixEpochId)
        (nextEpoch: PrefixEpochId)
        (state: ActivePrefixEpoch)
        : Result<ActivePrefixEpoch, PrefixFoldRejection> =
        if previousEpoch <> state.EpochId then
            Error(PrefixFoldRejection.StalePrefixEpoch(state.EpochId, previousEpoch))
        elif nextEpoch <> PrefixEpochId.next previousEpoch then
            Error PrefixFoldRejection.NonSequentialPrefixEpoch
        else
            Ok { EpochId = nextEpoch; Snapshot = None }

    /// COMPANION-009: is a companion-memory prefix in force for this session.
    let hasSnapshot (state: ActivePrefixEpoch) = Option.isSome state.Snapshot
