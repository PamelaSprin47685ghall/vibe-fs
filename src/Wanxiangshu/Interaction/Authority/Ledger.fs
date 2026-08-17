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
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
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
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Fission

/// Prompt Authority folds (docs/what/prompt.md).
///
/// Each fold takes the fact payload directly. The previous version accepted
/// anonymous records of strings that mirrored the fact shape and re-parsed every
/// identity, which let the fold disagree with the fact about what a field meant —
/// and it did: an unparseable AuthorityKind defaulted to HumanRoot, silently
/// granting human authority to an agent-owned root.
module PromptAuthorityLedger =

    let empty = PromptAuthority.empty

    /// PROMPT-004 has exactly two root kinds. An unrecognised label is NOT a
    /// HumanRoot by default: HOST-001 requires fail-closed, and HumanRoot is the
    /// most privileged value in this domain.
    let private tryParseAuthorityKind (value: string) =
        match value with
        | "HumanRoot" -> Some PromptAuthority.RootAuthorityKind.HumanRoot
        | "AgentOwnerRoot" -> Some PromptAuthority.RootAuthorityKind.AgentOwnerRoot
        | _ -> None

    /// The recorded peer wins when present; blank facts fall back to the
    /// parser-proven peer without inferring anything from the role.
    let private recordedPeerOrDerived (recorded: string) (derived: string) =
        if System.String.IsNullOrWhiteSpace recorded then derived else recorded

    /// PROMPT-002: an Authority Root took effect and fixed the profile.
    ///
    /// An uninterpretable fact leaves the projection alone. A fact naming an
    /// illegal agent (AGENT-004) or an unknown authority kind is not evidence of
    /// authority, and building a profile from it would hand the run to whatever
    /// the defaults happened to be.
    let foldAuthorityRootAccepted
        (projection: PromptAuthority.PromptAuthorityProjection)
        (fact:
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               AuthorityKind: string
               SelectedAgent: string
               PeerAgent: string
               CanonicalRole: string
               SelectedTier: string |})
        =
        match tryParseAuthorityKind fact.AuthorityKind, PromptAuthority.parseAgentName fact.SelectedAgent with
        | None, _ -> projection
        | _, Error _ -> projection
        | Some authorityKind, Ok(name, role, tier, derivedPeer) ->
            // The recorded peer wins when present. AGENT-003 requires the pair to
            // be proven during config validation, and the fact preserves what was
            // proven then; deriving it here would silently repair a journal
            // written under a different config.
            let peerAgent = recordedPeerOrDerived fact.PeerAgent derivedPeer

            let profile: PromptAuthority.AuthorityExecutionProfile =
                { SessionId = fact.SessionId
                  LogicalRunId = fact.LogicalRunId
                  AuthorityRootUserMessageId = fact.AuthorityRootUserMessageId
                  AuthorityKind = authorityKind
                  SelectedAgent = name
                  PeerAgent = peerAgent
                  CanonicalRole = role
                  SelectedTier = tier }

            PromptAuthorityRun.registerAuthority profile projection

    /// The named Logical Run reached a durable terminal boundary.
    /// FINALITY-022 / INTERACTION-AUTHORITY-018: a completed HumanRoot Manager
    /// Life releases only the active HumanRoot authority. AgentOwnerRoot sessions
    /// may continue owner-directed post-Life work (for example publish-conflict
    /// resumption), so their authority lifetime is not derived from LifeCompleted.
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

    /// PROMPT-003: was this physical message accepted as a continuation, and of
    /// what kind.
    ///
    /// Requires the SessionId. The previous version searched every session with
    /// `Map.tryPick`, which violates PERSIST-008 and is also wrong in principle:
    /// a message id belongs to exactly one session, so a hit under a different
    /// one would be a bug the scan silently tolerated.
    let acceptedContinuation
        (sessionId: SessionId)
        (physicalMessageId: PhysicalUserMessageId)
        (agentProjections: AgentProjectionSet)
        =
        projectionFor sessionId agentProjections
        |> Option.bind (fun authority -> Map.tryFind physicalMessageId authority.AcceptedContinuationIds)

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

    let dispatchStatusFor
        (sessionId: SessionId)
        (payloadDigest: string)
        (agentProjections: AgentProjectionSet)
        : DispatchStatus =
        let key = PromptAuthority.acceptedDispatchKey sessionId payloadDigest

        match projectionFor sessionId agentProjections with
        | Some authority when Map.containsKey key authority.AcceptedDispatches ->
            DispatchStatus.Accepted(Map.find key authority.AcceptedDispatches)
        | Some authority when
            authority.PendingClaims
            |> Seq.exists (fun (KeyValue(_, claim)) ->
                claim.SessionId = sessionId && claim.PayloadDigest = payloadDigest)
            ->
            DispatchStatus.Pending
        | _ -> DispatchStatus.Dispatchable
