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
open Wanxiangshu.Foundation.Identity

/// One registered business integration oracle. It receives exactly one durable
/// EventEnvelope and its own current slot; it never receives a history reader.
type IntegrationRule =
    { Name: string
      Initial: obj
      Accepts: EventEnvelope -> bool
      Integrate: obj -> EventEnvelope -> Result<obj, string> }

/// Structural event-graph Current is one registered Integrator slot, not a
/// second history fold. It preserves every stream frontier so conflict remains
/// distinguishable from an empty stream after the online-Git projector removal.
type StructuralProjection =
    { Heads: Map<string, Set<EventId>> }

[<RequireQualifiedAccess>]
module StructuralProjection =
    let empty = { Heads = Map.empty }

    let heads (streamId: EventStreamId) (projection: StructuralProjection) =
        projection.Heads
        |> Map.tryFind (EventStreamId.value streamId)
        |> Option.defaultValue Set.empty
        |> Set.toList

    let apply (projection: StructuralProjection) (envelope: EventEnvelope) =
        let key = EventStreamId.value envelope.StreamId
        let prior = Map.tryFind key projection.Heads |> Option.defaultValue Set.empty
        let next = envelope.Parents |> List.fold (fun heads parent -> Set.remove parent heads) prior |> Set.add envelope.EventId
        { projection with Heads = Map.add key next projection.Heads }

/// Read-only view exposed by the unique canonical Integrator.
type ICanonicalIntegrator =
    /// Boot/reload ownership: only the canonical Integrator opens local event
    /// history for the purpose of deriving Current.
    abstract ReloadLocal: commonDir: string -> Result<unit, string>
    /// Validate a live batch against Current and return the commit closure.
    /// EventStore invokes the closure only after the complete canonical lines are durable.
    abstract PrepareLive: events: EventEnvelope list -> Result<(unit -> unit), string>
    abstract TryCurrent: key: string -> obj option
    abstract TryEvent: eventId: EventId -> EventEnvelope option
    abstract TryHeads: streamId: EventStreamId -> EventId list
    abstract TryHead: streamId: EventStreamId -> EventId option

[<RequireQualifiedAccess>]
module IntegrationCurrent =
    let tryGet<'T> (key: string) (integrator: ICanonicalIntegrator) : 'T option =
        integrator.TryCurrent key |> Option.map unbox<'T>
