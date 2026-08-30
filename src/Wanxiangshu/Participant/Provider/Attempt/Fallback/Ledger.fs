namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Strength.Persistence

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// FALLBACK-003 single writer: confirmed provider failure → durable dedupe →
/// cursor advance/exhaust. Callers may retain the precise single-attempt outcome
/// without collapsing duplicate or missing-run states into permission to continue.
module FallbackLedger =

    let private invalidOffsetMessage decodeError =
        match decodeError with
        | AgentPairCursor.FallbackOffsetDecodeError.InvalidFallbackOffset value ->
            $"Fallback advance rejected: corrupt offset byte {value} (FALLBACK-002)"

    let private appendExhausted
        (journal: AgentJournal)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (current: FallbackProjection)
        (next: AgentPairCursor.FallbackCursor)
        : Task<Result<ConfirmedFailureOutcome, string>> =
        task {
            let exhausted =
                FallbackFact.FallbackExhausted
                    {| SessionId = sessionId
                       LogicalRunId = current.LogicalRunId
                       AuthorityRootUserMessageId = current.AuthorityRootUserMessageId
                       FinalConsecutiveFailureCount = next.ConsecutiveFailureCount
                       FinalOffset = AgentPairCursor.FallbackOffsetCodec.toByte next.Offset |}

            let! appended = AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) exhausted journal

            return
                appended
                |> Result.map (fun _ -> ConfirmedFailureOutcome.RecoveryExhausted)
                |> Result.mapError JournalAppendFailure.describe
        }

    let private completeAdvance
        (journal: AgentJournal)
        (budget: int)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (current: FallbackProjection)
        (next: AgentPairCursor.FallbackCursor)
        : Task<Result<ConfirmedFailureOutcome, string>> =
        task {
            match AgentPairCursor.recoveryVerdict budget next with
            | AgentPairCursor.MayContinue _ ->
                let opportunity =
                    RecoverySlot.opportunity RecoverySlot.afterFailureAdvance next.Offset

                return Ok(ConfirmedFailureOutcome.RecoveryAdvanced opportunity)
            | AgentPairCursor.Exhausted _ -> return! appendExhausted journal sessionId providerRun current next
        }

    let private appendAdvanced
        (journal: AgentJournal)
        (budget: int)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (reason: string)
        (current: FallbackProjection)
        (next: AgentPairCursor.FallbackCursor)
        : Task<Result<ConfirmedFailureOutcome, string>> =
        task {
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

            let! appended = AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) advanced journal

            match appended with
            | Error failure -> return Error(JournalAppendFailure.describe failure)
            | Ok _ -> return! completeAdvance journal budget sessionId providerRun current next
        }

    let recordConfirmedFailure
        (journal: AgentJournal)
        (budget: int)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (reason: string)
        : Task<Result<ConfirmedFailureOutcome, string>> =
        task {
            match FallbackEvidence.tryCurrentState sessionId (AgentJournal.snapshot journal) with
            | None -> return Ok ConfirmedFailureOutcome.NoActiveRun
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
                | Error FallbackAdvanceRejection.AlreadyExhausted -> return Ok ConfirmedFailureOutcome.AlreadyRecorded
                | Error FallbackAdvanceRejection.DifferentRun
                | Error FallbackAdvanceRejection.NoCursor -> return Ok ConfirmedFailureOutcome.NoActiveRun
                | Error FallbackAdvanceRejection.InvalidTransition ->
                    return Error "Fallback advance violates FALLBACK-007 (offset or count is not the successor)"
                | Error(FallbackAdvanceRejection.InvalidFallbackOffset decodeError) ->
                    return Error(invalidOffsetMessage decodeError)
                | Ok _ -> return! appendAdvanced journal budget sessionId providerRun reason current next
        }

    let private appendSuccessFact
        (journal: AgentJournal)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (current: FallbackProjection)
        : Task<Result<unit, string>> =
        task {
            let succeeded =
                FallbackFact.FallbackSucceeded
                    {| SessionId = sessionId
                       LogicalRunId = current.LogicalRunId
                       AuthorityRootUserMessageId = current.AuthorityRootUserMessageId
                       ProviderRun = providerRun |}

            let! appended = AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) succeeded journal

            return
                appended
                |> Result.map (fun _ -> ())
                |> Result.mapError JournalAppendFailure.describe
        }

    let private recordSuccessForCurrent
        (journal: AgentJournal)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (current: FallbackProjection)
        : Task<Result<unit, string>> =
        if current.Cursor.ConsecutiveFailureCount = 0 then
            Task.FromResult(Ok())
        else
            appendSuccessFact journal sessionId providerRun current

    let recordConfirmedSuccess
        (journal: AgentJournal)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        : Task<Result<unit, string>> =
        task {
            match FallbackEvidence.tryCurrentState sessionId (AgentJournal.snapshot journal) with
            | None -> return Error "NoActiveRun: no cursor for session"
            | Some current -> return! recordSuccessForCurrent journal sessionId providerRun current
        }
