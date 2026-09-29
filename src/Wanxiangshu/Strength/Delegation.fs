namespace Wanxiangshu.Strength

open Wanxiangshu.Foundation.Identity

/// DELEGATE-6.1: stable version of this contract's field set and schema. It is
/// a code-level contract version, never a runtime policy switch.
type DelegationContractRevision = DelegationContractRevision of int

module DelegationContractRevisions =
    let create value = DelegationContractRevision value
    let value (DelegationContractRevision value) = value

/// DELEGATE-6.1: owner logical-run evidence. It reuses the existing
/// authority/identity vocabulary instead of inventing a second string identity.
type OwnerLogicalRunIdentity =
    { LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId }

/// DELEGATE-6.1: one readonly delegation request, frozen when the whole source
/// batch completed and the owner produced genuine new output. RequestedRounds
/// is owned here and nowhere else; Bound and Prepared only reference the
/// DecisionId. There is deliberately no SelfNote, Hint or TrustScore field.
type DelegationRequest =
    { DecisionId: StrengthDecisionId
      OwnerSessionId: SessionId
      OwnerLogicalRun: OwnerLogicalRunIdentity
      SourcePhysicalUserMessageId: PhysicalUserMessageId
      SourceProviderRun: ProviderRunIdentity
      SourceToolCallIds: ToolCallId list
      RequestedRounds: ReadonlyRoundBudget
      ContractRevision: DelegationContractRevision }

/// DELEGATE-6.2: the one execution that actually consumed this request. Bound
/// is the fact of consumption authorization, not proof the provider saw the
/// returned material.
type DelegationBinding =
    { DecisionId: StrengthDecisionId
      TargetProviderRun: ProviderRunIdentity
      ReplicaSessionId: SessionId
      AnchorDigest: string }

/// DELEGATE-6.2: why a request closed before any material was produced.
[<RequireQualifiedAccess>]
type DelegationClosedReason =
    | NoMaterial
    | CannotContinue
    | Cancelled
    | Superseded
    | RecoveryAbandoned

/// DELEGATE-6.3: which legal predecessor a Closed fact closes from. Closing is
/// one terminal fact; the source keeps the parent edge honest instead of giving
/// every close the same parent.
[<RequireQualifiedAccess>]
type DelegationClosedFrom =
    | Requested
    | Bound

type DelegationClosed =
    { DecisionId: StrengthDecisionId
      From: DelegationClosedFrom
      Reason: DelegationClosedReason }

