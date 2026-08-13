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
        let nodeCount = byId.Count

        let indegree = Dictionary<string, int>()
        let children = Dictionary<string, ResizeArray<string>>()

        for KeyValue(id, _) in byId do
            indegree.[id] <- 0
            children.[id] <- ResizeArray<string>()

        for KeyValue(id, envelope) in byId do
            for parent in envelope.Parents do
                let parentKey = eventIdKey parent

                if indegree.ContainsKey parentKey then
                    children.[parentKey].Add id
                    indegree.[id] <- indegree.[id] + 1

        let queued = HashSet<string>()
        let mutable ready = Set.empty<string>

        for KeyValue(id, deg) in indegree do
            if deg = 0 && queued.Add id then
                ready <- Set.add id ready

        let acc = ResizeArray<EventId>()

        while not ready.IsEmpty do
            let next = Set.minElement ready
            ready <- Set.remove next ready

            acc.Add(byId.[next].EventId)

            for child in children.[next] do
                let deg = indegree.[child] - 1
                indegree.[child] <- deg

                if deg = 0 && queued.Add child then
                    ready <- Set.add child ready

        if acc.Count <> nodeCount then
            Error(asFoldError StorageInvalid.CyclicParents)
        else
            Ok(List.ofSeq acc)

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
    let private snapshotHeads (heads: HashSet<string>) (idOf: Dictionary<string, EventId>) : EventId list =
        heads
        |> Seq.map (fun key -> idOf.[key])
        |> Seq.toList
        |> List.sortWith (fun a b -> compare (EventId.value a) (EventId.value b))

    let private applyStream
        (byId: Map<string, EventEnvelope>)
        (stream: string)
        (orderedInStream: ResizeArray<EventEnvelope>)
        : StreamHeadState =
        if orderedInStream.Count = 0 then
            StreamHeadState.Empty
        else
            let heads = HashSet<string>()
            let idOf = Dictionary<string, EventId>()

            for i = 0 to orderedInStream.Count - 1 do
                let envelope = orderedInStream.[i]
                let key = eventIdKey envelope.EventId
                idOf.[key] <- envelope.EventId

                if i = 0 then
                    heads.Add key |> ignore
                else
                    let parentSet = HashSet<string>()

                    for parent in envelope.Parents do
                        parentSet.Add(eventIdKey parent) |> ignore

                    let coversPriorHeads =
                        heads.Count > 1 && Seq.forall (fun h -> parentSet.Contains h) heads

                    if AuthoritativeEventTypes.isResolution envelope.EventType && coversPriorHeads then
                        heads.Clear()
                        heads.Add key |> ignore
                    else
                        for parent in inStreamParentIds byId stream envelope do
                            heads.Remove(eventIdKey parent) |> ignore

                        heads.Add key |> ignore

            match snapshotHeads heads idOf with
            | [] -> StreamHeadState.Empty
            | [ head ] -> StreamHeadState.Unique head
            | many ->
                StreamHeadState.Conflict(DomainConflict.ConcurrentHeads(EventStreamId.create stream, many))

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
                        let byStreamBuf = Dictionary<string, ResizeArray<EventEnvelope>>()

                        for eventId in order do
                            let envelope = byId.[eventIdKey eventId]
                            let stream = streamKey envelope.StreamId

                            match byStreamBuf.TryGetValue(stream) with
                            | true, buf -> buf.Add envelope
                            | false, _ ->
                                let buf = ResizeArray<EventEnvelope>()
                                buf.Add envelope
                                byStreamBuf.[stream] <- buf

                        let byStream =
                            byStreamBuf
                            |> Seq.map (fun (KeyValue(stream, buf)) -> stream, applyStream byId stream buf)
                            |> Map.ofSeq

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
