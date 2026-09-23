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
              // Sphinx clean-break: the only v2 Sphinx kind. The historical sphinx/*
              // kinds left the production program with the old kernel; old events stay
              // readable but are never accepted again.
              "sphinx/v2-transition@1" ]

    let isKnown eventType = Set.contains eventType builtins
