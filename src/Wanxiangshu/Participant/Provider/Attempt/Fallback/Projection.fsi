namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt

type FallbackProjection =
    { LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      Cursor: AgentPairCursor.FallbackCursor
      RecentFailureKeys: string list
      Exhausted: bool
      LastTransitionWasSuccess: bool }

type FallbackAdvanceRejection =
    | AlreadyObserved
    | AlreadyExhausted
    | DifferentRun
    | NoCursor
    | InvalidTransition
    | InvalidFallbackOffset of AgentPairCursor.FallbackOffsetDecodeError

module FallbackProjection =
    val forAuthority: logicalRunId: LogicalRunId -> authorityRoot: AuthorityRootUserMessageId -> FallbackProjection

    val applyAdvance:
        identity: FallbackAttemptIdentity ->
        previousOffset: AgentPairCursor.FallbackOffset ->
        nextOffset: AgentPairCursor.FallbackOffset ->
        consecutiveFailureCount: int ->
        current: FallbackProjection ->
            Result<FallbackProjection, FallbackAdvanceRejection>

    val applyExhausted: current: FallbackProjection -> FallbackProjection
    val recordSuccess: current: FallbackProjection -> FallbackProjection
    val mayContinue: budget: int -> current: FallbackProjection -> bool
