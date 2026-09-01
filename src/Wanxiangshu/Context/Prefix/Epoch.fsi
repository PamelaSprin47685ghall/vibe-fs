namespace Wanxiangshu.Context.Prefix

open Wanxiangshu.Foundation.Identity

type ActivePrefixEpoch =
    { EpochId: PrefixEpochId
      Snapshot: PrefixSnapshot option
      ReanchoredRuns: Set<ProviderRunIdentity> }

[<RequireQualifiedAccess>]
type PrefixFoldRejection =
    | StalePrefixEpoch of expected: PrefixEpochId * actual: PrefixEpochId
    | NonSequentialPrefixEpoch
    | CutoffRetreated of committed: int * proposed: int
    | CandidateNotNew
    | CompactionAlreadyReanchored of run: ProviderRunIdentity

module PrefixEpochProjection =
    val empty: ActivePrefixEpoch

    val applyRebase:
        previousEpoch: PrefixEpochId ->
        nextEpoch: PrefixEpochId ->
        candidate: PrefixSnapshot ->
        state: ActivePrefixEpoch ->
            Result<ActivePrefixEpoch, PrefixFoldRejection>

    val applyReanchor:
        previousEpoch: PrefixEpochId ->
        nextEpoch: PrefixEpochId ->
        observedRun: ProviderRunIdentity ->
        state: ActivePrefixEpoch ->
            Result<ActivePrefixEpoch, PrefixFoldRejection>

    val isReanchored: run: ProviderRunIdentity -> state: ActivePrefixEpoch -> bool
    val hasSnapshot: state: ActivePrefixEpoch -> bool
