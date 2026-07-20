module Wanxiangshu.Kernel.EventSourcing.EventPayload

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope

/// Wire payload field names (NDJSON Map keys). Only decode layer should use these strings.
module Field =
    let turnId = "turnId"
    let messageId = "messageId"
    let provider = "provider"
    let model = "model"
    let variant = "variant"
    let agent = "agent"
    let generation = "generation"
    let cancelGeneration = "cancelGeneration"
    let generationAtStart = "generationAtStart"
    let compactionId = "compactionId"
    let humanTurnOrdinal = "humanTurnOrdinal"
    let continuationOrdinal = "continuationOrdinal"
    let nudgeOrdinal = "nudgeOrdinal"
    let compactionOrdinal = "compactionOrdinal"
    let continuationId = "continuationId"
    let userMessageId = "userMessageId"
    let hostUserMessageId = "hostUserMessageId"
    let nudgeId = "nudgeId"
    let nonce = "nonce"
    let status = "status"
    let owner = "owner"
    let promptText = "promptText"
    let humanTurnId = "humanTurnId"
    let anchor = "anchor"

let tryField (key: string) (e: WanEvent) : string option = e.Payload |> Map.tryFind key

let fieldOr (key: string) (fallback: string) (e: WanEvent) : string =
    tryField key e |> Option.defaultValue fallback

let parseIntOpt (raw: string) : int option =
    if raw = "" then
        None
    else
        try
            Some(int raw)
        with _ ->
            None

let tryIntField (key: string) (e: WanEvent) : int option =
    tryField key e |> Option.bind parseIntOpt

let intFieldOr (key: string) (fallback: int) (e: WanEvent) : int =
    tryIntField key e |> Option.defaultValue fallback

let humanTurnOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    intFieldOr Field.humanTurnOrdinal (currentOrdinal + 1) e

let continuationStartOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    intFieldOr Field.continuationOrdinal (currentOrdinal + 1) e

let continuationStageOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    intFieldOr Field.continuationOrdinal currentOrdinal e

let nudgeStartOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    intFieldOr Field.nudgeOrdinal (currentOrdinal + 1) e

let nudgeStageOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    intFieldOr Field.nudgeOrdinal currentOrdinal e

let compactionStartOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    intFieldOr Field.compactionOrdinal (currentOrdinal + 1) e

let compactionStageOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    intFieldOr Field.compactionOrdinal currentOrdinal e
