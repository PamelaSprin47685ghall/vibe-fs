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

    let private trySessionId (raw: obj) : SessionId option =
        HostEventCodec.trySessionId raw
        |> Option.orElse (
            if isNull raw then
                None
            else
                let properties = raw?properties

                if not (isNull properties) && not (isNull properties?sessionID) then
                    Some(SessionId.create (unbox<string> properties?sessionID))
                elif not (isNull properties) && not (isNull properties?sessionId) then
                    Some(SessionId.create (unbox<string> properties?sessionId))
                else
                    None
        )

    let private stringField (value: obj) : string option =
        if isNull value then
            None
        else
            let text = string value
            if String.IsNullOrEmpty text then None else Some text

    let isLoopTextDelta (rawInput: obj) : bool =
        let raw = HostEventCodec.unwrap rawInput

        match HostEventCodec.eventTypeOf raw with
        | "message.part.delta" -> true
        | _ -> false

    /// Only `message.part.delta` with `field = "text"` (or missing field — Host
    /// historically omits it for text parts). Reasoning deltas are ignored so a
    /// long thinking loop does not kill a still-productive formal text stream.
    let tryDecodeTextDelta (rawInput: obj) : TextDelta option =
        let raw = HostEventCodec.unwrap rawInput

        if isNull raw then
            None
        else
            match HostEventCodec.eventTypeOf raw with
            | "message.part.delta" ->
                match trySessionId raw with
                | None -> None
                | Some sessionId ->
                    let properties = raw?properties

                    if isNull properties then
                        None
                    else
                        let field =
                            if isNull properties?field then
                                Some "text"
                            else
                                stringField properties?field

                        match field with
                        | Some "text" ->
                            match stringField properties?delta with
                            | None -> None
                            | Some delta when delta.Length = 0 -> None
                            | Some delta ->
                                Some
                                    { SessionId = sessionId
                                      MessageId =
                                        stringField properties?messageID
                                        |> Option.orElse (stringField properties?messageId)
                                      PartId =
                                        stringField properties?partID |> Option.orElse (stringField properties?partId)
                                      Field = field
                                      Delta = delta }
                        | _ -> None
            | _ -> None
