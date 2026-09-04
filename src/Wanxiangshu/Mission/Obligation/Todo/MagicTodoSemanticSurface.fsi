namespace Wanxiangshu.Mission.Obligation.Todo

/// JS-native semantic entry points for Magic Todo's pure owner modules.
/// Domain values are decoded here and only JSON-shaped observations leave.
[<RequireQualifiedAccess>]
module MagicTodoSemanticSurface =

    val canonicalObligationListWire: items: obj array -> string

    val obligationListDigest: sha256: (string -> string) -> items: obj array -> string

    val validateObligations: items: obj array -> obj

    val admitTodowriteBatch: callIds: string array -> obj

    val checkPreparedReplay: expected: obj -> observed: obj -> obj

    val admitObligations:
        sha256: (string -> string) ->
        incumbencyId: string ->
        current: obj array ->
        existing: obj ->
        localized: obj ->
        submitted: obj array ->
            obj

    val todoWriteId: sha256: (string -> string) -> incumbencyId: string -> toolCallId: string -> string

    val todoWriteIdValue: value: string -> string

    val desiredLag1Cutoff: acceptedInOrder: string array -> string option

    val workRecordStart: openingSequence: int -> int

    val blindPlanOpeningBoundary:
        openingSequence: int -> t1CallSequence: int -> t1ToolCallId: string -> parts: obj array -> int

    val effectiveOpeningFloor:
        hasOpenLife: bool ->
        planCommitted: bool ->
        openingSequence: int ->
        t1CallSequence: obj ->
        t1ToolCallId: obj ->
        xTraceHeadSequence: int ->
        parts: obj array ->
            obj

    val bloggerEffectiveStart: ingestedThrough: int -> workRecordStartSequence: int -> int

    val requirePlanCommitmentBeforeFirstSuicide: planCommitted: bool -> obj

    val todoCheckpointEvidence: trigger: string -> previousCommitted: obj -> obj

    val buildTodoCheckpointCommit: value: obj -> obj

    val requiresLag1Rebase: previousCommitted: obj -> bool

    val wrapT1AcceptedResult: sessionId: string -> body: string -> string
