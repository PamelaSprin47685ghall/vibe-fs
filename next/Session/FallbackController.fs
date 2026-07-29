namespace Wanxiangshu.Next.Session

open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Domain

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
            | Error FallbackAdvanceRejection.DifferentRun -> Ok NoActiveRun
            | Error FallbackAdvanceRejection.InvalidTransition ->
                Error "Fallback advance violates FALLBACK-007 (offset or count is not the successor)"
            | Ok _ ->
                let advanced =
                    AgentFact.FallbackCursorAdvanced
                        {| SessionId = sessionId
                           LogicalRunId = current.LogicalRunId
                           AuthorityRootUserMessageId = current.AuthorityRootUserMessageId
                           ProviderRun = providerRun
                           PreviousOffset = current.Cursor.Offset
                           NextOffset = next.Offset
                           ConsecutiveFailureCount = next.ConsecutiveFailureCount
                           Reason = reason |}

                match AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) advanced journal with
                | Error failure -> Error(sprintf "%A" failure.Failure)
                | Ok _ ->
                    // FALLBACK-005: judgement happens after the failure is
                    // recorded, so the 12th consecutive failure is immediately
                    // final and there is no automatic 13th attempt.
                    match AgentPairCursor.recoveryVerdict budget next with
                    | AgentPairCursor.MayContinue cursor -> Ok(Advanced cursor)
                    | AgentPairCursor.Exhausted cursor ->
                        let exhausted =
                            AgentFact.FallbackExhausted
                                {| SessionId = sessionId
                                   LogicalRunId = current.LogicalRunId
                                   AuthorityRootUserMessageId = current.AuthorityRootUserMessageId
                                   FinalConsecutiveFailureCount = cursor.ConsecutiveFailureCount
                                   FinalOffset = cursor.Offset |}

                        match
                            AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) exhausted journal
                        with
                        | Error failure -> Error(sprintf "%A" failure.Failure)
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
