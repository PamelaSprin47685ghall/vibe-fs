namespace Wanxiangshu.Next.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Domain.ProviderProjection
open Wanxiangshu.Next.Host
open Wanxiangshu.Next.Kernel.Identity

/// Host raw object → `ProviderWireProjection` (VERIFY-005 adapter boundary).
///
/// This module owns dynamic property access; `Domain.ProviderProjection` owns the
/// types and the questions asked of them. It produces ONLY the wire projection —
/// the semantic one is reached through `toSemantic`, so no second decoding path
/// can disagree about what a message meant.
///
/// The previous version defined its own `ProviderVisibleMessage` used for both
/// byte equality and cross-session comparison, which is why the canary matcher
/// grew a separate normaliser beside it (VERIFY-007).
module Projection =

    let private readField (value: obj) (name: string) : obj =
        if isNull value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let private readString (value: obj) (name: string) : string option =
        let field = readField value name

        if isNull field then
            None
        else
            let text = unbox<string> field
            if String.IsNullOrWhiteSpace text then None else Some text

    let private firstString (value: obj) (names: string list) =
        names |> List.tryPick (readString value)

    /// Host messages arrive either bare or wrapped as `{ info, parts }`.
    let private infoObject (rawObj: obj) : obj =
        if isNull rawObj then null
        elif not (isNull rawObj?info) then rawObj?info
        else rawObj

    let private rawArray (value: obj) : obj list =
        if isNull value then
            []
        else
            emitJsExpr value "Array.from($0)" |> unbox<obj array> |> Array.toList

    let private canonicalArgs (value: obj) : string =
        if isNull value then
            "{}"
        elif emitJsExpr value "typeof $0 === 'string'" then
            unbox<string> value
        else
            CanonicalJson.canonicalJson value

    let private firstCanonical (partObj: obj) (names: string list) =
        names
        |> List.tryPick (fun name ->
            let value = readField partObj name
            if isNull value then None else Some(canonicalArgs value))

    /// Decode one Host part.
    ///
    /// `None` for bookkeeping parts. ARCH-004 and COMPANION-012 both require that
    /// step markers, patches, files and compaction entries never enter a
    /// projection: the model never saw them, so including them would make the
    /// prefix-cache check fail on content that was never sent.
    let decodePart (partObj: obj) : WirePart option =
        if isNull partObj then
            None
        else
            let kind =
                readString partObj "type"
                |> Option.defaultValue ""
                |> fun value -> value.ToLowerInvariant()

            match kind with
            | "text" -> readString partObj "text" |> Option.map WireText

            | "reasoning"
            | "thinking" -> firstString partObj [ "text"; "reasoning" ] |> Option.map WireReasoning

            | "tool-call"
            | "tool_call"
            | "tool" ->
                let args =
                    firstCanonical partObj [ "args"; "arguments" ] |> Option.defaultValue "{}"

                // REVIEW-004 needs the call id, so a tool call without one is not
                // usable evidence. Dropped rather than given an empty id, which
                // would let "no identity" look like a real one.
                match firstString partObj [ "callID"; "callId"; "id" ], firstString partObj [ "tool"; "name" ] with
                | Some callId, Some name -> Some(WireToolCall(ToolCallId.create callId, name, args))
                | _ -> None

            | "tool-result"
            | "tool_result" ->
                let result =
                    firstCanonical partObj [ "result"; "output"; "content" ]
                    |> Option.defaultValue "null"

                firstString partObj [ "callID"; "callId"; "id" ]
                |> Option.map (fun callId -> WireToolResult(ToolCallId.create callId, result))

            // A Host `FilePart` (`{ type: "file", mime, url, filename? }`). The model
            // genuinely saw it, so ARCH-004's prefix check and COMPANION-011's cutoff
            // proof must both account for it.
            //
            // The DIGEST goes into the projection, not the bytes. Two different
            // images digest differently, so every question either projection answers
            // gets the same answer as it would from the bytes — while the projection
            // stays a value small enough to hold per request instead of megabytes of
            // base64.
            //
            // `url` is the identity: for an inline image it is the data URL, and for
            // a referenced one it is the location. A file whose url is missing is
            // dropped rather than digested as empty, which would make every such
            // part compare equal to every other.
            | "file" ->
                firstString partObj [ "url" ]
                |> Option.map (fun url ->
                    WireMedia(firstString partObj [ "mime"; "mediaType" ], HostDigest.sha256Hex url))

            | _ -> None

    let decodeMessage (rawObj: obj) : WireMessage option =
        if isNull rawObj then
            None
        else
            let info = infoObject rawObj

            let role =
                firstString rawObj [ "role" ]
                |> Option.orElse (firstString info [ "role" ])
                |> Option.defaultValue ""
                |> fun value -> value.ToLowerInvariant()

            let parts = rawArray (readField rawObj "parts") |> List.choose decodePart

            if String.IsNullOrWhiteSpace role && List.isEmpty parts then
                None
            else
                Some { Role = role; Parts = parts }

    /// Decode a whole provider request.
    ///
    /// `System` stays a separate list rather than becoming a `role = "system"`
    /// message, because that is how the Host sends it
    /// (`experimental.chat.system.transform` takes `system: string[]`). Folding it
    /// into messages would make the wire projection disagree with the bytes it
    /// exists to mirror.
    let decodeRequest (requestObj: obj) : ProviderWireProjection =
        let model = readField requestObj "model"

        { ProviderId = firstString model [ "providerID"; "providerId" ]
          ModelId = firstString model [ "modelID"; "modelId"; "id" ]
          Variant = firstString model [ "variant" ]
          Tools =
            rawArray (readField requestObj "tools")
            |> List.choose (fun tool ->
                firstString (readField tool "function") [ "name" ]
                |> Option.orElse (firstString tool [ "name" ]))
          System =
            rawArray (readField requestObj "system")
            |> List.choose (fun entry -> if isNull entry then None else Some(unbox<string> entry))
          Messages = rawArray (readField requestObj "messages") |> List.choose decodeMessage }

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

    /// The Host's own message id.
    ///
    /// Not part of either projection: an id identifies a message, it is not
    /// content the model saw. HOST-010's binding needs it, so it is read
    /// separately and stays out of both.
    let hostMessageId (rawObj: obj) : string option =
        let info = infoObject rawObj
        readString info "id" |> Option.orElse (readString rawObj "id")

    /// The last `role=user` message's wire address in a transform output.
    ///
    /// REVIEW-010's seal binds to the physical user message this request answers
    /// (PROMPT-001), and HOST-010's run binding matches it against the assistant's
    /// `parentID`. Resolved here, from the raw payload, rather than by pairing the
    /// projection's messages with a parallel id list: `decodeMessageView` drops
    /// messages it cannot decode, so positional pairing silently shifts and would
    /// seal against the wrong address.
    let lastUserMessageId (rawMessages: obj list) : string option =
        rawMessages
        |> List.choose (fun raw ->
            match decodeMessage raw with
            | Some message when message.Role = "user" -> hostMessageId raw
            | _ -> None)
        |> List.tryLast

    /// HOST-005: this turn's formal assistant text, excluding reasoning and tool
    /// parts. The Companion B record is built from it (COMPANION-005).
    let formalText (rawObj: obj) : string =
        match decodeMessage rawObj with
        | None -> ""
        | Some message ->
            message.Parts
            |> List.choose (fun part ->
                match part with
                | WireText text -> Some text
                | WireReasoning _
                | WireToolCall _
                | WireToolResult _
                // COMPANION-005: B is prose. Media contributes no formal text, and
                // CTX-013 forbids inventing any — a caption here would become a
                // claim about image content that nothing verified.
                | WireMedia _ -> None)
            |> String.concat ""
