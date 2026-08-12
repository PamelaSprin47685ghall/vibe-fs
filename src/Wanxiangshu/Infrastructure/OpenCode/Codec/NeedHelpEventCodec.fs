namespace Wanxiangshu.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Identity

/// HOST-027: decode the two Host stream events needed to classify an explicit
/// collaboration sentinel without mistaking visible text for reasoning.
///
/// OpenCode's v2 contract carries part KIND on `message.part.updated` and the
/// changed FIELD on `message.part.delta`. A reasoning delta therefore commonly
/// arrives as `field = "text"`; callers must correlate PartId with the preceding
/// updated event rather than treating `field` as the part kind.
module NeedHelpEventCodec =

    type PartIdentity =
        { SessionId: SessionId
          ProviderRun: ProviderRunIdentity
          PartId: string
          PartType: string }

    type StreamDelta =
        { SessionId: SessionId
          ProviderRun: ProviderRunIdentity
          PartId: string
          Field: string
          Delta: string }

    let private stringField (value: obj) : string option =
        if isNull value then
            None
        else
            let text = string value

            if String.IsNullOrEmpty text then None else Some text

    let private sessionIdOf (raw: obj) (properties: obj) =
        HostEventCodec.trySessionId raw
        |> Option.orElseWith (fun () ->
            stringField properties?sessionID
            |> Option.orElse (stringField properties?sessionId)
            |> Option.map SessionId.create)

    let private legacyReasoningField =
        function
        | "reasoning"
        | "thinking"
        | "model_thought"
        | "reasoning_content" -> true
        | _ -> false

    let isNeedHelpRelevantEvent (rawInput: obj) : bool =
        let raw = HostEventCodec.unwrap rawInput

        if isNull raw then
            false
        else
            match HostEventCodec.eventTypeOf raw with
            | "message.part.updated"
            | "message.part.delta" -> true
            | _ -> false

    /// Compatibility probe for Hosts that directly label a reasoning delta in
    /// `field`. Current OpenCode uses part.updated(type=reasoning) + delta(field=text).
    let isNeedHelpDelta (rawInput: obj) : bool =
        let raw = HostEventCodec.unwrap rawInput

        if isNull raw || HostEventCodec.eventTypeOf raw <> "message.part.delta" then
            false
        else
            let properties = raw?properties

            if isNull properties then
                false
            else
                stringField properties?field |> Option.exists legacyReasoningField

    let tryDecodePartUpdated (rawInput: obj) : PartIdentity option =
        let raw = HostEventCodec.unwrap rawInput

        if isNull raw || HostEventCodec.eventTypeOf raw <> "message.part.updated" then
            None
        else
            let properties = raw?properties

            if isNull properties || isNull properties?part then
                None
            else
                let part = properties?part

                match
                    sessionIdOf raw properties,
                    stringField part?messageID |> Option.orElse (stringField part?messageId),
                    stringField part?id |> Option.orElse (stringField properties?partID),
                    stringField part?``type``
                with
                | Some session, Some message, Some partId, Some partType ->
                    Some
                        { SessionId = session
                          ProviderRun = ProviderRunIdentity.create message
                          PartId = partId
                          PartType = partType }
                | _ -> None

    let tryDecodeDelta (rawInput: obj) : StreamDelta option =
        let raw = HostEventCodec.unwrap rawInput

        if isNull raw || HostEventCodec.eventTypeOf raw <> "message.part.delta" then
            None
        else
            let properties = raw?properties

            if isNull properties then
                None
            else
                match
                    sessionIdOf raw properties,
                    stringField properties?messageID
                    |> Option.orElse (stringField properties?messageId),
                    stringField properties?partID |> Option.orElse (stringField properties?partId),
                    stringField properties?field,
                    stringField properties?delta
                with
                | Some session, Some message, Some partId, Some field, Some delta ->
                    Some
                        { SessionId = session
                          ProviderRun = ProviderRunIdentity.create message
                          PartId = partId
                          Field = field
                          Delta = delta }
                | _ -> None

    /// Legacy direct-field decoder retained for focused compatibility proof.
    let tryDecodeReasoningDelta (rawInput: obj) : StreamDelta option =
        tryDecodeDelta rawInput
        |> Option.filter (fun delta -> legacyReasoningField delta.Field)
