namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module PromptAuthorityRun =
    val createAuthorityRoot:
        sha256: (string -> string) ->
        runtimeId: RuntimeId ->
        sessionId: SessionId ->
        rootKind: PromptAuthority.RootAuthorityKind ->
        physicalMessageId: PhysicalUserMessageId ->
        identitySeed: PromptAuthority.IdentitySeed ->
            Result<PromptAuthority.AuthorityExecutionProfile, string>

    val claimAgentOwnerRoot:
        key: PromptKey ->
        sessionId: SessionId ->
        payloadDigest: string ->
        identitySeed: PromptAuthority.IdentitySeed ->
            Result<PromptAuthority.PromptClaim, string>

    val claimContinuation:
        key: PromptKey ->
        sessionId: SessionId ->
        continuation: PromptAuthority.ContinuationKind ->
        profile: PromptAuthority.AuthorityExecutionProfile ->
        effectiveAgent: string ->
        payloadDigest: string ->
            PromptAuthority.PromptClaim

    val submitClaim:
        key: PromptKey ->
        receipt: TransportReceipt ->
        projection: PromptAuthority.PromptAuthorityProjection ->
            PromptAuthority.PromptAuthorityProjection

    type AuthorityRegistrationRejection =
        | ActiveRunIdentityConflict of
            active: PromptAuthority.AuthorityExecutionProfile *
            requested: PromptAuthority.AuthorityExecutionProfile

    val describeRegistrationRejection: AuthorityRegistrationRejection -> string

    val resolveAuthorityProfile:
        requested: PromptAuthority.AuthorityExecutionProfile ->
        projection: PromptAuthority.PromptAuthorityProjection ->
            Result<PromptAuthority.AuthorityExecutionProfile, AuthorityRegistrationRejection>

    val registerAuthority:
        profile: PromptAuthority.AuthorityExecutionProfile ->
        projection: PromptAuthority.PromptAuthorityProjection ->
            Result<PromptAuthority.PromptAuthorityProjection, AuthorityRegistrationRejection>

    val closeAuthority:
        logicalRunId: LogicalRunId ->
        authorityRoot: AuthorityRootUserMessageId ->
        projection: PromptAuthority.PromptAuthorityProjection ->
            Result<PromptAuthority.PromptAuthorityProjection, string>

    val registerClaim:
        claim: PromptAuthority.PromptClaim ->
        projection: PromptAuthority.PromptAuthorityProjection ->
            PromptAuthority.PromptAuthorityProjection

    val acceptClaim:
        key: PromptKey ->
        physicalMessageId: PhysicalUserMessageId ->
        projection: PromptAuthority.PromptAuthorityProjection ->
            PromptAuthority.PromptAuthorityProjection

    val abandonClaim:
        key: PromptKey ->
        projection: PromptAuthority.PromptAuthorityProjection ->
            PromptAuthority.PromptAuthorityProjection

    val resolveKnownOrigin:
        physicalMessageId: PhysicalUserMessageId ->
        promptKey: PromptKey option ->
        hostCompaction: bool ->
        projection: PromptAuthority.PromptAuthorityProjection ->
            PromptAuthority.PromptOrigin
