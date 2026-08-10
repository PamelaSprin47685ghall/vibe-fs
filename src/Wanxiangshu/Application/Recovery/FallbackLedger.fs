namespace Wanxiangshu.Recovery

open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

[<RequireQualifiedAccess>]
type ConfirmedFailureOutcome =
    | RecoveryAdvanced
    | RecoveryExhausted
    | AlreadyRecorded
    | NoActiveRun

[<RequireQualifiedAccess>]
type RecoveryAdmission =
    | ContinueRecovery
    | RecoveryExhausted

/// FALLBACK-003 single writer: confirmed provider failure → durable dedupe →
/// cursor advance/exhaust. Callers may retain the precise single-attempt outcome
/// or project it to host-facing RecoveryAdmission.
module FallbackLedger =

    let recordConfirmedFailure
        (journal: AgentJournal)
        (budget: int)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (reason: string)
        : Result<ConfirmedFailureOutcome, string> =
        match FallbackEvidence.tryCurrentState sessionId (AgentJournal.snapshot journal) with
        | None -> Ok ConfirmedFailureOutcome.NoActiveRun
        | Some current ->
            let identity =
                AgentPairCursor.attemptIdentity
                    sessionId
                    current.LogicalRunId
                    current.AuthorityRootUserMessageId
                    providerRun

            let next = AgentPairCursor.recordFailure current.Cursor

            match
                FallbackProjection.applyAdvance
                    identity
                    current.Cursor.Offset
                    next.Offset
                    next.ConsecutiveFailureCount
                    current
            with
            | Error FallbackAdvanceRejection.AlreadyObserved
            | Error FallbackAdvanceRejection.AlreadyExhausted ->
                Ok ConfirmedFailureOutcome.AlreadyRecorded
            | Error FallbackAdvanceRejection.DifferentRun
            | Error FallbackAdvanceRejection.NoCursor ->
                Ok ConfirmedFailureOutcome.NoActiveRun
            | Error FallbackAdvanceRejection.InvalidTransition ->
                Error "Fallback advance violates FALLBACK-007 (offset or count is not the successor)"
            | Error(FallbackAdvanceRejection.InvalidFallbackOffset decodeError) ->
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
                    match AgentPairCursor.recoveryVerdict budget next with
                    | AgentPairCursor.MayContinue _ -> Ok ConfirmedFailureOutcome.RecoveryAdvanced
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
                        | Ok _ -> Ok ConfirmedFailureOutcome.RecoveryExhausted

    let admitConfirmedFailure journal budget sessionId providerRun reason =
        recordConfirmedFailure journal budget sessionId providerRun reason
        |> Result.map (function
            | ConfirmedFailureOutcome.RecoveryExhausted -> RecoveryAdmission.RecoveryExhausted
            | ConfirmedFailureOutcome.RecoveryAdvanced
            | ConfirmedFailureOutcome.AlreadyRecorded
            | ConfirmedFailureOutcome.NoActiveRun -> RecoveryAdmission.ContinueRecovery)
