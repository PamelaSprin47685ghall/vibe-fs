module Wanxiangshu.Runtime.SubsessionEventParse

open Thoth.Json
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Fold
open Wanxiangshu.Runtime.Fallback.FallbackConfigCodec

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
