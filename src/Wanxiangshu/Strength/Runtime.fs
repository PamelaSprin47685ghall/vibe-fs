namespace Wanxiangshu.Strength

open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Collections.Generic
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// STRENGTH-004/014: decision-local physical state for a StrengthReplica leaf.
///
/// This is deliberately not durable lifecycle authority. Durable causality lives
/// in StrengthCandidatePrepared/Promoted/Traced EventStore facts; this registry
/// only answers which currently-live child is the request-scoped readonly leaf so
/// Host schema and execution gates can share the same ToolCapabilitySet.
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

    let capabilities (role: Role) =
        PromptAuthority.toolCapabilitiesFor role ProviderRequestKind.StrengthReplica

    /// Host PromptInput.tools becomes a session permission ruleset. `* = false`
    /// is required: merely enabling read/glob/grep would leave the role's broad
    /// agent permissions visible. Map ordering is canonical and puts the wildcard
    /// before the three specific allows, so Host's last-match permission rule lets
    /// only these tools through.
    let exactReadonlyHostToolMap =
        Map.ofList [ "*", false; "glob", true; "grep", true; "read", true ]

    let isExactReadonly (capabilities: Set<ToolPermission>) =
        capabilities = set [ ToolPermission.Read; ToolPermission.Glob; ToolPermission.Grep ]

/// Process-local single-flight registry. One owner may have at most one live
/// replica, and a replica session may belong to exactly one decision. Retire
/// removes both indexes atomically.
type StrengthRuntime() =
    let gate = obj ()
    /// DSL-cross-callback-proof: physical single-flight — live replica ownership index by owner
    // DSL-MUTABLE: resource — owner-to-replica binding map
    let byOwner = Dictionary<string, StrengthReplicaBinding>()
    /// DSL-cross-callback-proof: physical single-flight — reverse index for the same live replica capability
    // DSL-MUTABLE: resource — replica-to-binding map
    let byReplica = Dictionary<string, StrengthReplicaBinding>()

    member _.Register(binding: StrengthReplicaBinding) : Result<unit, StrengthRuntimeRegisterError> =
        lock gate (fun () ->
            let ownerKey = SessionId.value binding.OwnerSessionId
            let replicaKey = SessionId.value binding.ReplicaSessionId

            if binding.Budget = StrengthBudget.K0 then
                Error StrengthRuntimeRegisterError.EmptyBudget
            elif not (StrengthReplicaTools.isExactReadonly binding.ToolCapabilitySet) then
                Error(StrengthRuntimeRegisterError.RoleIneligible binding.CanonicalRole)
            elif byOwner.ContainsKey ownerKey then
                Error(StrengthRuntimeRegisterError.OwnerAlreadyHasReplica binding.OwnerSessionId)
            elif byReplica.ContainsKey replicaKey then
                Error(StrengthRuntimeRegisterError.ReplicaAlreadyBound binding.ReplicaSessionId)
            else
                byOwner.[ownerKey] <- binding
                byReplica.[replicaKey] <- binding
                Ok())

    member _.TryFindByOwner(ownerSessionId: SessionId) : StrengthReplicaBinding option =
        lock gate (fun () ->
            match byOwner.TryGetValue(SessionId.value ownerSessionId) with
            | true, binding -> Some binding
            | false, _ -> None)

    member _.TryFindByReplica(replicaSessionId: SessionId) : StrengthReplicaBinding option =
        lock gate (fun () ->
            match byReplica.TryGetValue(SessionId.value replicaSessionId) with
            | true, binding -> Some binding
            | false, _ -> None)

    member this.TryCapabilities(replicaSessionId: SessionId) : Set<ToolPermission> option =
        this.TryFindByReplica replicaSessionId
        |> Option.map (fun binding -> binding.ToolCapabilitySet)

    member _.Retire(replicaSessionId: SessionId) : StrengthReplicaBinding option =
        lock gate (fun () ->
            let replicaKey = SessionId.value replicaSessionId

            match byReplica.TryGetValue replicaKey with
            | false, _ -> None
            | true, binding ->
                byReplica.Remove replicaKey |> ignore
                byOwner.Remove(SessionId.value binding.OwnerSessionId) |> ignore
                Some binding)

    member _.Clear() =
        lock gate (fun () ->
            byOwner.Clear()
            byReplica.Clear())
