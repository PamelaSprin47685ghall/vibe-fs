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
    /// Shock package B gives this its remaining fields (SystemPromptId,
    /// ToolCapabilitySet) and the single `buildAttemptExecutionProfile`
    /// constructor. Package 0 only retypes the identities.
    type AttemptExecutionProfile =
        { Authority: AuthorityExecutionProfile
          PhysicalUserMessageId: PhysicalUserMessageId
          ProviderRun: ProviderRunIdentity
          EffectiveAgent: string
          Origin: PromptOrigin }

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
            RepairClaims: Set<string>
        }

    let empty: PromptAuthorityProjection =
        { LastAuthorityProfile = None
          ActiveLogicalRun = None
          PendingClaims = Map.empty
          AcceptedContinuationIds = Map.empty
          RepairClaims = Set.empty }

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

    let effectiveAgentAt (profile: AuthorityExecutionProfile) (offset: byte) : string =
        AgentPairCursor.effectiveAgent (agentPair profile) (AgentPairCursor.atOffset offset)

    let effectiveAgentFor (profile: AuthorityExecutionProfile) (cursor: AgentPairCursor.FallbackCursor) : string =
        AgentPairCursor.effectiveAgent (agentPair profile) cursor

    /// FALLBACK-008: an interaction repair fires at most once per terminal
    /// assistant message, so the dedupe key names exactly that occasion.
    let repairIdentity
        (logicalRunId: LogicalRunId)
        (authorityRoot: AuthorityRootUserMessageId)
        (terminalProviderRun: ProviderRunIdentity)
        (repairKind: string)
        =
        String.Join(
            "\u001f",
            [| LogicalRunId.value logicalRunId
               AuthorityRootUserMessageId.value authorityRoot
               ProviderRunIdentity.value terminalProviderRun
               repairKind |]
        )
