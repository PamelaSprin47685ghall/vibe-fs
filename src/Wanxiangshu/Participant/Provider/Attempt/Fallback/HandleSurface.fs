namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

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
            let! result =
                FallbackLedger.recordConfirmedFailure
                    handle.Journal
                    budget
                    (SessionId.create session)
                    (ProviderRunIdentity.create providerRun)
                    reason

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
