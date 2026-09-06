namespace Wanxiangshu.Mission.Relay

module Surface =
    val empty: unit -> RelayState

    val openIncumbency:
        state: RelayState -> road: string -> incumbent: string -> snapshot: string -> authority: string -> obj

    val assess:
        state: RelayState ->
        road: string ->
        incumbent: string ->
        assessment: string ->
        snapshot: string ->
        authority: string ->
        languageAlgorithms: string ->
        simplicity: string ->
        structure: string ->
        granularity: string ->
        testsEvidence: string ->
        logicReliabilityBoundaries: string ->
        callerErgonomics: string ->
        completeness: string ->
            obj

    val invalidateCertificate: state: RelayState -> road: string -> reason: string -> obj

    val advanceAuthority:
        state: RelayState ->
        road: string ->
        incumbent: string ->
        expected: string ->
        next: string ->
        authorityMessageId: string ->
        snapshot: string ->
            obj

    val blockCleanup: state: RelayState -> road: string -> incumbent: string -> blockerDigest: string -> obj

    val retireContinue:
        state: RelayState ->
        road: string ->
        incumbent: string ->
        retirement: string ->
        providerRun: string ->
        toolCall: string ->
        snapshot: string ->
            obj

    val retireAccepted:
        state: RelayState ->
        road: string ->
        incumbent: string ->
        retirement: string ->
        providerRun: string ->
        toolCall: string ->
        certificateId: string ->
        snapshot: string ->
            obj

    val view: state: RelayState -> road: string -> obj
    val authority: state: RelayState -> road: string -> obj
    val certificate: state: RelayState -> road: string -> obj
    val retirement: state: RelayState -> road: string -> obj
