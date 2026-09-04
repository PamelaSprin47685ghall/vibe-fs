namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// LOOP-002 / LOOP-009: extract streaming text deltas from Host events.
///
/// Fail closed: missing sessionId or missing delta text → None. Never invents
/// order or stitches part bodies from updated snapshots.
module LoopEventCodec =

    type TextDelta =
        { SessionId: SessionId
          MessageId: string option
          PartId: string option
          Field: string option
          Delta: string }

    let private stringField (value: obj) : string option =
        if isNull value || String.IsNullOrEmpty(string value) then
            None
        else
            Some(string value)

    let private isTextualDeltaField =
        function
        | "text"
        | "reasoning"
        | "thinking"
        | "model_thought"
        | "reasoning_content" -> true
        | _ -> false

    let private textDeltaField (properties: obj) =
        if isNull properties?field then
            Some "text"
        else
            stringField properties?field

    let private decodeTextDeltaProperties (sessionId: SessionId) (properties: obj) : TextDelta option =
        match textDeltaField properties, stringField properties?delta with
        | Some field, Some delta when isTextualDeltaField field && delta.Length > 0 ->
            Some
                { SessionId = sessionId
                  MessageId =
                    stringField properties?messageID
                    |> Option.orElse (stringField properties?messageId)
                  PartId = stringField properties?partID |> Option.orElse (stringField properties?partId)
                  Field = Some field
                  Delta = delta }
        | _ -> None

    let private tryTextDeltaBody (sessionId: SessionId) (properties: obj) : TextDelta option =
        if isNull properties then
            None
        else
            decodeTextDeltaProperties sessionId properties

    let isLoopTextDelta (rawInput: obj) : bool =
        let raw = HostEventEnvelope.unwrap rawInput

        match HostEventEnvelope.eventTypeOf raw with
        | "message.part.delta" -> true
        | _ -> false

    /// Only `message.part.delta` with textual fields (`field = "text"` or missing field,
    /// or reasoning/thinking stream fields). Non-textual deltas (e.g. tool) are ignored.
    let tryDecodeTextDelta (rawInput: obj) : TextDelta option =
        let raw = HostEventEnvelope.unwrap rawInput

        match isNull raw, HostEventEnvelope.eventTypeOf raw, HostEventEnvelope.trySessionId raw with
        | true, _, _ -> None
        | _, eventType, _ when eventType <> "message.part.delta" -> None
        | _, _, None -> None
        | _, _, Some sessionId -> tryTextDeltaBody sessionId (raw?properties)
