namespace Wanxiangshu.Interaction.Dispatch.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Enforcer
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Resources
open Wanxiangshu.Strength.Persistence

open System
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

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

    let private eventTypeOf (raw: obj) =
        if isNull raw then None else Some(HostEventCodec.eventTypeOf raw)

    let isNeedHelpRelevantEvent (rawInput: obj) : bool =
        let raw = HostEventCodec.unwrap rawInput

        match eventTypeOf raw with
        | Some("message.part.updated" | "message.part.delta") -> true
        | _ -> false

    /// Compatibility probe for Hosts that directly label a reasoning delta in
    /// `field`. Current OpenCode uses part.updated(type=reasoning) + delta(field=text).
    let private present (value: obj) =
        if isNull value then None else Some value

    let private deltaProperties (raw: obj) =
        if isNull raw || HostEventCodec.eventTypeOf raw <> "message.part.delta" then
            None
        else
            present raw?properties

    let private updatedPart (properties: obj) = present properties?part

    let private updatedProperties (raw: obj) =
        if isNull raw || HostEventCodec.eventTypeOf raw <> "message.part.updated" then
            None
        else
            present raw?properties

    let private decodeUpdatedPart (raw: obj) (properties: obj) (part: obj) : PartIdentity option =
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

    let private decodeDelta (raw: obj) (properties: obj) : StreamDelta option =
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

    let isNeedHelpDelta (rawInput: obj) : bool =
        let raw = HostEventCodec.unwrap rawInput

        match deltaProperties raw with
        | Some properties -> stringField properties?field |> Option.exists legacyReasoningField
        | None -> false

    let tryDecodePartUpdated (rawInput: obj) : PartIdentity option =
        let raw = HostEventCodec.unwrap rawInput

        updatedProperties raw
        |> Option.bind (fun properties ->
            updatedPart properties
            |> Option.bind (decodeUpdatedPart raw properties))

    let tryDecodeDelta (rawInput: obj) : StreamDelta option =
        let raw = HostEventCodec.unwrap rawInput

        deltaProperties raw |> Option.bind (decodeDelta raw)

    /// Legacy direct-field decoder retained for focused compatibility proof.
    let tryDecodeReasoningDelta (rawInput: obj) : StreamDelta option =
        tryDecodeDelta rawInput
        |> Option.filter (fun delta -> legacyReasoningField delta.Field)
