namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Foundation.Identity

/// Durable fallback state for one Logical Run (FALLBACK-007).
///
/// PERSIST-008: O(1) integrated state, never a scan. `RecentFailureKeys` is a
/// bounded window, so dedupe stays constant-time and the projection cannot grow
/// with history length.
/// DSL-state-combination: domain — Exhausted is terminal fallback state, while
/// LastTransitionWasSuccess is replay-derived transition provenance needed only
/// to recognize one already-durable pre-PAR-004 successor shape.
type FallbackProjection =
    {
        LogicalRunId: LogicalRunId
        AuthorityRootUserMessageId: AuthorityRootUserMessageId
        Cursor: AgentPairCursor.FallbackCursor
        /// FALLBACK-003: the same failed attempt observed twice — once by a retry
        /// signal, once by an idle reconcile — must advance the cursor once.
        RecentFailureKeys: string list
        /// FALLBACK-005 terminal state. Stored rather than re-derived from the
        /// count, so the fold can refuse a late advance without knowing the
        /// configured budget.
        Exhausted: bool
        /// Replay-only discriminator for the one historical transition emitted by
        /// the pre-PAR-004 writer after a successful primed attempt. This is derived
        /// from durable facts, not process-local recovery authority.
        LastTransitionWasSuccess: bool
    }

/// Why a `FallbackCursorAdvanced` line was not applied.
type FallbackAdvanceRejection =
    /// FALLBACK-003 dedupe: this attempt already advanced the cursor.
    | AlreadyObserved
    /// FALLBACK-005: no advance is accepted after exhaustion.
    | AlreadyExhausted
    /// The line belongs to a different Logical Run or Authority Root.
    | DifferentRun
    /// FALLBACK-001: the session has no cursor, so no Authority Root was ever
    /// accepted for it — the root fact is missing from the journal.
    ///
    /// Separate from `InvalidTransition` because the offsets on such a line are
    /// usually perfectly valid; the damage is the absent root. Reporting it as a
    /// successor violation sends an operator to inspect numbers that are correct.
    | NoCursor
    /// FALLBACK-007 fold validation: NextOffset must be the modulo-4 successor
    /// and the count must advance by exactly one. A line failing this is corrupt
    /// or forged and is refused rather than absorbed.
    | InvalidTransition
    /// FALLBACK-002: the wire byte is outside 0..3 — corrupt/forged line. The
    /// decode error is a typed value, never an exception.
    | InvalidFallbackOffset of AgentPairCursor.FallbackOffsetDecodeError

