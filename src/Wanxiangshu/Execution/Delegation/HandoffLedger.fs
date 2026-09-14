namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation.Outcome
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
module DelegationHandoffLedger =

    let private previousEnd (journal: AgentJournal) parent route =
        let projection = (AgentJournal.snapshot journal).AgentProjections

        Map.tryFind (DelegationHandoff.key parent route) projection.DelegationCompletedHandoffs
        |> Option.map XTraceCursor.create

    let private traceHead (journal: AgentJournal) sessionId =
        AgentJournal.snapshot journal
        |> fun snapshot -> AgentProjection.tryFind sessionId snapshot.AgentProjections
        |> Option.bind (fun session -> session.XTrace)
        |> Option.defaultValue XTraceProjection.empty
        |> XTraceProjection.headCursor

    let prepare
        (workRecord: DelegationWorkRecordCapability)
        (journal: AgentJournal)
        (parent: SessionId)
        (route: DelegationHandoffRoute)
        : Task<PreparedDelegationHandoff> =
        task {
            let previous = previousEnd journal parent route
            let current = traceHead journal parent
            let handoff = DelegationHandoff.window previous current

            let! parentRecord =
                if handoff.IsInitial then
                    workRecord.ParentWorkRecord parent
                elif XTraceRange.isEmpty handoff.Range then
                    Task.FromResult None
                else
                    workRecord.ParentWorkRecordBounded parent handoff.Range

            return
                { Route = route
                  ParentStartInclusive = XTraceRange.startInclusive handoff.Range
                  ParentRecord = parentRecord
                  ParentEndExclusive = XTraceRange.endExclusive handoff.Range }
        }

    /// PERSIST-002 / DELEG-031 classification: the failure taxonomy IS the
    /// settlement. WriterUnavailable is known-not-committed; WriteUnknown
    /// stays pending-evidence; FactRejected is the frontier invariant cut —
    /// PhaseConflict for the owner, never collapsed to a bare string.
    let private settlementFromAppend
        (parent: SessionId)
        (handoff: PreparedDelegationHandoff)
        (failure: JournalAppendFailure)
        : HandoffCheckpointSettlement =
        let reason = JournalAppendFailure.describe failure

        match failure with
        | JournalAppendFailure.WriterUnavailable _ ->
            HandoffCheckpointSettlement.notCommitted parent handoff reason
        | JournalAppendFailure.WriteUnknown _ ->
            HandoffCheckpointSettlement.unknown parent handoff reason
        | JournalAppendFailure.FactRejected _ ->
            HandoffCheckpointSettlement.phaseConflict parent handoff reason

    let checkpointCompleted
        (journal: AgentJournal)
        (parent: SessionId)
        (handoff: PreparedDelegationHandoff)
        : Task<HandoffCheckpointSettlement> =
        task {
            let! appended =
                AgentJournal.appendAgent
                    (StreamId.Session parent)
                    None
                    (DelegationFact.DelegationHandoffCompleted
                        {| ParentSessionId = parent
                           Route = handoff.Route
                           ParentEndExclusive = XTraceCursor.sequence handoff.ParentEndExclusive |})
                    journal

            // PERSIST-002 / DELEG-031: the failure taxonomy is the settlement.
            // WriterUnavailable is known-not-committed; WriteUnknown stays
            // pending-evidence (never auto-retried or re-emitted); FactRejected
            // is the frontier invariant cut (retreat/negative) and is reported
            // as PhaseConflict for the owner to escalate, never collapsed to a
            // bare string.
            return
                match appended with
                | Ok _ -> HandoffCheckpointSettlement.committed parent handoff
                | Error failure -> settlementFromAppend parent handoff failure
        }

    let port (workRecord: DelegationWorkRecordCapability) (journal: AgentJournal) : ReusableHandoffPort =
        { Prepare = fun parent route -> prepare workRecord journal parent route
          CheckpointCompleted = fun parent handoff -> checkpointCompleted journal parent handoff }
