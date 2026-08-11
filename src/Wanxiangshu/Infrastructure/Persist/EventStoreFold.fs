namespace Wanxiangshu.Infrastructure.Persist

open System.Collections.Generic
open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// Wave C seed vocabulary until domain folds migrate (§5.2 additive).
/// Includes JournalEnvelope (W1-vocab) alongside Job* types.
/// Unknown authoritative types fail closed via StorageInvalid.UnknownEventType.
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

    let isKnown (eventType: string) : bool = builtins.Contains eventType

    /// Resolution events leave DomainConflict once all competing heads are parents (§5.3).
    let isResolution (eventType: string) : bool =
        eventType.EndsWith("ConflictResolved") || eventType.EndsWith("Resolved")

/// Per-stream head state after DAG fold (§5.3 DomainConflict).
[<RequireQualifiedAccess>]
type StreamHeadState =
    | Empty
    | Unique of head: EventId
    | Conflict of DomainConflict

/// Persist fold substrate projection. Domain reducers attach later; Wave C owns DAG + conflict.
type EventStoreProjection =
    { Streams: Map<string, StreamHeadState>
      FoldOrder: EventId list
      Conflicts: DomainConflict list }

module EventStoreProjection =
    let empty =
        { Streams = Map.empty
          FoldOrder = []
          Conflicts = [] }

