namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Identity

/// ENFORCER-045/154: one committed enforcement cycle, as folded.
///
/// Field names carry the `Cycle` prefix deliberately: F# field inference picks
/// the most recently defined candidate type, and bare `TextRef`/`TextDigest`
/// would shadow `BlogFrame`'s fields for every unannotated `frame` variable in
/// the companion projections.
type EnforcementCycleRecord =
    { MainSessionId: SessionId
      BloggerSessionId: SessionId
      ProviderRun: ProviderRunIdentity
      ToolCallIds: ToolCallId list
      CycleTextRef: BlobRef
      CycleTextDigest: BlobDigest
      CycleScoreRef: BlobRef option
      CycleEvidenceRef: BlobRef option
      ObservedPrefixEpochId: PrefixEpochId }

/// ENFORCER-150 (second option) + PERSIST-008: the enforcement half of the
/// Companion projection.
///
/// O(1) by construction: the fold keeps an index keyed by ProviderRun, never a
/// scan of journal history. A provider step commits at most one cycle
/// (ENFORCER-154), so a duplicate append is a fold rejection — fail closed.
type EnforcementProjectionState =
    { ByProviderRun: Map<ProviderRunIdentity, EnforcementCycleRecord> }

module EnforcementProjection =

    let empty: EnforcementProjectionState = { ByProviderRun = Map.empty }

    /// Fold rule for EnforcementCycleCommitted.
    ///
    /// Refuses a second cycle for the same ProviderRunIdentity (ENFORCER-154:
    /// "同一 ProviderRunIdentity 最多产生一个 Entry"). The caller maps this to
    /// a FoldRejection so a corrupted journal stops replay instead of silently
    /// accepting the duplicate.
    let applyCycle
        (state: EnforcementProjectionState)
        (record: EnforcementCycleRecord)
        : Result<EnforcementProjectionState, string> =
        match Map.tryFind record.ProviderRun state.ByProviderRun with
        | Some _ ->
            Error(
                sprintf
                    "EnforcementCycleCommitted already recorded for provider run %s (ENFORCER-154)"
                    (ProviderRunIdentity.value record.ProviderRun)
            )
        | None ->
            Ok
                { state with
                    ByProviderRun = Map.add record.ProviderRun record state.ByProviderRun }

    let tryFindByProviderRun (run: ProviderRunIdentity) (state: EnforcementProjectionState) =
        Map.tryFind run state.ByProviderRun
