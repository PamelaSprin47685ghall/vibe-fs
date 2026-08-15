namespace Wanxiangshu.Repository.Knowledge.Casebook

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
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Foundation.Identity

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

    // ---- single-event integration codec ----------------------------------

    let isCasebookEventType eventType =
        eventType = CapturedEventType
        || eventType = RefreshedEventType
        || eventType = AccessedEventType
        || eventType = EvictedEventType

    let private decodeRefreshed (payload: JsonValue) : Result<CasebookEvent, string> =
        let decoder =
            Decode.object (fun get ->
                (get.Required.Field "session_id" Decode.string,
                 get.Required.Field "q" Decode.string,
                 get.Required.Field "a" Decode.string,
                 get.Required.Field "observations" (Decode.list decodeObservation)))

        Decode.fromValue "$" decoder payload
        |> Result.map (fun (sessionId, q, a, observations) ->
            CasebookEvent.CaseRefreshed(sessionId, q, a, observations))

    /// Integration oracle input decoder. It accepts exactly one EventEnvelope;
    /// history ordering/iteration belongs to CanonicalIntegrator.
    let tryDecodeEnvelope (envelope: EventEnvelope) : Result<CasebookEvent, string> =
        match envelope.EventType with
        | eventType when eventType = CapturedEventType ->
            Decode.fromValue "$" decodeCase envelope.Payload
            |> Result.map CasebookEvent.CaseCaptured
        | eventType when eventType = RefreshedEventType -> decodeRefreshed envelope.Payload
        | eventType when eventType = AccessedEventType ->
            Decode.fromValue "$" (Decode.field "session_id" Decode.string) envelope.Payload
            |> Result.map CasebookEvent.CaseAccessed
        | eventType when eventType = EvictedEventType ->
            Decode.fromValue "$" (Decode.field "session_id" Decode.string) envelope.Payload
            |> Result.map CasebookEvent.CaseEvicted
        | other -> Error(sprintf "not a Casebook event: %s" other)

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
                return Error(sprintf "%s semantic cut: %s" eventType cut.Reason)
            | Ok _ -> return Ok eventId
            | Error err -> return Error(sprintf "%s append failed: %A" eventType err)
        }

    let appendCaptured (store: IEventStore) (case: Case) : Task<Result<EventId, string>> =
        appendEvent store CapturedEventType (encodeCase case)

    let appendRefreshed
        (store: IEventStore)
        (sessionId: string)
        (q: string)
        (a: string)
        (observations: Observation list)
        : Task<Result<EventId, string>> =
        let payload =
            Encode.object
                [ "session_id", Encode.string sessionId
                  "q", Encode.string q
                  "a", Encode.string a
                  "observations", Encode.list (List.map encodeObservation observations) ]

        appendEvent store RefreshedEventType payload

    let appendAccessed (store: IEventStore) (sessionId: string) : Task<Result<EventId, string>> =
        appendEvent store AccessedEventType (Encode.object [ "session_id", Encode.string sessionId ])

    let appendEvicted (store: IEventStore) (sessionId: string) : Task<Result<EventId, string>> =
        appendEvent store EvictedEventType (Encode.object [ "session_id", Encode.string sessionId ])
