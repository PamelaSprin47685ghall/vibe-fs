namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Strength
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Sphinx.V2.Core

[<RequireQualifiedAccess>]
module AuthoritativeEventTypes =
    let private builtins =
        set
            [ // Spine-owned durable envelope/cut vocabulary.
              "JournalEnvelope"
              ProjectionCutTailEvent.EventType
              // Legacy job vocabulary: no in-tree producer or consumer remains;
              // the durable-convergence laws still persist these names.
              "JobRequested"
              "JobAccepted"
              "JobRejected"
              "JobConflictResolved"
              // Domain-owned names, joined from the owning vocabulary contracts.
              yield! JsTransactionEventTypes.all
              yield! CasebookEventTypes.all
              yield! StrengthEventTypes.all
              // Sphinx clean-break: the v2 kinds, joined from the owning Core
              // vocabulary (@1 stays readable, strict @2 is what new writes use).
              // The historical sphinx/* kinds left the production program with the
              // old kernel; old events stay readable but are never accepted again.
              yield! SphinxV2EventTypes.all ]

    let isKnown eventType = Set.contains eventType builtins
