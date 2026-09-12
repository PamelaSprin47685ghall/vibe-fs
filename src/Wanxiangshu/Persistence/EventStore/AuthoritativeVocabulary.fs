namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Sphinx
open Wanxiangshu.Strength
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js

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
              yield! SphinxEventTypes.all ]

    let isKnown eventType = Set.contains eventType builtins
