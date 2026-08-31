namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Execution.Session.ChatExecution

/// Opaque-journal recovery observations for the host recovery boundary.
/// FallbackLedger remains the sole writer; this surface only projects its
/// attempt outcome and resulting cursor state as JSON-native values.
[<RequireQualifiedAccess>]
module FallbackHandleSurface =

    let defaultAutoRecoveryBudget = AgentPairCursor.DefaultAutoRecoveryBudget

    let private outcomeName outcome =
        match outcome with
        | ConfirmedFailureOutcome.RecoveryAdvanced _ -> "Advanced"
        | ConfirmedFailureOutcome.RecoveryExhausted -> "Exhausted"
        | ConfirmedFailureOutcome.AlreadyRecorded -> "AlreadyRecorded"
        | ConfirmedFailureOutcome.NoActiveRun -> "NoActiveRun"

    /// Record one confirmed provider failure through the production ledger using
    /// an opaque JournalHandle. No F# Result/DU crosses the boundary.
    let recordConfirmedFailure
        (handle: Wanxiangshu.Persistence.Journal.JournalHandle)
        (budget: int)
        (session: string)
        (providerRun: string)
        (reason: string)
        : Task<obj> =
        task {
            let sessionId = SessionId.create session
            let providerRunId = ProviderRunIdentity.create providerRun

            let! result =
                match FallbackEvidence.tryCurrentState sessionId (AgentJournal.snapshot handle.Journal) with
                | None -> Task.FromResult(Ok ConfirmedFailureOutcome.NoActiveRun)
                | Some current when budget <> defaultAutoRecoveryBudget ->
                    Task.FromResult(Error "provider recovery budget must equal the declared default")
                | Some current ->
                    let available =
                        if FallbackProjection.mayContinue defaultAutoRecoveryBudget current then
                            ProviderRecoveryBudget.Available
                        else
                            ProviderRecoveryBudget.Exhausted

                    let decision =
                        ExecutionFailurePolicy.decide
                            { Failure = ExecutionFailure.ProviderTransient
                              Lifecycle = DurableExecutionLifecycle.ProviderStarted
                              ExecutionKey =
                                { SessionId = sessionId
                                  PhysicalUserMessageId = PhysicalUserMessageId.create ("proof-" + providerRun) }
                              Capacity = CapacityOwnership.NoCapacityFence
                              Provider =
                                { LogicalRun = current.LogicalRunId
                                  ProviderRun = providerRunId
                                  RequestKind = ProviderRequestKind.WorkMain
                                  RetryBudget = ProviderRecoveryBudget.Exhausted
                                  FallbackBudget = available
                                  Breaker = ProviderBreakerState.Closed } }

                    match decision.Fallback with
                    | FallbackDecision.AdvanceFallback authorization ->
                        FallbackLedger.recordAuthorizedFailure handle.Journal sessionId authorization reason
                    | FallbackDecision.NoFallback -> Task.FromResult(Ok ConfirmedFailureOutcome.AlreadyRecorded)

            return
                match result with
                | Ok outcome ->
                    box
                        {| ok = true
                           outcome = outcomeName outcome |}
                | Error error -> box {| ok = false; error = error |}
        }

    /// Read the durable fallback cursor for one session without exposing the
    /// projection record, map, or closed offset representation.
    let snapshot (handle: Wanxiangshu.Persistence.Journal.JournalHandle) (session: string) : obj =
        match FallbackEvidence.tryCurrentState (SessionId.create session) (AgentJournal.snapshot handle.Journal) with
        | None -> null
        | Some current ->
            box
                {| offset = AgentPairCursor.FallbackOffsetCodec.toByte current.Cursor.Offset
                   failures = current.Cursor.ConsecutiveFailureCount
                   exhausted = current.Exhausted |}
