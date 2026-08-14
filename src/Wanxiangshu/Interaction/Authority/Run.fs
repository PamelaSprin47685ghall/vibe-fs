namespace Wanxiangshu.Interaction.Authority
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Pure authority-run lifecycle transitions. Types and identity rules live in
/// PromptAuthority; this module owns claim / run / projection transitions only.
[<RequireQualifiedAccess>]
module PromptAuthorityRun =

    /// Create the Authority Root profile for a proven physical message.
    ///
    /// Takes a PhysicalUserMessageId and promotes it, because PROMPT-005 allows
    /// promotion only once `PhysicalAccepted` is established. The type signature
    /// therefore marks this as the one place that transition happens; a
    /// TransportReceipt cannot reach it at all.
    let createAuthorityRoot
        (sha256: string -> string)
        (runtimeId: RuntimeId)
        (sessionId: SessionId)
        (rootKind: PromptAuthority.RootAuthorityKind)
        (physicalMessageId: PhysicalUserMessageId)
        (selectedAgentName: string)
        : Result<PromptAuthority.AuthorityExecutionProfile, string> =
        match PromptAuthority.parseAgentName selectedAgentName with
        | Error error -> Error error
        | Ok(name, role, tier, peer) ->
            let authorityRoot = PhysicalUserMessageId.promoteToAuthorityRoot physicalMessageId

            Ok
                { SessionId = sessionId
                  LogicalRunId = PromptAuthority.stableLogicalRunId sha256 runtimeId sessionId authorityRoot
                  AuthorityRootUserMessageId = authorityRoot
                  AuthorityKind = rootKind
                  SelectedAgent = name
                  PeerAgent = peer
                  CanonicalRole = role
                  SelectedTier = tier }

    /// Claim a prompt that will become a new Authority Root (PROMPT-004
    /// AgentOwnerRoot).
    ///
    /// No LogicalRunId yet: the run's id derives from the physical message,
    /// which does not exist until the Host accepts. `None` says exactly that.
    let claimAgentOwnerRoot
        (key: PromptKey)
        (sessionId: SessionId)
        (payloadDigest: string)
        (selectedAgentName: string)
        : Result<PromptAuthority.PromptClaim, string> =
        match PromptAuthority.parseAgentName selectedAgentName with
        | Error error -> Error error
        | Ok(name, _role, _tier, _peer) ->
            Ok
                { PromptKey = key
                  SessionId = sessionId
                  Origin = PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                  LogicalRunId = None
                  AuthorityRootUserMessageId = None
                  EffectiveAgent = Some name
                  PayloadDigest = payloadDigest
                  Receipt = None
                  ClaimedAtRuntimeStartCount = 0 }

    /// Claim a continuation (PROMPT-003). It inherits the run and the root, and
    /// carries the EffectiveAgent the current fallback cursor selected.
    let claimContinuation
        (key: PromptKey)
        (sessionId: SessionId)
        (continuation: PromptAuthority.ContinuationKind)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        (effectiveAgent: string)
        (payloadDigest: string)
        : PromptAuthority.PromptClaim =
        { PromptKey = key
          SessionId = sessionId
          Origin = PromptAuthority.PromptOrigin.Continuation continuation
          LogicalRunId = Some profile.LogicalRunId
          AuthorityRootUserMessageId = Some profile.AuthorityRootUserMessageId
          EffectiveAgent = Some effectiveAgent
          PayloadDigest = payloadDigest
          Receipt = None
          ClaimedAtRuntimeStartCount = 0 }

    /// PROMPT-005 `Submitted`: the Host call returned a transport receipt.
    ///
    /// Recorded on the claim rather than discarded. PROMPT-011 has to tell "the Host
    /// accepted something we cannot locate" from "we never got that far", and those
    /// are different operator diagnoses even though both stay pending.
    let submitClaim
        (key: PromptKey)
        (receipt: TransportReceipt)
        (projection: PromptAuthority.PromptAuthorityProjection)
        =
        match Map.tryFind key projection.PendingClaims with
        | None -> projection
        | Some claim ->
            { projection with
                PendingClaims = Map.add key { claim with Receipt = Some receipt } projection.PendingClaims }

    /// A new Authority Root resets everything scoped to a Logical Run
    /// (PROMPT-002): continuation set, repair budget, and — via the caller's
    /// fallback projection — the cursor (FALLBACK-001).
    let registerAuthority
        (profile: PromptAuthority.AuthorityExecutionProfile)
        (projection: PromptAuthority.PromptAuthorityProjection)
        =
        { projection with
            LastAuthorityProfile = Some profile
            ActiveLogicalRun = Some profile
            PendingClaims = Map.empty
            AcceptedContinuationIds = Map.empty
            // PROMPT-011: ClaimSequence counts within one Logical Run, so a new
            // root restarts the count. This is also what bounds the map
            // (PERSIST-008) — it grows with distinct payloads in one run, not
            // with session lifetime.
            ClaimSequences = Map.empty }

    /// Register a claim and consume its ClaimSequence.
    ///
    /// PROMPT-011: the sequence must advance whether or not the claim later
    /// resolves. Advancing only on success would let an abandoned dispatch and its
    /// retry derive the same PromptKey, so recovery would find one metadata anchor
    /// for two distinct logical acts.
    let registerClaim (claim: PromptAuthority.PromptClaim) (projection: PromptAuthority.PromptAuthorityProjection) =
        let scope =
            PromptAuthority.claimScopeDigest claim.SessionId claim.LogicalRunId claim.Origin claim.PayloadDigest

        { projection with
            PendingClaims = Map.add claim.PromptKey claim projection.PendingClaims
            ClaimSequences =
                Map.add scope (PromptAuthority.nextClaimSequence scope projection) projection.ClaimSequences }

    /// PROMPT-005 `PhysicalAccepted`: a real Host message id resolved a claim.
    ///
    /// Only continuations are recorded. An Authority Root claim resolving does
    /// not belong in a continuation map, and recording the root a continuation
    /// belonged to is deliberately gone — that map existed solely to let
    /// ReviewWitness guess confirmation from a shared root, which REVIEW-003
    /// forbids. Review confirmation now requires the provider input seal
    /// (REVIEW-010), so no substitute lookup is provided here.
    let acceptClaim
        (key: PromptKey)
        (physicalMessageId: PhysicalUserMessageId)
        (projection: PromptAuthority.PromptAuthorityProjection)
        =
        match Map.tryFind key projection.PendingClaims with
        | None -> projection
        | Some claim ->
            let withoutClaim = Map.remove key projection.PendingClaims

            match claim.Origin with
            | PromptAuthority.PromptOrigin.Continuation continuation ->
                { projection with
                    PendingClaims = withoutClaim
                    AcceptedContinuationIds = Map.add physicalMessageId continuation projection.AcceptedContinuationIds }
            | PromptAuthority.PromptOrigin.AuthorityRoot _
            | PromptAuthority.PromptOrigin.HostInternal
            | PromptAuthority.PromptOrigin.UnknownOrigin ->
                { projection with
                    PendingClaims = withoutClaim }

    /// PROMPT-005 `Abandoned`. Must not change the Active Logical Run.
    let abandonClaim (key: PromptKey) (projection: PromptAuthority.PromptAuthorityProjection) =
        { projection with
            PendingClaims = Map.remove key projection.PendingClaims }

    /// PROMPT-009 resolution order, evaluated top to bottom:
    ///
    ///   accepted physical message id
    ///   → claimed PromptKey
    ///   → Host compaction / synthetic
    ///   → registered AgentOwnerRoot
    ///   → UnknownOrigin
    ///
    /// HumanRoot is absent on purpose: PROMPT-004 requires proven external
    /// acceptance carrying an explicit agent, which this pure function cannot
    /// observe. Anything unproven lands on UnknownOrigin and fails closed.
    let resolveKnownOrigin
        (physicalMessageId: PhysicalUserMessageId)
        (promptKey: PromptKey option)
        (hostCompaction: bool)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : PromptAuthority.PromptOrigin =
        match Map.tryFind physicalMessageId projection.AcceptedContinuationIds with
        | Some continuation -> PromptAuthority.PromptOrigin.Continuation continuation
        | None ->
            match promptKey |> Option.bind (fun key -> Map.tryFind key projection.PendingClaims) with
            | Some claim -> claim.Origin
            | None when hostCompaction -> PromptAuthority.PromptOrigin.HostInternal
            | None ->
                match promptKey, projection.ActiveLogicalRun with
                | Some _, Some { AuthorityKind = PromptAuthority.RootAuthorityKind.AgentOwnerRoot } ->
                    PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                | _ -> PromptAuthority.PromptOrigin.UnknownOrigin
