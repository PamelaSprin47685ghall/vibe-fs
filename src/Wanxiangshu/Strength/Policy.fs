namespace Wanxiangshu.Strength

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt

/// DELEGATE-7.2: everything admission needs, supplied by the caller as evidence.
/// No predictor sample, cost estimate, evidence count, holdout bucket or margin
/// participates any more: the only economic judgement is the owner's integer,
/// already collapsed to the batch maximum by the collection boundary.
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

    /// execution-model-routing-018 / office-capability: the current legal role
    /// set. Retired coder/inspector/inquiry roles are never restored to satisfy
    /// an outdated list.
    let eligibleRoles = Roles.all |> Set.ofList

    let eligibility (opportunity: StrengthOpportunity) : StrengthEligibility =
        if not opportunity.IsRootWork then
            StrengthEligibility.Ineligible "not-root-work"
        elif opportunity.RequestKind <> ProviderRequestKind.WorkMain then
            StrengthEligibility.Ineligible "not-work-main"
        elif not (Set.contains opportunity.CanonicalRole eligibleRoles) then
            StrengthEligibility.Ineligible "role-ineligible"
        elif opportunity.HasPrefixProbe then
            StrengthEligibility.Ineligible "prefix-probe"
        elif opportunity.IsReplicaOrInternalLeaf then
            StrengthEligibility.Ineligible "replica-or-internal-leaf"
        elif opportunity.IsInteractionRepair then
            StrengthEligibility.Ineligible "interaction-repair"
        elif opportunity.IsExplicitRecoveryBranch then
            StrengthEligibility.Ineligible "explicit-recovery-branch"
        elif opportunity.OwnerCancelled then
            StrengthEligibility.Ineligible "owner-cancelled"
        elif not opportunity.TargetProviderRunBound then
            StrengthEligibility.Ineligible "target-provider-run-unbound"
        elif not opportunity.EventStoreHealthy then
            StrengthEligibility.Ineligible "event-store-unhealthy"
        elif not opportunity.HostBoundaryHealthy then
            StrengthEligibility.Ineligible "host-boundary-unhealthy"
        elif not opportunity.ProcessFuseHealthy then
            StrengthEligibility.Ineligible "process-fuse-unhealthy"
        elif opportunity.OwnerLogicalRunSuperseded then
            StrengthEligibility.Ineligible "owner-logical-run-superseded"
        elif not opportunity.PendingRequested then
            StrengthEligibility.Ineligible "no-pending-requested"
        elif not opportunity.PredictorConfigured then
            StrengthEligibility.Ineligible "predictor-unconfigured"
        else
            StrengthEligibility.Eligible

    let private buildRequest (sha256: string -> string) (opportunity: StrengthOpportunity) budget calls =
        match calls with
        | [] -> Error "empty-source-tool-call-set"
        | _ ->
            Ok
                { DecisionId =
                    Delegation.deriveDecisionId
                        sha256
                        opportunity.ContractRevision
                        opportunity.OwnerLogicalRun
                        opportunity.SourceProviderRun
                  OwnerSessionId = opportunity.OwnerSessionId
                  OwnerLogicalRun = opportunity.OwnerLogicalRun
                  SourcePhysicalUserMessageId = opportunity.SourcePhysicalUserMessageId
                  SourceProviderRun = opportunity.SourceProviderRun
                  SourceToolCallIds = calls
                  RequestedRounds = budget
                  ContractRevision = opportunity.ContractRevision }

    let private checkEligibilityBudget (sha256: string -> string) (opportunity: StrengthOpportunity) =
        match opportunity.RequestedRounds with
        | None -> Error "no-authorization-opportunity"
        | Some budget when ReadonlyRoundBudget.value budget = 0 -> Error "zero-round-budget"
        | Some budget -> buildRequest sha256 opportunity budget opportunity.SourceToolCallIds

    /// Pure Evidence -> Decision. Predictor configuration is the caller's input
    /// and means existence only: temporary capacity shortage is not a reason to
    /// refuse admission, and capacity is never probed here.
    let tryRequest (sha256: string -> string) (opportunity: StrengthOpportunity) : Result<DelegationRequest, string> =
        match eligibility opportunity with
        | StrengthEligibility.Ineligible reason -> Error reason
        | StrengthEligibility.Eligible -> checkEligibilityBudget sha256 opportunity

    let decide (sha256: string -> string) (opportunity: StrengthOpportunity) : StrengthAdmission =
        match tryRequest sha256 opportunity with
        | Ok request -> StrengthAdmission.Admit request
        | Error reason -> StrengthAdmission.Skip reason
