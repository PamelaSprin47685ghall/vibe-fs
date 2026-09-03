namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Context.Prefix
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider.Attempt

[<RequireQualifiedAccess>]
module PromptAuthority =
    type RootAuthorityKind = PromptRootAuthorityKind
    type ContinuationKind = PromptContinuationKind
    type PromptOrigin = Wanxiangshu.Interaction.Authority.PromptOrigin
    type IdentitySeed = PromptIdentitySeed
    type IdentitySeedInput = PromptIdentitySeedInput
    type IdentitySeedValidationError = PromptIdentitySeedValidationError

    type AuthorityExecutionProfile =
        private
            { StoredSessionId: SessionId
              StoredLogicalRunId: LogicalRunId
              StoredAuthorityRootUserMessageId: AuthorityRootUserMessageId
              StoredAuthorityKind: RootAuthorityKind
              StoredIdentitySeed: IdentitySeed }

        member SessionId: SessionId
        member LogicalRunId: LogicalRunId
        member AuthorityRootUserMessageId: AuthorityRootUserMessageId
        member AuthorityKind: RootAuthorityKind
        member IdentitySeed: IdentitySeed
        member ParticipantIdentity: ParticipantIdentityEvidence
        member SelectedAgent: string
        member PeerAgent: string
        member CanonicalRole: Role
        member Persona: string
        member PersonaCatalogVersion: int
        member PersonaOrigin: PersonaOrigin

    val identitySeedParticipantIdentity: (IdentitySeed -> ParticipantIdentityEvidence)
    val identitySeedOwner: (IdentitySeed -> (SessionId * LogicalRunId * AuthorityRootUserMessageId) option)

    val issueInheritedIdentitySeed:
        canonicalChildName: string -> owner: AuthorityExecutionProfile -> Result<IdentitySeed, ParticipantIdentityError>

    val validateInheritedIdentitySeedAgainstActiveOwner:
        ownerOption: AuthorityExecutionProfile option ->
        seed: IdentitySeed ->
            Result<ParticipantIdentityEvidence, IdentitySeedValidationError>

    val validateInheritedIdentitySeed:
        owner: AuthorityExecutionProfile ->
        seed: IdentitySeed ->
            Result<ParticipantIdentityEvidence, IdentitySeedValidationError>

    val createAuthorityExecutionProfileFromSeed:
        sessionId: SessionId ->
        logicalRunId: LogicalRunId ->
        authorityRootUserMessageId: AuthorityRootUserMessageId ->
        authorityKind: RootAuthorityKind ->
        identitySeed: IdentitySeed ->
            Result<AuthorityExecutionProfile, string>

    val createAuthorityExecutionProfile:
        sessionId: SessionId ->
        logicalRunId: LogicalRunId ->
        authorityRootUserMessageId: AuthorityRootUserMessageId ->
        authorityKind: RootAuthorityKind ->
        participantIdentity: ParticipantIdentityEvidence ->
            Result<AuthorityExecutionProfile, string>

    type AttemptExecutionProfile =
        { Authority: AuthorityExecutionProfile
          PhysicalUserMessageId: PhysicalUserMessageId
          ProviderRun: ProviderRunIdentity
          Origin: PromptOrigin
          EffectiveAgent: string
          SystemPromptId: SystemPromptId
          ToolCapabilitySet: Set<ToolPermission>
          RequestKind: ProviderRequestKind
          ProjectionChoice: XProjectionChoice }

        member SessionId: SessionId
        member LogicalRunId: LogicalRunId
        member AuthorityRootUserMessageId: AuthorityRootUserMessageId
        member SelectedAgent: string
        member PeerAgent: string
        member CanonicalRole: Role

    type PromptClaim =
        { PromptKey: PromptKey
          SessionId: SessionId
          Origin: PromptOrigin
          LogicalRunId: LogicalRunId option
          AuthorityRootUserMessageId: AuthorityRootUserMessageId option
          EffectiveAgent: string option
          IdentitySeed: IdentitySeed
          PayloadDigest: string
          Receipt: TransportReceipt option
          ClaimedAtRuntimeStartCount: int }

    type AcceptedDispatch =
        { PromptKey: PromptKey
          SessionId: SessionId
          Origin: PromptOrigin
          IdentitySeed: IdentitySeed
          PayloadDigest: string
          PhysicalUserMessageId: PhysicalUserMessageId }

    type PromptAuthorityProjection =
        { LastAuthorityProfile: AuthorityExecutionProfile option
          ActiveLogicalRun: AuthorityExecutionProfile option
          PendingClaims: Map<PromptKey, PromptClaim>
          AcceptedDispatches: Map<string, AcceptedDispatch>
          AcceptedContinuationIds: Map<PhysicalUserMessageId, ContinuationKind>
          ClaimSequences: Map<string, int> }

    val empty: PromptAuthorityProjection
    val acceptedDispatchKey: sessionId: SessionId -> payloadDigest: string -> string
    val originLabel: origin: PromptOrigin -> string
    val tryParseContinuationKind: value: string -> ContinuationKind option

    [<RequireQualifiedAccess>]
    type AgentNameRejection =
        | LegacyAgentName of string
        | UnknownManagedAgent of string
        | Malformed of string

    type ParsedAgentName = { Name: string; Role: Role }

    val parseAgentNameTyped: value: string -> Result<ParsedAgentName, AgentNameRejection>
    val parseAgentName: value: string -> Result<string * Role, string>

    val stableLogicalRunId:
        sha256: (string -> string) ->
        runtimeId: RuntimeId ->
        sessionId: SessionId ->
        authorityRoot: AuthorityRootUserMessageId ->
            LogicalRunId

    val agentPair: profile: AuthorityExecutionProfile -> AgentPairCursor.AuthorityAgentPair
    val RecoveryTailWindow: int

    val claimScopeDigest:
        sessionId: SessionId ->
        logicalRunId: LogicalRunId option ->
        origin: PromptOrigin ->
        payloadDigest: string ->
            string

    val nextClaimSequence: scope: string -> projection: PromptAuthorityProjection -> int

    val derivePromptKey:
        sha256: (string -> string) ->
        sessionId: SessionId ->
        logicalRunId: LogicalRunId option ->
        authorityRoot: AuthorityRootUserMessageId option ->
        origin: PromptOrigin ->
        effectiveAgent: string option ->
        payloadDigest: string ->
        claimSequence: int ->
            PromptKey

    val effectiveAgentFor: profile: AuthorityExecutionProfile -> cursor: AgentPairCursor.FallbackCursor -> string

    val repairPayloadDigest:
        requestId: BloggerRequestId -> terminalProviderRun: ProviderRunIdentity -> repairKind: string -> string

    val repairPayloadBelongsToRequest: requestId: BloggerRequestId -> payloadDigest: string -> bool
    val gateNudgePayloadDigest: gateKind: string -> terminalProviderRun: ProviderRunIdentity -> string

    val gateNudgeAlreadyAdmitted:
        sessionId: SessionId ->
        logicalRunId: LogicalRunId ->
        continuation: ContinuationKind ->
        gateKind: string ->
        terminalProviderRun: ProviderRunIdentity ->
        projection: PromptAuthorityProjection ->
            bool

    val gateNudgeAcceptedPhysical:
        sessionId: SessionId ->
        continuation: ContinuationKind ->
        gateKind: string ->
        terminalProviderRun: ProviderRunIdentity ->
        projection: PromptAuthorityProjection ->
            PhysicalUserMessageId option

    val repairAlreadyClaimed:
        sessionId: SessionId ->
        logicalRunId: LogicalRunId ->
        requestId: BloggerRequestId ->
        terminalProviderRun: ProviderRunIdentity ->
        repairKind: string ->
        projection: PromptAuthorityProjection ->
            bool

    val idlePayloadDigest:
        lifeId: ManagerLifeId -> conditionKey: string -> terminalProviderRun: ProviderRunIdentity -> string

    val idleAlreadyAdmitted:
        sessionId: SessionId ->
        logicalRunId: LogicalRunId ->
        lifeId: ManagerLifeId ->
        conditionKey: string ->
        terminalProviderRun: ProviderRunIdentity ->
        projection: PromptAuthorityProjection ->
            bool

    val systemPromptIdFor: role: Role -> SystemPromptId
    val toolCapabilitiesFor: role: Role -> requestKind: ProviderRequestKind -> Set<ToolPermission>

    val buildAttemptExecutionProfile:
        authority: AuthorityExecutionProfile ->
        cursor: AgentPairCursor.FallbackCursor ->
        physicalUserMessageId: PhysicalUserMessageId ->
        providerRun: ProviderRunIdentity ->
        origin: PromptOrigin ->
        requestKind: ProviderRequestKind ->
        choice: XProjectionChoice ->
            AttemptExecutionProfile
