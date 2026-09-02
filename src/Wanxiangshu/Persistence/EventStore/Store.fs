namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation.Identity

/// Application-facing local EventStore port.
/// DURABLE-EVENTS-004/005/013/017/019: local append is one complete NDJSON-line
/// append to the process writer file followed by commit of the already-validated
/// canonical Integrator transition. Git transport/object identity is absent here.
type IEventStore =
    abstract Append: events: EventEnvelope list -> Task<Result<AppendReceipt, AppendError>>
    abstract WritePayload: content: byte[] -> Task<Result<PayloadRef, string>>
    abstract ReadPayload: payloadRef: PayloadRef -> Task<Result<byte[] option, string>>
    abstract TryCurrent: key: string -> obj option
    abstract TryEvent: eventId: EventId -> EventEnvelope option
    abstract TryHeads: streamId: EventStreamId -> EventId list
    abstract TryHead: streamId: EventStreamId -> EventId option
    abstract AllHeads: unit -> EventId list

[<RequireQualifiedAccess>]
module EventStore =

    let private asAppendStorage (error: StorageInvalid) = AppendError.StorageInvalid error

    let private validateVocabulary (events: EventEnvelope list) : Result<unit, StorageInvalid> =
        events
        |> List.tryFind (fun head -> not (AuthoritativeEventTypes.isKnown head.EventType))
        |> Option.map (fun head -> Error(StorageInvalid.UnknownEventType head.EventType))
        |> Option.defaultValue (Ok())

    let private validateBatchDag (events: EventEnvelope list) : Result<unit, StorageInvalid> =
        let keys =
            events |> List.map (fun event -> EventId.value event.EventId) |> Set.ofList

        let parentsById =
            events
            |> List.map (fun event ->
                EventId.value event.EventId,
                event.Parents
                |> List.map EventId.value
                |> List.filter (fun parent -> Set.contains parent keys))
            |> Map.ofList

        let rec visit key visiting visited =
            if Set.contains key visited then
                Ok visited
            elif Set.contains key visiting then
                Error StorageInvalid.CyclicParents
            else
                walkChildren key visiting visited

        and walkChildren key visiting visited =
            let nextVisiting = Set.add key visiting
            let parents = Map.tryFind key parentsById |> Option.defaultValue []

            let rec visitParents remaining currentVisited =
                match remaining with
                | [] -> Ok(Set.add key currentVisited)
                | parent :: tail ->
                    result {
                        let! nextVisited = visit parent nextVisiting currentVisited
                        return! visitParents tail nextVisited
                    }

            visitParents parents visited

        let rec all remaining visited =
            match remaining with
            | [] -> Ok()
            | key :: tail ->
                result {
                    let! nextVisited = visit key Set.empty visited
                    return! all tail nextVisited
                }

        all (Set.toList keys) Set.empty

    let private reuseSeenIdentity
        (normalized: EventEnvelope)
        (existing: EventEnvelope)
        (seen: Map<string, EventEnvelope>)
        (acc: EventEnvelope list)
        : Result<Map<string, EventEnvelope> * EventEnvelope list, StorageInvalid> =
        result {
            do! CanonicalEventCodec.checkIdentity normalized existing
            return seen, acc
        }

    let private observeStoreOrFresh
        (integrator: ICanonicalIntegrator)
        (key: string)
        (normalized: EventEnvelope)
        (seen: Map<string, EventEnvelope>)
        (acc: EventEnvelope list)
        : Result<Map<string, EventEnvelope> * EventEnvelope list, StorageInvalid> =
        match integrator.TryEvent normalized.EventId with
        | Some existing ->
            result {
                do! CanonicalEventCodec.checkIdentity normalized existing
                return Map.add key normalized seen, acc
            }
        | None -> Ok(Map.add key normalized seen, normalized :: acc)

    let private stepAgainstCurrent
        (integrator: ICanonicalIntegrator)
        (normalized: EventEnvelope)
        (seen: Map<string, EventEnvelope>)
        (acc: EventEnvelope list)
        : Result<Map<string, EventEnvelope> * EventEnvelope list, StorageInvalid> =
        let key = EventId.value normalized.EventId

        match Map.tryFind key seen with
        | Some existing -> reuseSeenIdentity normalized existing seen acc
        | None -> observeStoreOrFresh integrator key normalized seen acc

    let private newEventsAgainstCurrent
        (integrator: ICanonicalIntegrator)
        (events: EventEnvelope list)
        : Result<EventEnvelope list, StorageInvalid> =
        let rec loop remaining seen acc =
            match remaining with
            | [] -> Ok(List.rev acc)
            | head :: tail ->
                result {
                    let! nextSeen, nextAcc = stepAgainstCurrent integrator (EventEnvelope.normalize head) seen acc

                    return! loop tail nextSeen nextAcc
                }

        loop events Map.empty []

    let private parentKnown (integrator: ICanonicalIntegrator) (batchIds: Set<string>) (parent: EventId) =
        Set.contains (EventId.value parent) batchIds
        || Option.isSome (integrator.TryEvent parent)

    let private validateParentList
        (integrator: ICanonicalIntegrator)
        (batchIds: Set<string>)
        (parents: EventId list)
        : Result<unit, StorageInvalid> =
        let rec loop remaining =
            match remaining with
            | [] -> Ok()
            | parent :: tail when parentKnown integrator batchIds parent -> loop tail
            | parent :: _ -> Error(StorageInvalid.MissingParent parent)

        loop parents

    let private validateParents
        (integrator: ICanonicalIntegrator)
        (events: EventEnvelope list)
        : Result<unit, StorageInvalid> =
        let batchIds =
            events
            |> List.map (fun envelope -> EventId.value envelope.EventId)
            |> Set.ofList

        events
        |> List.traverseResultM (fun head -> validateParentList integrator batchIds head.Parents)
        |> Result.map ignore

    let private validatePayloadClosure (commonDir: string) (events: EventEnvelope list) : Result<unit, StorageInvalid> =
        let refs =
            events
            |> List.collect (fun envelope -> envelope.PayloadRefs)
            |> PayloadRefs.canonicalize

        match refs |> List.tryFind (ProcessEventLog.payloadExists commonDir >> not) with
        | Some missing -> Error(StorageInvalid.MissingPayload missing)
        | None -> Ok()

    let private validateFreshBatch
        (commonDir: string)
        (integrator: ICanonicalIntegrator)
        (fresh: EventEnvelope list)
        : Result<EventEnvelope list, StorageInvalid> =
        result {
            do! validateParents integrator fresh
            do! validateBatchDag fresh
            do! validatePayloadClosure commonDir fresh
            return fresh
        }

    let private validateForAppend
        (commonDir: string)
        (integrator: ICanonicalIntegrator)
        (events: EventEnvelope list)
        : Result<EventEnvelope list, StorageInvalid> =
        result {
            do! validateVocabulary events
            let! fresh = newEventsAgainstCurrent integrator events

            if List.isEmpty fresh then
                return []
            else
                return! validateFreshBatch commonDir integrator fresh
        }

    let private commitPrepared (log: ProcessEventLog) (prepared: PreparedIntegration) : AppendReceipt =
        ProcessEventLog.append log prepared.DurableEvents
        prepared.Commit()
        { Cuts = prepared.Cuts }

    let private appendFresh
        (integrator: ICanonicalIntegrator)
        (log: ProcessEventLog)
        (fresh: EventEnvelope list)
        : Result<AppendReceipt, AppendError> =
        result {
            let! prepared =
                integrator.PrepareLive fresh
                |> Result.mapError (fun reason -> AppendError.AppendFailed("integration preparation failed: " + reason))

            return commitPrepared log prepared
        }

    let private appendValidated
        (commonDir: string)
        (integrator: ICanonicalIntegrator)
        (log: ProcessEventLog)
        (events: EventEnvelope list)
        : Result<AppendReceipt, AppendError> =
        result {
            let! fresh = validateForAppend commonDir integrator events |> Result.mapError asAppendStorage

            if List.isEmpty fresh then
                return AppendReceipt.empty
            else
                return! appendFresh integrator log fresh
        }

    let createLocal (commonDir: string) (writerId: string) (integrator: ICanonicalIntegrator) : IEventStore =
        let log = ProcessEventLog.create commonDir writerId
        let gate = obj ()

        let reloadFromDisk () = integrator.ReloadLocal commonDir

        match reloadFromDisk () with
        | Error error -> failwith ("local EventStore boot failed: " + error)
        | Ok() -> ()

        { new IEventStore with
            member _.Append(events) =
                ProcessEventLog.withStoreLock commonDir (fun () ->
                    task {
                        return
                            lock gate (fun () ->
                                try
                                    appendValidated commonDir integrator log events
                                with ex ->
                                    Error(AppendError.AppendFailed ex.Message))
                    })

            member _.WritePayload(content) =
                ProcessEventLog.withStoreLock commonDir (fun () ->
                    task {
                        try
                            return Ok(ProcessEventLog.writePayload commonDir content)
                        with ex ->
                            return Error ex.Message
                    })

            member _.ReadPayload(payloadRef) =
                task {
                    try
                        return Ok(ProcessEventLog.readPayload commonDir payloadRef)
                    with ex ->
                        return Error ex.Message
                }

            member _.TryCurrent(key) = integrator.TryCurrent key
            member _.TryEvent(eventId) = integrator.TryEvent eventId
            member _.TryHeads(streamId) = integrator.TryHeads streamId
            member _.TryHead(streamId) = integrator.TryHead streamId
            member _.AllHeads() = integrator.AllHeads() }
