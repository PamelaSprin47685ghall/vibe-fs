namespace Wanxiangshu.Mission.WorkRecord

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

module LifecycleWorkRecordProjection =
    val lifecycleWorkRecordFromSnapshot:
        durable: AgentJournal ->
        snapshot: ProjectionSet ->
        sessionId: SessionId ->
        includeOpening: bool ->
        coverageOverride: RecordCoverage option ->
            Task<string option>

    val lifecycleWorkRecord:
        journal: AgentJournal option -> sessionId: SessionId -> includeOpening: bool -> Task<string option>

    val lifecycleWorkRecordBoundedFromSnapshot:
        durable: AgentJournal ->
        snapshot: ProjectionSet ->
        sessionId: SessionId ->
        range: XTraceRange ->
            Task<string option>

    val lifecycleWorkRecordBoundedFromSnapshotForRun:
        durable: AgentJournal ->
        snapshot: ProjectionSet ->
        sessionId: SessionId ->
        range: XTraceRange ->
        providerRun: ProviderRunIdentity ->
            Task<string option>

    val lifecycleWorkRecordBounded:
        journal: AgentJournal option -> sessionId: SessionId -> range: XTraceRange -> Task<string option>

    val lifecycleWorkRecordBoundedForRun:
        journal: AgentJournal option ->
        sessionId: SessionId ->
        range: XTraceRange ->
        providerRun: ProviderRunIdentity ->
            Task<string option>