module FallbackProjection =

    /// Bounded dedupe window. A failed attempt is re-observed within a few
    /// signals or not at all, so an unbounded set would only grow.
    [<Literal>]
    let private DedupeWindow = 32

    /// FALLBACK-001: a new Authority Root starts a fresh cursor.
    ///
    /// There is deliberately no `empty`. A fallback projection without a run and
    /// a root is not a state this domain has, and inventing one is how
    /// "unknown-run" / "unknown-root" sentinels end up in a journal — the old
    /// implementation did exactly that, and those strings then participated in
    /// dedupe identities.
    let forAuthority (logicalRunId: LogicalRunId) (authorityRoot: AuthorityRootUserMessageId) =
        { LogicalRunId = logicalRunId
          AuthorityRootUserMessageId = authorityRoot
          Cursor = AgentPairCursor.forNewAuthorityRoot
          RecentFailureKeys = []
          Exhausted = false
          LastTransitionWasSuccess = false }

    let private remember key keys =
        key :: (keys |> List.filter ((<>) key)) |> List.truncate DedupeWindow

    /// Historical journals written before PAR-004 closed primed slots on success
    /// contain this exact shape: success parked at A′/B′, then the next failure
    /// recorded A′→B or B′→A. Replaying the same FallbackSucceeded under the new
    /// semantics normalizes the in-memory cursor to A/B first, so the old line's
    /// PreviousOffset is one step ahead of the normalized cursor. Accept that one
    /// already-durable transition; new writers never emit it.
    let private isHistoricalPostSuccessAdvance
        (current: FallbackProjection)
        (previousOffset: AgentPairCursor.FallbackOffset)
        (nextOffset: AgentPairCursor.FallbackOffset)
        (consecutiveFailureCount: int)
        =
        current.LastTransitionWasSuccess
        && current.Cursor.ConsecutiveFailureCount = 0
        && AgentPairCursor.isRecoverySlot previousOffset
        && previousOffset = AgentPairCursor.advance current.Cursor.Offset
        && AgentPairCursor.isValidAdvance previousOffset nextOffset 0 consecutiveFailureCount

    /// Apply one advance.
    ///
    /// Returns the rejection reason rather than the unchanged projection: a
    /// caller must not be able to mistake "refused" for "applied, idempotent".
    /// The old fold returned the baseline on every rejection path, so a corrupt
    /// line and a duplicate line were indistinguishable from a no-op.
    let applyAdvance
        (identity: FallbackAttemptIdentity)
        (previousOffset: AgentPairCursor.FallbackOffset)
        (nextOffset: AgentPairCursor.FallbackOffset)
        (consecutiveFailureCount: int)
        (current: FallbackProjection)
        : Result<FallbackProjection, FallbackAdvanceRejection> =
        let key = FallbackAttemptIdentity.dedupeKey identity

        if current.Exhausted then
            Error AlreadyExhausted
        elif
            identity.LogicalRunId <> current.LogicalRunId
            || identity.AuthorityRootUserMessageId <> current.AuthorityRootUserMessageId
        then
            Error DifferentRun
        elif List.contains key current.RecentFailureKeys then
            Error AlreadyObserved
        elif
            previousOffset <> current.Cursor.Offset
            && not (isHistoricalPostSuccessAdvance current previousOffset nextOffset consecutiveFailureCount)
        then
            Error InvalidTransition
        elif
            not (
                AgentPairCursor.isValidAdvance
                    previousOffset
                    nextOffset
                    current.Cursor.ConsecutiveFailureCount
                    consecutiveFailureCount
            )
        then
            Error InvalidTransition
        else
            Ok
                { current with
                    Cursor =
                        { Offset = nextOffset
                          ConsecutiveFailureCount = consecutiveFailureCount }
                    RecentFailureKeys = remember key current.RecentFailureKeys
                    LastTransitionWasSuccess = false }

    /// FALLBACK-005 terminal state.
    let applyExhausted (current: FallbackProjection) = { current with Exhausted = true }

    /// FALLBACK-004: success clears the budget and closes the primed recovery
    /// subslot while preserving the current A/B side.
    ///
    /// Applied from the durable FallbackSucceeded fact (owner-owned, single
    /// writer). The dedupe window clears too: a later failure is a new attempt,
    /// and stale keys would let it be mistaken for a replay.
    let recordSuccess (current: FallbackProjection) =
        { current with
            Cursor = AgentPairCursor.recordSuccess current.Cursor
            RecentFailureKeys = []
            LastTransitionWasSuccess = true }

    /// Whether the automatic recovery budget still permits an attempt.
    ///
    /// This is the projection-level question, not the cursor's: `Exhausted` is
    /// durable terminal state that the pure cursor does not carry. The
    /// EffectiveAgent question has no such extra knowledge, so callers ask
    /// `AgentPairCursor.effectiveAgent pair projection.Cursor` directly rather
    /// than through a wrapper here — a second definition of that lookup would be
    /// the same knowledge in two places.
    let private cursorMayContinue (budget: int) (cursor: AgentPairCursor.FallbackCursor) =
        match AgentPairCursor.recoveryVerdict budget cursor with
        | AgentPairCursor.MayContinue _ -> true
        | AgentPairCursor.Exhausted _ -> false

    let mayContinue (budget: int) (current: FallbackProjection) =
        if current.Exhausted then
            false
        else
            cursorMayContinue budget current.Cursor
