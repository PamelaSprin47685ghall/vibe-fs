module Wanxiangshu.Shell.SubsessionEventWire

open Thoth.Json
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Fold
open Wanxiangshu.Shell.FallbackConfigCodec

let private payload (e: WanEvent) (k: string) : string = defaultArg (Map.tryFind k e.Payload) ""

let private emptyModel: FallbackModel =
    { ProviderID = ""
      ModelID = ""
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private tryParseModel (s: string) : FallbackModel option =
    if s = "" then
        None
    else
        let provider, model, variant = parseModelId s

        if provider = "" && model = "" then
            None
        else
            Some
                { ProviderID = provider
                  ModelID = model
                  Variant = variant
                  Temperature = None
                  TopP = None
                  MaxTokens = None
                  ReasoningEffort = None
                  Thinking = false }

let private tryParseReceipt (s: string) : HostStartReceipt option =
    if s = "ordered_marker" then
        Some OrderedTurnMarkerObserved
    elif s.StartsWith("user_message:") then
        Some(UserMessageObserved s.[13..])
    elif s.StartsWith("host_run:") then
        Some(HostRunAccepted s.[9..])
    else
        None

let private tryParseFinish (e: WanEvent) : TurnFinishOutcome option =
    let finish = payload e "finish"

    if finish = "completed" then
        Some(TurnCompleted(payload e "output"))
    elif finish = "cancelled" then
        Some TurnCancelled
    elif finish = "recovering" then
        Some TurnRecovering
    elif finish = "infra" then
        Some(TurnInfrastructureFailed(payload e "reason"))
    elif finish = "failed" || finish.StartsWith("failed") then
        let name =
            let n = payload e "errorName"

            if n <> "" then n
            elif finish.StartsWith("failed:") then finish.[7..]
            else "UnknownError"

        Some(
            TurnFailed
                { ErrorName = name
                  DomainError = None
                  Message = payload e "message"
                  StatusCode = None
                  IsRetryable = None }
        )
    else
        None

let private tryParsePoison (s: string) : PoisonReason option =
    if s = "unknown_after_restart" then
        Some SessionStateUnknownAfterRestart
    elif s = "session_closed" then
        Some SessionClosedUnexpectedly
    elif s.StartsWith("abort_did_not_settle:") then
        Some(AbortDidNotSettle(TurnId.create s.[21..]))
    elif s.StartsWith("abort_request_failed:") then
        // Map legacy abort_request_failed to AbortDidNotSettle for backward compatibility
        Some(AbortDidNotSettle(TurnId.create s.[21..]))
    elif s.StartsWith("host_protocol:") then
        Some(HostProtocolBroken s.[14..])
    elif s.StartsWith("event_store:") then
        Some(EventStoreCorrupt s.[12..])
    else
        Some(HostProtocolBroken s)

let private tryParseRunResult (status: string) (detail: string) : RunResult =
    match status with
    | "succeeded" -> Succeeded detail
    | "cancelled" -> Cancelled
    | "failed" ->
        if detail.StartsWith("infra:") then
            Failed(InfrastructureFailure detail.[6..])
        elif detail.StartsWith("protocol:") then
            Failed(ProtocolViolation detail.[9..])
        elif detail.StartsWith("recovery_exhausted:") then
            Failed(RecoveryExhausted detail.[19..])
        elif detail = "no_model" then
            Failed NoModelConfigured
        elif detail.StartsWith("fallback_exhausted:") then
            Failed(
                FallbackExhausted
                    { ErrorName = "FallbackExhausted"
                      DomainError = None
                      Message = detail
                      StatusCode = None
                      IsRetryable = None }
            )
        else
            Failed(InfrastructureFailure(if detail = "" then status else detail))
    | _ -> Failed(InfrastructureFailure(if detail = "" then status else detail))

let private ordinalFromInt (n: int) : TurnOrdinal =
    let rec loop i acc =
        if i <= 0 then acc else loop (i - 1) (TurnOrdinal.next acc)

    if n <= 0 then
        TurnOrdinal.first
    else
        loop n TurnOrdinal.first

/// Decode a WanEvent into a SubsessionEvent when kind matches.
let tryDecodeWanEvent (e: WanEvent) : SubsessionEvent option =
    let kind = e.Kind

    if kind = eventKindSubsessionRunStarted then
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
    elif kind = eventKindSubsessionRunSettled then
        let runId = payload e "runId"
        let status = payload e "status"
        let detail = payload e "detail"

        if runId = "" then
            None
        else
            Some(RunFinished(RunId.create runId, tryParseRunResult status detail))
    elif kind = eventKindSubsessionTurnDispatchRequested then
        let runId = payload e "runId"
        let turnId = payload e "turnId"
        let ordinalStr = payload e "turnOrdinal"
        let modelStr = payload e "model"
        let prompt = payload e "prompt"

        if runId = "" || turnId = "" then
            None
        else
            let model = defaultArg (tryParseModel modelStr) emptyModel

            let ord =
                match System.Int32.TryParse ordinalStr with
                | true, n -> ordinalFromInt n
                | _ -> TurnOrdinal.first

            Some(
                TurnDispatchRequested
                    { RunId = RunId.create runId
                      TurnId = TurnId.create turnId
                      Ordinal = ord
                      Model = model
                      Prompt = prompt }
            )
    elif kind = eventKindSubsessionTurnStarted then
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
    elif kind = eventKindSubsessionTurnFinished then
        let turnId = payload e "turnId"

        match tryParseFinish e with
        | None -> None
        | Some f -> Some(TurnFinished(TurnId.create turnId, f))
    elif kind = eventKindSubsessionAbortRequested then
        let turnId = payload e "turnId"
        let runId = payload e "runId"

        if turnId = "" then
            None
        else
            Some(AbortRequested(RunId.create runId, TurnId.create turnId))
    elif kind = eventKindSubsessionSessionPoisoned then
        let sessionId = payload e "sessionId"
        let reason = payload e "reason"

        match tryParsePoison reason with
        | None -> None
        | Some p -> Some(SessionPoisoned(SessionId.create sessionId, p))
    elif kind = eventKindSubsessionPhysicalSessionClosed then
        let sessionId = payload e "sessionId"

        if sessionId = "" then
            None
        else
            Some(PhysicalSessionClosed(SessionId.create sessionId))
    else
        None

/// Decode a WanEvent into zero or more SubsessionEvents.
/// Handles both the crash-atomic envelope (subsession_decision_committed)
/// and legacy single-event kinds for backward compatibility.
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
                              Payload = item.Payload }

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

/// Decode all matching WanEvents and fold active-run projection.
let projectFromWanEvents (events: WanEvent list) : SessionSafetyProjection =
    events |> List.collect tryDecodeWanEventBatch |> projectEvents
