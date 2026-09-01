namespace Wanxiangshu.Enforcer

open Wanxiangshu.Foundation.Identity

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

type RecentTip =
    { RuleId: string
      FieldName: string
      CycleId: string }

type EnforcementProjectionState =
    { ByProviderRun: Map<ProviderRunIdentity, EnforcementCycleRecord>
      RecentTips: RecentTip list }

module EnforcementProjection =
    [<Literal>]
    val RecentTipLimit: int = 8

    val empty: EnforcementProjectionState

    val applyFromEntry:
        state: EnforcementProjectionState ->
        record: EnforcementCycleRecord ->
            Result<EnforcementProjectionState, string>

    val applySquash: count: int -> state: EnforcementProjectionState -> EnforcementProjectionState

    val tryFindByProviderRun:
        run: ProviderRunIdentity -> state: EnforcementProjectionState -> EnforcementCycleRecord option

    val recentTips: state: EnforcementProjectionState -> RecentTip list
