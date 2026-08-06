namespace Wanxiangshu.Session

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Journal
open Wanxiangshu.Domain

/// FALLBACK-003: the only writer of `FallbackCursorAdvanced` and
/// `FallbackExhausted`.
///
/// Two writers reached the cursor before this existed — a retry signal handler
/// and an idle-driven provider-failure adapter — both of them Host event paths.
/// FALLBACK-003 says Host events only wake; the advance belongs to the
/// controller that has already reconciled a full snapshot. `shock-audit`
/// measured the result as 3 writers.
module FallbackController =

    /// What the caller may do next.
    ///
    /// `Advanced` and `Exhausted` are separate cases because FALLBACK-005 gives
    /// them opposite permissions: one allows the next automatic physical request,
    /// the other forbids it. A boolean would make "budget spent" and "advance
    /// refused" indistinguishable, and both would read as falsy.
    type AdvanceOutcome =
        /// The cursor moved and the automatic recovery budget still permits an
        /// attempt. The caller may send a continuation for the same Logical Run.
        | Advanced of AgentPairCursor.FallbackCursor
        /// FALLBACK-005: budget consumed. `FallbackExhausted` is written and no
        /// further automatic physical request may be issued for this run.
        | Exhausted of AgentPairCursor.FallbackCursor
        /// FALLBACK-003 dedupe: this exact attempt already advanced the cursor,
        /// or the run is already exhausted. Nothing was written.
        | AlreadyRecorded of AgentPairCursor.FallbackCursor
        /// FALLBACK-001: no cursor exists, so no Authority Root was accepted for
        /// this session. Nothing to advance, and nothing may be sent.
        | NoActiveRun

    /// Record one confirmed failed provider attempt.
    ///
    /// The Logical Run and Authority Root are read from the fallback projection
    /// rather than passed in. FALLBACK-001 has the Authority Root create the
    /// cursor, so the projection already holds the only correct values — and the
    /// previous code path assembled them from an `ActiveLogicalRun` lookup with a
    /// fall-back that hashed a session binding into a synthetic LogicalRunId,
    /// which is a second source that can disagree with the first.
    ///
    /// `providerRun` identifies the attempt that failed (HOST-010). It is the
    /// dedupe key's only caller-supplied part, which is what makes "the same
    /// failure observed by both an idle reconcile and a retry signal" advance the
    /// cursor once.
    let recordConfirmedFailure
        (journal: AgentJournal)
        (budget: int)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (reason: string)
        : Result<AdvanceOutcome, string> =
        match DurableFallback.tryCurrentState sessionId (AgentJournal.snapshot journal) with
        | None -> Ok NoActiveRun
        | Some current ->
            let identity =
                AgentPairCursor.attemptIdentity
                    sessionId
                    current.LogicalRunId
                    current.AuthorityRootUserMessageId
                    providerRun

            let next = AgentPairCursor.recordFailure current.Cursor

            // Ask the projection first. Writing and letting the fold reject is not
            // equivalent: `FallbackExhausted` would then be evaluated against a
            // count the journal never accepted.
            match
                FallbackProjection.applyAdvance
                    identity
                    current.Cursor.Offset
                    next.Offset
                    next.ConsecutiveFailureCount
                    current
            with
            | Error FallbackAdvanceRejection.AlreadyObserved
            | Error FallbackAdvanceRejection.AlreadyExhausted -> Ok(AlreadyRecorded current.Cursor)
            // Both say "there is no run this advance belongs to", which is exactly
            // what `NoActiveRun` means. `NoCursor` cannot arise here — reaching this
            // point required `tryCurrentState` to return a cursor — but it is the
            // fold's answer for the same shape, and inventing a distinct outcome for
            // an unreachable case would put a state in `AdvanceOutcome` that no
            // caller can ever observe.
            | Error FallbackAdvanceRejection.DifferentRun
            | Error FallbackAdvanceRejection.NoCursor -> Ok NoActiveRun
            | Error FallbackAdvanceRejection.InvalidTransition ->
                Error "Fallback advance violates FALLBACK-007 (offset or count is not the successor)"
            | Error(FallbackAdvanceRejection.InvalidFallbackOffset decodeError) ->
                // FALLBACK-002: reached only via a corrupt wire byte that the
                // fold decoded before applyAdvance; the controller sees the typed
                // error, never an exception.
                match decodeError with
                | AgentPairCursor.FallbackOffsetDecodeError.InvalidFallbackOffset value ->
                    Error $"Fallback advance rejected: corrupt offset byte {value} (FALLBACK-002)"
            | Ok _ ->
                let advanced =
                    FallbackFact.FallbackCursorAdvanced
                        {| SessionId = sessionId
                           LogicalRunId = current.LogicalRunId
                           AuthorityRootUserMessageId = current.AuthorityRootUserMessageId
                           ProviderRun = providerRun
                           PreviousOffset = AgentPairCursor.FallbackOffsetCodec.toByte current.Cursor.Offset
                           NextOffset = AgentPairCursor.FallbackOffsetCodec.toByte next.Offset
                           ConsecutiveFailureCount = next.ConsecutiveFailureCount
                           Reason = reason |}

                match AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) advanced journal with
                | Error failure -> Error(JournalAppendFailure.describe failure)
                | Ok _ ->
                    // FALLBACK-005: judgement happens after the failure is
                    // recorded, so the 12th consecutive failure is immediately
                    // final and there is no automatic 13th attempt.
                    match AgentPairCursor.recoveryVerdict budget next with
                    | AgentPairCursor.MayContinue cursor -> Ok(Advanced cursor)
                    | AgentPairCursor.Exhausted cursor ->
                        let exhausted =
                            FallbackFact.FallbackExhausted
                                {| SessionId = sessionId
                                   LogicalRunId = current.LogicalRunId
                                   AuthorityRootUserMessageId = current.AuthorityRootUserMessageId
                                   FinalConsecutiveFailureCount = cursor.ConsecutiveFailureCount
                                   FinalOffset = AgentPairCursor.FallbackOffsetCodec.toByte cursor.Offset |}

                        match
                            AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) exhausted journal
                        with
                        | Error failure -> Error(JournalAppendFailure.describe failure)
                        | Ok _ -> Ok(Exhausted cursor)

    /// FALLBACK-004: whether a continuation may be sent for this outcome.
    ///
    /// Named as a question about the outcome rather than checked inline at each
    /// call site, because "may I issue another physical request" is exactly the
    /// decision FALLBACK-005 bounds and it must not be spelled twice.
    let mayContinue (outcome: AdvanceOutcome) =
        match outcome with
        | Advanced _ -> true
        | Exhausted _
        | AlreadyRecorded _
        | NoActiveRun -> false
