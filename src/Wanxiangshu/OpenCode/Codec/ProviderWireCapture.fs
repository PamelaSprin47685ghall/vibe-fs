namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Wire + Host stable address decoding variants (Wave 3 split of the old
/// `Projection` module). Kept separate from `ProviderWireDecode` because capture
/// retains source identities the ordinary wire projection deliberately drops; the
/// two views share the same `ProviderWireDecode.decodePart` decoder, never a
/// parallel parser.
module ProviderWireCapture =

    /// One decoded wire part plus the stable Host ToolPart address when present.
    type CapturedWirePart =
        { WirePart: WirePart
          HostToolPartId: HostToolPartId option }

    /// A decoded wire message retaining the assistant provider-run identity.
    /// The normal wire projection deliberately omits source addresses; capture
    /// needs them to bind a tool call to its durable XTrace range.
    type CapturedWireMessage =
        { Role: string
          ProviderRun: ProviderRunIdentity option
          Parts: CapturedWirePart list }

    let private capturePart (rawPart: obj) : CapturedWirePart option =
        ProviderWireDecode.decodePart rawPart
        |> Option.map (fun wirePart ->
            let hostToolPartId =
                match wirePart with
                | WireToolCall _
                | WireToolResult _ ->
                    ProviderWireDecode.firstString rawPart [ "id" ]
                    |> Option.map HostToolPartId.create
                | _ -> None

            { WirePart = wirePart
              HostToolPartId = hostToolPartId })

    let private providerRunOf role rawObj info =
        match role with
        | "assistant" ->
            ProviderWireDecode.firstString rawObj [ "id" ]
            |> Option.orElse (ProviderWireDecode.firstString info [ "id" ])
            |> Option.map ProviderRunIdentity.create
        | _ -> None

    let private decodeCapturedMessageBody rawObj : CapturedWireMessage option =
        let info = ProviderWireDecode.infoObject rawObj

        let role =
            ProviderWireDecode.firstString rawObj [ "role" ]
            |> Option.orElse (ProviderWireDecode.firstString info [ "role" ])
            |> Option.defaultValue ""
            |> fun value -> value.ToLowerInvariant()

        let parts =
            ProviderWireDecode.rawArray (ProviderWireDecode.readField rawObj "parts")
            |> List.choose capturePart

        match String.IsNullOrWhiteSpace role && List.isEmpty parts with
        | true -> None
        | false ->
            Some
                { Role = role
                  ProviderRun = providerRunOf role rawObj info
                  Parts = parts }

    let decodeCapturedMessage (rawObj: obj) : CapturedWireMessage option =
        if isNull rawObj then
            None
        else
            decodeCapturedMessageBody rawObj

    let decodeMessage (rawObj: obj) : WireMessage option =
        decodeCapturedMessage rawObj
        |> Option.map (fun captured ->
            { Role = captured.Role
              Parts = captured.Parts |> List.map (fun part -> part.WirePart) })

    /// Decode a whole provider request.
    ///
    /// `System` stays a separate list rather than becoming a `role = "system"`
    /// message, because that is how the Host sends it
    /// (`experimental.chat.system.transform` takes `system: string[]`). Folding it
    /// into messages would make the wire projection disagree with the bytes it
    /// exists to mirror.
    let decodeRequest (requestObj: obj) : ProviderWireProjection =
        let model = ProviderWireDecode.readField requestObj "model"

        { ProviderId = ProviderWireDecode.firstString model [ "providerID"; "providerId" ]
          ModelId = ProviderWireDecode.firstString model [ "modelID"; "modelId"; "id" ]
          Variant = ProviderWireDecode.firstString model [ "variant" ]
          Tools =
            ProviderWireDecode.rawArray (ProviderWireDecode.readField requestObj "tools")
            |> List.choose (fun tool ->
                ProviderWireDecode.firstString (ProviderWireDecode.readField tool "function") [ "name" ]
                |> Option.orElse (ProviderWireDecode.firstString tool [ "name" ]))
          System =
            ProviderWireDecode.rawArray (ProviderWireDecode.readField requestObj "system")
            |> List.choose (fun entry -> if isNull entry then None else Some(unbox<string> entry))
          Messages =
            ProviderWireDecode.rawArray (ProviderWireDecode.readField requestObj "messages")
            |> List.choose decodeMessage }

    /// Build a wire projection from an already-extracted message list.
    ///
    /// Used at the `messages.transform` boundary, where the Host hands over the
    /// message view while tools and system live elsewhere in the request.
    let decodeMessageView (rawMessages: obj list) : ProviderWireProjection =
        { ProviderId = None
          ModelId = None
          Variant = None
          Tools = []
          System = []
          Messages = rawMessages |> List.choose decodeMessage }

    /// Decode transform messages once while retaining source identities needed by
    /// durable XTrace locality. The ordinary wire view remains a projection of
    /// this same decoder, never a parallel parser.
    let decodeCapturedMessageView (rawMessages: obj list) : CapturedWireMessage list =
        rawMessages |> List.choose decodeCapturedMessage

    let wireMessageView (captured: CapturedWireMessage list) : ProviderWireProjection =
        { ProviderId = None
          ModelId = None
          Variant = None
          Tools = []
          System = []
          Messages =
            captured
            |> List.map (fun message ->
                { Role = message.Role
                  Parts = message.Parts |> List.map (fun part -> part.WirePart) }) }

    /// The last `role=user` message's wire address in a transform output.
    ///
    /// REVIEW-010's seal binds to the physical user message this request answers
    /// (PROMPT-001), and HOST-010's run binding matches it against the assistant's
    /// `parentID`. Resolved here, from the raw payload, rather than by pairing the
    /// projection's messages with a parallel id list: `decodeMessageView` drops
    /// messages it cannot decode, so positional pairing silently shifts and would
    /// seal against the wrong address.
    let lastUserMessageId (rawMessages: obj list) : PhysicalUserMessageId option =
        rawMessages
        |> List.choose (fun raw ->
            match decodeMessage raw with
            | Some message when message.Role = "user" ->
                ProviderWireDecode.hostMessageId raw |> Option.map PhysicalUserMessageId.create
            | _ -> None)
        |> List.tryLast

    /// Execution binding follows the latest physical user turn, not the most recent
    /// PromptKey anywhere in history. If the latest user turn is external/keyless,
    /// an older plugin PromptKey must not leak into the new provider attempt.
    let lastUserPromptKey (rawMessages: obj list) : PromptKey option =
        rawMessages
        |> List.choose (fun raw ->
            match decodeMessage raw with
            | Some message when message.Role = "user" -> Some(ProviderWireDecode.promptKeyOfMessage raw)
            | _ -> None)
        |> List.tryLast
        |> Option.flatten

    let private formalTextOfMessage (message: CapturedWireMessage) =
        message.Parts
        |> List.choose (fun (part: CapturedWirePart) ->
            match part.WirePart with
            | WireText text -> Some text
            | WireReasoning _
            | WireToolCall _
            | WireToolResult _
            // COMPANION-005: B is prose. Media contributes no formal text, and
            // CTX-013 forbids inventing any — a caption here would become a
            // claim about image content that nothing verified.
            | WireMedia _ -> None)
        |> String.concat ""

    /// HOST-005: this turn's formal assistant text, excluding reasoning and tool
    /// parts. The Companion B record is built from it (COMPANION-005).
    let formalText (rawObj: obj) : string =
        match decodeCapturedMessage rawObj with
        | None -> ""
        | Some message -> formalTextOfMessage message
