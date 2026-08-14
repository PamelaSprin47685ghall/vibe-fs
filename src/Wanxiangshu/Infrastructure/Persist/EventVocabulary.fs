namespace Wanxiangshu.Infrastructure.Persist

open Wanxiangshu.Domain

/// Additive authoritative event vocabulary. Unknown durable facts fail closed
/// before they reach the canonical Integrator.
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
              StrengthEventTypes.CandidatePrepared
              StrengthEventTypes.CandidatePromoted
              StrengthEventTypes.FramesTraced
              StrengthEventTypes.CandidateAbandoned ]

    let isKnown eventType = Set.contains eventType builtins
