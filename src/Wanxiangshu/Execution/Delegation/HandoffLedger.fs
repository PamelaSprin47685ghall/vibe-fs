namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

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

    let checkpointCompleted
        (journal: AgentJournal)
        (parent: SessionId)
        (handoff: PreparedDelegationHandoff)
        : Task<Result<unit, string>> =
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

            return appended |> Result.map ignore |> Result.mapError JournalAppendFailure.describe
        }

    let port (workRecord: DelegationWorkRecordCapability) (journal: AgentJournal) : ReusableHandoffPort =
        { Prepare = fun parent route -> prepare workRecord journal parent route
          CheckpointCompleted = fun parent handoff -> checkpointCompleted journal parent handoff }
