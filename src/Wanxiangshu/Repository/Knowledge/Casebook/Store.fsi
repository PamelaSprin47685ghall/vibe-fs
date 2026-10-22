namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// CASE-007 / KR-007: Casebook durable facts through the unified EventStore — the only
/// persistence a Case may use (no feature ref / manifest tree / second
/// authority).
module CasebookStore =

    /// True if the event type is one of the Casebook event types.
    val isCasebookEventType: eventType: string -> bool

    /// Integration oracle input decoder. It accepts exactly one EventEnvelope;
    /// history ordering/iteration belongs to CanonicalIntegrator.
    val tryDecodeEnvelope: envelope: EventEnvelope -> Result<CasebookEvent, string>

    /// Append a CaseCaptured event to the Casebook stream.
    val appendCaptured: store: IEventStore -> case: Case -> Task<Result<EventId, CasebookAppendFailure>>

    /// Append a CaseRefreshed event to the Casebook stream.
    val appendRefreshed:
        store: IEventStore ->
        identity: string ->
        q: string ->
        a: string ->
        maintenanceFileState: string ->
        relatedPaths: string list ->
        observations: Observation list ->
            Task<Result<EventId, CasebookAppendFailure>>

    /// Append a CaseAccessed event to the Casebook stream.
    val appendAccessed: store: IEventStore -> identity: string -> Task<Result<EventId, CasebookAppendFailure>>

    /// Append a CaseEvicted event to the Casebook stream.
    val appendEvicted: store: IEventStore -> identity: string -> Task<Result<EventId, CasebookAppendFailure>>
