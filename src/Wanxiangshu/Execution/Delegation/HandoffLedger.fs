namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module DelegationHandoffLedger =

    let private previousEnd (journal: AgentJournal) parent route =
        let projection = (AgentJournal.snapshot journal).AgentProjections
        Map.tryFind (DelegationHandoff.key parent route) projection.DelegationCompletedHandoffs

    let private traceHead (journal: AgentJournal) sessionId =
        AgentJournal.snapshot journal
        |> fun snapshot -> AgentProjection.tryFind sessionId snapshot.AgentProjections
        |> Option.bind (fun session -> session.XTrace)
        |> Option.map XTraceProjection.head
        |> Option.defaultValue 0L

    let prepare
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
                    LifecycleWorkRecordProjection.lifecycleWorkRecord (Some journal) parent true
                elif handoff.Range.StartInclusive.Sequence = handoff.Range.EndExclusive.Sequence then
                    Task.FromResult None
                else
                    LifecycleWorkRecordProjection.lifecycleWorkRecordBounded (Some journal) parent handoff.Range

            return
                { Route = route
                  ParentStartInclusive = handoff.Range.StartInclusive
                  ParentRecord = parentRecord
                  ParentEndExclusive = handoff.Range.EndExclusive }
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
                           ParentEndExclusive = handoff.ParentEndExclusive.Sequence |})
                    journal

            return appended |> Result.map ignore |> Result.mapError JournalAppendFailure.describe
        }

    let port (journal: AgentJournal) : ReusableHandoffPort =
        { Prepare = fun parent route -> prepare journal parent route
          CheckpointCompleted = fun parent handoff -> checkpointCompleted journal parent handoff }
