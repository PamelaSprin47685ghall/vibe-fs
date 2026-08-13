namespace Wanxiangshu.Infrastructure

open System.Threading.Tasks
open Thoth.Json
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.Kernel.Identity

/// JS-012/JS-015: durable transaction facts through the unified EventStore —
/// the only persistence a js-* transaction may use (forbid js-transaction.db
/// / feature store). A transaction is Prepared before any filesystem effect
/// and Committed after; recovery scans for Prepared-without-Committed and
/// undoes only what we provably wrote.
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

    /// Append the Prepared fact. `parents` = the current head of the
    /// transaction stream (linear chain; empty on first use).
    let appendPrepared
        (store: IEventStore)
        (parents: EventId list)
        (prepared: JsTransactionPrepared)
        : Task<Result<EventId, string>> =
        task {
            let eventId = EventId.create (System.Guid.NewGuid().ToString("N"))

            let envelope =
                EventEnvelope.normalize
                    { EventId = eventId
                      StreamId = EventStreamId.create TransactionStream
                      EventType = PreparedEventType
                      Parents = parents
                      Payload = payload (encodePrepared prepared)
                      PayloadRefs = [] }

            let! snapshot = store.OpenSnapshot()

            match! store.Append(snapshot, [ envelope ]) with
            | Ok _ -> return Ok eventId
            | Error err -> return Error(sprintf "JsTransactionPrepared append failed: %A" err)
        }

    /// Append the Committed fact for a prepared transaction.
    let appendCommitted
        (store: IEventStore)
        (parents: EventId list)
        (transactionId: JsTransactionId)
        : Task<Result<EventId, string>> =
        task {
            let eventId = EventId.create (System.Guid.NewGuid().ToString("N"))

            let envelope =
                EventEnvelope.normalize
                    { EventId = eventId
                      StreamId = EventStreamId.create TransactionStream
                      EventType = CommittedEventType
                      Parents = parents
                      Payload = payload (encodeCommitted { TransactionId = transactionId })
                      PayloadRefs = [] }

            let! snapshot = store.OpenSnapshot()

            match! store.Append(snapshot, [ envelope ]) with
            | Ok _ -> return Ok eventId
            | Error err -> return Error(sprintf "JsTransactionCommitted append failed: %A" err)
        }

    // ---- load / scan ------------------------------------------------------

    /// All transaction-stream events from a snapshot, in causal order.
    let loadEvents (raw: IGitRawStore) (snapshot: StoreSnapshot) : Task<Result<EventEnvelope list, string>> =
        task {
            match! EventStoreMergeSpec.merge raw (MergeInput.ofList [ snapshot ]) with
            | Error(MergeError.StorageInvalid detail) -> return Error(sprintf "storage invalid: %A" detail)
            | Ok events ->
                return
                    events
                    |> List.filter (fun e -> e.EventType = PreparedEventType || e.EventType = CommittedEventType)
                    |> Ok
        }

    /// The stream head EventId (last event on the transaction stream), for
    /// linear-parent appends.
    let streamHead (events: EventEnvelope list) : EventId option =
        events |> List.tryLast |> Option.map (fun e -> e.EventId)

    let private tryDecodePrepared (envelope: EventEnvelope) : JsTransactionPrepared option =
        match Decode.fromValue "$" decodePrepared envelope.Payload with
        | Ok prepared -> Some prepared
        | Error _ -> None

    let private tryDecodeCommitted (envelope: EventEnvelope) : JsTransactionId option =
        match Decode.fromValue "$" decodeCommitted envelope.Payload with
        | Ok committed -> Some committed.TransactionId
        | Error _ -> None

    /// Prepared transactions with no matching Committed fact (JS-015:
    /// crash recovery candidates).
    let scanUncommitted (events: EventEnvelope list) : JsTransactionPrepared list =
        let committed =
            events
            |> List.choose (fun e ->
                if e.EventType = CommittedEventType then
                    tryDecodeCommitted e
                else
                    None)
            |> Set.ofList

        events
        |> List.choose (fun e ->
            if e.EventType = PreparedEventType then
                tryDecodePrepared e
                |> Option.filter (fun p -> not (Set.contains p.TransactionId committed))
            else
                None)

    /// JS-015: crash recovery — undo each uncommitted transaction's mutations,
    /// but only where the disk still holds exactly the text we wrote.
    let recover (root: string) (pending: JsTransactionPrepared list) : unit =
        for prepared in pending do
            for mutation in prepared.Mutations do
                JsMutationFs.undoIfMatches root mutation.Path mutation.NewText mutation.OriginalText
