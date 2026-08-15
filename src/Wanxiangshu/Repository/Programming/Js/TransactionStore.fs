namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
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
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System.Threading.Tasks
open Thoth.Json
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
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Foundation.Identity

/// Narrow durable capability exposed to js-* workflow/tool wiring. The Host
/// registry never owns both AgentJournal and the raw EventStore capability.
type IJsTransactionPersistence =
    abstract AppendPrepared: prepared: JsTransactionPrepared -> Task<Result<EventId, string>>
    abstract AppendCommitted: transactionId: JsTransactionId -> Task<Result<EventId, string>>

/// JS-012/JS-015: durable transaction facts through the unified EventStore —
/// the only persistence a js-* transaction may use (forbid js-transaction.db
/// / feature store). A transaction is Prepared before any filesystem effect
/// and Committed after; an uncommitted Prepared remains interrupted-tool evidence.
module JsToolsTransactionStore =

    /// Single linear stream for transaction facts.
    let TransactionStream = "js-tools/transactions"
    let PreparedEventType = "JsTransactionPrepared"
    let CommittedEventType = "JsTransactionCommitted"

    // ---- payload codec ----------------------------------------------------

    let private encodeDurableMutation (mutation: JsDurableMutation) : JsonValue =
        Encode.object
            [ "path", Encode.string mutation.Path
              "originalText",
              match mutation.OriginalText with
              | Some text -> Encode.string text
              | None -> Encode.nil
              "newText", Encode.string mutation.NewText ]

    let private decodeDurableMutation: Decoder<JsDurableMutation> =
        Decode.object (fun get ->
            { Path = get.Required.Field "path" Decode.string
              OriginalText = get.Optional.Field "originalText" Decode.string
              NewText = get.Required.Field "newText" Decode.string })

    let private encodePrepared (prepared: JsTransactionPrepared) : JsonValue =
        Encode.object
            [ "transactionId", Encode.string (JsTransactionId.value prepared.TransactionId)
              "workspaceRoot", Encode.string prepared.WorkspaceRoot
              "mutations", Encode.list (List.map encodeDurableMutation prepared.Mutations) ]

    let private decodePrepared: Decoder<JsTransactionPrepared> =
        Decode.object (fun get ->
            { TransactionId = JsTransactionId.create (get.Required.Field "transactionId" Decode.string)
              WorkspaceRoot = get.Required.Field "workspaceRoot" Decode.string
              Mutations = get.Required.Field "mutations" (Decode.list decodeDurableMutation) })

    let private encodeCommitted (committed: JsTransactionCommitted) : JsonValue =
        Encode.object [ "transactionId", Encode.string (JsTransactionId.value committed.TransactionId) ]

    let private decodeCommitted: Decoder<JsTransactionCommitted> =
        Decode.object (fun get ->
            { TransactionId = JsTransactionId.create (get.Required.Field "transactionId" Decode.string) })

    let private payload (json: JsonValue) : JsonValue = json

    // ---- append -----------------------------------------------------------

    let isTransactionEventType eventType =
        eventType = PreparedEventType || eventType = CommittedEventType

    type DecodedTransactionEvent =
        | Prepared of JsTransactionPrepared
        | Committed of JsTransactionCommitted

    /// Single-event integration decoder. History iteration belongs exclusively
    /// to CanonicalIntegrator.
    let tryDecodeEnvelope (envelope: EventEnvelope) : Result<DecodedTransactionEvent, string> =
        match envelope.EventType with
        | eventType when eventType = PreparedEventType ->
            Decode.fromValue "$" decodePrepared envelope.Payload |> Result.map Prepared
        | eventType when eventType = CommittedEventType ->
            Decode.fromValue "$" decodeCommitted envelope.Payload |> Result.map Committed
        | other -> Error(sprintf "not a JsTransaction event: %s" other)

    /// Append the Prepared fact using the Integrator-owned structural head.
    let appendPrepared (store: IEventStore) (prepared: JsTransactionPrepared) : Task<Result<EventId, string>> =
        task {
            let eventId = EventId.create (System.Guid.NewGuid().ToString("N"))
            let streamId = EventStreamId.create TransactionStream

            let envelope =
                EventEnvelope.normalize
                    { EventId = eventId
                      StreamId = streamId
                      EventType = PreparedEventType
                      Parents = store.TryHead streamId |> Option.toList
                      Payload = payload (encodePrepared prepared)
                      PayloadRefs = [] }

            match! store.Append [ envelope ] with
            | Ok receipt when AppendReceipt.cutFor eventId receipt |> Option.isSome ->
                let cut = AppendReceipt.cutFor eventId receipt |> Option.get
                return Error("JsTransactionPrepared semantic cut: " + cut.Reason)
            | Ok _ -> return Ok eventId
            | Error err -> return Error(sprintf "JsTransactionPrepared append failed: %A" err)
        }

    /// Append the Committed fact for a prepared transaction.
    let appendCommitted (store: IEventStore) (transactionId: JsTransactionId) : Task<Result<EventId, string>> =
        task {
            let eventId = EventId.create (System.Guid.NewGuid().ToString("N"))
            let streamId = EventStreamId.create TransactionStream

            let envelope =
                EventEnvelope.normalize
                    { EventId = eventId
                      StreamId = streamId
                      EventType = CommittedEventType
                      Parents = store.TryHead streamId |> Option.toList
                      Payload = payload (encodeCommitted { TransactionId = transactionId })
                      PayloadRefs = [] }

            match! store.Append [ envelope ] with
            | Ok receipt when AppendReceipt.cutFor eventId receipt |> Option.isSome ->
                let cut = AppendReceipt.cutFor eventId receipt |> Option.get
                return Error("JsTransactionCommitted semantic cut: " + cut.Reason)
            | Ok _ -> return Ok eventId
            | Error err -> return Error(sprintf "JsTransactionCommitted append failed: %A" err)
        }

    let createPersistence (store: IEventStore) : IJsTransactionPersistence =
        { new IJsTransactionPersistence with
            member _.AppendPrepared(prepared) = appendPrepared store prepared
            member _.AppendCommitted(transactionId) = appendCommitted store transactionId }
