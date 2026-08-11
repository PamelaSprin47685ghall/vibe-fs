namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Rendered projection → Host raw write-back adapters (Wave 3 split of the old
/// `Projection` module). This module turns `RenderedMessages` / `RenderedPrefix`
/// into Host object lists; decoding back the other way lives in
/// `ProviderWireDecode` / `ProviderWireCapture`.
module ProjectionMessageEdit =

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
            | (index, (message, hostId, _)) :: tail, pendingBatch ->
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
                    match pendingBatch with
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
                    match pendingBatch with
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
                    match pendingBatch with
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
            match (ProviderWireCapture.decodeMessageView [ raw ]).Messages with
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
