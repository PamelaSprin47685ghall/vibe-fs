namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Participant.Persona
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
        (identitySeed: PromptAuthority.IdentitySeed)
        : Result<PromptAuthority.AuthorityExecutionProfile, string> =
        let authorityRoot = PhysicalUserMessageId.promoteToAuthorityRoot physicalMessageId

        PromptAuthority.createAuthorityExecutionProfileFromSeed
            sessionId
            (PromptAuthority.stableLogicalRunId sha256 runtimeId sessionId authorityRoot)
            authorityRoot
            rootKind
            identitySeed

    let private requireInheritedOwnerIdentity identitySeed =
        match identitySeed with
        | PromptAuthority.IdentitySeed.RootSelection _ ->
            Error "AgentOwnerRoot requires an inherited owner identity seed"
        | PromptAuthority.IdentitySeed.InheritedFromOwner _ ->
            Ok(PromptAuthority.identitySeedParticipantIdentity identitySeed)

    let private admitPublicRole participantIdentity =
        match ParticipantIdentity.role participantIdentity with
        | None -> Error "public authority participant identity cannot be Bookkeeper"
        | Some _ -> Ok participantIdentity

    /// Claim a prompt that will become a new Authority Root (PROMPT-004
    /// AgentOwnerRoot).
    ///
    /// No LogicalRunId yet: the run's id derives from the physical message,
    /// which does not exist until the Host accepts. `None` says exactly that.
    let claimAgentOwnerRoot
        (key: PromptKey)
        (sessionId: SessionId)
        (payloadDigest: string)
        (identitySeed: PromptAuthority.IdentitySeed)
        : Result<PromptAuthority.PromptClaim, string> =
        requireInheritedOwnerIdentity identitySeed
        |> Result.bind admitPublicRole
        |> Result.map (fun participantIdentity ->
            { PromptKey = key
              SessionId = sessionId
              Origin = PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot
              LogicalRunId = None
              AuthorityRootUserMessageId = None
              EffectiveAgent = Some(ParticipantIdentity.selectedAgent participantIdentity)
              IdentitySeed = identitySeed
              PayloadDigest = payloadDigest
              Receipt = None
              ClaimedAtRuntimeStartCount = 0 })

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
          IdentitySeed = profile.IdentitySeed
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

    /// An exact duplicate root is idempotent. A fresh root is admitted only
    /// after the prior logical run has released the active binding.
    type AuthorityRegistrationRejection =
        | ActiveRunIdentityConflict of
            active: PromptAuthority.AuthorityExecutionProfile *
            requested: PromptAuthority.AuthorityExecutionProfile

    let describeRegistrationRejection =
        function
        | ActiveRunIdentityConflict(active, requested) ->
            sprintf
                "active logical run must close before replacement: active=%s requested=%s"
                (LogicalRunId.value active.LogicalRunId)
                (LogicalRunId.value requested.LogicalRunId)

    let private isExactDurableRootReplay
        (active: PromptAuthority.AuthorityExecutionProfile)
        (requested: PromptAuthority.AuthorityExecutionProfile)
        =
        active.SessionId = requested.SessionId
        && active.AuthorityRootUserMessageId = requested.AuthorityRootUserMessageId
        && active.AuthorityKind = requested.AuthorityKind
        && active.IdentitySeed = requested.IdentitySeed

    let resolveAuthorityProfile
        (requested: PromptAuthority.AuthorityExecutionProfile)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : Result<PromptAuthority.AuthorityExecutionProfile, AuthorityRegistrationRejection> =
        match projection.ActiveLogicalRun with
        | Some active when active = requested || isExactDurableRootReplay active requested -> Ok active
        | Some active -> Error(ActiveRunIdentityConflict(active, requested))
        | None -> Ok requested

    let registerAuthority
        (profile: PromptAuthority.AuthorityExecutionProfile)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : Result<PromptAuthority.PromptAuthorityProjection, AuthorityRegistrationRejection> =
        resolveAuthorityProfile profile projection
        |> Result.map (fun canonical ->
            match projection.ActiveLogicalRun with
            | Some _ -> projection
            | None ->
                { projection with
                    LastAuthorityProfile = Some canonical
                    ActiveLogicalRun = Some canonical
                    PendingClaims = Map.empty
                    AcceptedContinuationIds = Map.empty
                    // PROMPT-011: ClaimSequence counts within one Logical Run, so a new
                    // root restarts the count. This is also what bounds the map
                    // (PERSIST-008) — it grows with distinct payloads in one run, not
                    // with session lifetime.
                    ClaimSequences = Map.empty })

    /// Close exactly the active Logical Run named by durable terminal evidence.
    /// Run-scoped continuation resources are discarded; LastAuthorityProfile is
    /// intentionally retained as history but may never substitute for ActiveLogicalRun.
    let closeAuthority
        (logicalRunId: LogicalRunId)
        (authorityRoot: AuthorityRootUserMessageId)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : Result<PromptAuthority.PromptAuthorityProjection, string> =
        let sameRun (profile: PromptAuthority.AuthorityExecutionProfile) =
            profile.LogicalRunId = logicalRunId
            && profile.AuthorityRootUserMessageId = authorityRoot

        match projection.ActiveLogicalRun with
        | Some active when sameRun active ->
            Ok
                { projection with
                    ActiveLogicalRun = None
                    PendingClaims = Map.empty
                    AcceptedContinuationIds = Map.empty
                    ClaimSequences = Map.empty }
        | None when projection.LastAuthorityProfile |> Option.exists sameRun -> Ok projection
        | Some active ->
            Error(
                sprintf
                    "logical-run close mismatch: active=%s requested=%s"
                    (LogicalRunId.value active.LogicalRunId)
                    (LogicalRunId.value logicalRunId)
            )
        | None -> Error(sprintf "logical-run close has no matching authority: %s" (LogicalRunId.value logicalRunId))

    let closeCompletedAgentOwnerChildWork
        (logicalRunId: LogicalRunId)
        (authorityRoot: AuthorityRootUserMessageId)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : Result<PromptAuthority.PromptAuthorityProjection, string> =
        match projection.ActiveLogicalRun with
        | Some active when
            active.AuthorityKind = PromptAuthority.RootAuthorityKind.AgentOwnerRoot
            && active.CanonicalRole <> Role.Manager
            ->
            closeAuthority logicalRunId authorityRoot projection
        | Some _ -> Error "child-work closure requires a non-Manager AgentOwnerRoot authority"
        | None -> closeAuthority logicalRunId authorityRoot projection

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

    let private acceptedContinuationEvidence
        (physicalMessageId: PhysicalUserMessageId)
        (origin: PromptAuthority.PromptOrigin)
        (projection: PromptAuthority.PromptAuthorityProjection)
        =
        match origin with
        | PromptAuthority.PromptOrigin.Continuation continuation ->
            { projection with
                AcceptedContinuationIds = Map.add physicalMessageId continuation projection.AcceptedContinuationIds }
        | PromptAuthority.PromptOrigin.AuthorityRoot _
        | PromptAuthority.PromptOrigin.HostInternal
        | PromptAuthority.PromptOrigin.UnknownOrigin -> projection

    /// PROMPT-005 `PhysicalAccepted`: a real Host message id resolved a claim.
    ///
    /// The landing is kept as typed `AcceptedDispatch` evidence keyed by
    /// (session, payload digest) — business layers ask `dispatchStatusFor`
    /// instead of scanning the Journal. Only continuations additionally enter
    /// `AcceptedContinuationIds`: an Authority Root claim resolving does not
    /// belong in a continuation map, and recording the root a continuation
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

            let landed: PromptAuthority.AcceptedDispatch =
                { PromptKey = claim.PromptKey
                  SessionId = claim.SessionId
                  Origin = claim.Origin
                  IdentitySeed = claim.IdentitySeed
                  PayloadDigest = claim.PayloadDigest
                  PhysicalUserMessageId = physicalMessageId }

            let withEvidence =
                { projection with
                    PendingClaims = withoutClaim
                    AcceptedDispatches =
                        Map.add
                            (PromptAuthority.acceptedDispatchKey claim.SessionId claim.PayloadDigest)
                            landed
                            projection.AcceptedDispatches }

            acceptedContinuationEvidence physicalMessageId claim.Origin withEvidence

    /// PROMPT-005 `Abandoned`. Must not change the Active Logical Run.
    ///
    /// An explicit abandon of a still-pending claim also drops that payload's
    /// landing evidence slot, so a later reentry may claim the same logical
    /// dispatch again. An already-accepted claim is not in `PendingClaims`, so
    /// its evidence survives — abandonment never erases a physical landing.
    let abandonClaim (key: PromptKey) (projection: PromptAuthority.PromptAuthorityProjection) =
        match Map.tryFind key projection.PendingClaims with
        | None -> projection
        | Some claim ->
            { projection with
                PendingClaims = Map.remove key projection.PendingClaims
                AcceptedDispatches =
                    Map.remove
                        (PromptAuthority.acceptedDispatchKey claim.SessionId claim.PayloadDigest)
                        projection.AcceptedDispatches }

    /// Resolve every origin other than an already-accepted continuation. The
    /// tuple makes the precedence visible while keeping unknown input fail-closed.
    let private resolveUnacceptedOrigin
        (promptKey: PromptKey option)
        (hostCompaction: bool)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : PromptAuthority.PromptOrigin =
        let pending =
            promptKey |> Option.bind (fun key -> Map.tryFind key projection.PendingClaims)

        match pending, promptKey, projection.ActiveLogicalRun, hostCompaction with
        | Some claim, _, _, _ -> claim.Origin
        | None, _, _, true -> PromptAuthority.PromptOrigin.HostInternal
        | None, Some _, Some profile, _ when profile.AuthorityKind = PromptAuthority.RootAuthorityKind.AgentOwnerRoot ->
            PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot
        | _ -> PromptAuthority.PromptOrigin.UnknownOrigin

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
        | None -> resolveUnacceptedOrigin promptKey hostCompaction projection
