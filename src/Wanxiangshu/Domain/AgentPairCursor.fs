namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel.Identity

/// Pure A/A/B/B fallback cursor (spec/04). No Host, Journal or Fable dependency.
///
/// The whole point of this module is that two independent quantities were
/// previously conflated:
///
///   Offset                   unbounded cycle, position in A/A/B/B
///   ConsecutiveFailureCount  bounded budget, how much automatic recovery is left
///
/// FALLBACK-005 makes the distinction explicit: the cycle never dies, the budget
/// does. Collapsing them produces either "the 4th failure kills the run" or
/// "retry forever", and VERIFY-006 lists both as No-Go.
[<RequireQualifiedAccess>]
module AgentPairCursor =

    type ModelSide =
        | SideA
        | SideB

    /// FALLBACK-002. Offset is modulo-4 and never stops advancing;
    /// ConsecutiveFailureCount is the automatic-recovery budget consumed.
    ///
    /// Deliberately no dedupe state here. FALLBACK-003 gives deduplication to
    /// the projection keyed by FallbackAttemptIdentity, so a cursor carrying
    /// "the last attempt I saw" would be a second, weaker dedupe mechanism.
    type FallbackCursor =
        { Offset: byte
          ConsecutiveFailureCount: int }

    /// The A/B pair fixed by the Authority Root. FALLBACK-004: these never
    /// change for the life of the Logical Run; only EffectiveAgent moves.
    type AuthorityAgentPair =
        { SelectedAgent: string
          PeerAgent: string }

    /// FALLBACK-005. Default 12; an administrator may configure another finite
    /// positive value. Infinite is not a legal setting — that is the "keeps
    /// requesting after the budget" No-Go.
    ///
    /// Plain `let`, not `[<Literal>]`. Fable inlines a literal at every use and
    /// emits no export, so the value becomes unreadable from a layer 1 test — and a
    /// clause constant no test can assert is a clause with no gate. Nothing here
    /// needs literal semantics: it is never a match pattern or an attribute
    /// argument.
    let DefaultAutoRecoveryBudget = 12

    /// What the controller may do after a failure has been recorded.
    type RecoveryVerdict =
        /// Budget remains; the next automatic attempt may be issued.
        | MayContinue of FallbackCursor
        /// Budget consumed. Write FallbackExhausted; issue no further automatic
        /// physical request. Recovery requires a new Authority Root or an
        /// explicit user action.
        | Exhausted of FallbackCursor

    let initial: FallbackCursor =
        { Offset = 0uy
          ConsecutiveFailureCount = 0 }

    let side (offset: byte) : ModelSide =
        match offset with
        | 0uy
        | 1uy -> SideA
        | 2uy
        | 3uy -> SideB
        | _ -> invalidOp "Fallback offset must be in range 0..3"

    let advance (offset: byte) : byte = byte ((int offset + 1) % 4)

    /// CTX-006: is this offset one of the primed slots (A′ / B′).
    ///
    /// The A/A′/B/B′ shape: offsets 1 and 3 are the SECOND attempt on each side, and
    /// those are the slots a recovery action may run in. Offsets 0 and 2 are the first
    /// attempt on their side and always send an ordinary request.
    ///
    /// Necessary but not sufficient on its own. FALLBACK-012 forbids arming from
    /// parity alone — a success does not reset Offset, so a parked odd cursor would
    /// otherwise arm the first slot of every later sequence. `RecoverySlot.mayRecover`
    /// combines this with the control-flow arming fact.
    let isRecoverySlot (offset: byte) : bool = offset % 2uy = 1uy

    /// FALLBACK-004 on failure: Offset advances, budget is consumed by one.
    let recordFailure (cursor: FallbackCursor) : FallbackCursor =
        { Offset = advance cursor.Offset
          ConsecutiveFailureCount = cursor.ConsecutiveFailureCount + 1 }

    /// FALLBACK-004 on success: budget resets, Offset does NOT.
    ///
    /// Resetting Offset here would mean a Logical Run that fails once then
    /// succeeds silently returns to SideA, so the next failure re-tries the same
    /// side that already failed. VERIFY-006 lists resetting Offset on success as
    /// a No-Go for exactly this reason.
    let recordSuccess (cursor: FallbackCursor) : FallbackCursor =
        { cursor with
            ConsecutiveFailureCount = 0 }

    /// Judgement happens after the failure is recorded (FALLBACK-005), so the
    /// 12th consecutive failure lands on Offset=3 (SideB), advances to Offset=0,
    /// and is immediately final. There is no automatic 13th attempt.
    let recoveryVerdict (budget: int) (cursor: FallbackCursor) : RecoveryVerdict =
        if cursor.ConsecutiveFailureCount >= budget then
            Exhausted cursor
        else
            MayContinue cursor

    let effectiveAgent (authority: AuthorityAgentPair) (cursor: FallbackCursor) : string =
        match side cursor.Offset with
        | SideA -> authority.SelectedAgent
        | SideB -> authority.PeerAgent

    /// FALLBACK-006's table as a function: which side attempt N lands on,
    /// counting attempts from 0. Unbounded by construction.
    let sideSequence (count: int) : ModelSide list =
        if count < 0 then
            invalidOp "count must be non-negative"
        else
            [ 0 .. count - 1 ] |> List.map (fun index -> side (byte (index % 4)))

    let atOffset (offset: byte) : FallbackCursor =
        { Offset = offset
          ConsecutiveFailureCount = 0 }

    /// FALLBACK-001: a new Authority Root starts a fresh cursor. Expressed as a
    /// constructor rather than a `reset` mutation, because there is no operation
    /// that rewinds an existing cursor.
    let forNewAuthorityRoot: FallbackCursor = initial

    /// FALLBACK-007 fold validation: NextOffset must be the modulo-4 successor,
    /// and the count must advance by exactly one. A journal line failing either
    /// check is rejected rather than absorbed.
    let isValidAdvance (previousOffset: byte) (nextOffset: byte) (previousCount: int) (nextCount: int) : bool =
        nextOffset = advance previousOffset && nextCount = previousCount + 1

    /// The identity used to deduplicate one failed attempt (FALLBACK-003).
    /// Constructed here so callers never assemble it field by field from
    /// whatever strings happen to be in scope.
    let attemptIdentity
        (sessionId: SessionId)
        (logicalRunId: LogicalRunId)
        (authorityRoot: AuthorityRootUserMessageId)
        (providerRun: ProviderRunIdentity)
        : FallbackAttemptIdentity =
        { SessionId = sessionId
          LogicalRunId = logicalRunId
          AuthorityRootUserMessageId = authorityRoot
          ProviderRun = providerRun }
