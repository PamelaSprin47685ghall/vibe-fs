namespace Wanxiangshu.Strength

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// STRENGTH-003: the delegation budget counts provider requests, never tool
/// calls. It is a whole non-negative integer chosen by the owner model as the
/// maximum across one parallel tool batch; there are no tier levels.
[<Struct>]
type ReadonlyRoundBudget = private ReadonlyRoundBudget of int

[<Struct>]
type DelegationContractRevision = private DelegationContractRevision of int

module DelegationContractRevisions =
    let current = DelegationContractRevision 1
    let value (DelegationContractRevision v) = v
    let create v = DelegationContractRevision v

    let tryCreate v =
        if v = 1 then
            Ok(DelegationContractRevision v)
        else
            Error "unknown-contract-revision"

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
    let sameRequest (a: DelegationRequest) (b: DelegationRequest) = a = b
    let sameBinding (a: DelegationBinding) (b: DelegationBinding) = a = b

    let deriveDecisionId
        (sha256: string -> string)
        (contractRevision: DelegationContractRevision)
        (ownerLogicalRun: OwnerLogicalRunIdentity)
        (sourceProviderRun: ProviderRunIdentity)
        : StrengthDecisionId =
        let raw =
            String.concat
                "\u001f"
                [ "strength-delegation-v1"
                  string (DelegationContractRevisions.value contractRevision)
                  LogicalRunId.value ownerLogicalRun.LogicalRunId
                  AuthorityRootUserMessageId.value ownerLogicalRun.AuthorityRootUserMessageId
                  ProviderRunIdentity.value sourceProviderRun ]

        StrengthDecisionId.create (sha256 raw)

    let request (req: DelegationRequest) : DelegationLifecycle = DelegationLifecycle.Requested req

    let decisionId (lifecycle: DelegationLifecycle) : StrengthDecisionId =
        match lifecycle with
        | DelegationLifecycle.Requested r -> r.DecisionId
        | DelegationLifecycle.Bound(r, _) -> r.DecisionId
        | DelegationLifecycle.Prepared(r, _) -> r.DecisionId
        | DelegationLifecycle.Promoted(r, _) -> r.DecisionId
        | DelegationLifecycle.Traced(r, _) -> r.DecisionId
        | DelegationLifecycle.Closed c -> c.DecisionId
        | DelegationLifecycle.Abandoned(r, _) -> r.DecisionId

    let tryBind
        (lifecycle: DelegationLifecycle)
        (binding: DelegationBinding)
        : Result<DelegationLifecycle, DelegationTransitionError> =
        match lifecycle with
        | DelegationLifecycle.Requested r -> Ok(DelegationLifecycle.Bound(r, binding))
        | DelegationLifecycle.Bound(r, existing) when sameBinding existing binding ->
            Ok(DelegationLifecycle.Bound(r, existing))
        | DelegationLifecycle.Bound _ -> Error(DelegationTransitionError.Conflict "already-bound")
        | _ -> Error(DelegationTransitionError.IllegalFrom "bind")

    let tryPrepare (lifecycle: DelegationLifecycle) : Result<DelegationLifecycle, DelegationTransitionError> =
        match lifecycle with
        | DelegationLifecycle.Bound(r, b) -> Ok(DelegationLifecycle.Prepared(r, b))
        | _ -> Error(DelegationTransitionError.IllegalFrom "prepare")

    let tryPromote (lifecycle: DelegationLifecycle) : Result<DelegationLifecycle, DelegationTransitionError> =
        match lifecycle with
        | DelegationLifecycle.Prepared(r, b) -> Ok(DelegationLifecycle.Promoted(r, b))
        | _ -> Error(DelegationTransitionError.IllegalFrom "promote")

    let tryTrace (lifecycle: DelegationLifecycle) : Result<DelegationLifecycle, DelegationTransitionError> =
        match lifecycle with
        | DelegationLifecycle.Promoted(r, b) -> Ok(DelegationLifecycle.Traced(r, b))
        | _ -> Error(DelegationTransitionError.IllegalFrom "trace")

    let tryClose
        (lifecycle: DelegationLifecycle)
        (closed: DelegationClosed)
        : Result<DelegationLifecycle, DelegationTransitionError> =
        match lifecycle with
        | DelegationLifecycle.Requested _
        | DelegationLifecycle.Bound _ -> Ok(DelegationLifecycle.Closed closed)
        | _ -> Error(DelegationTransitionError.IllegalFrom "close")

    let tryAbandon (lifecycle: DelegationLifecycle) : Result<DelegationLifecycle, DelegationTransitionError> =
        match lifecycle with
        | DelegationLifecycle.Bound(r, b)
        | DelegationLifecycle.Prepared(r, b) -> Ok(DelegationLifecycle.Abandoned(r, b))
        | _ -> Error(DelegationTransitionError.IllegalFrom "abandon")

module ReadonlyRoundBudget =

    let tryCreate value =
        if value < 0 then
            Error "negative-readonly-round-budget"
        else
            Ok(ReadonlyRoundBudget value)

    let value (ReadonlyRoundBudget value) = value

    /// STRENGTH-003: one batch of already-validated budgets collapses to its
    /// maximum. None means the tool set grants no authorization opportunity at
    /// all; Some 0 means do not start a Replica, not a mode of its own.
    let maxOf (budgets: ReadonlyRoundBudget list) : ReadonlyRoundBudget option =
        match budgets with
        | [] -> None
        | _ -> Some(List.maxBy value budgets)
