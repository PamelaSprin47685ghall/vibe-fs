namespace Wanxiangshu.Next.Journal

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome

type JournalAppendFailure =
    { EventId: EventId
      Failure: JournalFailure }

type AgentJournal internal (writer: JournalWriter, initialProjection: ProjectionSet) =
    let gate = obj ()
    let mutable proj = initialProjection

    member _.Writer = writer
    member _.RuntimeId = writer.RuntimeId
    member _.IsPoisoned = writer.IsPoisoned

    member _.Snapshot: ProjectionSet = lock gate (fun () -> proj)

    member _.AppendAgent
        (stream: StreamId)
        (turnId: TurnId option)
        (fact: AgentFact)
        : Result<ProjectionSet, JournalAppendFailure> =
        lock gate (fun () ->
            match writer.Append stream turnId (Fact.Agent fact) with
            | Committed env ->
                let updated = Fold.foldEnvelope proj env
                proj <- updated
                Ok updated
            | CommitUnknown(eventId, failure) -> Error { EventId = eventId; Failure = failure })

    interface IDisposable with
        member _.Dispose() = (writer :> IDisposable).Dispose()

    interface IAsyncDisposable with
        member _.DisposeAsync() =
            (writer :> IAsyncDisposable).DisposeAsync()

module AgentJournal =

    let create (directory: string) (runtimeId: RuntimeId) (processId: int) (startedAt: DateTimeOffset) : AgentJournal =
        let writer, initEnv = JournalWriter.create directory runtimeId processId startedAt
        let initialProj = Fold.foldEnvelope Fold.empty initEnv
        new AgentJournal(writer, initialProj)

    let appendAgent
        (stream: StreamId)
        (turnId: TurnId option)
        (fact: AgentFact)
        (journal: AgentJournal)
        : Result<ProjectionSet, JournalAppendFailure> =
        journal.AppendAgent stream turnId fact

    let snapshot (journal: AgentJournal) : ProjectionSet = journal.Snapshot

    let runtimeId (journal: AgentJournal) : RuntimeId = journal.RuntimeId

    let isPoisoned (journal: AgentJournal) : bool = journal.IsPoisoned
