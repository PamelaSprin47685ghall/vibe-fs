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

    val closeCompletedAgentOwnerChildWork:
        logicalRunId: LogicalRunId ->
        authorityRoot: AuthorityRootUserMessageId ->
        projection: PromptAuthority.PromptAuthorityProjection ->
            Result<PromptAuthority.PromptAuthorityProjection, string>

    /// Durable replay of `AuthorityLogicalRunClosed`: the closed projection when
    /// it names the active run, `Ok None` when it repeats the closure already
    /// recorded for that run, an error otherwise.
    /// The closure must carry the exact source witness of the active root's
    /// expected closure kind (interaction-authority-018); otherwise it is rejected.
    /// A closure naming a run with no closure and no authority is an error, not
    /// a silent no-op: a conflicting closure must not look like a duplicate.
    val replayLogicalRunClosed:
        logicalRunId: LogicalRunId ->
        authorityRoot: AuthorityRootUserMessageId ->
        rootKind: string ->
        closureWitness: string ->
        projection: PromptAuthority.PromptAuthorityProjection ->
            Result<PromptAuthority.PromptAuthorityProjection option, string>

    /// interaction-authority-018: the canonical closure witness of a completed
    /// Manager life, or `None` when the root does not expect a Manager-life closure.
    val managerLifeClosureWitness:
        profile: PromptAuthority.AuthorityExecutionProfile -> retirementId: string -> string option

    /// interaction-authority-018: the canonical closure witness of a completed
    /// managed HumanRoot run, a child work, an attached work or an internal
    /// leaf. `None` when the accepted root does not expect that closure kind.
    val managedRunClosureWitness: expectedKind: string -> logicalRunId: string -> factId: string -> string option

    val childWorkClosureWitness:
        expectedKind: string ->
        ownerLogicalRunId: string ->
        childLogicalRunId: string ->
        factId: string ->
            string option

    val attachedWorkClosureWitness:
        expectedKind: string ->
        ownerLogicalRunId: string ->
        childLogicalRunId: string ->
        attachmentKind: string ->
        associationGeneration: string ->
        factId: string ->
            string option

    val internalLeafClosureWitness:
        expectedKind: string ->
        ownerLogicalRunId: string ->
        leafLogicalRunId: string ->
        decisionOrTransactionId: string ->
        factId: string ->
            string option

    /// interaction-authority-018: the unique durable source terminal a closure
    /// of this kind may cite.
    ///
    /// Exactly one source must match the accepted root's identity; no source
    /// and an ambiguous source are both refused.
    val uniqueTerminalSource:
        sources: PromptAuthority.AuthorityTerminalSource list ->
        sourceKind: string ->
        ownerLogicalRunId: string option ->
        childLogicalRunId: string ->
            Result<string * PromptAuthority.AuthorityTerminalSource, string>

    /// interaction-authority-018: the closure witness of one accepted root,
    /// built from the unique durable source terminal of its closure kind.
    val closureWitnessOfSource:
        expectedKind: string ->
        sources: PromptAuthority.AuthorityTerminalSource list ->
        ownerLogicalRunId: string option ->
        childLogicalRunId: string ->
            string option

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
