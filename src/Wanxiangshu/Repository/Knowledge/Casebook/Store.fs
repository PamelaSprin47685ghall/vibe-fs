namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Thoth.Json
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// CASE-007 / KR-007: Casebook durable facts through the unified EventStore — the only
/// persistence a Case may use (no feature ref / manifest tree / second
/// authority).
module CasebookStore =

    let CasebookStream = "casebook"
    let CapturedEventType = CasebookEventTypes.Captured
    let RefreshedEventType = CasebookEventTypes.Refreshed
    let AccessedEventType = CasebookEventTypes.Accessed
    let EvictedEventType = CasebookEventTypes.Evicted

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
            [ "identity", Encode.string case.Identity
              "session_id", Encode.string case.Identity
              "source_trace", Encode.string case.SourceTrace
              "q", Encode.string case.Q
              "a", Encode.string case.A
              "related_paths", Encode.list (List.map Encode.string case.RelatedPaths)
              "completion_file_state", Encode.string case.CompletionFileState
              "maintenance_file_state", Encode.string case.MaintenanceFileState
              "access_order", Encode.int64 case.AccessOrder
              "observations", Encode.list (List.map encodeObservation case.Observations) ]

    let private decodeCase: Decoder<Case> =
        Decode.object (fun get ->
            let identity =
                match get.Optional.Field "identity" Decode.string with
                | Some id when not (System.String.IsNullOrWhiteSpace id) -> id
                | _ -> get.Required.Field "session_id" Decode.string

            let sourceTrace =
                get.Optional.Field "source_trace" Decode.string |> Option.defaultValue ""

            let q = get.Required.Field "q" Decode.string
            let a = get.Required.Field "a" Decode.string

            let relatedPaths =
                get.Optional.Field "related_paths" (Decode.list Decode.string)
                |> Option.defaultValue []

            let completionFileState =
                get.Optional.Field "completion_file_state" Decode.string
                |> Option.defaultValue ""

            let maintenanceFileState =
                get.Optional.Field "maintenance_file_state" Decode.string
                |> Option.defaultValue completionFileState

            let accessOrder =
                get.Optional.Field "access_order" Decode.int64 |> Option.defaultValue 0L

            let observations =
                get.Optional.Field "observations" (Decode.list decodeObservation)
                |> Option.defaultValue []

            { Identity = identity
              SourceTrace = sourceTrace
              Q = q
              A = a
              RelatedPaths = relatedPaths
              CompletionFileState = completionFileState
              MaintenanceFileState = maintenanceFileState
              AccessOrder = accessOrder
              Observations = observations })

    // ---- single-event integration codec ----------------------------------

    let isCasebookEventType eventType =
        CasebookEventTypes.isCasebookEvent eventType

    let private decodeRefreshed (payload: JsonValue) : Result<CasebookEvent, string> =
        let decoder =
            Decode.object (fun get ->
                let identity =
                    match get.Optional.Field "identity" Decode.string with
                    | Some id when not (System.String.IsNullOrWhiteSpace id) -> id
                    | _ -> get.Required.Field "session_id" Decode.string

                let q = get.Required.Field "q" Decode.string
                let a = get.Required.Field "a" Decode.string

                let maintenanceFileState =
                    get.Optional.Field "maintenance_file_state" Decode.string
                    |> Option.defaultValue ""

                let relatedPaths =
                    get.Optional.Field "related_paths" (Decode.list Decode.string)
                    |> Option.defaultValue []

                let observations =
                    get.Optional.Field "observations" (Decode.list decodeObservation)
                    |> Option.defaultValue []

                (identity, q, a, maintenanceFileState, relatedPaths, observations))

        Decode.fromValue "$" decoder payload
        |> Result.map (fun (identity, q, a, maintenanceFileState, relatedPaths, observations) ->
            CasebookEvent.CaseRefreshed(identity, q, a, maintenanceFileState, relatedPaths, observations))

    /// Integration oracle input decoder. It accepts exactly one EventEnvelope;
    /// history ordering/iteration belongs to CanonicalIntegrator.
    /// Historical envelopes may carry `identity`; older ones only `session_id`.
    let private decodeIdentityOrSessionId =
        Decode.object (fun get ->
            match get.Optional.Field "identity" Decode.string with
            | Some id when not (System.String.IsNullOrWhiteSpace id) -> id
            | _ -> get.Required.Field "session_id" Decode.string)

    let tryDecodeEnvelope (envelope: EventEnvelope) : Result<CasebookEvent, string> =
        let eventType = envelope.EventType

        if eventType = CapturedEventType || eventType = CasebookEventTypes.LegacyCaptured then
            Decode.fromValue "$" decodeCase envelope.Payload
            |> Result.map CasebookEvent.CaseCaptured
        elif eventType = RefreshedEventType || eventType = CasebookEventTypes.LegacyRefreshed then
            decodeRefreshed envelope.Payload
        elif eventType = AccessedEventType || eventType = CasebookEventTypes.LegacyAccessed then
            Decode.fromValue "$" decodeIdentityOrSessionId envelope.Payload
            |> Result.map CasebookEvent.CaseAccessed
        elif eventType = EvictedEventType || eventType = CasebookEventTypes.LegacyEvicted then
            Decode.fromValue "$" decodeIdentityOrSessionId envelope.Payload
            |> Result.map CasebookEvent.CaseEvicted
        else
            Error(sprintf "not a Casebook event: %s" eventType)

    // ---- append -----------------------------------------------------------

    let private appendEvent
        (store: IEventStore)
        (eventType: string)
        (payload: JsonValue)
        : Task<Result<EventId, string>> =
        task {
            let eventId = EventId.create (System.Guid.NewGuid().ToString("N"))
            let streamId = EventStreamId.create CasebookStream
            let parents = store.TryHead streamId |> Option.toList

            let envelope =
                EventEnvelope.normalize
                    { EventId = eventId
                      StreamId = streamId
                      EventType = eventType
                      Parents = parents
                      Payload = payload
                      PayloadRefs = [] }

            match! store.Append [ envelope ] with
            | Ok receipt when AppendReceipt.cutFor eventId receipt |> Option.isSome ->
                let cut = AppendReceipt.cutFor eventId receipt |> Option.get
                let reason = sprintf "%s semantic cut: %s" eventType cut.Reason
                return Error reason
            | Ok _ -> return Ok eventId
            | Error err -> return Error(sprintf "%s append failed: %A" eventType err)
        }

    let appendCaptured (store: IEventStore) (case: Case) : Task<Result<EventId, string>> =
        appendEvent store CapturedEventType (encodeCase case)

    let appendRefreshed
        (store: IEventStore)
        (identity: string)
        (q: string)
        (a: string)
        (maintenanceFileState: string)
        (relatedPaths: string list)
        (observations: Observation list)
        : Task<Result<EventId, string>> =
        let payload =
            Encode.object
                [ "identity", Encode.string identity
                  "session_id", Encode.string identity
                  "q", Encode.string q
                  "a", Encode.string a
                  "maintenance_file_state", Encode.string maintenanceFileState
                  "related_paths", Encode.list (List.map Encode.string relatedPaths)
                  "observations", Encode.list (List.map encodeObservation observations) ]

        appendEvent store RefreshedEventType payload

    let appendAccessed (store: IEventStore) (identity: string) : Task<Result<EventId, string>> =
        appendEvent
            store
            AccessedEventType
            (Encode.object [ "identity", Encode.string identity; "session_id", Encode.string identity ])

    let appendEvicted (store: IEventStore) (identity: string) : Task<Result<EventId, string>> =
        appendEvent
            store
            EvictedEventType
            (Encode.object [ "identity", Encode.string identity; "session_id", Encode.string identity ])
