namespace Wanxiangshu.Mission.Relay

module Surface =
    val empty: unit -> RelayState

    val openIncumbency:
        state: RelayState ->
        road: string ->
        incumbent: string ->
        snapshot: string ->
        authority: string ->
        sourceName: string ->
        obj

    val assess:
        state: RelayState ->
        road: string ->
        incumbent: string ->
        assessment: string ->
        snapshot: string ->
        authority: string ->
        languageAlgorithms: int ->
        simplicity: int ->
        structure: int ->
        granularity: int ->
        testsEvidence: int ->
        logicReliabilityBoundaries: int ->
        callerErgonomics: int ->
        completeness: int ->
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

    val retire:
        state: RelayState ->
        road: string ->
        incumbent: string ->
        retirement: string ->
        snapshot: string ->
        baton: string ->
        cut: string ->
        qualityCandidateAccepted: bool ->
        obj

    val activateSuccessor:
        state: RelayState ->
        road: string ->
        predecessor: string ->
        incumbent: string ->
        snapshot: string ->
        authority: string ->
        obj

    val view: state: RelayState -> road: string -> obj
    val obligations: state: RelayState -> road: string -> string array
    val authority: state: RelayState -> road: string -> obj
    val certificate: state: RelayState -> road: string -> obj
    val retirement: state: RelayState -> road: string -> obj

