namespace Wanxiangshu.Next.Domain

open System
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity

[<RequireQualifiedAccess>]
module PromptAuthority =

    type RootAuthorityKind =
        | HumanRoot
        | AgentOwnerRoot

    /// PROMPT-003. Every one of these extends an existing Logical Run and may
    /// not change the execution profile.
    type ContinuationKind =
        | InteractionRepair
        | ManagerGuard
        | ReviewerGuard
        | ReviewConfirmation
        | BusyAgentNudge
        | ProviderRetryAttempt
        | HostCompactionContinue

    type PromptOrigin =
        | AuthorityRoot of RootAuthorityKind
        | Continuation of ContinuationKind
        | HostInternal
        | UnknownOrigin

    /// What an Authority Root fixes for the whole Logical Run (PROMPT-002).
    ///
    /// FALLBACK-004: SelectedAgent, PeerAgent, CanonicalRole and SelectedTier
    /// never change here. Fallback moves EffectiveAgent, which lives on the
    /// per-attempt profile instead — that separation is the clause.
    ///
    /// PROMPT-002 also forbids a model id: there is deliberately no field for
    /// one, so "Authority Root overrides the model" is not expressible.
    type AuthorityExecutionProfile =
        { SessionId: SessionId
          LogicalRunId: LogicalRunId
          AuthorityRootUserMessageId: AuthorityRootUserMessageId
          AuthorityKind: RootAuthorityKind
          SelectedAgent: string
          PeerAgent: string
          CanonicalRole: Role
          SelectedTier: AgentTier }

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
            /// AGENT-001: fast-ROLE and deep-ROLE share one system prompt, so this
            /// is derived from CanonicalRole alone.
            SystemPromptId: SystemPromptId
            /// AGENT-007 both layers read this same set: the Host-visible schema
            /// and the ToolRegistry execution gate. Two sources would let an
            /// unauthorised tool into the schema while the gate still refused it,
            /// or worse, the reverse.
            ToolCapabilitySet: Set<ToolPermission>
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
        member this.SelectedTier = this.Authority.SelectedTier

    /// A dispatched prompt before the Host has confirmed anything (PROMPT-005
    /// `Claimed`).
    ///
    /// `LogicalRunId` is optional because the two origins differ in kind:
    /// a Continuation extends a run that already exists, while an Authority Root
    /// *creates* the run — and its id derives from the physical message that
    /// does not exist yet at claim time. An empty-string sentinel would make
    /// "no run yet" and "run with a blank id" the same value.
    type PromptClaim =
        {
            PromptKey: PromptKey
            SessionId: SessionId
            Origin: PromptOrigin
            LogicalRunId: LogicalRunId option
            AuthorityRootUserMessageId: AuthorityRootUserMessageId option
            EffectiveAgent: string option
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
            /// PROMPT-011 `RecoveryAttemptBudget`: how many plugin starts have seen
            /// this claim still unresolved.
            ///
            /// Counted by folding `RuntimeStarted`, not stored by a writer. A fact
            /// saying "I tried to recover this" would itself have to be written
            /// during recovery, so a crash before writing it would lose the attempt
            /// and the budget could never expire.
            RecoveryAttempts: int
        }

    type PromptAuthorityProjection =
        {
            LastAuthorityProfile: AuthorityExecutionProfile option
            ActiveLogicalRun: AuthorityExecutionProfile option
            PendingClaims: Map<PromptKey, PromptClaim>
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
          AcceptedContinuationIds = Map.empty
          ClaimSequences = Map.empty }

    let originLabel (origin: PromptOrigin) =
        match origin with
        | AuthorityRoot HumanRoot -> "HumanRoot"
        | AuthorityRoot AgentOwnerRoot -> "AgentOwnerRoot"
        | Continuation InteractionRepair -> "InteractionRepair"
        | Continuation ManagerGuard -> "ManagerGuard"
        | Continuation ReviewerGuard -> "ReviewerGuard"
        | Continuation ReviewConfirmation -> "ReviewConfirmation"
        | Continuation BusyAgentNudge -> "BusyAgentNudge"
        | Continuation ProviderRetryAttempt -> "ProviderRetryAttempt"
        | Continuation HostCompactionContinue -> "HostCompactionContinue"
        | HostInternal -> "HostInternal"
        | UnknownOrigin -> "UnknownOrigin"

    let tryParseContinuationKind (value: string) =
        match value with
        | "InteractionRepair" -> Some InteractionRepair
        | "ManagerGuard" -> Some ManagerGuard
        | "ReviewerGuard" -> Some ReviewerGuard
        | "ReviewConfirmation" -> Some ReviewConfirmation
        | "BusyAgentNudge" -> Some BusyAgentNudge
        | "ProviderRetryAttempt" -> Some ProviderRetryAttempt
        | "HostCompactionContinue" -> Some HostCompactionContinue
        | _ -> None

    let roleLabel (role: Role) =
        match role with
        | Role.Manager -> "manager"
        | Role.Orchestrator -> "orchestrator"
        | Role.Coder -> "coder"
        | Role.Inspector -> "inspector"
        | Role.DevOps -> "devops"
        | Role.Browser -> "browser"
        | Role.Meditator -> "meditator"
        | Role.Reviewer -> "reviewer"
        | Role.Executor -> "executor"
        | Role.Blogger -> "blogger"

    let tryParseRole (value: string) =
        match value.ToLowerInvariant() with
        | "manager" -> Some Role.Manager
        | "orchestrator" -> Some Role.Orchestrator
        | "coder" -> Some Role.Coder
        | "inspector" -> Some Role.Inspector
        | "devops" -> Some Role.DevOps
        | "browser" -> Some Role.Browser
        | "meditator" -> Some Role.Meditator
        | "reviewer" -> Some Role.Reviewer
        | "executor" -> Some Role.Executor
        | "blogger" -> Some Role.Blogger
        | _ -> None

    let tierLabel (tier: AgentTier) =
        match tier with
        | AgentTier.Fast -> "Fast"
        | AgentTier.Deep -> "Deep"

    let tryParseTier (value: string) =
        match value.ToLowerInvariant() with
        | "fast" -> Some AgentTier.Fast
        | "deep" -> Some AgentTier.Deep
        | _ -> None

    /// AGENT-004: these are illegal, with no alias and no autocompletion.
    let private legacyAgentNames =
        set
            [ "orchestrator"
              "manager"
              "build"
              "plan"
              "coder"
              "inspector"
              "devops"
              "browser"
              "meditator"
              "reviewer"
              "blogger"
              "executor"
              "fast"
              "deep" ]

    /// AGENT-002 and AGENT-003: parse `fast-ROLE` / `deep-ROLE` and derive the peer.
    ///
    /// This is one of the four places AGENT-001 permits an agent string to be
    /// interpreted (config parsing, Authority Root creation, profile
    /// construction, Host send boundary). Package B removes every other site.
    let parseAgentName (value: string) : Result<string * Role * AgentTier * string, string> =
        if String.IsNullOrWhiteSpace value then
            Error "Expected fast-ROLE or deep-ROLE."
        else
            let trimmed = value.Trim()
            let lower = trimmed.ToLowerInvariant()

            if
                legacyAgentNames.Contains lower
                || lower.Contains("_")
                || lower.EndsWith("-fast")
                || lower.EndsWith("-deep")
                || lower.StartsWith("fast_")
                || lower.StartsWith("deep_")
            then
                Error(sprintf "Legacy agent name '%s' is not supported." trimmed)
            else
                let parts = trimmed.Split([| '-' |], 2)

                if parts.Length <> 2 then
                    Error "Expected fast-ROLE or deep-ROLE."
                else
                    match tryParseTier parts.[0], tryParseRole parts.[1] with
                    | None, _ -> Error "Unknown tier. Use fast-* or deep-*."
                    | _, None -> Error "Unknown role. Use fast-* or deep-*."
                    | Some tier, Some role ->
                        let peerTier =
                            match tier with
                            | AgentTier.Fast -> AgentTier.Deep
                            | AgentTier.Deep -> AgentTier.Fast

                        let peerName =
                            sprintf "%s-%s" ((tierLabel peerTier).ToLowerInvariant()) (roleLabel role)

                        Ok(trimmed, role, tier, peerName)

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
        { AgentPairCursor.AuthorityAgentPair.SelectedAgent = profile.SelectedAgent
          AgentPairCursor.AuthorityAgentPair.PeerAgent = profile.PeerAgent }

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
    [<Literal>]
    let RecoveryTailWindow = 50

    /// After this many plugin starts an unresolved claim is abandoned rather than
    /// carried forever.
    [<Literal>]
    let RecoveryAttemptBudget = 3

    /// PROMPT-011: a plugin start was observed, so every still-pending claim has
    /// now survived one more recovery attempt.
    ///
    /// Bounded by the number of pending claims, which PROMPT-005 resolves or
    /// abandons — it does not grow with session lifetime (PERSIST-008).
    let countRecoveryAttempt (projection: PromptAuthorityProjection) =
        { projection with
            PendingClaims =
                projection.PendingClaims
                |> Map.map (fun _ claim ->
                    { claim with
                        RecoveryAttempts = claim.RecoveryAttempts + 1 }) }

    /// PROMPT-011: this claim has spent its recovery budget and must be abandoned
    /// with `UnresolvedAfterRecovery`.
    let recoveryBudgetSpent (claim: PromptClaim) =
        claim.RecoveryAttempts >= RecoveryAttemptBudget

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

    let effectiveAgentAt (profile: AuthorityExecutionProfile) (offset: byte) : string =
        AgentPairCursor.effectiveAgent (agentPair profile) (AgentPairCursor.atOffset offset)

    let effectiveAgentFor (profile: AuthorityExecutionProfile) (cursor: AgentPairCursor.FallbackCursor) : string =
        AgentPairCursor.effectiveAgent (agentPair profile) cursor

    /// FALLBACK-008: the payload digest of an interaction repair.
    ///
    /// A repair's prompt text is fixed per kind, so digesting the text would make
    /// every repair of that kind one logical act. The occasion is what the clause
    /// bounds — one terminal provider run earns one repair — so the run is what
    /// the digest names.
    ///
    /// Using the payload digest for this is what makes the budget durable without
    /// a new fact: the digest enters the claim scope, so `ClaimSequences` already
    /// counts repairs per occasion. The previous design kept a `RepairClaims` set
    /// that no fact wrote, so the guarantee died with the process.
    let repairPayloadDigest (terminalProviderRun: ProviderRunIdentity) (repairKind: string) =
        String.Join("\u001f", [| ProviderRunIdentity.value terminalProviderRun; repairKind |])

    /// FALLBACK-008: has this occasion already spent its one repair.
    ///
    /// Derived, not stored. `nextClaimSequence` returns 1 for a scope no claim has
    /// ever used, so anything above 1 means a repair was already claimed for this
    /// terminal — whether or not it went on to succeed, which is the point:
    /// a failed repair must not license a second attempt.
    let repairAlreadyClaimed
        (sessionId: SessionId)
        (logicalRunId: LogicalRunId)
        (terminalProviderRun: ProviderRunIdentity)
        (repairKind: string)
        (projection: PromptAuthorityProjection)
        =
        let scope =
            claimScopeDigest
                sessionId
                (Some logicalRunId)
                (PromptOrigin.Continuation ContinuationKind.InteractionRepair)
                (repairPayloadDigest terminalProviderRun repairKind)

        nextClaimSequence scope projection > 1

    /// AGENT-001: fast-ROLE and deep-ROLE share one system prompt, so the prompt
    /// identity is a function of CanonicalRole alone. Tier deliberately does not
    /// participate — if it did, `permissions(fast-coder) = permissions(deep-coder)`
    /// (AGENT-010) would stop being structurally guaranteed.
    let systemPromptIdFor (role: Role) : SystemPromptId = SystemPromptId.create (roleLabel role)

    /// The ONLY way to build an AttemptExecutionProfile (PROMPT-008).
    ///
    /// Everything a provider request needs is derived here from two inputs: the
    /// authority profile fixed by the Authority Root, and the fallback cursor
    /// that selects a side. Nothing is passed in that could be derived, so a
    /// caller cannot supply a CanonicalRole that disagrees with the agent name,
    /// or a tool set that disagrees with the role.
    ///
    /// That is the whole clause. The previous code assembled these fields from a
    /// mutable session cache, the last user message, a Role map and the fallback
    /// projection — four sources that can disagree, and did (the B-side request
    /// occasionally carried the wrong tool set).
    let buildAttemptExecutionProfile
        (authority: AuthorityExecutionProfile)
        (cursor: AgentPairCursor.FallbackCursor)
        (physicalUserMessageId: PhysicalUserMessageId)
        (providerRun: ProviderRunIdentity)
        (origin: PromptOrigin)
        : AttemptExecutionProfile =
        { Authority = authority
          PhysicalUserMessageId = physicalUserMessageId
          ProviderRun = providerRun
          Origin = origin
          EffectiveAgent = effectiveAgentFor authority cursor
          SystemPromptId = systemPromptIdFor authority.CanonicalRole
          ToolCapabilitySet = Roles.permissions authority.CanonicalRole }

    /// COMPANION-001/002: Companion eligibility reads the CanonicalRole of the
    /// active Logical Run and nothing else.
    ///
    /// Takes the AUTHORITY profile, not an attempt profile. Eligibility is fixed by
    /// the Authority Root for the whole Logical Run, so per-attempt state cannot
    /// change it — and the `messages.transform` boundary that asks this question
    /// holds `ActiveLogicalRun`, which is exactly this type. Requiring an attempt
    /// profile there would force a caller to assemble one, which PROMPT-008 forbids.
    ///
    /// The role set is spelled here rather than read from a `RoleDefinition` flag.
    /// That flag was a second source and it disagreed: it marked Reviewer, DevOps
    /// and Meditator as having no Companion, so three of COMPANION-001's six
    /// eligible roles silently never got one.
    let hasCompanion (profile: AuthorityExecutionProfile) : bool =
        match profile.CanonicalRole with
        | Role.Orchestrator
        | Role.Manager
        | Role.Coder
        | Role.Meditator
        | Role.DevOps
        | Role.Reviewer -> true
        // AGENT-008: internal agents never recursively create a Companion.
        | Role.Inspector
        | Role.Browser
        | Role.Blogger
        | Role.Executor -> false

    /// AGENT-007 layer two: the runtime execution gate reads the same set the
    /// Host-visible schema was built from.
    let allowsTool (permission: ToolPermission) (profile: AttemptExecutionProfile) : bool =
        Set.contains permission profile.ToolCapabilitySet
