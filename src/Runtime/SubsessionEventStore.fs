module Wanxiangshu.Runtime.SubsessionEventStore

open Fable.Core
open Thoth.Json
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.EventLogRuntimeAppend
open Wanxiangshu.Runtime.CommandProcessor
open Wanxiangshu.Runtime.SubsessionPorts

let private runResultStatus (r: RunResult) : string =
    match r with
    | Succeeded _ -> "succeeded"
    | Failed _ -> "failed"
    | Cancelled -> "cancelled"

let private runResultDetail (r: RunResult) : string =
    match r with
    | Succeeded output -> output
    | Cancelled -> "cancelled"
    | Failed(NoModelConfigured) -> "no_model"
    | Failed(FallbackExhausted err) -> "fallback_exhausted:" + err.ErrorName + ":" + err.Message
    | Failed(RecoveryExhausted reason) -> "recovery_exhausted:" + reason
    | Failed(ProtocolViolation reason) -> "protocol:" + reason
    | Failed(InfrastructureFailure reason) -> "infra:" + reason

let private modelString (m: FallbackModel) : string =
    match m.Variant with
    | Some v -> sprintf "%s/%s:%s" m.ProviderID m.ModelID v
    | None -> sprintf "%s/%s" m.ProviderID m.ModelID

let private receiptTag (r: HostStartReceipt) : string =
    match r with
    | UserMessageObserved id -> "user_message:" + id
    | HostRunAccepted id -> "host_run:" + id
    | OrderedTurnMarkerObserved -> "ordered_marker"

let private finishPayload (o: TurnFinishOutcome) : Map<string, string> =
    match o with
    | TurnCompleted output -> Map [ "finish", "completed"; "output", output ]
    | TurnFailed err -> Map [ "finish", "failed"; "errorName", err.ErrorName; "message", err.Message ]
    | TurnCancelled -> Map [ "finish", "cancelled" ]
    | TurnRecovering -> Map [ "finish", "recovering" ]
    | TurnInfrastructureFailed reason -> Map [ "finish", "infra"; "reason", reason ]

let private poisonTag (p: PoisonReason) : string =
    match p with
    | AbortDidNotSettle tid -> "abort_did_not_settle:" + TurnId.value tid
    | HostProtocolBroken s -> "host_protocol:" + s
    | SessionStateUnknownAfterRestart -> "unknown_after_restart"
    | SessionClosedUnexpectedly -> "session_closed"
    | EventStoreCorrupt s -> "event_store:" + s // AbortRequestFailed matched branch physically removed

let private effectiveRoot (workspaceRoot: string) : string =
    if System.String.IsNullOrWhiteSpace workspaceRoot then
        failwith "SubsessionEventStore: workspaceRoot must not be empty"
    else
        workspaceRoot

let private encodeEvent (sid: string) (evt: SubsessionEvent) : string * Map<string, string> =
    match evt with
    | RunStarted data ->
        eventKindSubsessionRunStarted,
        Map
            [ "childId", SessionId.value data.SessionId
              "parentSessionId", SessionId.value data.ParentSessionId
              "runId", RunId.value data.RunId
              "sessionId", sid ]

    | RunFinished(runId, result) ->
        eventKindSubsessionRunSettled,
        Map
            [ "childId", sid
              "runId", RunId.value runId
              "status", runResultStatus result
              "detail", runResultDetail result
              "sessionId", sid ]

    | TurnDispatchRequested data ->
        eventKindSubsessionTurnDispatchRequested,
        Map
            [ "runId", RunId.value data.RunId
              "turnId", TurnId.value data.TurnId
              "turnOrdinal", string (TurnOrdinal.value data.Ordinal)
              "sessionId", sid
              "model", modelString data.Model
              "prompt", data.Prompt ]

    | TurnStarted data ->
        eventKindSubsessionTurnStarted,
        Map
            [ "runId", RunId.value data.RunId
              "turnId", TurnId.value data.TurnId
              "sessionId", sid
              "receipt", receiptTag data.Receipt ]

    | TurnFinished(turnId, finish) ->
        eventKindSubsessionTurnFinished,
        Map.add "turnId" (TurnId.value turnId) (Map.add "sessionId" sid (finishPayload finish))

    | AbortRequested(runId, turnId) ->
        eventKindSubsessionAbortRequested,
        Map [ "turnId", TurnId.value turnId; "sessionId", sid; "runId", RunId.value runId ]

    | SessionPoisoned(s, reason) ->
        eventKindSubsessionSessionPoisoned, Map [ "sessionId", SessionId.value s; "reason", poisonTag reason ]

    | PhysicalSessionClosed s -> eventKindSubsessionPhysicalSessionClosed, Map [ "sessionId", SessionId.value s ]

let private encodeEnvelopePayload (sid: string) (events: SubsessionEvent list) : Map<string, string> =
    let inner =
        events
        |> List.map (fun evt ->
            let kind, payload = encodeEvent sid evt
            {| Kind = kind; Payload = payload |})

    Map [ "events", Encode.Auto.toString (0, inner) ]

/// Wire SubsessionEvent list into NDJSON as a single crash-atomic envelope line.
type NdjsonSubsessionEventStore(workspaceRoot: string) =
    interface ISubsessionEventStore with
        member _.Append(sessionId, events) =
            promise {
                if List.isEmpty events then
                    return ()
                else
                    let root = effectiveRoot workspaceRoot
                    let sid = SessionId.value sessionId
                    let payload = encodeEnvelopePayload sid events

                    do! appendSubsessionDomainEventOrFail root sid eventKindSubsessionDecisionCommitted payload
            }

let create (workspaceRoot: string) : ISubsessionEventStore =
    NdjsonSubsessionEventStore(workspaceRoot) :> ISubsessionEventStore

/// In-memory store for tests. Append is all-or-nothing: if a test throws mid-list,
/// prior partial state is not exposed (single assignment).
type MemorySubsessionEventStore(?failAfter: int) =
    let mutable events: SubsessionEvent list = []
    let mutable appendCount = 0
    let failAfterN = defaultArg failAfter System.Int32.MaxValue

    member _.Events = events
    member _.AppendCount = appendCount

    interface ISubsessionEventStore with
        member _.Append(_sessionId, evts) =
            appendCount <- appendCount + 1

            if appendCount > failAfterN then
                Promise.reject (exn "ndjson write failed")
            else
                // Atomic: only commit the whole batch.
                events <- events @ evts
                Promise.lift ()
