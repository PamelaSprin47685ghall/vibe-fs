module Wanxiangshu.Kernel.EventLog.HumanTurnProjection

/// Independent projection for human turn state.
///
/// Owner: Session lifecycle
/// Input events: human_turn_started
/// Query: LatestTurnId, Provider, Model, Agent
///
/// Phase 6: Split from SessionState.

open Wanxiangshu.Kernel.EventLog.Types

type HumanTurnState =
    { TurnId: string
      Provider: string
      Model: string
      Variant: string
      Agent: string
      MessageId: string option }

let private payloadField (key: string) (e: WanEvent) : string option = e.Payload |> Map.tryFind key

/// Extract HumanTurnState from a single human_turn_started event.
/// Returns None if the event is not a human_turn_started.
let foldSingleEvent (e: WanEvent) : HumanTurnState option =
    if e.Kind = eventKindHumanTurnStarted then
        let turnId = defaultArg (payloadField "turnId" e) ""
        let provider = defaultArg (payloadField "provider" e) ""
        let model = defaultArg (payloadField "model" e) ""
        let variant = defaultArg (payloadField "variant" e) ""
        let agent = defaultArg (payloadField "agent" e) ""
        let msgId = payloadField "messageId" e

        Some
            { TurnId = turnId
              Provider = provider
              Model = model
              Variant = variant
              Agent = agent
              MessageId = msgId }
    else
        None

let private humanTurnFolder (st: HumanTurnState option) (e: WanEvent) : HumanTurnState option =
    match foldSingleEvent e with
    | Some state -> Some state
    | None -> st

let foldHumanTurn (sessionId: string) (events: WanEvent list) : HumanTurnState option =
    events
    |> List.filter (fun e -> e.Session = sessionId)
    |> List.fold humanTurnFolder None

let private humanTurnOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "humanTurnOrdinal"
    |> Option.bind (fun raw ->
        try
            Some(int raw)
        with _ ->
            None)
    |> Option.defaultValue (currentOrdinal + 1)

let isDuplicateHumanTurn (currentOrdinal: int) (lastMsgId: string option) (e: WanEvent) : bool =
    if e.Kind <> eventKindHumanTurnStarted then
        false
    else
        let newOrdinal = humanTurnOrdinal currentOrdinal e
        let msgId = payloadField "messageId" e

        newOrdinal <= currentOrdinal
        || (msgId.IsSome && lastMsgId.IsSome && msgId.Value = lastMsgId.Value)

let messageId (e: WanEvent) : string option =
    payloadField "messageId" e
    |> Option.bind (fun s -> if s = "" then None else Some s)
