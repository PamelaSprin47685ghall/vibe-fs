namespace Wanxiangshu.Strength

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt

type StrengthOpportunity =
    { OwnerSessionId: SessionId
      OwnerLogicalRun: OwnerLogicalRunIdentity
      SourcePhysicalUserMessageId: PhysicalUserMessageId
      SourceProviderRun: ProviderRunIdentity
      SourceToolCallIds: ToolCallId list
      RequestedRounds: ReadonlyRoundBudget option
      ContractRevision: DelegationContractRevision
      IsRootWork: bool
      RequestKind: ProviderRequestKind
      CanonicalRole: Role
      HasPrefixProbe: bool
      IsReplicaOrInternalLeaf: bool
      IsInteractionRepair: bool
      IsExplicitRecoveryBranch: bool
      OwnerCancelled: bool
      TargetProviderRunBound: bool
      EventStoreHealthy: bool
      HostBoundaryHealthy: bool
      ProcessFuseHealthy: bool
      OwnerLogicalRunSuperseded: bool
      PendingRequested: bool
      PredictorConfigured: bool }

[<RequireQualifiedAccess>]
type StrengthEligibility =
    | Ineligible of reason: string
    | Eligible

[<RequireQualifiedAccess>]
type StrengthAdmission =
    | Admit of DelegationRequest
    | Skip of reason: string

[<RequireQualifiedAccess>]
module StrengthPolicy =
    val eligibleRoles: Set<Role>
    val eligibility: opportunity: StrengthOpportunity -> StrengthEligibility

    val tryRequest: sha256: (string -> string) -> opportunity: StrengthOpportunity -> Result<DelegationRequest, string>

    val decide: sha256: (string -> string) -> opportunity: StrengthOpportunity -> StrengthAdmission
