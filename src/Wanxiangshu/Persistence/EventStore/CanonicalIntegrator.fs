namespace Wanxiangshu.Persistence.EventStore

open System
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
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation.Identity
open FsToolkit.ErrorHandling

/// Structural frontier oracle. It sees every durable event but owns no business
/// meaning; DomainConflict is simply `heads.Count > 1` in this Integrator slot.
[<RequireQualifiedAccess>]
module StructuralIntegration =
    let rule: IntegrationRule =
        { Name = "Structural"
          Initial = box StructuralProjection.empty
          Accepts = fun _ -> true
          Integrate =
            fun current envelope ->
                StructuralProjection.apply (unbox<StructuralProjection> current) envelope
                |> box
                |> Ok }

/// Registered Journal oracle: one EventEnvelope in, one ProjectionSet out.
[<RequireQualifiedAccess>]
module JournalIntegration =
    let rule: IntegrationRule =
        { Name = "Journal"
          Initial = box Fold.empty
          Accepts = fun envelope -> envelope.EventType = EventStoreJournalCodec.JournalEnvelopeEventType
          Integrate =
            fun current envelope ->
                match EventStoreJournalCodec.tryDecode envelope with
                | Error error -> Error error
                | Ok journalEnvelope ->
                    Fold.foldEnvelope (unbox<ProjectionSet> current) journalEnvelope
                    |> Result.map box
                    |> Result.mapError (fun rejection ->
                        sprintf "Journal fact '%s' rejected: %s" rejection.Fact rejection.Reason) }

[<RequireQualifiedAccess>]
module StrengthIntegration =
    let rule: IntegrationRule =
        { Name = "Strength"
          Initial = box StrengthProjection.empty
          Accepts = fun envelope -> StrengthEventTypes.isStrengthEvent envelope.EventType
          Integrate =
            fun current envelope ->
                match StrengthStore.tryDecodeEnvelope envelope with
                | Error error -> Error error
                | Ok event ->
                    StrengthProjection.apply (unbox<StrengthProjection> current) event
                    |> Result.map box
                    |> Result.mapError (fun error -> sprintf "Strength integration rejected: %A" error) }

[<RequireQualifiedAccess>]
module CasebookIntegration =
    let rule: IntegrationRule =
        { Name = "Casebook"
          Initial = box CasebookProjection.emptyState
          Accepts = fun envelope -> CasebookStore.isCasebookEventType envelope.EventType
          Integrate =
            fun current envelope ->
                CasebookStore.tryDecodeEnvelope envelope
                |> Result.map (fun event ->
                    CasebookProjection.apply (unbox<CasebookProjection.State> current) event |> box) }

[<RequireQualifiedAccess>]
module JsTransactionIntegration =
    let rule: IntegrationRule =
        { Name = "JsTransaction"
          Initial = box JsTransactionProjection.empty
          Accepts = fun envelope -> JsToolsTransactionStore.isTransactionEventType envelope.EventType
          Integrate =
            fun current envelope ->
                let projection = unbox<JsTransactionProjection> current

                match JsToolsTransactionStore.tryDecodeEnvelope envelope with
                | Error error -> Error error
                | Ok(JsToolsTransactionStore.DecodedTransactionEvent.Prepared prepared) ->
                    JsTransactionProjection.prepared envelope.EventId prepared projection
                    |> box
                    |> Ok
                | Ok(JsToolsTransactionStore.DecodedTransactionEvent.Committed committed) ->
                    JsTransactionProjection.committed envelope.EventId committed projection
                    |> box
                    |> Ok }

/// Tiny registration CE. Business modules contribute only single-event oracles;
/// this is the only place that assembles a history integration program.
type IntegratorBuilder() =
    member _.Yield(()) : IntegrationRule list = []
    member _.Zero() : IntegrationRule list = []
    member _.Run(rules: IntegrationRule list) = List.rev rules

    [<CustomOperation("register")>]
    member _.Register(rules: IntegrationRule list, rule: IntegrationRule) = rule :: rules

