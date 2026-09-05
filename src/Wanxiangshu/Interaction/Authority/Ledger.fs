namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Prompt Authority folds (docs/what/prompt.md).
///
/// Each fold takes the fact payload directly. The previous version accepted
/// anonymous records of strings that mirrored the fact shape and re-parsed every
/// identity, which let the fold disagree with the fact about what a field meant —
/// and it did: an unparseable AuthorityKind defaulted to HumanRoot, silently
/// granting human authority to an agent-owned root.
module PromptAuthorityLedger =

    let empty = PromptAuthority.empty

    let private parseAuthorityKind (value: string) =
        match value with
        | "HumanRoot" -> Ok PromptAuthority.RootAuthorityKind.HumanRoot
        | "AgentOwnerRoot" -> Ok PromptAuthority.RootAuthorityKind.AgentOwnerRoot
        | unknown -> Error(sprintf "unknown authority root kind: %s" unknown)

    let foldAuthorityRootAccepted
        (projection: PromptAuthority.PromptAuthorityProjection)
        (payload: AuthorityRootAcceptedPayload)
        : Result<PromptAuthority.PromptAuthorityProjection, string> =
        if payload.SchemaVersion <> 2 then
            Error(sprintf "unsupported AuthorityRootAccepted schema version: %d" payload.SchemaVersion)
        else
            parseAuthorityKind payload.AuthorityKind
            |> Result.bind (fun authorityKind ->
                PromptAuthority.createAuthorityExecutionProfileFromSeed
                    payload.SessionId
                    payload.LogicalRunId
                    payload.AuthorityRootUserMessageId
                    authorityKind
                    payload.IdentitySeed)
            |> Result.bind (fun profile ->
                PromptAuthorityRun.registerAuthority profile projection
                |> Result.mapError PromptAuthorityRun.describeRegistrationRejection)

    /// Road completion closes HumanRoot authority; AgentOwnerRoot closure belongs to its owner.
    let closeCompletedHumanRootManager
        (projection: PromptAuthority.PromptAuthorityProjection)
        : PromptAuthority.PromptAuthorityProjection =
        match projection.ActiveLogicalRun with
        | Some profile when
            profile.AuthorityKind = PromptAuthority.RootAuthorityKind.HumanRoot
            && profile.CanonicalRole = Role.Manager
            ->
            { projection with
                ActiveLogicalRun = None
                PendingClaims = Map.empty
                AcceptedContinuationIds = Map.empty
                ClaimSequences = Map.empty }
        | _ -> projection

    /// PROMPT-005 `Claimed`.
    let foldPromptClaimed
        (runtimeStartCount: int)
        (projection: PromptAuthority.PromptAuthorityProjection)
        (fact:
            {| PromptKey: PromptKey
               SessionId: SessionId
               ContinuationKind: string
               LogicalRunId: LogicalRunId option
               AuthorityRootUserMessageId: AuthorityRootUserMessageId option
               EffectiveAgent: string option
               IdentitySeed: PromptIdentitySeed
               PayloadDigest: string |})
        =
        let origin =
            if fact.ContinuationKind = "AgentOwnerRoot" then
                Some(PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot)
            else
                PromptAuthority.tryParseContinuationKind fact.ContinuationKind
                |> Option.map PromptAuthority.PromptOrigin.Continuation

        match origin with
        // PROMPT-004: UnknownOrigin fails closed. A registered claim with an
        // unrecognised origin would later resolve into whatever the resolution
        // order defaults to.
        | None -> projection
        | Some resolvedOrigin ->
            let claim: PromptAuthority.PromptClaim =
                { PromptKey = fact.PromptKey
                  SessionId = fact.SessionId
                  Origin = resolvedOrigin
                  LogicalRunId = fact.LogicalRunId
                  AuthorityRootUserMessageId = fact.AuthorityRootUserMessageId
                  EffectiveAgent = fact.EffectiveAgent
                  IdentitySeed = fact.IdentitySeed
                  PayloadDigest = fact.PayloadDigest
                  // PROMPT-005: `Claimed` precedes the Host call, so no transport
                  // receipt can exist yet. `foldPromptSubmitted` attaches it.
                  Receipt = None
                  // PROMPT-011: watermark of the workspace RuntimeStartCount.
                  // Subsequent RuntimeStarted envelopes only advance that counter.
                  ClaimedAtRuntimeStartCount = runtimeStartCount }

            PromptAuthorityRun.registerClaim claim projection

    /// PROMPT-005 `Submitted`.
    ///
    /// The claim stays pending: a transport receipt is not physical acceptance,
    /// and PROMPT-011 needs the claim resolvable after a restart. The receipt is
    /// durable in the journal; the projection only needs to know the claim is
    /// still open, which it already does. Present as a named no-op so the fold's
    /// four-fact coverage is visible rather than inferred from an absence.
    /// PROMPT-005 `Submitted`: the Host call returned a transport receipt.
    ///
    /// The receipt lands on the pending claim. It used to be discarded (`_fact ->
    /// projection`), which erased the distinction PROMPT-011 step 4 vs step 5 is
    /// built on: "the Host accepted something we cannot locate" and "we never got a
    /// receipt at all" both stay pending, but only the first means a logical effect
    /// may already exist.
    let foldPromptSubmitted
        (projection: PromptAuthority.PromptAuthorityProjection)
        (fact:
            {| PromptKey: PromptKey
               SessionId: SessionId
               Receipt: TransportReceipt |})
        =
        PromptAuthorityRun.submitClaim fact.PromptKey fact.Receipt projection

    /// PROMPT-005 `PhysicalAccepted`: a real physical message resolved the claim.
    let foldPromptPhysicalAccepted
        (projection: PromptAuthority.PromptAuthorityProjection)
        (fact:
            {| PromptKey: PromptKey
               SessionId: SessionId
               PhysicalUserMessageId: PhysicalUserMessageId |})
        =
        PromptAuthorityRun.acceptClaim fact.PromptKey fact.PhysicalUserMessageId projection

    /// PROMPT-005 `Abandoned`. Must not change the Active Logical Run.
    ///
    /// `Reason` is part of the fact's shape, so it appears here even though the
    /// projection does not branch on it: PROMPT-005 keeps the reason on the one
    /// `Abandoned` fact instead of splitting it into a fifth fact name, and an
    /// anonymous record that omitted the field would not accept the payload.
    let foldPromptAbandoned
        (projection: PromptAuthority.PromptAuthorityProjection)
        (fact:
            {| PromptKey: PromptKey
               SessionId: SessionId
               Reason: PromptAbandonReason |})
        =
        PromptAuthorityRun.abandonClaim fact.PromptKey projection

    // ── queries ─────────────────────────────────────────────────────────────
    // PERSIST-008: every lookup is keyed by SessionId. Nothing scans all
    // sessions.

    let projectionFor (sessionId: SessionId) (agentProjections: AgentProjectionSet) =
        Map.tryFind sessionId agentProjections.Sessions
        |> Option.bind (fun session -> session.PromptAuthority)

    let private profileOwner (sessionId: SessionId) (agentProjections: AgentProjectionSet) =
        FissionProjection.tryOwnerOfLane sessionId agentProjections.Fission
        |> Option.defaultValue sessionId

    let activeProfile (sessionId: SessionId) (agentProjections: AgentProjectionSet) =
        projectionFor (profileOwner sessionId agentProjections) agentProjections
        |> Option.bind (fun authority -> authority.ActiveLogicalRun)

    let lastAuthorityProfile (sessionId: SessionId) (agentProjections: AgentProjectionSet) =
        projectionFor (profileOwner sessionId agentProjections) agentProjections
        |> Option.bind (fun authority -> authority.LastAuthorityProfile)

    let pendingClaim (sessionId: SessionId) (promptKey: PromptKey) (agentProjections: AgentProjectionSet) =
        projectionFor sessionId agentProjections
        |> Option.bind (fun authority -> Map.tryFind promptKey authority.PendingClaims)

    /// The durable outcome of one logical dispatch, for resend admission.
    ///
    /// REVIEW-013/018 (process-review assignment reentry): `Accepted` means the
    /// payload physically landed and must not be sent again; `Pending` means a
    /// claim exists whose outcome is undetermined — recovery owns it, never a
    /// blind resend; `Dispatchable` means no logical dispatch (or an explicitly
    /// Abandoned one), so a new claim is allowed. Read from projection evidence;
    /// the caller never scans the Journal.
    [<RequireQualifiedAccess>]
    type DispatchStatus =
        | Accepted of evidence: PromptAuthority.AcceptedDispatch
        | Pending
        | Dispatchable

    let pendingDispatchClaim
        (sessionId: SessionId)
        (payloadDigest: string)
        (agentProjections: AgentProjectionSet)
        : PromptAuthority.PromptClaim option =
        projectionFor sessionId agentProjections
        |> Option.bind (fun authority ->
            authority.PendingClaims
            |> Seq.tryPick (fun (KeyValue(_, claim)) ->
                if claim.SessionId = sessionId && claim.PayloadDigest = payloadDigest then
                    Some claim
                else
                    None))

    let acceptedDispatchForPhysicalMessage
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (agentProjections: AgentProjectionSet)
        : PromptAuthority.AcceptedDispatch option =
        projectionFor sessionId agentProjections
        |> Option.bind (fun authority ->
            authority.AcceptedDispatches
            |> Seq.tryPick (fun (KeyValue(_, dispatch)) ->
                if dispatch.PhysicalUserMessageId = physicalUserMessageId then
                    Some dispatch
                else
                    None))

    let dispatchStatusFor
        (sessionId: SessionId)
        (payloadDigest: string)
        (agentProjections: AgentProjectionSet)
        : DispatchStatus =
        let key = PromptAuthority.acceptedDispatchKey sessionId payloadDigest

        match projectionFor sessionId agentProjections with
        | Some authority when Map.containsKey key authority.AcceptedDispatches ->
            DispatchStatus.Accepted(Map.find key authority.AcceptedDispatches)
        | _ when pendingDispatchClaim sessionId payloadDigest agentProjections |> Option.isSome ->
            DispatchStatus.Pending
        | _ -> DispatchStatus.Dispatchable
