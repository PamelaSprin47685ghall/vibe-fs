namespace Wanxiangshu.Infrastructure

open Thoth.Json
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.Kernel.Identity

/// CASE-007: Casebook durable facts through the unified EventStore — the only
/// persistence a Case may use (no feature ref / manifest tree / second
/// authority). Event types: InspectorCaseCaptured / InspectorCaseRefreshed /
/// InspectorCaseAccessed / InspectorCaseEvicted; Q/A/observations ride the
/// event payload (large bodies via PayloadRef in later phases).
module CasebookStore =

    let CasebookStream = "casebook"
    let CapturedEventType = "InspectorCaseCaptured"
    let RefreshedEventType = "InspectorCaseRefreshed"
    let AccessedEventType = "InspectorCaseAccessed"
    let EvictedEventType = "InspectorCaseEvicted"

    // ---- observation codec ------------------------------------------------

    let private encodeObservation (observation: Observation) : JsonValue =
        match observation with
        | Observation.FileRead(path, hash) ->
            Encode.object
                [ "kind", Encode.string "read"
                  "path", Encode.string path
                  "hash", Encode.string hash ]
        | Observation.GlobResult(pattern, paths) ->
            Encode.object
                [ "kind", Encode.string "glob"
                  "pattern", Encode.string pattern
                  "paths", Encode.list (List.map Encode.string paths) ]
        | Observation.GrepResult(pattern, matches) ->
            let encodedMatches =
                matches
                |> List.map (fun (path, index, text) ->
                    Encode.object
                        [ "path", Encode.string path
                          "index", Encode.int index
                          "text", Encode.string text ])

            Encode.object
                [ "kind", Encode.string "grep"
                  "pattern", Encode.string pattern
                  "matches", Encode.list encodedMatches ]

    let private decodeObservation: Decoder<Observation> =
        Decode.object (fun get ->
            match get.Required.Field "kind" Decode.string with
            | "read" ->
                Observation.FileRead(get.Required.Field "path" Decode.string, get.Required.Field "hash" Decode.string)
            | "glob" ->
                Observation.GlobResult(
                    get.Required.Field "pattern" Decode.string,
                    get.Required.Field "paths" (Decode.list Decode.string)
                )
            | "grep" ->
                let matches =
                    get.Required.Field
                        "matches"
                        (Decode.list (
                            Decode.object (fun g ->
                                g.Required.Field "path" Decode.string,
                                g.Required.Field "index" Decode.int,
                                g.Required.Field "text" Decode.string)
                        ))

                Observation.GrepResult(get.Required.Field "pattern" Decode.string, matches)
            | other -> failwithf "unknown observation kind: %s" other)

    // ---- case codec -------------------------------------------------------

    let private encodeCase (case: Case) : JsonValue =
        Encode.object
            [ "session_id", Encode.string case.SessionId
              "q", Encode.string case.Q
              "a", Encode.string case.A
              "observations", Encode.list (List.map encodeObservation case.Observations) ]

    let private decodeCase: Decoder<Case> =
        Decode.object (fun get ->
            { SessionId = get.Required.Field "session_id" Decode.string
              Q = get.Required.Field "q" Decode.string
              A = get.Required.Field "a" Decode.string
              Observations = get.Required.Field "observations" (Decode.list decodeObservation)
              LastAccessOrder = 0L })

    // ---- append -----------------------------------------------------------

    let private appendEvent
        (store: IEventStore)
        (parents: EventId list)
        (eventType: string)
        (payload: JsonValue)
        : Result<EventId, string> =
        let eventId = EventId.create (System.Guid.NewGuid().ToString("N"))

        let envelope =
            EventEnvelope.normalize
                { EventId = eventId
                  StreamId = EventStreamId.create CasebookStream
                  EventType = eventType
                  Parents = parents
                  Payload = payload
                  PayloadRefs = [] }

        match store.Append(store.OpenSnapshot(), [ envelope ]) with
        | Ok _ -> Ok eventId
        | Error err -> Error(sprintf "%s append failed: %A" eventType err)

    let appendCaptured (store: IEventStore) (parents: EventId list) (case: Case) : Result<EventId, string> =
        appendEvent store parents CapturedEventType (encodeCase case)

    let appendRefreshed
        (store: IEventStore)
        (parents: EventId list)
        (sessionId: string)
        (q: string)
        (a: string)
        (observations: Observation list)
        : Result<EventId, string> =
        let payload =
            Encode.object
                [ "session_id", Encode.string sessionId
                  "q", Encode.string q
                  "a", Encode.string a
                  "observations", Encode.list (List.map encodeObservation observations) ]

        appendEvent store parents RefreshedEventType payload

    let appendAccessed (store: IEventStore) (parents: EventId list) (sessionId: string) : Result<EventId, string> =
        appendEvent store parents AccessedEventType (Encode.object [ "session_id", Encode.string sessionId ])

    let appendEvicted (store: IEventStore) (parents: EventId list) (sessionId: string) : Result<EventId, string> =
        appendEvent store parents EvictedEventType (Encode.object [ "session_id", Encode.string sessionId ])

    // ---- load / project ---------------------------------------------------

    /// Causal order: parents before children (merge returns tree order, not
    /// application order — the fold must see Captured before Refreshed of the
    /// same Case regardless of physical tree layout).
    let topoSort (envelopes: EventEnvelope list) : EventEnvelope list =
        let byId = envelopes |> List.map (fun e -> e.EventId, e) |> Map.ofList

        let rec visit
            (e: EventEnvelope)
            (visited: Set<EventId>)
            (acc: EventEnvelope list)
            : Set<EventId> * EventEnvelope list =
            if Set.contains e.EventId visited then
                visited, acc
            else
                let visitedAfterParents, accAfterParents =
                    e.Parents
                    |> List.filter (fun p -> Map.containsKey p byId)
                    |> List.fold (fun (v, a) p -> visit byId[p] v a) (visited, acc)

                Set.add e.EventId visitedAfterParents, e :: accAfterParents

        (Set.empty, [])
        |> fun (v, a) -> List.fold (fun (v, a) e -> visit e v a) (v, a) envelopes
        |> snd
        |> List.rev

    let loadEnvelopes (raw: IGitRawStore) (snapshot: StoreSnapshot) : Result<EventEnvelope list, string> =
        match EventStoreMergeSpec.merge raw (MergeInput.ofList [ snapshot ]) with
        | Error(MergeError.StorageInvalid detail) -> Error(sprintf "storage invalid: %A" detail)
        | Ok events ->
            events
            |> List.filter (fun e ->
                e.EventType = "InspectorCaseCaptured"
                || e.EventType = "InspectorCaseRefreshed"
                || e.EventType = "InspectorCaseAccessed"
                || e.EventType = "InspectorCaseEvicted")
            |> Ok

    let headOf (envelopes: EventEnvelope list) : EventId option =
        envelopes |> List.tryLast |> Option.map (fun e -> e.EventId)

    let loadEvents (raw: IGitRawStore) (snapshot: StoreSnapshot) : Result<CasebookEvent list, string> =
        match loadEnvelopes raw snapshot with
        | Error err -> Error err
        | Ok envelopes ->
            let ordered = topoSort envelopes

            let decodeRefreshed (payload: JsonValue) : Result<CasebookEvent, string> =
                let decoder =
                    Decode.object (fun get ->
                        (get.Required.Field "session_id" Decode.string,
                         get.Required.Field "q" Decode.string,
                         get.Required.Field "a" Decode.string,
                         get.Required.Field "observations" (Decode.list decodeObservation)))

                match Decode.fromValue "$" decoder payload with
                | Ok(sessionId, q, a, observations) -> Ok(CasebookEvent.CaseRefreshed(sessionId, q, a, observations))
                | Error err -> Error err

            let rec decodeAll
                (remaining: EventEnvelope list)
                (acc: CasebookEvent list)
                : Result<CasebookEvent list, string> =
                match remaining with
                | [] -> Ok(List.rev acc)
                | head :: tail ->
                    match head.EventType with
                    | "InspectorCaseCaptured" ->
                        match Decode.fromValue "$" decodeCase head.Payload with
                        | Ok case -> decodeAll tail (CasebookEvent.CaseCaptured case :: acc)
                        | Error err -> Error err
                    | "InspectorCaseRefreshed" ->
                        match decodeRefreshed head.Payload with
                        | Ok event -> decodeAll tail (event :: acc)
                        | Error err -> Error err
                    | "InspectorCaseAccessed" ->
                        match Decode.fromValue "$" (Decode.field "session_id" Decode.string) head.Payload with
                        | Ok sessionId -> decodeAll tail (CasebookEvent.CaseAccessed sessionId :: acc)
                        | Error err -> Error err
                    | "InspectorCaseEvicted" ->
                        match Decode.fromValue "$" (Decode.field "session_id" Decode.string) head.Payload with
                        | Ok sessionId -> decodeAll tail (CasebookEvent.CaseEvicted sessionId :: acc)
                        | Error err -> Error err
                    | _ -> decodeAll tail acc

            decodeAll ordered []

    /// The stream head EventId — use loadEnvelopes + headOf instead (this
    /// function drops EventIds by design).
    let streamHead (_events: CasebookEvent list) : EventId option = None

    /// Full projection: fold all events, then apply LRU capacity.
    let project (capacity: int) (events: CasebookEvent list) : Map<string, Case> =
        let cases = CasebookProjection.fold events
        CasebookProjection.evict capacity cases |> fst
