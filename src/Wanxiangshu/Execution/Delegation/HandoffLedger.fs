namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module DelegationHandoffLedger =

    let private previousEnd (journal: AgentJournal) parent delegateSession =
        let projection = (AgentJournal.snapshot journal).AgentProjections
        Map.tryFind (DelegationHandoff.key parent delegateSession) projection.DelegationHandoffs

    let private traceHead (journal: AgentJournal) sessionId =
        AgentJournal.snapshot journal
        |> fun snapshot -> AgentProjection.tryFind sessionId snapshot.AgentProjections
        |> Option.bind (fun session -> session.XTrace)
        |> Option.map XTraceProjection.head
        |> Option.defaultValue 0L

    let prepare
        (journal: AgentJournal)
        (parent: SessionId)
        (delegateSession: SessionId)
        : Task<PreparedDelegationHandoff> =
        task {
            let previous = previousEnd journal parent delegateSession
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
                { ParentRecord = parentRecord
                  ParentEndExclusive = handoff.Range.EndExclusive }
        }

    let prepareInitial (journal: AgentJournal) (parent: SessionId) : Task<PreparedDelegationHandoff> =
        task {
            let current = traceHead journal parent
            let! parentRecord = LifecycleWorkRecordProjection.lifecycleWorkRecord (Some journal) parent true

            return
                { ParentRecord = parentRecord
                  ParentEndExclusive = { Sequence = current } }
        }

    let advance
        (journal: AgentJournal)
        (parent: SessionId)
        (delegateSession: SessionId)
        (parentEndExclusive: XTraceCursor)
        : Task<Result<unit, string>> =
        task {
            let! appended =
                AgentJournal.appendAgent
                    (StreamId.Session parent)
                    None
                    (DelegationFact.DelegationHandoffAdvanced
                        {| ParentSessionId = parent
                           DelegateSessionId = delegateSession
                           ParentEndExclusive = parentEndExclusive.Sequence |})
                    journal

            return appended |> Result.map ignore |> Result.mapError JournalAppendFailure.describe
        }
