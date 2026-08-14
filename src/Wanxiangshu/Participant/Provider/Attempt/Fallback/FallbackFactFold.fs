namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal.ProjectionUpdate

module FallbackFactFold =

    let private reject = FoldRejection.reject

    /// A dedupe refusal is the fold working as intended: the same failed attempt
    /// or the same tool call arrived twice. The projection stays as it was and
    /// the fold continues.
    ///
    /// A validation refusal is different. FALLBACK-007's modulo-4 check and
    /// REVIEW-003's causal proof can only fail on a line that could not have been
    /// written by a correct writer, so absorbing it would mean replaying a
    /// journal into a state the domain forbids.
    let private fallbackOutcome factName projection result =
        match result with
        | Ok updated -> Ok updated
        | Error AlreadyObserved
        | Error AlreadyExhausted
        | Error DifferentRun -> Ok projection
        | Error NoCursor ->
            reject factName "cursor advance has no cursor to advance: FALLBACK-001 requires an accepted Authority Root"
        | Error InvalidTransition ->
            reject factName "cursor advance violates FALLBACK-007 (offset or count is not the successor)"
        | Error(InvalidFallbackOffset decodeError) ->
            match decodeError with
            | AgentPairCursor.FallbackOffsetDecodeError.InvalidFallbackOffset value ->
                reject factName $"cursor advance rejected: corrupt offset byte {value} (FALLBACK-002)"

    let fold (projection: AgentProjectionSet) (fact: FallbackFactCases) : Result<AgentProjectionSet, FoldRejection> =
        // ── fallback ────────────────────────────────────────────────────────
        match fact with
        | FallbackFactCases.FallbackCursorAdvanced payload ->
            let identity =
                { SessionId = payload.SessionId
                  LogicalRunId = payload.LogicalRunId
                  AuthorityRootUserMessageId = payload.AuthorityRootUserMessageId
                  ProviderRun = payload.ProviderRun }

            AgentProjection.tryUpdate
                payload.SessionId
                (fun session ->
                    // An advance for a run with no cursor cannot be validated:
                    // FALLBACK-001 says the cursor is created by the Authority
                    // Root, so its absence means the root fact is missing.
                    match session.Fallback with
                    | None -> Error NoCursor
                    | Some current ->
                        // FALLBACK-002 codec boundary: an illegal wire byte is a
                        // corrupt/forged line — typed refusal, never an exception
                        // and never a fake Append CommitUnknown.
                        match
                            AgentPairCursor.FallbackOffsetCodec.ofByte payload.PreviousOffset,
                            AgentPairCursor.FallbackOffsetCodec.ofByte payload.NextOffset
                        with
                        | Error decodeError, _
                        | _, Error decodeError -> Error(InvalidFallbackOffset decodeError)
                        | Ok previousOffset, Ok nextOffset ->
                            FallbackProjection.applyAdvance
                                identity
                                previousOffset
                                nextOffset
                                payload.ConsecutiveFailureCount
                                current
                            |> Result.map (fun updated -> { session with Fallback = Some updated }))
                projection
            |> fallbackOutcome "FallbackCursorAdvanced" projection

        | FallbackFactCases.FallbackExhausted payload ->
            Ok(
                updateSession
                    payload.SessionId
                    (fun session ->
                        { session with
                            Fallback = session.Fallback |> Option.map FallbackProjection.applyExhausted })
                    projection
            )
