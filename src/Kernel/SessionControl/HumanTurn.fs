module Wanxiangshu.Kernel.SessionControl.HumanTurn

/// Independent projection for human turn state.
///
/// Owner: Session lifecycle
/// Input events: human_turn_started
/// Query: LatestTurnId, Provider, Model, Agent

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.EventSourcing.EventPayload

type HumanTurnState =
    { TurnId: string
      Provider: string
      Model: string
      Variant: string
      Agent: string
      MessageId: string option }

/// Extract HumanTurnState from a single human_turn_started event.
/// Returns None if the event is not a human_turn_started.
let foldSingleEvent (e: WanEvent) : HumanTurnState option =
    if e.Kind = eventKindHumanTurnStarted then
        Some
            { TurnId = fieldOr Field.turnId "" e
              Provider = fieldOr Field.provider "" e
              Model = fieldOr Field.model "" e
              Variant = fieldOr Field.variant "" e
              Agent = fieldOr Field.agent "" e
              MessageId = tryField Field.messageId e }
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

let isDuplicateHumanTurn (currentOrdinal: int) (lastMsgId: string option) (e: WanEvent) : bool =
    if e.Kind <> eventKindHumanTurnStarted then
        false
    else
        let newOrdinal = humanTurnOrdinal currentOrdinal e
        let msgId = tryField Field.messageId e

        newOrdinal <= currentOrdinal
        || (msgId.IsSome && lastMsgId.IsSome && msgId.Value = lastMsgId.Value)

let messageId (e: WanEvent) : string option =
    tryField Field.messageId e
    |> Option.bind (fun s -> if s = "" then None else Some s)