[<RequireQualifiedAccess>]
module CanonicalIntegrator =

    let integrator = IntegratorBuilder()

    let private program =
        integrator {
            register StructuralIntegration.rule
            register JournalIntegration.rule
            register StrengthIntegration.rule
            register CasebookIntegration.rule
            register JsTransactionIntegration.rule
        }

    type private IntegratorState =
        { Currents: Map<string, obj>
          Events: Map<string, EventEnvelope> }

    let private initialState =
        { Currents = program |> List.map (fun rule -> rule.Name, rule.Initial) |> Map.ofList
          Events = Map.empty }

    let private eventKey (eventId: EventId) = EventId.value eventId
    let private streamKey (streamId: EventStreamId) = EventStreamId.value streamId

    let private matchingRule (envelope: EventEnvelope) =
        program |> List.filter (fun rule -> rule.Accepts envelope)

    /// The only single-event integration primitive. Both replay and live paths
    /// call this exact function.
    let private integrateExisting
        (state: IntegratorState)
        (existing: EventEnvelope)
        (normalized: EventEnvelope)
        : Result<IntegratorState, string> =
        CanonicalEventCodec.checkIdentity existing normalized
        |> Result.map (fun () -> state)
        |> Result.mapError (sprintf "identity collision: %A")

    let private integrateRules
        (currents: Map<string, obj>)
        (normalized: EventEnvelope)
        (remaining: IntegrationRule list)
        : Result<Map<string, obj>, string> =
        let rec loop (currents: Map<string, obj>) (remaining: IntegrationRule list) =
            match remaining with
            | [] -> Ok currents
            | rule :: tail ->
                let current = currents |> Map.tryFind rule.Name |> Option.defaultValue rule.Initial

                rule.Integrate current normalized
                |> Result.mapError (sprintf "%s: %s" rule.Name)
                |> Result.bind (fun next -> loop (Map.add rule.Name next currents) tail)

        loop currents remaining

    let private integrateNew
        (state: IntegratorState)
        (normalized: EventEnvelope)
        (key: string)
        : Result<IntegratorState, string> =
        let missingParent =
            normalized.Parents
            |> List.tryFind (fun parent -> not (Map.containsKey (eventKey parent) state.Events))

        match missingParent with
        | Some parent -> Error(sprintf "missing parent during integration: %s" (EventId.value parent))
        | None ->
            integrateRules state.Currents normalized (matchingRule normalized)
            |> Result.map (fun currents ->
                { Currents = currents
                  Events = Map.add key normalized state.Events })

    let private integrateOne (state: IntegratorState) (envelope: EventEnvelope) : Result<IntegratorState, string> =
        let normalized = EventEnvelope.normalize envelope
        let key = eventKey normalized.EventId

        match Map.tryFind key state.Events with
        | Some existing -> integrateExisting state existing normalized
        | None -> integrateNew state normalized key

    /// Boot history ordering is delegated to the one structural k-way primitive.
    /// This module alone turns that ordered history into business Current.
    let private replay (streams: (string * EventEnvelope list) list) : Result<IntegratorState, string> =
        let rec integrate (state: IntegratorState) (remaining: EventEnvelope list) =
            match remaining with
            | [] -> Ok state
            | head :: tail -> integrateOne state head |> Result.bind (fun next -> integrate next tail)

        EventKWayMerge.merge streams
        |> Result.mapError (sprintf "writer-stream replay invalid: %A")
        |> Result.bind (fun ordered -> integrate initialState ordered)

    /// Live and boot both reduce through integrateOne. This helper exists so the
    /// two entry paths cannot grow separate reducers while still letting EventStore
    /// prepare a whole append batch before making its bytes durable.
    let private integrateLive (state: IntegratorState) (events: EventEnvelope list) =
        let rec loop (current: IntegratorState) (remaining: EventEnvelope list) =
            match remaining with
            | [] -> Ok current
            | head :: tail -> integrateOne current head |> Result.bind (fun next -> loop next tail)

        loop state events

    let private validateLocalHistory commonDir streams =
        result {
            let events = streams |> List.collect snd

            do!
                events
                |> List.tryFind (fun envelope -> not (AuthoritativeEventTypes.isKnown envelope.EventType))
                |> Option.map (fun envelope ->
                    Error(sprintf "unknown durable event type during replay: %s" envelope.EventType))
                |> Option.defaultValue (Ok())

            do!
                events
                |> List.collect (fun envelope -> envelope.PayloadRefs)
                |> PayloadRefs.canonicalize
                |> List.tryFind (ProcessEventLog.payloadExists commonDir >> not)
                |> Option.map (fun payloadRef ->
                    Error(sprintf "missing durable payload during replay: %s" (PayloadRef.value payloadRef)))
                |> Option.defaultValue (Ok())
        }

    let create () : ICanonicalIntegrator =
        let gate = obj ()
        // DSL-MUTABLE: resource — the sole process Current owned by this Integrator.
        let mutable state = initialState
        // DSL-MUTABLE: resource — physical publication generation; not domain state.
        let mutable generation = 0L

        { new ICanonicalIntegrator with
            member _.ReloadLocal(commonDir) =
                lock gate (fun () ->
                    result {
                        let! streams =
                            ProcessEventLog.readStreams commonDir
                            |> Result.mapError (sprintf "local event history read failed: %A")

                        do! validateLocalHistory commonDir streams
                        let! replayed = replay streams
                        state <- replayed
                        generation <- generation + 1L
                        return ()
                    })

            member _.PrepareLive(events) =
                lock gate (fun () ->
                    result {
                        let! prepared = integrateLive state events
                        let expectedGeneration = generation

                        return
                            fun () ->
                                lock gate (fun () ->
                                    if generation <> expectedGeneration then
                                        failwith "CanonicalIntegrator Current changed between prepare and durable append"

                                    state <- prepared
                                    generation <- generation + 1L)
                    })

            member _.TryCurrent(key) =
                lock gate (fun () -> Map.tryFind key state.Currents)

            member _.TryEvent(eventId) =
                lock gate (fun () -> Map.tryFind (eventKey eventId) state.Events)

            member _.TryHeads(streamId) =
                lock gate (fun () ->
                    match Map.tryFind "Structural" state.Currents with
                    | None -> []
                    | Some current -> StructuralProjection.heads streamId (unbox<StructuralProjection> current))

            member this.TryHead(streamId) =
                match this.TryHeads streamId with
                | [ head ] -> Some head
                | _ -> None

            member _.AllHeads() =
                lock gate (fun () ->
                    match Map.tryFind "Structural" state.Currents with
                    | None -> []
                    | Some current -> StructuralProjection.allHeads (unbox<StructuralProjection> current)) }
