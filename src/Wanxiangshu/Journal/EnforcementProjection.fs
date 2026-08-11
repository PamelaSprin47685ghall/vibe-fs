namespace Wanxiangshu.Journal

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// ENFORCER-045/154: one committed enforcement cycle, derived from BlogEntryCommitted.
/// Tip v2: TipRuleId replaces CycleScoreRef (ENFORCER-072).
type EnforcementCycleRecord =
    { MainSessionId: SessionId
      BloggerSessionId: SessionId
      ProviderRun: ProviderRunIdentity
      ToolCallIds: ToolCallId list
      CycleTextRef: BlobRef
      CycleTextDigest: BlobDigest
      TipRuleId: string
      FieldNameAtCommit: string option
      CycleEvidenceRef: BlobRef option
      ObservedPrefixEpochId: PrefixEpochId }

/// ENFORCER-070: one tip in the bounded history (oldest → newest).
/// CycleId = ProviderRunIdentity of the committed cycle (one cycle per run).
type RecentTip =
    { RuleId: string
      FieldName: string
      CycleId: string }

/// ENFORCER-150 + PERSIST-008: O(1) index by ProviderRun + bounded RecentTips.
/// No separate fact — fold derives this from BlogEntryCommitted.
type EnforcementProjectionState =
    { ByProviderRun: Map<ProviderRunIdentity, EnforcementCycleRecord>
      RecentTips: RecentTip list }

module EnforcementProjection =

    [<Literal>]
    let RecentTipLimit = 8

    let empty: EnforcementProjectionState =
        { ByProviderRun = Map.empty
          RecentTips = [] }

    let private keepLast (limit: int) (tips: RecentTip list) : RecentTip list =
        let n = List.length tips

        if n <= limit then tips else tips |> List.skip (n - limit)

    /// Apply the enforcement half of BlogEntryCommitted.
    /// Duplicate ProviderRun → Error (ENFORCER-154). Caller may absorb as idempotent.
    /// Appends RecentTips and keeps last RecentTipLimit (ENFORCER-070).
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
            let fieldName =
                match record.FieldNameAtCommit with
                | Some f when not (isNull f) && f.Trim().Length > 0 -> f
                | _ -> record.TipRuleId

            let tip =
                { RuleId = record.TipRuleId
                  FieldName = fieldName
                  CycleId = ProviderRunIdentity.value record.ProviderRun }

            Ok
                { ByProviderRun = Map.add record.ProviderRun record state.ByProviderRun
                  RecentTips = keepLast RecentTipLimit (state.RecentTips @ [ tip ]) }

    /// Observation squash half of `BlogSquashCommitted`.
    ///
    /// Co-truncate RecentTips when BlogSquashCommitted collapses the oldest `count`
    /// frames. Together with BlogProjection.applySquash this is the Journal substrate
    /// for rulebook `BlogObservationsSquashed`: work-log frames and tips move as one
    /// Observation history (see `ObservationProjection.observationsOf`).
    ///
    /// Assumption (1:1): each BlogEntryCommitted appends one Entry frame and one tip.
    /// Squash frames do not add tips. On squash of the oldest `count` frames, drop the
    /// oldest `min(count, tips.Length)` tips so tip history collapses with the covered
    /// frame range (ENFORCER observation co-move; imperfect if a prior squash frame is
    /// among the covered range, but improves residual vs independent tip lifetime).
    let applySquash (count: int) (state: EnforcementProjectionState) : EnforcementProjectionState =
        if count <= 0 then
            state
        else
            let n = List.length state.RecentTips
            let drop = if count < n then count else n

            if drop = 0 then
                state
            else
                { state with
                    RecentTips = List.skip drop state.RecentTips }

    let tryFindByProviderRun (run: ProviderRunIdentity) (state: EnforcementProjectionState) =
        Map.tryFind run state.ByProviderRun

    let recentTips (state: EnforcementProjectionState) : RecentTip list = state.RecentTips
