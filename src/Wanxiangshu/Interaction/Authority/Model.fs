namespace Wanxiangshu.Interaction.Authority

open System
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module PromptAuthority =

    type RootAuthorityKind = PromptRootAuthorityKind
    type ContinuationKind = PromptContinuationKind
    type PromptOrigin = Wanxiangshu.Interaction.Authority.PromptOrigin

    type IdentitySeed = PromptIdentitySeed
    type IdentitySeedInput = PromptIdentitySeedInput
    type IdentitySeedValidationError = PromptIdentitySeedValidationError

    /// What an Authority Root fixes for the whole Logical Run (PROMPT-002).
    ///
    /// FALLBACK-004: SelectedAgent, PeerAgent, and CanonicalRole
    /// never change here. Fallback moves EffectiveAgent, which lives on the
    /// per-attempt profile instead — that separation is the clause.
    ///
    /// PROMPT-002 also forbids a model id: there is deliberately no field for
    /// one, so "Authority Root overrides the model" is not expressible.
    type AuthorityExecutionProfile =
        private
            { StoredSessionId: SessionId
              StoredLogicalRunId: LogicalRunId
              StoredAuthorityRootUserMessageId: AuthorityRootUserMessageId
              StoredAuthorityKind: RootAuthorityKind
              StoredIdentitySeed: IdentitySeed }

        member this.SessionId = this.StoredSessionId
        member this.LogicalRunId = this.StoredLogicalRunId
        member this.AuthorityRootUserMessageId = this.StoredAuthorityRootUserMessageId
        member this.AuthorityKind = this.StoredAuthorityKind
        member this.IdentitySeed = this.StoredIdentitySeed

        member this.ParticipantIdentity =
            match this.StoredIdentitySeed with
            | RootSelection identity -> identity
            | InheritedFromOwner witness -> witness.ParticipantIdentity

        member this.SelectedAgent = ParticipantIdentity.selectedAgent this.ParticipantIdentity
        member this.PeerAgent = ParticipantIdentity.peerAgent this.ParticipantIdentity

        member this.CanonicalRole =
            match ParticipantIdentity.role this.ParticipantIdentity with
            | Some role -> role
            | None -> invalidOp "public authority participant identity cannot be Bookkeeper"

        member this.Persona = ParticipantIdentity.persona this.ParticipantIdentity

        member this.PersonaCatalogVersion =
            ParticipantIdentity.personaCatalogVersion this.ParticipantIdentity

        member this.PersonaOrigin = ParticipantIdentity.origin this.ParticipantIdentity

    let identitySeedParticipantIdentity = PromptIdentitySeed.participantIdentity

    let identitySeedOwner = PromptIdentitySeed.owner

    let issueInheritedIdentitySeed
        (canonicalChildName: string)
        (owner: AuthorityExecutionProfile)
        : Result<IdentitySeed, ParticipantIdentityError> =
        PromptIdentitySeed.inheritFromOwner
            canonicalChildName
            owner.SessionId
            owner.LogicalRunId
            owner.AuthorityRootUserMessageId
            owner.ParticipantIdentity

    let validateInheritedIdentitySeedAgainstActiveOwner
        (ownerOption: AuthorityExecutionProfile option)
        (seed: IdentitySeed)
        : Result<ParticipantIdentityEvidence, IdentitySeedValidationError> =
        match ownerOption, seed with
        | _, RootSelection _ -> Error ExpectedInheritedFromOwner
        | None, InheritedFromOwner witness -> Error(OwnerAuthorityNotActive witness.OwnerSessionId)
        | Some owner, InheritedFromOwner witness when witness.OwnerSessionId <> owner.SessionId ->
            Error(OwnerSessionIdMismatch(owner.SessionId, witness.OwnerSessionId))
        | Some owner, InheritedFromOwner witness when witness.OwnerLogicalRunId <> owner.LogicalRunId ->
            Error(OwnerLogicalRunIdMismatch(owner.LogicalRunId, witness.OwnerLogicalRunId))
        | Some owner, InheritedFromOwner witness when
            witness.OwnerAuthorityRootUserMessageId <> owner.AuthorityRootUserMessageId
            ->
            Error(
                OwnerAuthorityRootUserMessageIdMismatch(
                    owner.AuthorityRootUserMessageId,
                    witness.OwnerAuthorityRootUserMessageId
                )
            )
        | Some owner, InheritedFromOwner witness ->
            ParticipantIdentity.rehydrate
                (Some owner.ParticipantIdentity)
                (ParticipantIdentity.toInput witness.ParticipantIdentity)
            |> Result.mapError InvalidInheritedParticipantIdentity
            |> Result.map (fun _ -> witness.ParticipantIdentity)

    let validateInheritedIdentitySeed owner seed =
        validateInheritedIdentitySeedAgainstActiveOwner (Some owner) seed

    let createAuthorityExecutionProfileFromSeed
        (sessionId: SessionId)
        (logicalRunId: LogicalRunId)
        (authorityRootUserMessageId: AuthorityRootUserMessageId)
        (authorityKind: RootAuthorityKind)
        (identitySeed: IdentitySeed)
        : Result<AuthorityExecutionProfile, string> =
        let participantIdentity = identitySeedParticipantIdentity identitySeed

        match authorityKind, identitySeed, ParticipantIdentity.role participantIdentity with
        | _, _, None -> Error "public authority participant identity cannot be Bookkeeper"
        | RootAuthorityKind.HumanRoot, RootSelection _, Some _ when
            ParticipantIdentity.origin participantIdentity = PersonaOrigin.ResolvedAtRoot
            ->
            Ok
                { StoredSessionId = sessionId
                  StoredLogicalRunId = logicalRunId
                  StoredAuthorityRootUserMessageId = authorityRootUserMessageId
                  StoredAuthorityKind = authorityKind
                  StoredIdentitySeed = identitySeed }
        | RootAuthorityKind.AgentOwnerRoot, InheritedFromOwner _, Some _ ->
            Ok
                { StoredSessionId = sessionId
                  StoredLogicalRunId = logicalRunId
                  StoredAuthorityRootUserMessageId = authorityRootUserMessageId
                  StoredAuthorityKind = authorityKind
                  StoredIdentitySeed = identitySeed }
        | RootAuthorityKind.HumanRoot, _, Some _ -> Error "HumanRoot requires a root-selection identity seed"
        | RootAuthorityKind.AgentOwnerRoot, _, Some _ ->
            Error "AgentOwnerRoot requires an inherited owner identity seed"

    let createAuthorityExecutionProfile
        (sessionId: SessionId)
        (logicalRunId: LogicalRunId)
        (authorityRootUserMessageId: AuthorityRootUserMessageId)
        (authorityKind: RootAuthorityKind)
        (participantIdentity: ParticipantIdentityEvidence)
        : Result<AuthorityExecutionProfile, string> =
        createAuthorityExecutionProfileFromSeed
            sessionId
            logicalRunId
            authorityRootUserMessageId
            authorityKind
            (RootSelection participantIdentity)

    /// One provider request (PROMPT-008).
    ///
    /// Every field a request needs comes from this one immutable value. The
    /// clause exists because the previous code assembled them separately from a
    /// mutable session cache, the last user message, a Role map and the fallback
    /// projection — four sources that can disagree, and did.
    ///
    /// Construct ONLY through `buildAttemptExecutionProfile`. The architecture
    /// gate rejects a record expression for this type outside its owning module,
    /// because a hand-assembled profile is exactly the "temporary assembly" the
    /// clause forbids.
    type AttemptExecutionProfile =
        {
            Authority: AuthorityExecutionProfile
            PhysicalUserMessageId: PhysicalUserMessageId
            ProviderRun: ProviderRunIdentity
            Origin: PromptOrigin
            /// FALLBACK-002: the side the cursor currently selects. The only field
            /// fallback may move (FALLBACK-004).
            EffectiveAgent: string
            /// AGENT-001: Canonical role shares one system prompt, so this
            /// is derived from CanonicalRole alone.
            SystemPromptId: SystemPromptId
            /// AGENT-007 both layers read this same set: the Host-visible schema
            /// and the ToolRegistry execution gate. Two sources would let an
            /// unauthorised tool into the schema while the gate still refused it,
            /// or worse, the reverse.
            ToolCapabilitySet: Set<ToolPermission>
            /// PROMPT-008: which physical request this is.
            ///
            /// Real request semantics, not a flow stage (ARCH-001). It decides which
            /// projection is built, which instruction is sent, and — through CTX-007
            /// — what a success does to the fallback cursor.
            RequestKind: ProviderRequestKind
            /// CTX-010: which prefix this attempt sends.
            ///
            /// Part of the immutable profile because the candidate must be valid for
            /// exactly one attempt. Held in mutable session state instead, a probe
            /// would outlive the request that justified it, and CTX-012's "a failed
            /// probe never became a fact" would stop being structurally true.
            ProjectionChoice: XProjectionChoice
        }

        /// Convenience projections. Reading through the authority profile keeps
        /// FALLBACK-004 visible: these never change for the Logical Run, while
        /// EffectiveAgent does.
        member this.SessionId = this.Authority.SessionId
        member this.LogicalRunId = this.Authority.LogicalRunId
        member this.AuthorityRootUserMessageId = this.Authority.AuthorityRootUserMessageId
        member this.SelectedAgent = this.Authority.SelectedAgent
        member this.PeerAgent = this.Authority.PeerAgent
        member this.CanonicalRole = this.Authority.CanonicalRole

    /// A dispatched prompt before the Host has confirmed anything (PROMPT-005
    /// `Claimed`).
    ///
    /// `LogicalRunId` is optional because the two origins differ in kind:
    /// a Continuation extends a run that already exists, while an Authority Root
    /// *creates* the run — and its id derives from the physical message that
    /// does not exist yet at claim time. An empty-string sentinel would make
    /// "no run yet" and "run with a blank id" the same value.
    /// DSL-state-combination: domain — optional logical/authority/agent/receipt
    /// facets describe one dispatch claim and its evidence, not a continuation
    /// program.
    type PromptClaim =
        {
            PromptKey: PromptKey
            SessionId: SessionId
            Origin: PromptOrigin
            LogicalRunId: LogicalRunId option
            AuthorityRootUserMessageId: AuthorityRootUserMessageId option
            EffectiveAgent: string option
            IdentitySeed: IdentitySeed
            /// PROMPT-005 requires the payload digest at claim time so recovery can
            /// tell two dispatches of the same shape apart.
            PayloadDigest: string
            /// PROMPT-005 `Submitted`: the transport receipt, once the Host call has
            /// returned. `None` while the claim is still only `Claimed`.
            ///
            /// PROMPT-011's recovery needs the two states distinguishable: step 4 (a
            /// receipt exists but no physical message was found) and step 5 (not even
            /// a receipt) both stay pending, but they are different diagnoses for an
            /// operator — one means the Host accepted something we cannot locate.
            Receipt: TransportReceipt option
            /// Historical workspace `RuntimeStartCount` observed when this claim
            /// was registered. Retained in the durable shape for audit and exact
            /// replay only; restart count no longer authorizes recovery or abandon.
            ClaimedAtRuntimeStartCount: int
        }

    /// PROMPT-005 typed evidence that one logical dispatch physically landed.
    ///
    /// Folded by the single integrator when `PhysicalAccepted` resolves a claim,
    /// so business layers (process-review assignment reentry) decide resend
    /// from projection evidence instead of scanning the Journal.
    type AcceptedDispatch =
        { PromptKey: PromptKey
          SessionId: SessionId
          Origin: PromptOrigin
          IdentitySeed: IdentitySeed
          PayloadDigest: string
          PhysicalUserMessageId: PhysicalUserMessageId }

    /// DSL-state-combination: domain — the two optional authority profiles
    /// represent durable before/active evidence; they are not stage latches.
    type PromptAuthorityProjection =
        {
            LastAuthorityProfile: AuthorityExecutionProfile option
            ActiveLogicalRun: AuthorityExecutionProfile option
            PendingClaims: Map<PromptKey, PromptClaim>
            /// PROMPT-005 accepted dispatch evidence, keyed by
            /// session + payload digest. `Pending` on a claim means the outcome
            /// is undetermined; an entry here means the payload physically
            /// landed. Keyed — never a session scan (PERSIST-008).
            AcceptedDispatches: Map<string, AcceptedDispatch>
            /// Physical message id -> the continuation kind it was accepted as.
            ///
            /// PROMPT-003 and PROMPT-009 only: this answers "was this message a
            /// continuation, and of what kind". REVIEW-003 forbids it as review
            /// confirmation evidence — a continuation being accepted says nothing
            /// about whether a model consumed the challenge.
            AcceptedContinuationIds: Map<PhysicalUserMessageId, ContinuationKind>
            /// PROMPT-011 ClaimSequence, keyed by claim scope digest.
            ///
            /// Counts claims ever registered for one
            /// (LogicalRunId, Origin, PayloadDigest) triple, so "the same Guard
            /// fired twice against the same tree" yields two distinct PromptKeys
            /// instead of one that looks like a duplicate.
            ///
            /// Bounded by the current Logical Run: `registerAuthority` clears it,
            /// so it grows with the number of distinct payloads in one run, not
            /// with session lifetime (PERSIST-008).
            ClaimSequences: Map<string, int>
        }

    let empty: PromptAuthorityProjection =
        { LastAuthorityProfile = None
          ActiveLogicalRun = None
          PendingClaims = Map.empty
          AcceptedDispatches = Map.empty
          AcceptedContinuationIds = Map.empty
          ClaimSequences = Map.empty }

    /// Key of one logical dispatch's landing evidence: the session plus the
    /// payload digest both send paths derive identically (`sha256 text`).
    let acceptedDispatchKey (sessionId: SessionId) (payloadDigest: string) =
        SessionId.value sessionId + "\x1f" + payloadDigest

    let originLabel (origin: PromptOrigin) =
        match origin with
        | AuthorityRoot HumanRoot -> "HumanRoot"
        | AuthorityRoot AgentOwnerRoot -> "AgentOwnerRoot"
        | Continuation InteractionRepair -> "InteractionRepair"
        | Continuation JoinGuard -> "JoinGuard"
        | Continuation ManagerGuard -> "ManagerGuard"
        | Continuation ReviewerGuard -> "ReviewerGuard"
        | Continuation BusyAgentNudge -> "BusyAgentNudge"
        | Continuation HumanMessage -> "HumanMessage"
        | Continuation ManagedDelegationAssignment -> "ManagedDelegationAssignment"
        | Continuation ProviderRetryAttempt -> "ProviderRetryAttempt"
        | Continuation DegenerationGuard -> "DegenerationGuard"
        | Continuation ManagerIdleEncouragement -> "ManagerIdleEncouragement"
        | Continuation FinalityRejected -> "FinalityRejected"
        | Continuation FinalitySteer -> "FinalitySteer"
        | Continuation FissionHandoff -> "FissionHandoff"
        | HostInternal -> "HostInternal"
        | UnknownOrigin -> "UnknownOrigin"

    let tryParseContinuationKind (value: string) =
        match value with
        | "InteractionRepair" -> Some InteractionRepair
        | "JoinGuard" -> Some JoinGuard
        | "ManagerGuard" -> Some ManagerGuard
        | "ReviewerGuard" -> Some ReviewerGuard
        | "BusyAgentNudge" -> Some BusyAgentNudge
        | "HumanMessage" -> Some HumanMessage
        | "ManagedDelegationAssignment" -> Some ManagedDelegationAssignment
        | "ProviderRetryAttempt" -> Some ProviderRetryAttempt
        | "DegenerationGuard" -> Some DegenerationGuard
        | "ManagerIdleEncouragement" -> Some ManagerIdleEncouragement
        | "FinalityRejected" -> Some FinalityRejected
        | "FinalitySteer" -> Some FinalitySteer
        | "FissionHandoff" -> Some FissionHandoff
        | _ -> None

    /// Why a managed agent name was refused.
    ///
    /// Typed rather than a message, because the three cases mean different things to
    /// a caller: a legacy name is a migration error the operator must fix, an unknown
    /// name may be a typo worth a suggestion, and malformed means the shape itself is
    /// wrong. A single string forced every consumer that wanted to distinguish them
    /// to match on prose.
    [<RequireQualifiedAccess>]
    type AgentNameRejection =
        | LegacyAgentName of string
        | UnknownManagedAgent of string
        | Malformed of string

    /// A parsed canonical managed agent name.
    type ParsedAgentName = { Name: string; Role: Role }

    let private parseStructuredAgentName (trimmed: string) : Result<ParsedAgentName, AgentNameRejection> =
        match Roles.tryParseRole (trimmed.ToLowerInvariant()) with
        | Some role when trimmed = Roles.roleLabel role -> Ok { Name = trimmed; Role = role }
        | Some _ -> Error(AgentNameRejection.Malformed trimmed)
        | None -> Error(AgentNameRejection.UnknownManagedAgent trimmed)

    let private parseNonBlankAgentName (trimmed: string) : Result<ParsedAgentName, AgentNameRejection> =
        if ManagedAgentCatalog.isLegacyAgentName (trimmed.ToLowerInvariant()) then
            Error(AgentNameRejection.LegacyAgentName trimmed)
        elif trimmed.Contains("-") then
            Error(AgentNameRejection.Malformed trimmed)
        else
            parseStructuredAgentName trimmed

    let parseAgentNameTyped (value: string) : Result<ParsedAgentName, AgentNameRejection> =
        if String.IsNullOrWhiteSpace value then
            Error(AgentNameRejection.Malformed value)
        else
            parseNonBlankAgentName (value.Trim())

    let parseAgentName (value: string) : Result<string * Role, string> =
        parseAgentNameTyped value
        |> Result.map (fun parsed -> parsed.Name, parsed.Role)
        |> Result.mapError (fun rejection ->
            match rejection with
            | AgentNameRejection.LegacyAgentName name -> ManagedAgentCatalog.formatLegacyNameNotSupported name
            | AgentNameRejection.UnknownManagedAgent name -> sprintf "Unknown managed agent '%s'." name
            | AgentNameRejection.Malformed name -> sprintf "Malformed managed agent name '%s'." name)

    /// Deterministic Logical Run id. PROMPT-011 requires stability across
    /// restarts, so it is derived from durable identities and never generated.
    let stableLogicalRunId
        (sha256: string -> string)
        (runtimeId: RuntimeId)
        (sessionId: SessionId)
        (authorityRoot: AuthorityRootUserMessageId)
        : LogicalRunId =
        LogicalRunId.create (
            sha256 (
                String.Join(
                    "\n",
                    [| RuntimeId.value runtimeId
                       SessionId.value sessionId
                       AuthorityRootUserMessageId.value authorityRoot |]
                )
            )
        )

    let agentPair (profile: AuthorityExecutionProfile) : AgentPairCursor.AuthorityAgentPair =
        { AgentPairCursor.AuthorityAgentPair.SelectedAgent =
            ParticipantIdentity.selectedAgent profile.ParticipantIdentity
          AgentPairCursor.AuthorityAgentPair.PeerAgent = ParticipantIdentity.peerAgent profile.ParticipantIdentity }

    // ── PromptKey derivation (PROMPT-011) ───────────────────────────────────
    //
    // The key must be a STABLE idempotency anchor: after a crash, recovery looks
    // for it in Host metadata to decide whether a dispatch physically landed.
    // A random GUID cannot serve that purpose — a restarted process would derive
    // a different key for the same logical dispatch and conclude nothing was
    // sent.

    /// Absent identities participate in the digest as an explicit marker rather
    /// than an empty string, so "no Logical Run yet" cannot collide with "a run
    /// whose id happens to be blank".
    let private digestField (value: string option) =
        match value with
        | Some text -> text
        | None -> "\u0000absent"

    /// PROMPT-011 recovery bounds.
    ///
    /// The tail window exists because a Host session's history is unbounded while
    /// a pending claim is minutes old at most. Scanning further would not find a
    /// message that is genuinely absent, and PROMPT-011 forbids resending either
    /// way — so a wider window buys nothing and costs an unbounded read.
    ///
    /// Plain `let`, not `[<Literal>]`: Fable inlines a literal and emits no export,
    /// leaving the clause value unassertable from a layer 1 test.
    let RecoveryTailWindow = 50

    /// The scope a ClaimSequence counts within.
    ///
    /// PROMPT-011 names (SessionId, LogicalRunId, Origin, PayloadDigest). Two
    /// dispatches agreeing on all four are the same logical act repeated, which
    /// is exactly when a distinct sequence number is needed.
    let claimScopeDigest
        (sessionId: SessionId)
        (logicalRunId: LogicalRunId option)
        (origin: PromptOrigin)
        (payloadDigest: string)
        =
        String.Join(
            "\u001f",
            [| SessionId.value sessionId
               digestField (logicalRunId |> Option.map LogicalRunId.value)
               originLabel origin
               payloadDigest |]
        )

    /// The ClaimSequence this scope's next claim would carry.
    let nextClaimSequence (scope: string) (projection: PromptAuthorityProjection) =
        (Map.tryFind scope projection.ClaimSequences |> Option.defaultValue 0) + 1

    /// PROMPT-011's key. Deterministic in every input, so the same logical
    /// dispatch derives the same key on any process that folds the same journal.
    let derivePromptKey
        (sha256: string -> string)
        (sessionId: SessionId)
        (logicalRunId: LogicalRunId option)
        (authorityRoot: AuthorityRootUserMessageId option)
        (origin: PromptOrigin)
        (effectiveAgent: string option)
        (payloadDigest: string)
        (claimSequence: int)
        : PromptKey =
        PromptKey.create (
            sha256 (
                String.Join(
                    "\u001f",
                    [| SessionId.value sessionId
                       digestField (logicalRunId |> Option.map LogicalRunId.value)
                       digestField (authorityRoot |> Option.map AuthorityRootUserMessageId.value)
                       originLabel origin
                       digestField effectiveAgent
                       payloadDigest
                       string claimSequence |]
                )
            )
        )

    /// FALLBACK-001: the profile's agent pair for a given cursor.
    let effectiveAgentFor (profile: AuthorityExecutionProfile) (cursor: AgentPairCursor.FallbackCursor) : string =
        ignore cursor
        profile.SelectedAgent

    /// Blogger-request + terminal-scoped repair identity used by the exact-one
    /// chronicle nudge→AABB state machine. Both axes matter: terminal identity
    /// makes same-terminal re-entry idempotent, while BloggerRequestId prevents a
    /// previous request on the same Session/LogicalRun from spending the next
    /// request's protocol budget.
    ///
    /// This request+terminal digest is reserved for Blogger's explicit
    /// nudge→AABB escalation protocol. Ordinary interaction nudges are gate
    /// reminders and use `gateNudgePayloadDigest` instead.
    let repairPayloadDigest
        (requestId: BloggerRequestId)
        (terminalProviderRun: ProviderRunIdentity)
        (repairKind: string)
        =
        String.Join(
            "\u001f",
            [| BloggerRequestId.value requestId
               ProviderRunIdentity.value terminalProviderRun
               repairKind |]
        )

    let repairPayloadBelongsToRequest (requestId: BloggerRequestId) (payloadDigest: string) =
        payloadDigest.StartsWith(BloggerRequestId.value requestId + "\u001f", System.StringComparison.Ordinal)

    /// Gate nudges are not a finite repair budget. They are repeatable while the
    /// guarded condition remains unsatisfied, but one exact terminal occasion is
    /// idempotent across duplicate Host observations / restart replay.
    let gateNudgePayloadDigest (gateKind: string) (terminalProviderRun: ProviderRunIdentity) =
        String.Join("\u001f", [| gateKind; ProviderRunIdentity.value terminalProviderRun |])

    let private exactOccasionIsAdmitted
        (sessionId: SessionId)
        (logicalRunId: LogicalRunId)
        (origin: PromptOrigin)
        (payloadDigest: string)
        (projection: PromptAuthorityProjection)
        =
        let pending =
            projection.PendingClaims
            |> Map.exists (fun _ claim ->
                claim.SessionId = sessionId
                && claim.LogicalRunId = Some logicalRunId
                && claim.Origin = origin
                && claim.PayloadDigest = payloadDigest)

        let accepted =
            projection.AcceptedDispatches
            |> Map.tryFind (acceptedDispatchKey sessionId payloadDigest)
            |> Option.exists (fun dispatch -> dispatch.Origin = origin)

        pending || accepted

    let gateNudgeAlreadyAdmitted
        (sessionId: SessionId)
        (logicalRunId: LogicalRunId)
        (continuation: ContinuationKind)
        (gateKind: string)
        (terminalProviderRun: ProviderRunIdentity)
        (projection: PromptAuthorityProjection)
        =
        exactOccasionIsAdmitted
            sessionId
            logicalRunId
            (PromptOrigin.Continuation continuation)
            (gateNudgePayloadDigest gateKind terminalProviderRun)
            projection

    let gateNudgeAcceptedPhysical
        (sessionId: SessionId)
        (continuation: ContinuationKind)
        (gateKind: string)
        (terminalProviderRun: ProviderRunIdentity)
        (projection: PromptAuthorityProjection)
        =
        let payloadDigest = gateNudgePayloadDigest gateKind terminalProviderRun

        projection.AcceptedDispatches
        |> Map.tryFind (acceptedDispatchKey sessionId payloadDigest)
        |> Option.filter (fun dispatch ->
            dispatch.Origin = PromptOrigin.Continuation continuation
            && dispatch.PayloadDigest = payloadDigest)
        |> Option.map (fun dispatch -> dispatch.PhysicalUserMessageId)

    /// FALLBACK-008: has this Blogger request + terminal occasion already spent its one repair.
    /// Blogger protocol repair deliberately uses both axes: request identity
    /// prevents cross-request leakage on a long-lived run, while terminal identity
    /// distinguishes same-terminal re-entry from a new invalid terminal.
    ///
    /// Derived, not stored. `nextClaimSequence` returns 1 for a scope no claim has
    /// ever used, so anything above 1 means a repair was already claimed for this
    /// request+terminal occasion — whether or not it went on to succeed, which is the point:
    /// a failed repair must not license a second attempt.
    let repairAlreadyClaimed
        (sessionId: SessionId)
        (logicalRunId: LogicalRunId)
        (requestId: BloggerRequestId)
        (terminalProviderRun: ProviderRunIdentity)
        (repairKind: string)
        (projection: PromptAuthorityProjection)
        =
        let scope =
            claimScopeDigest
                sessionId
                (Some logicalRunId)
                (PromptOrigin.Continuation ContinuationKind.InteractionRepair)
                (repairPayloadDigest requestId terminalProviderRun repairKind)

        nextClaimSequence scope projection > 1

    /// GLORY-029: the durable occasion identity of one Manager idle encouragement.
    ///
    /// Manager encouragement is intentionally unbounded across fresh terminals.
    /// Life + condition explain the business context, while ProviderRunIdentity
    /// is the exact physical terminal occasion that must be idempotent across
    /// duplicate idle delivery / restart replay.
    let idlePayloadDigest (lifeId: ManagerLifeId) (conditionKey: string) (terminalProviderRun: ProviderRunIdentity) =
        String.Join(
            "\u001f",
            [| ManagerLifeId.value lifeId
               conditionKey
               ProviderRunIdentity.value terminalProviderRun |]
        )

    /// GLORY-029: has this exact terminal occasion already received its Manager
    /// encouragement. A new ProviderRun is always a fresh occasion, even when the
    /// Life and pre/post-T1 condition are unchanged.
    let idleAlreadyAdmitted
        (sessionId: SessionId)
        (logicalRunId: LogicalRunId)
        (lifeId: ManagerLifeId)
        (conditionKey: string)
        (terminalProviderRun: ProviderRunIdentity)
        (projection: PromptAuthorityProjection)
        =
        exactOccasionIsAdmitted
            sessionId
            logicalRunId
            (PromptOrigin.Continuation ContinuationKind.ManagerIdleEncouragement)
            (idlePayloadDigest lifeId conditionKey terminalProviderRun)
            projection

    /// AGENT-001: Canonical role shares one system prompt, so the prompt
    /// identity is a function of CanonicalRole alone.
    let systemPromptIdFor (role: Role) : SystemPromptId =
        SystemPromptId.create (Roles.roleLabel role)

    /// STRENGTH-004 / PROMPT-008: the request-specific authority is exact, not
    /// inferred by intersecting the ordinary role surface. Inquiry is eligible
    /// even though its ordinary WorkMain surface delegates reads through Inspector;
    /// Browser/Reviewer are intentionally ineligible despite having readonly tools.
    let private strengthReplicaReadonly =
        set [ ToolPermission.Read; ToolPermission.Glob; ToolPermission.Grep ]

    let private strengthReplicaEligibleRole =
        function
        | Role.Coder
        | Role.Inspector
        | Role.DevOps
        | Role.Inquiry -> true
        | Role.Manager
        | Role.Orchestrator
        | Role.Browser
        | Role.Reviewer
        | Role.Distiller
        | Role.Blogger -> false

    let private strengthReplicaCapabilities (role: Role) : Set<ToolPermission> =
        match strengthReplicaEligibleRole role with
        | true -> strengthReplicaReadonly
        | false -> Set.empty

    /// AGENT-007: ordinary requests use role permissions; StrengthReplica uses
    /// its own narrower request contract and fails closed for every ineligible role.
    let toolCapabilitiesFor (role: Role) (requestKind: ProviderRequestKind) : Set<ToolPermission> =
        match requestKind with
        | ProviderRequestKind.StrengthReplica -> strengthReplicaCapabilities role
        | ProviderRequestKind.WorkMain
        | ProviderRequestKind.BloggerMain
        | ProviderRequestKind.BloggerSquash
        | ProviderRequestKind.InteractionRepair -> OfficeCapability.permissions role

    /// The ONLY way to build an AttemptExecutionProfile (PROMPT-008).
    ///
    /// Everything a provider request needs is derived here from two inputs: the
    /// authority profile fixed by the Authority Root, and the fallback cursor
    /// that selects a side. Nothing is passed in that could be derived, so a
    /// caller cannot supply a CanonicalRole that disagrees with the agent name,
    /// or a tool set that disagrees with the role.
    ///
    /// FALLBACK-014 / AGENT-029: `EffectiveAgent` may move to PeerAgent on B-side;
    /// `SystemPromptId` and ParticipantIdentity stay fixed by the Authority Root
    /// IdentitySeed, while SessionProviderLanguage stays session bind-once. None
    /// follows EffectiveAgent name.
    ///
    /// That is the whole clause. The previous code assembled these fields from a
    /// mutable session cache, the last user message, a Role map and the fallback
    /// projection — four sources that can disagree, and did (the B-side request
    /// occasionally carried the wrong tool set).
    ///
    /// `requestKind` and `choice` cannot be derived and so must be supplied. The
    /// probe is validated against the kind rather than trusted: CTX-010 permits one
    /// only on a work main request, and enforcing that here means a Companion
    /// request carrying a probe is not expressible rather than merely discouraged.
    let buildAttemptExecutionProfile
        (authority: AuthorityExecutionProfile)
        (cursor: AgentPairCursor.FallbackCursor)
        (physicalUserMessageId: PhysicalUserMessageId)
        (providerRun: ProviderRunIdentity)
        (origin: PromptOrigin)
        (requestKind: ProviderRequestKind)
        (choice: XProjectionChoice)
        : AttemptExecutionProfile =
        { Authority = authority
          PhysicalUserMessageId = physicalUserMessageId
          ProviderRun = providerRun
          Origin = origin
          EffectiveAgent = effectiveAgentFor authority cursor
          SystemPromptId = systemPromptIdFor authority.CanonicalRole
          ToolCapabilitySet = toolCapabilitiesFor authority.CanonicalRole requestKind
          RequestKind = requestKind
          ProjectionChoice =
            if ProviderRequestKind.mayCarryProbe requestKind then
                choice
            else
                XProjectionChoice.UseCommittedEpoch }
