namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Identity

/// ENFORCER-045/154: one committed enforcement cycle, derived from BlogEntryCommitted.
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

/// ENFORCER-150 + PERSIST-008: O(1) index by ProviderRun. No separate fact —
/// fold derives this from the extended BlogEntryCommitted.
type EnforcementProjectionState =
    { ByProviderRun: Map<ProviderRunIdentity, EnforcementCycleRecord> }

module EnforcementProjection =

    let empty: EnforcementProjectionState = { ByProviderRun = Map.empty }

    /// Apply the enforcement half of BlogEntryCommitted.
    /// Duplicate ProviderRun → Error (ENFORCER-154). Caller may absorb as idempotent.
    let applyFromEntry
        (state: EnforcementProjectionState)
        (record: EnforcementCycleRecord)
        : Result<EnforcementProjectionState, string> =
        match Map.tryFind record.ProviderRun state.ByProviderRun with
        | Some _ ->
            Error(
                sprintf
                    "BlogEntryCommitted enforcement half already recorded for provider run %s (ENFORCER-154)"
                    (ProviderRunIdentity.value record.ProviderRun)
            )
        | None ->
            Ok
                { state with
                    ByProviderRun = Map.add record.ProviderRun record state.ByProviderRun }

    let tryFindByProviderRun (run: ProviderRunIdentity) (state: EnforcementProjectionState) =
        Map.tryFind run state.ByProviderRun
