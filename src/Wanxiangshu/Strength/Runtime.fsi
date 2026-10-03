namespace Wanxiangshu.Strength

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection

type StrengthReplicaBinding =
    { OwnerSessionId: SessionId
      ReplicaSessionId: SessionId
      DecisionId: StrengthDecisionId
      TargetProviderRun: ProviderRunIdentity
      CanonicalRole: Role
      RequestedRounds: ReadonlyRoundBudget
      SemanticDigest: string
      LocalizedMirrorMessages: ProviderProjection.WireMessage list
      SynchronizedTextMessages: Set<int>
      ToolCapabilitySet: Set<ToolPermission> }

[<RequireQualifiedAccess>]
type StrengthRuntimeRegisterError =
    | OwnerAlreadyHasReplica of ownerSessionId: SessionId
    | ReplicaAlreadyBound of replicaSessionId: SessionId
    | RoleIneligible of role: Role
    | EmptyBudget

[<RequireQualifiedAccess>]
module StrengthReplicaTools =
    val capabilities: role: Role -> Set<ToolPermission>
    val exactReadonlyHostToolMap: Map<string, bool>
    val isExactReadonly: capabilities: Set<ToolPermission> -> bool

type StrengthRuntime =
    new: unit -> StrengthRuntime
    member Register: binding: StrengthReplicaBinding -> Result<unit, StrengthRuntimeRegisterError>
    /// STRENGTH-004: the owner's resident replica session, reused across decisions
    /// and released only when the owner ends.
    member TryFindResident: owner: SessionId -> SessionId option
    member IsResidentSession: replica: SessionId -> bool
    member BindResident: owner: SessionId * replica: SessionId -> unit
    member ReleaseResident: owner: SessionId -> SessionId option
    member ReleaseResidentByReplica: replica: SessionId -> SessionId option
    member ReleaseAllResidents: unit -> (SessionId * SessionId) list
    member TryFindByOwner: ownerSessionId: SessionId -> StrengthReplicaBinding option
    member TryFindByReplica: replicaSessionId: SessionId -> StrengthReplicaBinding option
    member TryCapabilities: replicaSessionId: SessionId -> Set<ToolPermission> option
    member TryAdmitRequest: replicaSessionId: SessionId -> bool
    member Retire: replicaSessionId: SessionId -> StrengthReplicaBinding option
    member Clear: unit -> unit
