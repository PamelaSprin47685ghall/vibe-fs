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
open Thoth.Json

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
                |> Ok
          PlanCut = fun _ _ _ _ -> Error "Structural integration cannot be cut"
          ApplyCut = fun current _ -> Ok current }

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
                        sprintf "Journal fact '%s' rejected: %s" rejection.Fact rejection.Reason)
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }

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
                    |> Result.mapError (fun error -> sprintf "Strength integration rejected: %A" error)
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }

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
                    CasebookProjection.apply (unbox<CasebookProjection.State> current) event |> box)
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }

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
                    |> Ok
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }

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

    type private RuleFault =
        { FailedEventId: EventId
          Reason: string }

    type private IntegratorState =
        { Currents: Map<string, obj>
          Events: Map<string, EventEnvelope>
          Faults: Map<string, RuleFault> }

    type private IntegrationStep =
        { State: IntegratorState
          FailedRules: (string * string) list }

    type private CutPayload =
        { Rule: string
          FailedEventId: EventId
          Reason: string
          ResetJson: string }

    let private initialState =
        { Currents = program |> List.map (fun rule -> rule.Name, rule.Initial) |> Map.ofList
          Events = Map.empty
          Faults = Map.empty }

    let private eventKey (eventId: EventId) = EventId.value eventId
    let private streamKey (streamId: EventStreamId) = EventStreamId.value streamId

    let private structuralRule = StructuralIntegration.rule

    let private businessRules =
        program |> List.filter (fun rule -> rule.Name <> structuralRule.Name)

    let private matchingBusinessRules (envelope: EventEnvelope) =
        businessRules |> List.filter (fun rule -> rule.Accepts envelope)

    let private tryRule name = businessRules |> List.tryFind (fun rule -> rule.Name = name)

    let private encodeCutPayload (payload: CutPayload) =
        Encode.object
            [ "rule", Encode.string payload.Rule
              "failed_event_id", Encode.string (EventId.value payload.FailedEventId)
              "reason", Encode.string payload.Reason
              "reset_json", Encode.string payload.ResetJson ]

    let private tryDecodeCutPayload (envelope: EventEnvelope) =
        if envelope.EventType <> ProjectionCutTailEvent.EventType then
            None
        else
            Decode.fromValue
                "$"
                (Decode.object (fun get ->
                    { Rule = get.Required.Field "rule" Decode.string
                      FailedEventId = EventId.create (get.Required.Field "failed_event_id" Decode.string)
                      Reason = get.Required.Field "reason" Decode.string
                      ResetJson = get.Required.Field "reset_json" Decode.string }))
                envelope.Payload
            |> Result.toOption

    let private structuralStep (state: IntegratorState) (normalized: EventEnvelope) =
        let current =
            state.Currents |> Map.tryFind structuralRule.Name |> Option.defaultValue structuralRule.Initial

        match structuralRule.Integrate current normalized with
        | Ok next ->
            { state with
                Currents = Map.add structuralRule.Name next state.Currents }
        | Error _ -> state

    let private applyCut (state: IntegratorState) (payload: CutPayload) =
        match tryRule payload.Rule with
        | None -> state
        | Some rule ->
            let current = state.Currents |> Map.tryFind rule.Name |> Option.defaultValue rule.Initial

            match rule.ApplyCut current payload.ResetJson with
            | Error _ -> state
            | Ok reset ->
                { state with
                    Currents = Map.add rule.Name reset state.Currents
                    Faults = Map.remove rule.Name state.Faults }

    let private integrateBusiness (state: IntegratorState) (normalized: EventEnvelope) =
        let rec loop (current: IntegratorState) failures (remaining: IntegrationRule list) =
            match remaining with
            | [] ->
                { State = current
                  FailedRules = List.rev failures }
            | rule :: tail ->
                if Map.containsKey rule.Name current.Faults then
                    loop current failures tail
                else
                    let prior = current.Currents |> Map.tryFind rule.Name |> Option.defaultValue rule.Initial

                    match rule.Integrate prior normalized with
                    | Ok next ->
                        loop
                            { current with
                                Currents = Map.add rule.Name next current.Currents }
                            failures
                            tail
                    | Error reason ->
                        let fault =
                            { FailedEventId = normalized.EventId
                              Reason = reason }

                        loop
                            { current with
                                Faults = Map.add rule.Name fault current.Faults }
                            ((rule.Name, reason) :: failures)
                            tail

        loop state [] (matchingBusinessRules normalized)

    /// The only single-event integration primitive. Replay preserves timeline
    /// order exactly: a semantic failure leaves last-good Current in place and
    /// marks that rule faulted; a later durable ProjectionCutTail applies the
    /// rule-owned reset patch and clears the fault.
    let private integrateOne (state: IntegratorState) (envelope: EventEnvelope) : Result<IntegrationStep, string> =
        let normalized = EventEnvelope.normalize envelope
        let key = eventKey normalized.EventId

        match Map.tryFind key state.Events with
        | Some existing ->
            CanonicalEventCodec.checkIdentity existing normalized
            |> Result.mapError (sprintf "identity collision: %A")
            |> Result.map (fun () ->
                { State = state
                  FailedRules = [] })
        | None ->
            let missingParent =
                normalized.Parents
                |> List.tryFind (fun parent -> not (Map.containsKey (eventKey parent) state.Events))

            match missingParent with
            | Some parent -> Error(sprintf "missing parent during integration: %s" (EventId.value parent))
            | None ->
                let withStructure = structuralStep state normalized

                let semantic =
                    match tryDecodeCutPayload normalized with
                    | Some payload ->
                        { State = applyCut withStructure payload
                          FailedRules = [] }
                    | None -> integrateBusiness withStructure normalized

                Ok
                    { semantic with
                        State =
                            { semantic.State with
                                Events = Map.add key normalized semantic.State.Events } }

    /// Boot history ordering is delegated to the one structural k-way primitive.
    /// Business semantic failures never abort replay; they cut only that rule's
    /// tail until an in-order ProjectionCutTail reset fact is encountered.
    let private replay (streams: (string * EventEnvelope list) list) : Result<IntegratorState, string> =
        let rec integrate (state: IntegratorState) (remaining: EventEnvelope list) =
            match remaining with
            | [] -> Ok state
            | head :: tail ->
                integrateOne state head
                |> Result.bind (fun step -> integrate step.State tail)

        EventKWayMerge.merge streams
        |> Result.mapError (sprintf "writer-stream replay invalid: %A")
        |> Result.bind (fun ordered -> integrate initialState ordered)

    let private fullReplayGate = obj ()
    // DSL-MUTABLE: resource — one process-wide emergency full-history replay budget.
    let mutable private fullReplayUsed = false

    let private trySpendFullReplayBudget () =
        lock fullReplayGate (fun () ->
            if fullReplayUsed then
                false
            else
                fullReplayUsed <- true
                true)

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
        // DSL-MUTABLE: resource — last loaded local EventStore for the one allowed
        // process-wide full replay fallback when a business rule cannot infer a cut.
        let mutable loadedCommonDir: string option = None

        let currentForRule (rule: IntegrationRule) (currentState: IntegratorState) =
            currentState.Currents |> Map.tryFind rule.Name |> Option.defaultValue rule.Initial

        let planCut (currentState: IntegratorState) (rule: IntegrationRule) (fault: RuleFault) =
            result {
                let! failed =
                    currentState.Events
                    |> Map.tryFind (eventKey fault.FailedEventId)
                    |> Option.map Ok
                    |> Option.defaultValue (Error("cut-tail lost failed event: " + EventId.value fault.FailedEventId))

                let liveCurrent = currentForRule rule currentState

                let validatePlan (plan: SemanticCutPlan) =
                    rule.ApplyCut liveCurrent plan.ResetJson
                    |> Result.map (fun _ -> plan)

                match rule.PlanCut liveCurrent failed fault.Reason false |> Result.bind validatePlan with
                | Ok plan -> return plan
                | Error firstReason ->
                    match loadedCommonDir with
                    | Some commonDir when trySpendFullReplayBudget () ->
                        let! streams =
                            ProcessEventLog.readStreams commonDir
                            |> Result.mapError (sprintf "cut-tail full replay read failed: %A")

                        do! validateLocalHistory commonDir streams
                        let! replayed = replay streams
                        let replayCurrent = currentForRule rule replayed

                        return!
                            rule.PlanCut replayCurrent failed fault.Reason true
                            |> Result.bind validatePlan
                            |> Result.mapError (fun secondReason ->
                                sprintf
                                    "cut-tail inference failed before and after the one full replay: %s; %s"
                                    firstReason
                                    secondReason)
                    | _ ->
                        return!
                            Error(
                                "cut-tail inference failed and full replay budget is unavailable: " + firstReason
                            )
            }

        let cutEnvelope (currentState: IntegratorState) (rule: IntegrationRule) (fault: RuleFault) (plan: SemanticCutPlan) =
            let streamId = ProjectionCutTailEvent.streamId rule.Name

            let existingHeads =
                match Map.tryFind structuralRule.Name currentState.Currents with
                | None -> []
                | Some current -> StructuralProjection.heads streamId (unbox<StructuralProjection> current)

            let payload =
                { Rule = rule.Name
                  FailedEventId = fault.FailedEventId
                  Reason = fault.Reason
                  ResetJson = plan.ResetJson }

            { EventId = EventId.create (Guid.NewGuid().ToString("N"))
              StreamId = streamId
              EventType = ProjectionCutTailEvent.EventType
              Parents = fault.FailedEventId :: existingHeads
              Payload = encodeCutPayload payload
              PayloadRefs = [] }
            |> EventEnvelope.normalize

        let ensureRuleReset (currentState: IntegratorState) (rule: IntegrationRule) =
            result {
                match Map.tryFind rule.Name currentState.Faults with
                | None -> return currentState, [], []
                | Some fault ->
                    let! plan = planCut currentState rule fault
                    let cut = cutEnvelope currentState rule fault plan
                    let! integrated = integrateOne currentState cut

                    let receipt =
                        { Rule = rule.Name
                          FailedEventId = fault.FailedEventId
                          Reason = fault.Reason
                          CutEventId = cut.EventId }

                    return integrated.State, [ cut ], [ receipt ]
            }

        let ensureMatchingResets (currentState: IntegratorState) (envelope: EventEnvelope) =
            let rec loop state cuts receipts remaining =
                result {
                    match remaining with
                    | [] -> return state, cuts, receipts
                    | rule :: tail ->
                        let! next, addedCuts, addedReceipts = ensureRuleReset state rule
                        return! loop next (cuts @ addedCuts) (receipts @ addedReceipts) tail
                }

            loop currentState [] [] (matchingBusinessRules envelope)

        let prepareLive (startState: IntegratorState) (events: EventEnvelope list) =
            let rec closeFailures state addedEvents addedCuts failures =
                result {
                    match failures with
                    | [] -> return state, addedEvents, addedCuts
                    | (ruleName, _) :: rest ->
                        match tryRule ruleName with
                        | None -> return! closeFailures state addedEvents addedCuts rest
                        | Some rule ->
                            let! next, eventCuts, receipts = ensureRuleReset state rule
                            return!
                                closeFailures next (addedEvents @ eventCuts) (addedCuts @ receipts) rest
                }

            let rec loop current durable cuts remaining =
                result {
                    match remaining with
                    | [] -> return current, durable, cuts
                    | raw :: tail ->
                        let normalized = EventEnvelope.normalize raw
                        let! before, resetEvents, resetCuts = ensureMatchingResets current normalized
                        let! step = integrateOne before normalized
                        let! after, postCutEvents, postCuts = closeFailures step.State [] [] step.FailedRules

                        return!
                            loop
                                after
                                (durable @ resetEvents @ [ normalized ] @ postCutEvents)
                                (cuts @ resetCuts @ postCuts)
                                tail
                }

            loop startState [] [] events

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
                        loadedCommonDir <- Some commonDir
                        generation <- generation + 1L
                        return ()
                    })

            member _.PrepareLive(events) =
                lock gate (fun () ->
                    result {
                        let! preparedState, durableEvents, cuts = prepareLive state events
                        let expectedGeneration = generation

                        return
                            { DurableEvents = durableEvents
                              Cuts = cuts
                              Commit =
                                fun () ->
                                    lock gate (fun () ->
                                        if generation <> expectedGeneration then
                                            failwith
                                                "CanonicalIntegrator Current changed between prepare and durable append"

                                        state <- preparedState
                                        generation <- generation + 1L) }
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
