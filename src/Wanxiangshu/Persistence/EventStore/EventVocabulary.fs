namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.Persistence

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

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
