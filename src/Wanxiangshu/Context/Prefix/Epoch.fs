namespace Wanxiangshu.Context.Prefix

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// COMPANION-009: which prefix generation is in force.
///
/// `Snapshot = None` is the honest representation of two different histories that
/// call for identical behaviour: nothing has been promoted yet, and a Host
/// compaction retired what had been promoted (HOST-006). Both mean "send raw
/// history", so they are one state, not two.
///
/// `PrefixSnapshot` comes from `Domain.PrefixCandidate`: the attempt profile carries
/// one (PROMPT-008), the selector builds one (CTX-011), and this fold validates one.
/// A separate copy here would let the profile's snapshot and the committed snapshot
/// differ in shape, and CTX-012 requires them to be byte-identical.
type ActivePrefixEpoch =
    {
        EpochId: PrefixEpochId
        Snapshot: PrefixSnapshot option
        /// HOST-006: which compaction pseudo-runs have already been reanchored.
        ///
        /// Durable, because a compaction message stays in the Host transcript forever.
        /// Every later reconcile observes the same run again, and the epoch check alone
        /// does NOT stop that: by then the epoch has moved on, so a freshly decided
        /// reanchor for that old compaction carries a `PreviousEpochId` that MATCHES the
        /// current one and would be accepted — advancing the epoch again and zeroing
        /// coverage the session had legitimately rebuilt.
        ///
        /// The epoch check and this set therefore guard different failures. The epoch
        /// check catches a replayed LINE (a crash between append and fold). This set
        /// catches a repeated DECISION (the same observation acted on twice). Neither
        /// subsumes the other.
        ///
        /// Bounded by the number of compactions in one session, not by turns.
        ReanchoredRuns: Set<ProviderRunIdentity>
    }

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
    /// HOST-006: this compaction pseudo-run was already reanchored.
    ///
    /// Separate from `StalePrefixEpoch` because it is reachable with a perfectly
    /// current epoch — see `ReanchoredRuns`. Collapsing the two would make the fold
    /// accept a second reanchor for one compaction whenever any other epoch change
    /// happened in between.
    | CompactionAlreadyReanchored of run: ProviderRunIdentity

module PrefixEpochProjection =

    let empty =
        { EpochId = PrefixEpochId.initial
          Snapshot = None
          ReanchoredRuns = Set.empty }

    /// CTX-011 snapshot identity: cutoff, prefix digest, FrozenRecordPrefix digest.
    ///
    /// SealRoot and SyntheticMessageId are excluded because COMPANION-013 derives
    /// both from these three; including them would make the comparison circular.
    let private sameCandidate (a: PrefixSnapshot) (b: PrefixSnapshot) =
        a.CutoffExclusive = b.CutoffExclusive
        && a.CoveredPrefixDigest = b.CoveredPrefixDigest
        && a.FrozenRecordPrefixDigest = b.FrozenRecordPrefixDigest

    let private rebaseSnapshot
        (nextEpoch: PrefixEpochId)
        (candidate: PrefixSnapshot)
        (state: ActivePrefixEpoch)
        : Result<ActivePrefixEpoch, PrefixFoldRejection> =
        match state.Snapshot with
        | Some committed when candidate.CutoffExclusive < committed.CutoffExclusive ->
            Error(PrefixFoldRejection.CutoffRetreated(committed.CutoffExclusive, candidate.CutoffExclusive))
        | Some committed when sameCandidate candidate committed -> Error PrefixFoldRejection.CandidateNotNew
        | _ ->
            Ok
                { state with
                    EpochId = nextEpoch
                    Snapshot = Some candidate }

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
            rebaseSnapshot nextEpoch candidate state

    /// PERSIST-010 `ContextReanchored`: HOST-006 containment.
    ///
    /// Retirement, not replacement. The projection cannot repoint the snapshot at a
    /// position after the Host summary: `CutoffExclusive` belongs to the voided
    /// current-generation XTrace semantic-turn numbering, and the Companion may have
    /// been behind the Host when compaction happened, so any new cutoff would be a
    /// claim the journal cannot support.
    ///
    /// The epoch still advances. This is a real cold boundary — the provider-visible
    /// prefix changed and the seal barrier broke — and COMPANION-009's byte-stability
    /// guarantee is scoped to one epoch, so staying on the same number would state
    /// something false.
    ///
    /// `observedRun` is recorded so the same compaction cannot be reanchored twice.
    /// That check has to be here and not only at the decision site: the compaction
    /// message stays in the transcript forever, so once the epoch has moved on for any
    /// other reason, a freshly decided reanchor for that old run would carry a matching
    /// `PreviousEpochId` and be accepted — advancing the epoch again and zeroing
    /// coverage the session had legitimately rebuilt.
    let applyReanchor
        (previousEpoch: PrefixEpochId)
        (nextEpoch: PrefixEpochId)
        (observedRun: ProviderRunIdentity)
        (state: ActivePrefixEpoch)
        : Result<ActivePrefixEpoch, PrefixFoldRejection> =
        if Set.contains observedRun state.ReanchoredRuns then
            Error(PrefixFoldRejection.CompactionAlreadyReanchored observedRun)
        elif previousEpoch <> state.EpochId then
            Error(PrefixFoldRejection.StalePrefixEpoch(state.EpochId, previousEpoch))
        elif nextEpoch <> PrefixEpochId.next previousEpoch then
            Error PrefixFoldRejection.NonSequentialPrefixEpoch
        else
            Ok
                { EpochId = nextEpoch
                  Snapshot = None
                  ReanchoredRuns = Set.add observedRun state.ReanchoredRuns }

    /// HOST-006: has this compaction pseudo-run already been reanchored.
    ///
    /// The predicate `HostCompactionPolicy.nextReanchor` consumes. Exposed as a query
    /// so the adapter reads it from the projection rather than keeping a runtime set
    /// that a restart would lose.
    let isReanchored (run: ProviderRunIdentity) (state: ActivePrefixEpoch) = Set.contains run state.ReanchoredRuns

    /// COMPANION-009: is a companion-memory prefix in force for this session.
    let hasSnapshot (state: ActivePrefixEpoch) = Option.isSome state.Snapshot
