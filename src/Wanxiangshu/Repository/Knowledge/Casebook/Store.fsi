namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// CASE-007: Casebook durable facts through the unified EventStore — the only
/// persistence a Case may use (no feature ref / manifest tree / second
/// authority). Event types: InspectorCaseCaptured / InspectorCaseRefreshed /
/// InspectorCaseAccessed / InspectorCaseEvicted; Q/A/observations ride the
/// event payload (large bodies via PayloadRef in later phases).
module CasebookStore =

    /// Canonical Casebook event stream id.
    val CasebookStream: string

    /// Event type for a captured Case.
    val CapturedEventType: string

    /// Event type for a refreshed Case.
    val RefreshedEventType: string

    /// Event type for a touched Case.
    val AccessedEventType: string

    /// Event type for an evicted Case.
    val EvictedEventType: string

    /// True if the event type is one of the four Casebook event types.
    val isCasebookEventType: eventType: string -> bool

    /// Integration oracle input decoder. It accepts exactly one EventEnvelope;
    /// history ordering/iteration belongs to CanonicalIntegrator.
    val tryDecodeEnvelope: envelope: EventEnvelope -> Result<CasebookEvent, string>

    /// Append a CaseCaptured event to the Casebook stream.
    val appendCaptured: store: IEventStore -> case: Case -> Task<Result<EventId, string>>

    /// Append a CaseRefreshed event to the Casebook stream.
    val appendRefreshed:
        store: IEventStore ->
        sessionId: string ->
        q: string ->
        a: string ->
        observations: Observation list ->
            Task<Result<EventId, string>>

    /// Append a CaseAccessed event to the Casebook stream.
    val appendAccessed: store: IEventStore -> sessionId: string -> Task<Result<EventId, string>>

    /// Append a CaseEvicted event to the Casebook stream.
    val appendEvicted: store: IEventStore -> sessionId: string -> Task<Result<EventId, string>>
