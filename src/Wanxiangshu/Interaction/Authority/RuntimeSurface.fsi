namespace Wanxiangshu.Interaction.Authority

module RuntimeSurface =
    val empty: obj
    val issueInheritedIdentitySeed: childName: string -> ownerProfile: obj -> obj
    val validateInheritedIdentitySeedAgainstActiveOwner: ownerProfile: obj -> seedValue: obj -> obj
    val validateInheritedIdentitySeed: ownerProfile: obj -> seedValue: obj -> obj
    val serializeIdentitySeed: seedValue: obj -> obj
    val rehydrateIdentitySeed: serialized: string -> obj
    val recoverActiveIdentity: projection: obj -> obj
    val projectClaimIdentitySeed: claim: obj -> obj
    val promotePhysical: physical: string -> string
    val transportReceiptShape: receipt: string -> bool

    val createAuthorityRoot:
        hash: (string -> string) ->
        runtime: string ->
        session: string ->
        kind: string ->
        physical: string ->
        seedValue: obj ->
            obj

    val parseAgentName: agent: string -> obj
    val registerAuthority: profile: obj -> projection: obj -> obj

    val claimContinuation:
        promptKey: string ->
        session: string ->
        kind: string ->
        profile: obj ->
        effectiveAgent: string ->
        payloadDigest: string ->
            obj

    val claimAgentOwnerRoot: promptKey: string -> session: string -> payloadDigest: string -> seedValue: obj -> obj
    val registerClaim: claim: obj -> projection: obj -> obj
    val acceptClaim: promptKey: string -> physical: string -> projection: obj -> obj
    val abandonClaim: promptKey: string -> projection: obj -> obj
    val nextClaimSequence: scope: string -> projection: obj -> int
    val submitClaim: promptKey: string -> receipt: string -> projection: obj -> obj
    val claimScopeDigest: session: string -> logicalRun: obj -> origin: obj -> payloadDigest: string -> string

    val derivePromptKey:
        hash: (string -> string) ->
        session: string ->
        logicalRun: obj ->
        authorityRoot: obj ->
        origin: obj ->
        effectiveAgent: obj ->
        payloadDigest: string ->
        claimSequence: int ->
            string

    val closeAuthority: logicalRun: string -> authorityRoot: string -> projection: obj -> obj
    val closeCompletedHumanRootManager: projection: obj -> obj
    val closeCompletedAgentOwnerChildWork: logicalRun: string -> authorityRoot: string -> projection: obj -> obj
    val resolveKnownOrigin: physical: string -> promptKey: string -> hostCompaction: bool -> projection: obj -> string
    val stableLogicalRunId: hash: (string -> string) -> runtime: string -> session: string -> physical: string -> string
    val originForContinuation: kind: string -> obj
    val tryParseContinuationKind: kind: string -> obj
    val repairPayloadDigest: request: string -> terminal: string -> kind: string -> string

    val repairAlreadyClaimed:
        session: string ->
        logicalRun: string ->
        request: string ->
        terminal: string ->
        kind: string ->
        projection: obj ->
            bool

    val gateNudgePayloadDigest: kind: string -> providerRun: string -> string

    val gateNudgeAlreadyAdmitted:
        session: string ->
        logicalRun: string ->
        continuation: string ->
        kind: string ->
        providerRun: string ->
        projection: obj ->
            bool
