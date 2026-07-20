module Wanxiangshu.Runtime.SubsessionEventPayload

open Thoth.Json
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Fold
open Wanxiangshu.Runtime.SubsessionEventParse

let private tryDecodeRunStarted (e: WanEvent) : SubsessionEvent option =
    let childId = payload e "childId"
    let parent = payload e "parentSessionId"
    let runId = payload e "runId"

    if childId = "" || runId = "" then
        None
    else
        Some(
            RunStarted
                { RunId = RunId.create runId
                  ParentSessionId = SessionId.create parent
                  SessionId = SessionId.create childId }
        )

let private tryDecodeRunSettled (e: WanEvent) : SubsessionEvent option =
    let runId = payload e "runId"
    let status = payload e "status"
    let detail = payload e "detail"

    if runId = "" then
        None
    else
        Some(RunFinished(RunId.create runId, tryParseRunResult status detail))

let private tryDecodeTurnDispatchRequested (e: WanEvent) : SubsessionEvent option =
    let runId = payload e "runId"
    let turnId = payload e "turnId"
    let ordinalStr = payload e "turnOrdinal"
    let modelStr = payload e "model"
    let prompt = payload e "prompt"
    let deadlineAtMsStr = payload e "deadlineAtMs"

    if runId = "" || turnId = "" then
        None
    else
        let model = defaultArg (tryParseModel modelStr) emptyModel

        let ord =
            match System.Int32.TryParse ordinalStr with
            | true, n -> ordinalFromInt n
            | _ -> TurnOrdinal.first

        let deadlineAtMs =
            match System.Int64.TryParse deadlineAtMsStr with
            | true, d -> d
            | _ -> 0L

        Some(
            TurnDispatchRequested
                { RunId = RunId.create runId
                  TurnId = TurnId.create turnId
                  Ordinal = ord
                  Model = model
                  Prompt = prompt
                  DeadlineAtMs = deadlineAtMs }
        )

let private tryDecodeTurnStarted (e: WanEvent) : SubsessionEvent option =
    let runId = payload e "runId"
    let turnId = payload e "turnId"
    let receipt = payload e "receipt"

    match tryParseReceipt receipt with
    | None -> None
    | Some r ->
        Some(
            TurnStarted
                { RunId = RunId.create runId
                  TurnId = TurnId.create turnId
                  Receipt = r }
        )

let private tryDecodeTurnFinished (e: WanEvent) : SubsessionEvent option =
    let turnId = payload e "turnId"

    match tryParseFinish e with
    | None -> None
    | Some f -> Some(TurnFinished(TurnId.create turnId, f))

let private tryDecodeAbortRequested (e: WanEvent) : SubsessionEvent option =
    let turnId = payload e "turnId"
    let runId = payload e "runId"
    let abortDeadlineAtMsStr = payload e "abortDeadlineAtMs"

    if turnId = "" then
        None
    else
        let abortDeadlineAtMs =
            match System.Int64.TryParse abortDeadlineAtMsStr with
            | true, d -> d
            | _ -> 0L

        Some(AbortRequested(RunId.create runId, TurnId.create turnId, abortDeadlineAtMs))

let private tryDecodeSessionPoisoned (e: WanEvent) : SubsessionEvent option =
    let sessionId = payload e "sessionId"
    let reason = payload e "reason"

    match tryParsePoison reason with
    | None -> None
    | Some p -> Some(SessionPoisoned(SessionId.create sessionId, p))

let private tryDecodePhysicalSessionClosed (e: WanEvent) : SubsessionEvent option =
    let sessionId = payload e "sessionId"

    if sessionId = "" then
        None
    else
        Some(PhysicalSessionClosed(SessionId.create sessionId))

/// Decode a WanEvent into a SubsessionEvent when kind matches.
let tryDecodeWanEvent (e: WanEvent) : SubsessionEvent option =
    match e.Kind with
    | k when k = eventKindSubsessionRunStarted -> tryDecodeRunStarted e
    | k when k = eventKindSubsessionRunSettled -> tryDecodeRunSettled e
    | k when k = eventKindSubsessionTurnDispatchRequested -> tryDecodeTurnDispatchRequested e
    | k when k = eventKindSubsessionTurnStarted -> tryDecodeTurnStarted e
    | k when k = eventKindSubsessionTurnFinished -> tryDecodeTurnFinished e
    | k when k = eventKindSubsessionAbortRequested -> tryDecodeAbortRequested e
    | k when k = eventKindSubsessionSessionPoisoned -> tryDecodeSessionPoisoned e
    | k when k = eventKindSubsessionPhysicalSessionClosed -> tryDecodePhysicalSessionClosed e
    | _ -> None

/// Decode a WanEvent into zero or more SubsessionEvents.
/// Handles both the crash-atomic envelope (subsession_decision_committed)
/// and single-event kinds.
type private CommittedEventEnvelope =
    { Kind: string
      Payload: Map<string, string> }

let tryDecodeWanEventBatch (e: WanEvent) : SubsessionEvent list =
    let sessionId = SessionId.create e.Session

    if e.Kind = eventKindSubsessionDecisionCommitted then
        let eventsJson = defaultArg (Map.tryFind "events" e.Payload) ""

        if eventsJson = "" then
            [ SessionPoisoned(sessionId, EventStoreCorrupt "Empty events JSON in committed decision") ]
        else
            match Decode.Auto.fromString<CommittedEventEnvelope list> eventsJson with
            | Ok items ->
                let decodedOpts =
                    items
                    |> List.map (fun item ->
                        let synth: WanEvent =
                            { V = e.V
                              Session = e.Session
                              Kind = item.Kind
                              At = e.At
                              Payload = item.Payload
                              EventId = None
                              WriterId = None
                              Sequence = None
                              Checksum = None }

                        tryDecodeWanEvent synth)

                if List.exists Option.isNone decodedOpts then
                    [ SessionPoisoned(
                          sessionId,
                          EventStoreCorrupt "One or more inner events in decision committed envelope failed to decode"
                      ) ]
                else
                    decodedOpts |> List.choose id
            | Error err -> [ SessionPoisoned(sessionId, EventStoreCorrupt err) ]
    else
        match tryDecodeWanEvent e with
        | Some evt -> [ evt ]
        | None -> []
