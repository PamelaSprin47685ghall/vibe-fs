namespace Wanxiangshu.Persistence.Journal

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts

type JournalChange =
    { Revision: JournalRevision
      Envelope: Envelope }

type MagicTodoAppendReceipt =
    { EventId: EventId
      Projection: ProjectionSet }

type JournalAppendFailure =
    | WriteUnknown of EventId * JournalFailure
    | WriterUnavailable of EventId * JournalUnavailable
    | FactRejected of EventId * FoldRejection

module JournalAppendFailure =
    val toExecutionFailure: JournalAppendFailure -> ExecutionFailure
    val describe: failure: JournalAppendFailure -> string

type AgentJournal =
    interface IAsyncDisposable
    interface IDisposable

    internal new: writer: IJournalWriter * initialProjection: ProjectionSet -> AgentJournal

    member AppendAgent:
        stream: StreamId ->
        providerRun: ProviderRunIdentity option ->
        fact: AgentFact ->
            Task<Result<ProjectionSet, JournalAppendFailure>>

    member AppendMagicTodo:
        stream: StreamId ->
        providerRun: ProviderRunIdentity option ->
        fact: MagicTodoFact ->
            Task<Result<MagicTodoAppendReceipt, JournalAppendFailure>>

    member AwaitChangeFrom: fromRevision: JournalRevision -> Task<JournalChange>

    member AwaitChangeFromOrCancel:
        fromRevision: JournalRevision * cancellation: CancellationToken -> Task<JournalChange option>

    member WriteBlob: content: string -> Task<Result<BlobWriteReceipt, string>>
    member IsPoisoned: bool
    member Revision: JournalRevision
    member RuntimeId: RuntimeId
    member Snapshot: ProjectionSet
    member SnapshotWithRevision: ProjectionSet * JournalRevision
    member Writer: IJournalWriter

module AgentJournal =
    val createFromProjection: writer: IJournalWriter -> projection: ProjectionSet -> Result<AgentJournal, FoldRejection>

    val appendAgent:
        stream: StreamId ->
        providerRun: ProviderRunIdentity option ->
        fact: AgentFact ->
        journal: AgentJournal ->
            Task<Result<ProjectionSet, JournalAppendFailure>>

    val appendMagicTodo:
        stream: StreamId ->
        providerRun: ProviderRunIdentity option ->
        fact: MagicTodoFact ->
        journal: AgentJournal ->
            Task<Result<MagicTodoAppendReceipt, JournalAppendFailure>>

    val snapshot: journal: AgentJournal -> ProjectionSet
    val revision: journal: AgentJournal -> JournalRevision
    val snapshotWithRevision: journal: AgentJournal -> ProjectionSet * JournalRevision
    val awaitChangeFrom: fromRevision: JournalRevision -> journal: AgentJournal -> Task<JournalChange>

    val awaitChangeFromOrCancel:
        fromRevision: JournalRevision ->
        cancellation: CancellationToken ->
        journal: AgentJournal ->
            Task<JournalChange option>

    val handleProjection: journal: AgentJournal -> sessionId: SessionId -> AgentLinkageProjection
    val runtimeId: journal: AgentJournal -> RuntimeId
    val writeBlob: content: string -> journal: AgentJournal -> Task<Result<BlobWriteReceipt, string>>
    val isPoisoned: journal: AgentJournal -> bool
