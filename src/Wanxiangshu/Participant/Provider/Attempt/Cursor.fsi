namespace Wanxiangshu.Participant.Provider.Attempt

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module AgentPairCursor =
    type ModelSide =
        | SideA
        | SideB

    type FallbackOffset =
        | Fork0
        | Fork1
        | Fork2
        | Fork3

    type FallbackOffsetDecodeError = InvalidFallbackOffset of byte

    module FallbackOffsetCodec =
        val toByte: offset: FallbackOffset -> byte
        val ofByte: value: byte -> Result<FallbackOffset, FallbackOffsetDecodeError>

    type FallbackCursor =
        { Offset: FallbackOffset
          ConsecutiveFailureCount: int }

    type AuthorityAgentPair =
        { SelectedAgent: string
          PeerAgent: string }

    val DefaultAutoRecoveryBudget: int

    type RecoveryVerdict =
        | MayContinue of FallbackCursor
        | Exhausted of FallbackCursor

    val initial: FallbackCursor
    val side: offset: FallbackOffset -> ModelSide
    val advance: offset: FallbackOffset -> FallbackOffset
    val isRecoverySlot: offset: FallbackOffset -> bool
    val recordFailure: cursor: FallbackCursor -> FallbackCursor
    val recordSuccess: cursor: FallbackCursor -> FallbackCursor
    val recoveryVerdict: budget: int -> cursor: FallbackCursor -> RecoveryVerdict
    val effectiveAgent: authority: AuthorityAgentPair -> cursor: FallbackCursor -> string
    val sideSequence: count: int -> ModelSide list
    val atOffset: offset: FallbackOffset -> FallbackCursor
    val forNewAuthorityRoot: FallbackCursor

    val isValidAdvance:
        previousOffset: FallbackOffset -> nextOffset: FallbackOffset -> previousCount: int -> nextCount: int -> bool

    val attemptIdentity:
        sessionId: SessionId ->
        logicalRunId: LogicalRunId ->
        authorityRoot: AuthorityRootUserMessageId ->
        providerRun: ProviderRunIdentity ->
            FallbackAttemptIdentity
