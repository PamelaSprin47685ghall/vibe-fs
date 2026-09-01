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
      Budget: StrengthBudget
      MaxFrameBytes: int
      SemanticDigest: string
      LocalizedMirrorMessages: ProviderProjection.WireMessage list
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
    member TryFindByOwner: ownerSessionId: SessionId -> StrengthReplicaBinding option
    member TryFindByReplica: replicaSessionId: SessionId -> StrengthReplicaBinding option
    member TryCapabilities: replicaSessionId: SessionId -> Set<ToolPermission> option
    member Retire: replicaSessionId: SessionId -> StrengthReplicaBinding option
    member Clear: unit -> unit
