namespace Wanxiangshu.Infrastructure.Persist

open System
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// Application-facing local EventStore port.
/// DURABLE-EVENTS-004/005/013/017/019: local append is one complete NDJSON-line
/// append to the process writer file followed by commit of the already-validated
/// canonical Integrator transition. Git transport/object identity is absent here.
type IEventStore =
    abstract Append: events: EventEnvelope list -> Task<Result<unit, AppendError>>
    abstract WritePayload: content: byte[] -> Task<Result<PayloadRef, string>>
    abstract ReadPayload: payloadRef: PayloadRef -> Task<Result<byte[] option, string>>
    abstract TryCurrent: key: string -> obj option
    abstract TryEvent: eventId: EventId -> EventEnvelope option
    abstract TryHeads: streamId: EventStreamId -> EventId list
    abstract TryHead: streamId: EventStreamId -> EventId option

[<RequireQualifiedAccess>]
module EventStore =

    let private asAppendStorage (error: StorageInvalid) = AppendError.StorageInvalid error

    let private validateVocabulary (events: EventEnvelope list) : Result<unit, StorageInvalid> =
        let rec loop remaining =
            match remaining with
            | [] -> Ok()
            | head :: tail ->
                if AuthoritativeEventTypes.isKnown head.EventType then
                    loop tail
                else
                    Error(StorageInvalid.UnknownEventType head.EventType)

        loop events

    let private validateBatchDag (events: EventEnvelope list) : Result<unit, StorageInvalid> =
        let keys = events |> List.map (fun event -> EventId.value event.EventId) |> Set.ofList

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
                let nextVisiting = Set.add key visiting
                let parents = Map.tryFind key parentsById |> Option.defaultValue []

                let rec visitParents remaining currentVisited =
                    match remaining with
                    | [] -> Ok(Set.add key currentVisited)
                    | parent :: tail ->
                        match visit parent nextVisiting currentVisited with
                        | Error error -> Error error
                        | Ok nextVisited -> visitParents tail nextVisited

                visitParents parents visited

        let rec all remaining visited =
            match remaining with
            | [] -> Ok()
            | key :: tail ->
                match visit key Set.empty visited with
                | Error error -> Error error
                | Ok nextVisited -> all tail nextVisited

        all (Set.toList keys) Set.empty

    let private newEventsAgainstCurrent
        (integrator: ICanonicalIntegrator)
        (events: EventEnvelope list)
        : Result<EventEnvelope list, StorageInvalid> =
        let rec loop remaining seen acc =
            match remaining with
            | [] -> Ok(List.rev acc)
            | head :: tail ->
                let normalized = EventEnvelope.normalize head
                let key = EventId.value normalized.EventId

                match Map.tryFind key seen with
                | Some existing ->
                    match CanonicalEventCodec.checkIdentity normalized existing with
                    | Error error -> Error error
                    | Ok() -> loop tail seen acc
                | None ->
                    match integrator.TryEvent normalized.EventId with
                    | Some existing ->
                        match CanonicalEventCodec.checkIdentity normalized existing with
                        | Error error -> Error error
                        | Ok() -> loop tail (Map.add key normalized seen) acc
                    | None -> loop tail (Map.add key normalized seen) (normalized :: acc)

        loop events Map.empty []

    let private validateParents
        (integrator: ICanonicalIntegrator)
        (events: EventEnvelope list)
        : Result<unit, StorageInvalid> =
        let batchIds =
            events
            |> List.map (fun envelope -> EventId.value envelope.EventId)
            |> Set.ofList

        let rec parents remaining =
            match remaining with
            | [] -> Ok()
            | parent :: tail ->
                if Set.contains (EventId.value parent) batchIds || Option.isSome (integrator.TryEvent parent) then
                    parents tail
                else
                    Error(StorageInvalid.MissingParent parent)

        let rec eventsLeft remaining =
            match remaining with
            | [] -> Ok()
            | head :: tail ->
                match parents head.Parents with
                | Error error -> Error error
                | Ok() -> eventsLeft tail

        eventsLeft events

    let private validatePayloadClosure
        (commonDir: string)
        (events: EventEnvelope list)
        : Result<unit, StorageInvalid> =
        let refs =
            events
            |> List.collect (fun envelope -> envelope.PayloadRefs)
            |> PayloadRefs.canonicalize

        match refs |> List.tryFind (ProcessEventLog.payloadExists commonDir >> not) with
        | Some missing -> Error(StorageInvalid.MissingPayload missing)
        | None -> Ok()

    let private validateForAppend
        (commonDir: string)
        (integrator: ICanonicalIntegrator)
        (events: EventEnvelope list)
        : Result<EventEnvelope list, StorageInvalid> =
        match validateVocabulary events with
        | Error error -> Error error
        | Ok() ->
            match newEventsAgainstCurrent integrator events with
            | Error error -> Error error
            | Ok [] -> Ok []
            | Ok fresh ->
                match validateParents integrator fresh with
                | Error error -> Error error
                | Ok() ->
                    match validateBatchDag fresh with
                    | Error error -> Error error
                    | Ok() ->
                        match validatePayloadClosure commonDir fresh with
                        | Error error -> Error error
                        | Ok() -> Ok fresh

    let createLocal
        (commonDir: string)
        (writerId: string)
        (integrator: ICanonicalIntegrator)
        : IEventStore =
        let log = ProcessEventLog.create commonDir writerId
        let gate = obj ()

        let reloadFromDisk () = integrator.ReloadLocal commonDir

        match reloadFromDisk () with
        | Error error -> failwith ("local EventStore boot failed: " + error)
        | Ok() -> ()

        { new IEventStore with
            member _.Append(events) =
                task {
                    use! _physical = ProcessEventLog.acquireStoreLock commonDir

                    return
                        lock gate (fun () ->
                            try
                                match validateForAppend commonDir integrator events with
                                | Error error -> Error(asAppendStorage error)
                                | Ok [] -> Ok()
                                | Ok fresh ->
                                    match integrator.PrepareLive fresh with
                                    | Error reason ->
                                        Error(AppendError.AppendFailed("integration rejected: " + reason))
                                    | Ok commit ->
                                        // Cross-process store lock + process-local gate make the
                                        // append linear with standalone Git-hook snapshots while
                                        // keeping all business meaning in the Integrator.
                                        ProcessEventLog.append log fresh
                                        commit ()
                                        Ok()
                            with ex ->
                                Error(AppendError.AppendFailed ex.Message))
                }

            member _.WritePayload(content) =
                task {
                    use! _physical = ProcessEventLog.acquireStoreLock commonDir

                    try
                        return Ok(ProcessEventLog.writePayload commonDir content)
                    with ex ->
                        return Error ex.Message
                }

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
            member _.TryHead(streamId) = integrator.TryHead streamId }
