namespace Wanxiangshu.Strength

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<Struct>]
type ReadonlyRoundBudget = private ReadonlyRoundBudget of int

[<Struct>]
type DelegationContractRevision = private DelegationContractRevision of int

module DelegationContractRevisions =
    val current: DelegationContractRevision
    val value: DelegationContractRevision -> int
    val create: int -> DelegationContractRevision
    val tryCreate: int -> Result<DelegationContractRevision, string>

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
type DelegationClosedFrom =
    | Requested
    | Bound

[<RequireQualifiedAccess>]
type DelegationClosedReason =
    | NoMaterial
    | CannotContinue
    | Cancelled
    | Superseded
    | RecoveryAbandoned

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
    | Closed of DelegationClosed
    | Abandoned of DelegationRequest * DelegationBinding

[<RequireQualifiedAccess>]
type DelegationTransitionError =
    | IllegalFrom of string
    | Conflict of string

module Delegation =
    val sameRequest: DelegationRequest -> DelegationRequest -> bool
    val sameBinding: DelegationBinding -> DelegationBinding -> bool

    val deriveDecisionId:
        (string -> string) ->
        DelegationContractRevision ->
        OwnerLogicalRunIdentity ->
        ProviderRunIdentity ->
            StrengthDecisionId

    val request: DelegationRequest -> DelegationLifecycle
    val decisionId: DelegationLifecycle -> StrengthDecisionId
    val tryBind: DelegationLifecycle -> DelegationBinding -> Result<DelegationLifecycle, DelegationTransitionError>
    val tryPrepare: DelegationLifecycle -> Result<DelegationLifecycle, DelegationTransitionError>
    val tryPromote: DelegationLifecycle -> Result<DelegationLifecycle, DelegationTransitionError>
    val tryTrace: DelegationLifecycle -> Result<DelegationLifecycle, DelegationTransitionError>
    val tryClose: DelegationLifecycle -> DelegationClosed -> Result<DelegationLifecycle, DelegationTransitionError>
    val tryAbandon: DelegationLifecycle -> Result<DelegationLifecycle, DelegationTransitionError>

module ReadonlyRoundBudget =
    val tryCreate: value: int -> Result<ReadonlyRoundBudget, string>
    val value: budget: ReadonlyRoundBudget -> int

    val maxOf: budgets: ReadonlyRoundBudget list -> ReadonlyRoundBudget option
