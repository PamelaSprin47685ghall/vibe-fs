namespace Wanxiangshu.Strength

open Wanxiangshu.Foundation.Identity

type DelegationContractRevision = DelegationContractRevision of int

module DelegationContractRevisions =
    val create: value: int -> DelegationContractRevision
    val value: revision: DelegationContractRevision -> int

type OwnerLogicalRunIdentity =
    { LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId }

type DelegationRequest =
    { DecisionId: StrengthDecisionId
      OwnerSessionId: SessionId
      OwnerLogicalRun: OwnerLogicalRunIdentity
      SourcePhysicalUserMessageId: PhysicalUserMessageId
      SourceProviderRun: ProviderRunIdentity
      SourceToolCallIds: ToolCallId list
      RequestedRounds: ReadonlyRoundBudget
      ContractRevision: DelegationContractRevision }

type DelegationBinding =
    { DecisionId: StrengthDecisionId
      TargetProviderRun: ProviderRunIdentity
      ReplicaSessionId: SessionId
      AnchorDigest: string }

[<RequireQualifiedAccess>]
type DelegationClosedReason =
    | NoMaterial
    | CannotContinue
    | Cancelled
    | Superseded
    | RecoveryAbandoned

[<RequireQualifiedAccess>]
type DelegationClosedFrom =
    | Requested
    | Bound

type DelegationClosed =
    { DecisionId: StrengthDecisionId
      From: DelegationClosedFrom
      Reason: DelegationClosedReason }

[<RequireQualifiedAccess>]
type DelegationLifecycle =
    | Requested of DelegationRequest
    | Bound of DelegationRequest * DelegationBinding
    | Prepared of DelegationRequest * DelegationBinding
    | Promoted of DelegationRequest * DelegationBinding
    | Traced of DelegationRequest * DelegationBinding
    | Closed of DelegationRequest * DelegationClosed
    | Abandoned of DelegationRequest * DelegationBinding

[<RequireQualifiedAccess>]
type DelegationTransitionError =
    | IllegalFrom of decisionId: StrengthDecisionId
    | Conflict of decisionId: StrengthDecisionId

[<RequireQualifiedAccess>]
module Delegation =
    val deriveDecisionId:
        sha256: (string -> string) ->
        contractRevision: DelegationContractRevision ->
        owner: OwnerLogicalRunIdentity ->
        sourceProviderRun: ProviderRunIdentity ->
            StrengthDecisionId

    val sameRequest: left: DelegationRequest -> right: DelegationRequest -> bool
    val sameBinding: left: DelegationBinding -> right: DelegationBinding -> bool
    val request: value: DelegationRequest -> DelegationLifecycle
    val decisionId: lifecycle: DelegationLifecycle -> StrengthDecisionId

    val tryBind:
        lifecycle: DelegationLifecycle ->
        binding: DelegationBinding ->
            Result<DelegationLifecycle, DelegationTransitionError>

    val tryPrepare: lifecycle: DelegationLifecycle -> Result<DelegationLifecycle, DelegationTransitionError>
    val tryPromote: lifecycle: DelegationLifecycle -> Result<DelegationLifecycle, DelegationTransitionError>
    val tryTrace: lifecycle: DelegationLifecycle -> Result<DelegationLifecycle, DelegationTransitionError>

    val tryClose:
        lifecycle: DelegationLifecycle ->
        closed: DelegationClosed ->
            Result<DelegationLifecycle, DelegationTransitionError>

    val tryAbandon: lifecycle: DelegationLifecycle -> Result<DelegationLifecycle, DelegationTransitionError>
