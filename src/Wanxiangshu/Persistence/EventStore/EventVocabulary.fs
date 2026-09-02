namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Strength

/// Additive authoritative event vocabulary. Unknown durable facts fail closed
/// before they reach the canonical Integrator.
[<RequireQualifiedAccess>]
module ProjectionCutTailEvent =
    [<Literal>]
    let EventType = "ProjectionCutTail"

    let streamId rule =
        EventStreamId.create ("integrator/cut-tail/" + rule)

[<RequireQualifiedAccess>]
module AuthoritativeEventTypes =
    let private builtins =
        set
            [ "JobRequested"
              "JobAccepted"
              "JobRejected"
              "JobConflictResolved"
              "JournalEnvelope"
              "JsTransactionPrepared"
              "JsTransactionCommitted"
              "InspectorCaseCaptured"
              "InspectorCaseRefreshed"
              "InspectorCaseAccessed"
              "InspectorCaseEvicted"
              ProjectionCutTailEvent.EventType
              StrengthEventTypes.CandidatePrepared
              StrengthEventTypes.CandidatePromoted
              StrengthEventTypes.FramesTraced
              StrengthEventTypes.CandidateAbandoned ]

    let isKnown eventType = Set.contains eventType builtins