/// DAG topological fold (§5.1 / §5.3): StorageInvalid fail-closed; DomainConflict deterministic.
[<RequireQualifiedAccess>]
module EventStoreFold =

    let private asFoldError (error: StorageInvalid) : FoldError = FoldError.StorageInvalid error

    let private eventIdKey (eventId: EventId) : string = EventId.value eventId

    let private streamKey (streamId: EventStreamId) : string = EventStreamId.value streamId

    /// Index by EventId; identity collision → fail closed.
    let private indexById (events: EventEnvelope list) : Result<Map<string, EventEnvelope>, FoldError> =
        match CanonicalEventCodec.mergeByIdentity events with
        | Error err -> Error(asFoldError err)
        | Ok normalized ->
            normalized
            |> List.map (fun envelope -> eventIdKey envelope.EventId, envelope)
            |> Map.ofList
            |> Ok

    let private validateVocabulary (events: EventEnvelope list) : Result<unit, FoldError> =
        let rec loop remaining =
            match remaining with
            | [] -> Ok()
            | head :: tail ->
                if AuthoritativeEventTypes.isKnown head.EventType then
                    loop tail
                else
                    Error(asFoldError (StorageInvalid.UnknownEventType head.EventType))

        loop events

    let private validateParents (byId: Map<string, EventEnvelope>) : Result<unit, FoldError> =
        let rec loop remaining =
            match remaining with
            | [] -> Ok()
            | (_, envelope: EventEnvelope) :: tail ->
                let rec parentsLeft parents =
                    match parents with
                    | [] -> loop tail
                    | parent :: rest ->
                        if Map.containsKey (eventIdKey parent) byId then
                            parentsLeft rest
                        else
                            Error(asFoldError (StorageInvalid.MissingParent parent))

                parentsLeft envelope.Parents

        loop (Map.toList byId)

    /// Kahn topo order; EventId lexicographic tie-break among ready nodes (§5.0 physical only).
    let private topologicalOrder (byId: Map<string, EventEnvelope>) : Result<EventId list, FoldError> =
        let ids = byId |> Map.toList |> List.map fst

        let indegree = Dictionary<string, int>()
        let children = Dictionary<string, ResizeArray<string>>()

        for id in ids do
            indegree.[id] <- 0
            children.[id] <- ResizeArray<string>()

        for KeyValue(id, envelope) in byId do
            for parent in envelope.Parents do
                let parentKey = eventIdKey parent

                if indegree.ContainsKey parentKey then
                    children.[parentKey].Add id
                    indegree.[id] <- indegree.[id] + 1

        let ready =
            indegree
            |> Seq.filter (fun (KeyValue(_, deg)) -> deg = 0)
            |> Seq.map (fun (KeyValue(id, _)) -> id)
            |> Seq.toList
            |> List.sort

        let rec kahn (pending: string list) (acc: EventId list) (visited: int) =
            match pending with
            | [] ->
                if visited <> ids.Length then
                    Error(asFoldError StorageInvalid.CyclicParents)
                else
                    Ok(List.rev acc)
            | next :: rest ->
                let envelope = byId.[next]
                let unlocked = ResizeArray<string>()

                for child in children.[next] do
                    let deg = indegree.[child] - 1
                    indegree.[child] <- deg

                    if deg = 0 then
                        unlocked.Add child

                let readyQueue = rest @ (unlocked |> Seq.toList) |> List.distinct |> List.sort

                kahn readyQueue (envelope.EventId :: acc) (visited + 1)

        kahn ready [] 0

    let private inStreamParentIds
        (byId: Map<string, EventEnvelope>)
        (stream: string)
        (envelope: EventEnvelope)
        : EventId list =
        envelope.Parents
        |> List.filter (fun parent ->
            match Map.tryFind (eventIdKey parent) byId with
            | Some parentEnv when streamKey parentEnv.StreamId = stream -> true
            | _ -> false)

    /// Heads = in-stream events that are not an in-stream parent of another event in the stream.
    let private streamHeads (byId: Map<string, EventEnvelope>) (streamEvents: EventEnvelope list) : EventId list =
        let referenced =
            streamEvents
            |> List.collect (fun envelope ->
                inStreamParentIds byId (streamKey envelope.StreamId) envelope
                |> List.map eventIdKey)
            |> Set.ofList

        streamEvents
        |> List.map (fun e -> e.EventId)
        |> List.filter (fun id -> not (Set.contains (eventIdKey id) referenced))
        |> List.sortWith (fun a b -> compare (EventId.value a) (EventId.value b))

    let private applyStream
        (byId: Map<string, EventEnvelope>)
        (stream: string)
        (orderedInStream: EventEnvelope list)
        : StreamHeadState =
        let rec foldOne remaining (state: StreamHeadState) =
            match remaining with
            | [] -> state
            | envelope :: rest ->
                let nextState =
                    match state with
                    | StreamHeadState.Empty -> StreamHeadState.Unique envelope.EventId
                    | StreamHeadState.Unique _
                    | StreamHeadState.Conflict _ ->
                        let prefix =
                            orderedInStream
                            |> List.takeWhile (fun e -> e.EventId <> envelope.EventId)
                            |> fun prior -> prior @ [ envelope ]

                        let prior = prefix |> List.filter (fun e -> e.EventId <> envelope.EventId)

                        let priorHeads = streamHeads byId prior

                        let parentSet = envelope.Parents |> List.map eventIdKey |> Set.ofList

                        let coversPriorHeads =
                            priorHeads.Length > 1
                            && priorHeads |> List.forall (fun head -> Set.contains (eventIdKey head) parentSet)

                        if AuthoritativeEventTypes.isResolution envelope.EventType && coversPriorHeads then
                            StreamHeadState.Unique envelope.EventId
                        else
                            match streamHeads byId prefix with
                            | [] -> StreamHeadState.Empty
                            | [ head ] -> StreamHeadState.Unique head
                            | many ->
                                StreamHeadState.Conflict(
                                    DomainConflict.ConcurrentHeads(EventStreamId.create stream, many)
                                )

                foldOne rest nextState

        foldOne orderedInStream StreamHeadState.Empty

    /// Structural DAG + vocabulary validation without building projection.
    let validate (events: EventEnvelope list) : Result<unit, FoldError> =
        match indexById events with
        | Error e -> Error e
        | Ok byId ->
            match validateVocabulary (byId |> Map.toList |> List.map snd) with
            | Error e -> Error e
            | Ok() ->
                match validateParents byId with
                | Error e -> Error e
                | Ok() ->
                    match topologicalOrder byId with
                    | Error e -> Error e
                    | Ok _ -> Ok()

    /// Append-path DAG for a *new* batch only.
    ///
    /// Parents outside the batch are assumed already present on the store tip
    /// (caller checked via `tryReadEvent`). Unlike `validate`, this does not
    /// require every parent to appear in `byId` — that requirement forced
    /// `validateAppendSet` to reload full history. Store-backed parents contribute
    /// no indegree (same rule as `topologicalOrder` when parent ∉ byId).
    let validateBatchDag (events: EventEnvelope list) : Result<unit, FoldError> =
        match indexById events with
        | Error e -> Error e
        | Ok byId ->
            match topologicalOrder byId with
            | Error e -> Error e
            | Ok _ -> Ok()

    /// Full projection fold: topo order + per-stream DomainConflict (§5.3).
    let fold (events: EventEnvelope list) : Result<EventStoreProjection, FoldError> =
        match indexById events with
        | Error e -> Error e
        | Ok byId ->
            let normalized = byId |> Map.toList |> List.map snd

            match validateVocabulary normalized with
            | Error e -> Error e
            | Ok() ->
                match validateParents byId with
                | Error e -> Error e
                | Ok() ->
                    match topologicalOrder byId with
                    | Error e -> Error e
                    | Ok order ->
                        let byStream =
                            normalized
                            |> List.groupBy (fun e -> streamKey e.StreamId)
                            |> List.map (fun (stream, _group) ->
                                let orderedInStream =
                                    order
                                    |> List.choose (fun eventId ->
                                        match Map.tryFind (eventIdKey eventId) byId with
                                        | Some envelope when streamKey envelope.StreamId = stream -> Some envelope
                                        | _ -> None)

                                stream, applyStream byId stream orderedInStream)
                            |> Map.ofList

                        let conflicts =
                            byStream
                            |> Map.toList
                            |> List.choose (fun (_, state) ->
                                match state with
                                | StreamHeadState.Conflict conflict -> Some conflict
                                | _ -> None)

                        Ok
                            { Streams = byStream
                              FoldOrder = order
                              Conflicts = conflicts }
