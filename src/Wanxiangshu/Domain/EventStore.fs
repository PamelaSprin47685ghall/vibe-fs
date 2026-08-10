namespace Wanxiangshu.Domain

open Thoth.Json
open Wanxiangshu.Kernel.Identity

/// Opaque Persist-mapped payload handle. Domain never interprets Git OIDs (§45 / §7.1).
type PayloadRef = private PayloadRef of string

module PayloadRef =
    let create (value: string) = PayloadRef value
    let value (PayloadRef v) = v

    let compare (PayloadRef a) (PayloadRef b) = compare a b

/// Durable event-store stream identity (`stream_id` in §5).
/// Named EventStreamId so `open Wanxiangshu.Domain` does not shadow Journal.StreamId
/// (same rationale as ProjectionBlogFrameKind).
type EventStreamId = private EventStreamId of string

module EventStreamId =
    let create (value: string) = EventStreamId value
    let value (EventStreamId v) = v

/// Versionless causal event envelope (storage.md §5 / Phase 2 §2.1).
/// Forbidden here: GitObjectId / RootOid / StoreSnapshot / AppendCandidate.
type EventEnvelope =
    {
        EventId: EventId
        StreamId: EventStreamId
        /// Additive vocabulary; committed shapes freeze (§5.0.1 / §5.2).
        EventType: string
        /// Causal predecessors. Canonicalize via EventParents before persist.
        Parents: EventId list
        /// Canonical JSON body; large material referenced only via PayloadRefs.
        Payload: JsonValue
        /// Opaque payload handles; Persist maps these to GitObjectId (§7.1).
        PayloadRefs: PayloadRef list
    }

module EventParents =
    /// §5.0: dedupe, then EventId canonical text order (hex / lexicographic).
    let canonicalize (parents: EventId list) : EventId list =
        parents
        |> List.distinct
        |> List.sortWith (fun a b -> compare (EventId.value a) (EventId.value b))

module PayloadRefs =
    /// §7.1 / §5.0: dedupe, then opaque-ref text order (OID text once Persist maps).
    let canonicalize (refs: PayloadRef list) : PayloadRef list =
        refs
        |> List.distinct
        |> List.sortWith PayloadRef.compare

module EventEnvelope =
    /// Normalize set-shaped fields so identity bytes do not depend on caller order.
    let normalize (envelope: EventEnvelope) : EventEnvelope =
        { envelope with
            Parents = EventParents.canonicalize envelope.Parents
            PayloadRefs = PayloadRefs.canonicalize envelope.PayloadRefs }