/// DELEGATE-6.2/6.3: the authorization lifecycle. Legal edges are
/// Requested -> Bound -> Prepared -> Promoted -> Traced,
/// Requested -> Closed, Bound -> Closed and Prepared -> Abandoned.
/// Prepared/Promoted/Traced remain the candidate consumption proof chain.
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

    /// DELEGATE-6.1: the DecisionId derives deterministically from the contract
    /// version, the owner logical run and the SourceProviderRun. It never
    /// derives from tool completion order or from a future target request, so a
    /// retry that changes target cannot mint a second budget.
    let deriveDecisionId
        (sha256: string -> string)
        (contractRevision: DelegationContractRevision)
        (owner: OwnerLogicalRunIdentity)
        (sourceProviderRun: ProviderRunIdentity)
        : StrengthDecisionId =
        String.concat
            ""
            [ "strength-delegation-v1"
              string (DelegationContractRevisions.value contractRevision)
              LogicalRunId.value owner.LogicalRunId
              AuthorityRootUserMessageId.value owner.AuthorityRootUserMessageId
              ProviderRunIdentity.value sourceProviderRun ]
        |> sha256
        |> StrengthDecisionId.create

    let sameRequest (left: DelegationRequest) (right: DelegationRequest) = left = right

    let sameBinding (left: DelegationBinding) (right: DelegationBinding) = left = right

    let request (value: DelegationRequest) = DelegationLifecycle.Requested value

    let decisionId (lifecycle: DelegationLifecycle) : StrengthDecisionId =
        match lifecycle with
        | DelegationLifecycle.Requested value -> value.DecisionId
        | DelegationLifecycle.Bound(value, _)
        | DelegationLifecycle.Prepared(value, _)
        | DelegationLifecycle.Promoted(value, _)
        | DelegationLifecycle.Traced(value, _)
        | DelegationLifecycle.Abandoned(value, _) -> value.DecisionId
        | DelegationLifecycle.Closed(value, _) -> value.DecisionId

    /// DELEGATE-6.2: only a still-unbound Requested may bind. An identical
    /// rebind is idempotent — at the fold and here in the lifecycle helper; a
    /// different child, or any state past Bound, is refused.
    let tryBind
        (lifecycle: DelegationLifecycle)
        (binding: DelegationBinding)
        : Result<DelegationLifecycle, DelegationTransitionError> =
        match lifecycle with
        | DelegationLifecycle.Requested value when binding.DecisionId = value.DecisionId ->
            Ok(DelegationLifecycle.Bound(value, binding))
        | DelegationLifecycle.Requested value -> Error(DelegationTransitionError.Conflict value.DecisionId)
        | DelegationLifecycle.Bound(_, current) when current = binding -> Ok lifecycle
        | _ -> Error(DelegationTransitionError.IllegalFrom(decisionId lifecycle))

    let tryPrepare (lifecycle: DelegationLifecycle) : Result<DelegationLifecycle, DelegationTransitionError> =
        match lifecycle with
        | DelegationLifecycle.Bound(value, binding) -> Ok(DelegationLifecycle.Prepared(value, binding))
        | _ -> Error(DelegationTransitionError.IllegalFrom(decisionId lifecycle))

    let tryPromote (lifecycle: DelegationLifecycle) : Result<DelegationLifecycle, DelegationTransitionError> =
        match lifecycle with
        | DelegationLifecycle.Prepared(value, binding) -> Ok(DelegationLifecycle.Promoted(value, binding))
        | _ -> Error(DelegationTransitionError.IllegalFrom(decisionId lifecycle))

    let tryTrace (lifecycle: DelegationLifecycle) : Result<DelegationLifecycle, DelegationTransitionError> =
        match lifecycle with
        | DelegationLifecycle.Promoted(value, binding) -> Ok(DelegationLifecycle.Traced(value, binding))
        | _ -> Error(DelegationTransitionError.IllegalFrom(decisionId lifecycle))

    /// DELEGATE-6.2/6.3: closing is legal only from Requested or Bound, and the
    /// closed fact must name the predecessor it actually closes from.
    let tryClose
        (lifecycle: DelegationLifecycle)
        (closed: DelegationClosed)
        : Result<DelegationLifecycle, DelegationTransitionError> =
        let requestOf =
            function
            | DelegationLifecycle.Requested value -> Some value
            | DelegationLifecycle.Bound(value, _) -> Some value
            | DelegationLifecycle.Prepared(value, _)
            | DelegationLifecycle.Promoted(value, _)
            | DelegationLifecycle.Traced(value, _)
            | DelegationLifecycle.Abandoned(value, _)
            | DelegationLifecycle.Closed(value, _) -> Some value

        let expectedFrom =
            function
            | DelegationLifecycle.Requested _ -> Some DelegationClosedFrom.Requested
            | DelegationLifecycle.Bound _ -> Some DelegationClosedFrom.Bound
            | _ -> None

        match expectedFrom lifecycle with
        | Some expected when expected = closed.From ->
            Ok(DelegationLifecycle.Closed(Option.get (requestOf lifecycle), closed))
        | Some _ -> Error(DelegationTransitionError.Conflict closed.DecisionId)
        | None -> Error(DelegationTransitionError.IllegalFrom(decisionId lifecycle))

    let tryAbandon (lifecycle: DelegationLifecycle) : Result<DelegationLifecycle, DelegationTransitionError> =
        match lifecycle with
        | DelegationLifecycle.Prepared(value, binding) -> Ok(DelegationLifecycle.Abandoned(value, binding))
        | _ -> Error(DelegationTransitionError.IllegalFrom(decisionId lifecycle))
