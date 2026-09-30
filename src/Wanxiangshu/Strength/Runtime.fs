namespace Wanxiangshu.Strength

open System.Collections.Generic
open Wanxiangshu.Foundation
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
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
      RequestedRounds: ReadonlyRoundBudget
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
    /// is required: the replica holds exactly one tool, and a wildcard allow
    /// would leave the owner role's broad agent permissions visible. The wildcard
    /// comes before the single specific allow so Host's last-match permission rule
    /// lets exactly `js-predictor` through and nothing else -- the replica has no
    /// read/glob/grep; all investigation goes through the readonly JS surface.
    let exactReadonlyHostToolMap = Map.ofList [ "*", false; "js-predictor", true ]

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

    /// DELEGATE-5.3: the single request-budget account, keyed by replica
    /// session. Every outbound request the registry admits counts against that
    /// replica's own RequestedRounds; no owner ever shares or inherits another
    /// owner's count, and a refused request consumes nothing.
    // DSL-MUTABLE: resource — admitted outbound request count per live replica
    let admittedRequests = Dictionary<string, int>()

    /// STRENGTH-004: the owner's resident read-only replica session. Unlike the
    /// per-decision binding index below, this survives decision boundaries: the
    /// owner keeps ONE predictor child for as long as it lives, so its provider
    /// session (and therefore its prefix cache) is reused instead of being
    /// recreated per delegation. Released only when the owner ends.
    // DSL-MUTABLE: resource — owner-to-resident-replica map, one entry per owner
    let residentByOwner = Dictionary<string, SessionId>()

    let tryConsumeBudget key (binding: StrengthReplicaBinding) =
        let used =
            match admittedRequests.TryGetValue key with
            | true, count -> count
            | false, _ -> 0

        if used < ReadonlyRoundBudget.value binding.RequestedRounds then
            admittedRequests.[key] <- used + 1
            true
        else
            false

    member _.TryFindResident(owner: SessionId) : SessionId option =
        lock gate (fun () ->
            match residentByOwner.TryGetValue(SessionId.value owner) with
            | true, replica -> Some replica
            | false, _ -> None)

    /// STRENGTH-004: bind the owner's resident replica session. A later bind
    /// replaces the slot: that is the recovery path where the previously recorded
    /// child is no longer listed by the Host, so the recorded id is stale rather
    /// than authoritative.
    member _.BindResident(owner: SessionId, replica: SessionId) =
        lock gate (fun () -> residentByOwner.[SessionId.value owner] <- replica)

    /// The owner ended: its resident replica is released and the slot cleared so
    /// the next owner of that id starts fresh.
    member _.ReleaseResident(owner: SessionId) : SessionId option =
        lock gate (fun () ->
            match residentByOwner.TryGetValue(SessionId.value owner) with
            | true, replica ->
                residentByOwner.Remove(SessionId.value owner) |> ignore
                Some replica
            | false, _ -> None)

    /// The resident replica itself was deleted: find its owner so that owner's
    /// slot is cleared too, instead of leaving a stale entry that a later
    /// decision would trust.
    member _.ReleaseResidentByReplica(replica: SessionId) : SessionId option =
        lock gate (fun () ->
            let target = SessionId.value replica

            let owner =
                residentByOwner
                |> Seq.tryPick (fun entry ->
                    if SessionId.value entry.Value = target then
                        Some entry.Key
                    else
                        None)

            match owner with
            | Some ownerKey ->
                residentByOwner.Remove ownerKey |> ignore
                Some(SessionId.create ownerKey)
            | None -> None)

    /// Process teardown: hand back every resident lease at once.
    member _.ReleaseAllResidents() : SessionId list =
        lock gate (fun () ->
            let all = residentByOwner.Values |> Seq.toList
            residentByOwner.Clear()
            all)

    member _.Register(binding: StrengthReplicaBinding) : Result<unit, StrengthRuntimeRegisterError> =
        lock gate (fun () ->
            let ownerKey = SessionId.value binding.OwnerSessionId
            let replicaKey = SessionId.value binding.ReplicaSessionId

            if ReadonlyRoundBudget.value binding.RequestedRounds = 0 then
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
                admittedRequests.[replicaKey] <- 0
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

    /// DELEGATE-5.3: the one request-budget account. Admits one more outbound
    /// request for this replica while its budget is not spent and consumes the
    /// round on success; a refused request consumes nothing. The account is per
    /// live binding, so per-owner isolation holds by construction.
    member _.TryAdmitRequest(replicaSessionId: SessionId) : bool =
        lock gate (fun () ->
            let key = SessionId.value replicaSessionId

            match byReplica.TryGetValue key with
            | false, _ -> false
            | true, binding -> tryConsumeBudget key binding)

    member _.Retire(replicaSessionId: SessionId) : StrengthReplicaBinding option =
        lock gate (fun () ->
            let replicaKey = SessionId.value replicaSessionId

            match byReplica.TryGetValue replicaKey with
            | false, _ -> None
            | true, binding ->
                byReplica.Remove replicaKey |> ignore
                byOwner.Remove(SessionId.value binding.OwnerSessionId) |> ignore
                admittedRequests.Remove replicaKey |> ignore
                Some binding)

    member _.Clear() =
        lock gate (fun () ->
            byOwner.Clear()
            byReplica.Clear()
            residentByOwner.Clear()
            admittedRequests.Clear())
