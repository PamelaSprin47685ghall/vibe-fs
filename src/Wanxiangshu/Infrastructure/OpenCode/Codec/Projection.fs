namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

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
                // REVIEW-004 needs the call id, so a tool call without one is not
                // usable evidence. Dropped rather than given an empty id, which
                // would let "no identity" look like a real one.
                match firstString partObj [ "callID"; "callId"; "id" ], firstString partObj [ "tool"; "name" ] with
                | None, _
                | _, None -> None
                | Some callId, Some name ->
                    // Host session-shaped tool part (message-v2.ts): one part
                    // carries the call AND its completed result — `{ type:
                    // "tool", tool, callID, state: { status, input, output?,
                    // error? } }`. The result the model saw is `state.output`
                    // (or `state.error`). REVIEW-010's `IncludedToolResultDigests`
                    // must contain it — the challenge text lives in the previous
                    // verdict's tool result — so a completed/errored tool part
                    // projects as the RESULT, and only a pending call (this
                    // request's own previous assistant turn, or a legacy shape
                    // with no state object) projects as the call.
                    let stateObj = readField partObj "state"

                    match readString stateObj "status" with
                    | Some "completed" ->
                        let result =
                            firstCanonical stateObj [ "output"; "result"; "content" ]
                            |> Option.defaultValue "null"

                        Some(WireToolResult(ToolCallId.create callId, result))
                    | Some "error" ->
                        let result =
                            firstCanonical stateObj [ "error"; "errorText"; "output" ]
                            |> Option.defaultValue "null"

                        Some(WireToolResult(ToolCallId.create callId, result))
                    | _ ->
                        let args =
                            firstCanonical stateObj [ "input" ]
                            |> Option.orElse (firstCanonical partObj [ "args"; "arguments" ])
                            |> Option.defaultValue "{}"

                        Some(WireToolCall(ToolCallId.create callId, name, args))

            | "tool-result"
            | "tool_result" ->
                let result =
                    firstCanonical partObj [ "result"; "output"; "content" ]
                    |> Option.defaultValue "null"

                firstString partObj [ "callID"; "callId"; "id" ]
                |> Option.map (fun callId -> WireToolResult(ToolCallId.create callId, result))

            // Host 1.18.10's assembled tool part: `{ type: "tool-<tool>", state:
            // "output-available"|"output-error", toolCallId, input, output?,
            // errorText? }` (message-v2.ts). The result the model actually saw is
            // `output` (or `errorText` on failure). Without this case the tool
            // results in every assembled request projected to an empty
            // `IncludedToolResultDigests`, so REVIEW-003's challenge proof could
            // never be satisfied (measured: dual-PERFECT always
            // `ChallengeUnproven`).
            | kind when kind.StartsWith "tool-" ->
                let result =
                    firstCanonical partObj [ "output"; "errorText"; "result"; "content" ]
                    |> Option.defaultValue "null"

                firstString partObj [ "toolCallId"; "callID"; "callId"; "id" ]
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

    let private capturePart (rawPart: obj) : CapturedWirePart option =
        decodePart rawPart
        |> Option.map (fun wirePart ->
            let hostToolPartId =
                match wirePart with
                | WireToolCall _
                | WireToolResult _ -> firstString rawPart [ "id" ] |> Option.map HostToolPartId.create
                | _ -> None

            { WirePart = wirePart
              HostToolPartId = hostToolPartId })

    let decodeCapturedMessage (rawObj: obj) : CapturedWireMessage option =
        if isNull rawObj then
            None
        else
            let info = infoObject rawObj

            let role =
                firstString rawObj [ "role" ]
                |> Option.orElse (firstString info [ "role" ])
                |> Option.defaultValue ""
                |> fun value -> value.ToLowerInvariant()

            let parts = rawArray (readField rawObj "parts") |> List.choose capturePart

            if String.IsNullOrWhiteSpace role && List.isEmpty parts then
                None
            else
                let providerRun =
                    if role = "assistant" then
                        firstString rawObj [ "id" ]
                        |> Option.orElse (firstString info [ "id" ])
                        |> Option.map ProviderRunIdentity.create
                    else
                        None

                Some
                    { Role = role
                      ProviderRun = providerRun
                      Parts = parts }

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

    let messagesFromTransformOutput (output: obj) : obj list =
        unbox<obj array> output?messages |> Array.toList

    let prependCompanionMemory
        (rawMessages: obj list)
        (syntheticId: string)
        (memory: string)
        (dropLeading: int)
        : obj list =
        if dropLeading > List.length rawMessages then
            invalidArg "dropLeading" "X-wire prefix cutoff exceeds the current provider snapshot"

        let head =
            createObj
                [ "info", box (createObj [ "id", box syntheticId; "role", box "user" ])
                  "parts", box [| createObj [ "type", box "text"; "text", box memory ] |] ]

        head :: List.skip dropLeading rawMessages

    let private canonicalValue (canonical: string) : obj =
        try
            emitJsExpr canonical "JSON.parse($0)"
        with _ ->
            box canonical

    let private encodeWirePart (part: WirePart) : Result<obj, string> =
        match part with
        | WireText text -> Ok(createObj [ "type", box "text"; "text", box text ])
        | WireReasoning text -> Ok(createObj [ "type", box "reasoning"; "text", box text ])
        | WireToolCall(callId, name, argsCanonical) ->
            Ok(
                createObj
                    [ "type", box "tool-call"
                      "callID", box (ToolCallId.value callId)
                      "tool", box name
                      "args", canonicalValue argsCanonical ]
            )
        | WireToolResult(callId, resultCanonical) ->
            Ok(
                createObj
                    [ "type", box "tool-result"
                      "callID", box (ToolCallId.value callId)
                      "result", canonicalValue resultCanonical ]
            )
        | WireMedia _ ->
            // VERIFY-007: semantic/media digests are one-way. Reconstructing media
            // bytes from a digest would invent provider-visible content, so a
            // Strength mirror containing media is ineligible rather than lossy.
            Error "wire media cannot be reconstructed from semantic digest"

    /// STRENGTH-009 / PROJ-004: write a fully rendered DSL message view back to
    /// Host objects. This is intentionally an adapter, not business assembly.
    /// Missing Host ids are derived from wire bytes + ordinal and are Host-only;
    /// the generated identity never enters ProviderSemanticProjection.
    let tryApplyRenderedMessages
        (sessionId: string)
        (sha256: string -> string)
        (rendered: Wanxiangshu.Domain.RenderedMessages)
        : Result<obj list, string> =
        let rec encodeParts (remaining: WirePart list) (acc: obj list) : Result<obj list, string> =
            match remaining with
            | [] -> Ok(List.rev acc)
            | part :: tail ->
                match encodeWirePart part with
                | Error error -> Error error
                | Ok encoded -> encodeParts tail (encoded :: acc)

        let triples =
            List.zip3 rendered.Messages rendered.HostMessageIds rendered.HostIsPhysical
            |> List.mapi (fun index triple -> index, triple)

        let rec encodeMessages
            (remaining: (int * (WireMessage * string option * bool)) list)
            (acc: obj list)
            : Result<obj list, string> =
            match remaining with
            | [] -> Ok(List.rev acc)
            | (index, (message, hostId, _)) :: tail ->
                match encodeParts message.Parts [] with
                | Error error -> Error error
                | Ok parts ->
                    let id =
                        hostId
                        |> Option.defaultWith (fun () ->
                            let single: ProviderWireProjection =
                                { ProviderId = None
                                  ModelId = None
                                  Variant = None
                                  Tools = []
                                  System = []
                                  Messages = [ message ] }

                            sha256 (sprintf "%d\u001f%s" index (renderWire single)))

                    let raw =
                        createObj
                            [ "info",
                              box (createObj [ "id", box id; "sessionID", box sessionId; "role", box message.Role ])
                              "parts", box (List.toArray parts) ]

                    encodeMessages tail (raw :: acc)

        encodeMessages triples []

    let private strengthHostMessageId
        (sha256: string -> string)
        (index: int)
        (message: WireMessage)
        (hostId: string option)
        =
        hostId
        |> Option.defaultWith (fun () ->
            let single: ProviderWireProjection =
                { ProviderId = None
                  ModelId = None
                  Variant = None
                  Tools = []
                  System = []
                  Messages = [ message ] }

            sha256 (sprintf "%d\u001f%s" index (renderWire single)))

    let private strengthCompletedPart
        (callId: ToolCallId)
        (name: string)
        (argsCanonical: string)
        (resultCanonical: string)
        : obj =
        createObj
            [ "type", box "tool"
              "tool", box name
              "callID", box (ToolCallId.value callId)
              "state",
              box (
                  createObj
                      [ "status", box "completed"
                        "input", canonicalValue argsCanonical
                        "output", canonicalValue resultCanonical
                        "time", box (createObj [ "start", box 0; "end", box 0 ]) ]
              ) ]

    /// STRENGTH-003/009: encode a Strength Replica provider view into the Host's
    /// native OpenCode tool-part shape. `MessageV2.toModelMessagesEffect` renders
    /// one completed `type=tool` part as the provider's tool-call + tool-result
    /// exchange; pending/running parts are rendered as interrupted errors. The
    /// logical assistant-call + tool-result pair must therefore collapse to one
    /// completed Host assistant message, never two physical Host rows.
    let tryApplyStrengthRenderedMessages
        (sessionId: string)
        (sha256: string -> string)
        (rendered: Wanxiangshu.Domain.RenderedMessages)
        : Result<obj list, string> =
        let rec encodeRegularParts (remaining: WirePart list) (acc: obj list) : Result<obj list, string> =
            match remaining with
            | [] -> Ok(List.rev acc)
            | (WireToolCall _ | WireToolResult _) :: _ -> Error "Strength Host adapter received an unpaired tool part"
            | part :: tail ->
                match encodeWirePart part with
                | Error error -> Error error
                | Ok encoded -> encodeRegularParts tail (encoded :: acc)

        let rawMessage index message hostId role parts =
            let id = strengthHostMessageId sha256 index message hostId

            createObj
                [ "info", box (createObj [ "id", box id; "sessionID", box sessionId; "role", box role ])
                  "parts", box (List.toArray parts) ]

        let triples =
            List.zip3 rendered.Messages rendered.HostMessageIds rendered.HostIsPhysical
            |> List.mapi (fun index triple -> index, triple)

        let rec encodeMessages
            (remaining: (int * (WireMessage * string option * bool)) list)
            (pending: (Map<string, ToolCallId * string * string> * obj list * int * WireMessage * string option) option)
            (acc: obj list)
            : Result<obj list, string> =
            match remaining, pending with
            | [], None -> Ok(List.rev acc)
            | [], Some _ -> Error "Strength Host adapter ended with an incomplete tool batch"
            | (index, (message, hostId, _)) :: tail, currentPending ->
                let calls =
                    message.Parts
                    |> List.choose (function
                        | WireToolCall(callId, name, args) -> Some(callId, name, args)
                        | _ -> None)

                let results =
                    message.Parts
                    |> List.choose (function
                        | WireToolResult(callId, result) -> Some(callId, result)
                        | _ -> None)

                if not (List.isEmpty calls) && not (List.isEmpty results) then
                    Error "Strength Host adapter refuses a message mixing tool calls and results"
                elif not (List.isEmpty calls) then
                    match currentPending with
                    | Some _ -> Error "Strength Host adapter saw a new tool batch before the previous batch completed"
                    | None when not (String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)) ->
                        Error "Strength tool calls must originate from an assistant message"
                    | None ->
                        let regular =
                            message.Parts
                            |> List.filter (function
                                | WireToolCall _ -> false
                                | _ -> true)

                        match encodeRegularParts regular [] with
                        | Error error -> Error error
                        | Ok regularParts ->
                            let distinctIds =
                                calls |> List.map (fun (id, _, _) -> ToolCallId.value id) |> Set.ofList

                            if Set.count distinctIds <> List.length calls then
                                Error "Strength Host adapter refuses duplicate tool call ids in one batch"
                            else
                                let pendingCalls =
                                    calls
                                    |> List.map (fun (callId, name, args) ->
                                        ToolCallId.value callId, (callId, name, args))
                                    |> Map.ofList

                                encodeMessages tail (Some(pendingCalls, regularParts, index, message, hostId)) acc
                elif not (List.isEmpty results) then
                    match currentPending with
                    | None -> Error "Strength Host adapter found tool results without a preceding call batch"
                    | Some(pendingCalls, regularParts, callIndex, callMessage, callHostId) ->
                        if not (String.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase)) then
                            Error "Strength tool results must originate from a logical tool message"
                        elif List.length results <> List.length message.Parts then
                            Error "Strength tool result message contains non-result parts"
                        elif Map.count pendingCalls <> List.length results then
                            Error "Strength Host adapter requires every tool call/result in the request batch"
                        else
                            let distinctIds =
                                results |> List.map (fun (id, _) -> ToolCallId.value id) |> Set.ofList

                            if Set.count distinctIds <> List.length results then
                                Error "Strength Host adapter refuses duplicate tool result ids in one batch"
                            else
                                let rec completedParts remainingResults accParts =
                                    match remainingResults with
                                    | [] -> Ok(List.rev accParts)
                                    | (callId, result) :: rest ->
                                        match Map.tryFind (ToolCallId.value callId) pendingCalls with
                                        | None -> Error "Strength Host adapter found an orphan tool result"
                                        | Some(_, name, args) ->
                                            completedParts
                                                rest
                                                (strengthCompletedPart callId name args result :: accParts)

                                match completedParts results [] with
                                | Error error -> Error error
                                | Ok parts ->
                                    let raw =
                                        rawMessage callIndex callMessage callHostId "assistant" (regularParts @ parts)

                                    encodeMessages tail None (raw :: acc)
                else
                    match currentPending with
                    | Some _ ->
                        Error "Strength Host adapter requires tool results immediately after the tool-call message"
                    | None ->
                        match encodeRegularParts message.Parts [] with
                        | Error error -> Error error
                        | Ok parts ->
                            let raw = rawMessage index message hostId message.Role parts
                            encodeMessages tail None (raw :: acc)

        encodeMessages triples None []

    /// STRENGTH-006/009: insertion-only write-back for owner Work history.
    /// Existing Host objects are reused byte/object-for-object; only renderer rows
    /// that do not match the next raw semantic message may be synthesized, and
    /// such rows must carry an explicit algebra-owned Host id. This prevents a
    /// Strength insertion from silently re-identifying physical owner history.
    let tryApplyRenderedInsertionsPreservingBase
        (sessionId: string)
        (sha256: string -> string)
        (rawMessages: obj list)
        (rendered: Wanxiangshu.Domain.RenderedMessages)
        : Result<obj list, string> =
        let decodeSingle raw =
            match (decodeMessageView [ raw ]).Messages with
            | [ message ] -> Ok message
            | _ -> Error "raw Host message does not decode to exactly one wire message"

        let rec decodeRaw remaining acc =
            match remaining with
            | [] -> Ok(List.rev acc)
            | raw :: tail ->
                match decodeSingle raw with
                | Error error -> Error error
                | Ok wire -> decodeRaw tail ((raw, wire) :: acc)

        let encodeInserted (message: WireMessage) (hostId: string option) =
            match hostId with
            | None -> Error "insertion-only write-back requires an explicit synthetic Host message id"
            | Some _ ->
                let singleton: Wanxiangshu.Domain.RenderedMessages =
                    { Messages = [ message ]
                      HostMessageIds = [ hostId ]
                      HostIsPhysical = [ false ] }

                match tryApplyRenderedMessages sessionId sha256 singleton with
                | Ok [ raw ] -> Ok raw
                | Ok _ -> Error "single synthetic message encoded to an unexpected cardinality"
                | Error error -> Error error

        match decodeRaw rawMessages [] with
        | Error error -> Error error
        | Ok decodedRaw ->
            let renderedRows =
                List.zip3 rendered.Messages rendered.HostMessageIds rendered.HostIsPhysical

            let rec merge rows raw acc =
                match rows, raw with
                | [], [] -> Ok(List.rev acc)
                | [], _ :: _ -> Error "projection insertion dropped raw Host messages"
                | (message, _, _) :: tail, (rawObj, rawWire) :: rawTail when message = rawWire ->
                    merge tail rawTail (rawObj :: acc)
                | (message, hostId, isPhysical) :: tail, rawRemaining ->
                    if isPhysical then
                        Error "projection insertion invented a physical Host message"
                    else
                        match encodeInserted message hostId with
                        | Error error -> Error error
                        | Ok inserted -> merge tail rawRemaining (inserted :: acc)

            merge renderedRows decodedRaw []

    /// PROJ-004: apply a rendered prefix to the Host message view — the one write-back
    /// adapter for the projection DSL's prefix stage. Business modules declare intents
    /// (PROJ-005) and never assemble messages themselves; this function turns the
    /// renderer's instruction into the Host object list, preserving the untouched tail
    /// verbatim so byte equality with what the provider saw is never re-derived.
    let applyRenderedPrefix (rawMessages: obj list) (rendered: Wanxiangshu.Domain.RenderedPrefix) : obj list =
        match rendered with
        | Wanxiangshu.Domain.RenderedPrefix.PhysicalPrefix -> rawMessages
        | Wanxiangshu.Domain.RenderedPrefix.SyntheticPrefix activation ->
            prependCompanionMemory rawMessages activation.SyntheticMessageId activation.Memory activation.DropLeading

    /// The Host's own message id.
    ///
    /// Not part of either projection: an id identifies a message, it is not
    /// content the model saw. HOST-010's binding needs it, so it is read
    /// separately and stays out of both.
    let hostMessageId (rawObj: obj) : string option =
        let info = infoObject rawObj
        readString info "id" |> Option.orElse (readString rawObj "id")

    /// Extract the single, unambiguous session id from a transform output's
    /// `messages` array. Used by hooks that need to identify the managed session
    /// before the Host has bound the run.
    ///
    /// Returns `None` when there are zero, multiple, or malformed session ids.
    let projectionSessionIdFromMessages (output: obj) : string option =
        if isNull output || isNull output?messages then
            None
        else
            let messages = unbox<obj array> output?messages

            let sessionIds =
                messages
                |> Array.choose (fun msg ->
                    if not (isNull msg) && not (isNull msg?info) && not (isNull msg?info?sessionID) then
                        Some(unbox<string> msg?info?sessionID)
                    else
                        None)
                |> Array.distinct

            match sessionIds with
            | [| sessionId |] -> Some sessionId
            | _ -> None

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
            | Some message when message.Role = "user" -> hostMessageId raw |> Option.map PhysicalUserMessageId.create
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
