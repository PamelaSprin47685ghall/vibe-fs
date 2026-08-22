namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Rendered projection → Host raw write-back adapters (Wave 3 split of the old
/// `Projection` module). This module turns `RenderedMessages` / `RenderedPrefix`
/// into Host object lists; decoding back the other way lives in
/// `ProviderWireDecode` / `ProviderWireCapture`.
module ProjectionMessageEdit =

    let private rawPartCallId (part: obj) =
        ProviderWireDecode.firstString part [ "callID"; "callId"; "toolCallId"; "id" ]

    let private isTodoWritePart (part: obj) =
        ProviderWireDecode.firstString part [ "tool"; "name" ]
        |> Option.exists (fun tool -> String.Equals(tool, "todowrite", StringComparison.OrdinalIgnoreCase))

    let private retentionFacts (message: obj) : XPrefixProjection.RawPrefixMessageFacts =
        let parts = ProviderWireDecode.rawPartsOf message

        { ContainsTodoWrite = parts |> List.exists isTodoWritePart
          ToolCallIds = parts |> List.choose rawPartCallId |> List.map ToolCallId.create |> Set.ofList }

    let private retainedTodoRounds (covered: obj list) =
        List.zip covered (covered |> List.map retentionFacts |> XPrefixProjection.retainTodoWriteRounds)
        |> List.choose (fun (message, retain) -> if retain then Some message else None)

    let private companionHead (syntheticId: string) (memory: string) =
        createObj
            [ "info", box (createObj [ "id", box syntheticId; "role", box "user" ])
              "parts", box [| createObj [ "type", box "text"; "text", box memory ] |] ]

    let prependCompanionMemory
        (rawMessages: obj list)
        (syntheticId: string)
        (memory: string)
        (dropLeading: int)
        : obj list =
        if dropLeading > List.length rawMessages then
            invalidArg "dropLeading" "X-wire prefix cutoff exceeds the current provider snapshot"

        let dropped = List.take dropLeading rawMessages

        companionHead syntheticId memory
        :: (retainedTodoRounds dropped @ List.skip dropLeading rawMessages)

    /// Prefix replacement for stable XTrace-backed sessions.
    ///
    /// `coveredHostMessageIds` names the physical historical messages proved by
    /// canonical XTrace. Request-local provider rows are not in that set, so a
    /// narrative/replay/grounding insertion can neither shift the cutoff nor be
    /// accidentally deleted merely because it occupies an earlier array index.
    let prependCompanionMemoryByHostIds
        (rawMessages: obj list)
        (syntheticId: string)
        (memory: string)
        (coveredHostMessageIds: string list)
        (insertAfterHostMessageId: string option)
        : obj list =
        let coveredIds = coveredHostMessageIds |> Set.ofList

        let covered =
            rawMessages
            |> List.filter (fun message ->
                ProviderWireDecode.hostMessageId message
                |> Option.exists (fun messageId -> Set.contains messageId coveredIds))

        let retainedCoveredIds =
            retainedTodoRounds covered
            |> List.choose ProviderWireDecode.hostMessageId
            |> Set.ofList

        let survivesReplacement message =
            match ProviderWireDecode.hostMessageId message with
            | Some messageId when Set.contains messageId coveredIds -> Set.contains messageId retainedCoveredIds
            | _ -> true

        let surviving = rawMessages |> List.filter survivesReplacement
        let head = companionHead syntheticId memory

        let insertionIndex =
            insertAfterHostMessageId
            |> Option.map (fun anchor ->
                surviving
                |> List.tryFindIndex (fun message -> ProviderWireDecode.hostMessageId message = Some anchor)
                |> Option.defaultWith (fun () ->
                    invalidArg "insertAfterHostMessageId" "stable prefix insertion anchor is absent from Host view"))
            |> Option.defaultValue -1

        let before, after = List.splitAt (insertionIndex + 1) surviving
        before @ (head :: after)

    /// Host transport membrane: remove exactly the addressed physical rows.
    /// No role/count heuristic is permitted here — transport metadata identifies
    /// Host messages, so write-back must consume the same stable identity universe.
    let suppressHostMessagesByIds (rawMessages: obj list) (messageIds: Set<string>) : obj list =
        if Set.isEmpty messageIds then
            rawMessages
        else
            rawMessages
            |> List.filter (fun message ->
                ProviderWireDecode.hostMessageId message
                |> Option.forall (fun messageId -> not (Set.contains messageId messageIds)))

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

    let private encodeParts (parts: WirePart list) : Result<obj list, string> =
        parts |> List.traverseResultM encodeWirePart

    let private derivedHostMessageId (sha256: string -> string) (index: int) (message: WireMessage) =
        let single: ProviderWireProjection =
            { ProviderId = None
              ModelId = None
              Variant = None
              Tools = []
              System = []
              Messages = [ message ] }

        sha256 (sprintf "%d\u001f%s" index (renderWire single))

    let private encodeRenderedMessage
        (sessionId: string)
        (sha256: string -> string)
        (index: int)
        (message: WireMessage)
        (hostId: string option)
        : Result<obj, string> =
        result {
            let! parts = encodeParts message.Parts

            let id =
                hostId
                |> Option.defaultWith (fun () -> derivedHostMessageId sha256 index message)

            return
                createObj
                    [ "info", box (createObj [ "id", box id; "sessionID", box sessionId; "role", box message.Role ])
                      "parts", box (List.toArray parts) ]
        }

    /// STRENGTH-009 / PROJ-004: write a fully rendered DSL message view back to
    /// Host objects. This is intentionally an adapter, not business assembly.
    /// Missing Host ids are derived from wire bytes + ordinal and are Host-only;
    /// the generated identity never enters ProviderSemanticProjection.
    let tryApplyRenderedMessages
        (sessionId: string)
        (sha256: string -> string)
        (rendered: Wanxiangshu.Participant.Provider.Projection.RenderedMessages)
        : Result<obj list, string> =
        List.zip3 rendered.Messages rendered.HostMessageIds rendered.HostIsPhysical
        |> List.mapi (fun index (message, hostId, _) -> index, message, hostId)
        |> List.traverseResultM (fun (index, message, hostId) ->
            encodeRenderedMessage sessionId sha256 index message hostId)

    let private strengthHostMessageId
        (sha256: string -> string)
        (index: int)
        (message: WireMessage)
        (hostId: string option)
        =
        hostId
        |> Option.defaultWith (fun () -> derivedHostMessageId sha256 index message)

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

    let private encodeRegularPart (part: WirePart) : Result<obj, string> =
        match part with
        | WireToolCall _
        | WireToolResult _ -> Error "Strength Host adapter received an unpaired tool part"
        | _ -> encodeWirePart part

    let private encodeRegularParts (parts: WirePart list) : Result<obj list, string> =
        parts |> List.traverseResultM encodeRegularPart

    let private collectToolCalls (parts: WirePart list) =
        parts
        |> List.choose (function
            | WireToolCall(callId, name, args) -> Some(callId, name, args)
            | _ -> None)

    let private collectToolResults (parts: WirePart list) =
        parts
        |> List.choose (function
            | WireToolResult(callId, result) -> Some(callId, result)
            | _ -> None)

    let private nonCallParts (parts: WirePart list) =
        parts
        |> List.filter (function
            | WireToolCall _ -> false
            | _ -> true)

    let private requireDistinctIds (ids: string list) (error: string) : Result<unit, string> =
        if Set.count (Set.ofList ids) <> List.length ids then
            Error error
        else
            Ok()

    let private requireAssistantRole (role: string) : Result<unit, string> =
        if String.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) then
            Ok()
        else
            Error "Strength tool calls must originate from an assistant message"

    let private requireToolRole (role: string) : Result<unit, string> =
        if String.Equals(role, "tool", StringComparison.OrdinalIgnoreCase) then
            Ok()
        else
            Error "Strength tool results must originate from a logical tool message"

    type private StrengthCallBatch =
        Map<string, ToolCallId * string * string> * obj list * int * WireMessage * string option

    let private startCallBatch
        (pendingBatch: StrengthCallBatch option)
        (message: WireMessage)
        (index: int)
        (hostId: string option)
        (calls: (ToolCallId * string * string) list)
        : Result<StrengthCallBatch option, string> =
        if Option.isSome pendingBatch then
            Error "Strength Host adapter saw a new tool batch before the previous batch completed"
        else
            result {
                do! requireAssistantRole message.Role
                let! regularParts = encodeRegularParts (nonCallParts message.Parts)

                do!
                    requireDistinctIds
                        (calls |> List.map (fun (id, _, _) -> ToolCallId.value id))
                        "Strength Host adapter refuses duplicate tool call ids in one batch"

                let pendingCalls =
                    calls
                    |> List.map (fun (callId, name, args) -> ToolCallId.value callId, (callId, name, args))
                    |> Map.ofList

                return Some(pendingCalls, regularParts, index, message, hostId)
            }

    let private completeOneResult
        (pendingCalls: Map<string, ToolCallId * string * string>)
        (callId: ToolCallId, resultCanonical: string)
        : Result<obj, string> =
        match Map.tryFind (ToolCallId.value callId) pendingCalls with
        | None -> Error "Strength Host adapter found an orphan tool result"
        | Some(_, name, args) -> Ok(strengthCompletedPart callId name args resultCanonical)

    let private strengthRawMessage (sessionId: string) (sha256: string -> string) index message hostId role parts =
        let id = strengthHostMessageId sha256 index message hostId

        createObj
            [ "info", box (createObj [ "id", box id; "sessionID", box sessionId; "role", box role ])
              "parts", box (List.toArray parts) ]

    let private requireResultPartsOnly
        (message: WireMessage)
        (results: (ToolCallId * string) list)
        : Result<unit, string> =
        if List.length results <> List.length message.Parts then
            Error "Strength tool result message contains non-result parts"
        else
            Ok()

    let private requireBatchCardinality
        (pendingCalls: Map<string, ToolCallId * string * string>)
        (results: (ToolCallId * string) list)
        : Result<unit, string> =
        if Map.count pendingCalls <> List.length results then
            Error "Strength Host adapter requires every tool call/result in the request batch"
        else
            Ok()

    let private requirePendingBatch (pendingBatch: StrengthCallBatch option) : Result<StrengthCallBatch, string> =
        match pendingBatch with
        | None -> Error "Strength Host adapter found tool results without a preceding call batch"
        | Some batch -> Ok batch

    let private finishResultBatch
        (sessionId: string)
        (sha256: string -> string)
        (pendingBatch: StrengthCallBatch option)
        (message: WireMessage)
        (results: (ToolCallId * string) list)
        : Result<obj, string> =
        result {
            let! pendingCalls, regularParts, callIndex, callMessage, callHostId = requirePendingBatch pendingBatch

            do! requireToolRole message.Role
            do! requireResultPartsOnly message results
            do! requireBatchCardinality pendingCalls results

            do!
                requireDistinctIds
                    (results |> List.map (fun (id, _) -> ToolCallId.value id))
                    "Strength Host adapter refuses duplicate tool result ids in one batch"

            let! completed = results |> List.traverseResultM (completeOneResult pendingCalls)

            return
                strengthRawMessage
                    sessionId
                    sha256
                    callIndex
                    callMessage
                    callHostId
                    "assistant"
                    (regularParts @ completed)
        }

    let private emitRegularMessage
        (sessionId: string)
        (sha256: string -> string)
        (pendingBatch: StrengthCallBatch option)
        (index: int)
        (message: WireMessage)
        (hostId: string option)
        : Result<obj, string> =
        if Option.isSome pendingBatch then
            Error "Strength Host adapter requires tool results immediately after the tool-call message"
        else
            result {
                let! parts = encodeRegularParts message.Parts
                return strengthRawMessage sessionId sha256 index message hostId message.Role parts
            }

    let private continueStrengthMessage
        (sessionId: string)
        (sha256: string -> string)
        (loop:
            (int * (WireMessage * string option * bool)) list
                -> StrengthCallBatch option
                -> obj list
                -> Result<obj list, string>)
        (tail: (int * (WireMessage * string option * bool)) list)
        (pendingBatch: StrengthCallBatch option)
        (acc: obj list)
        (index: int)
        (message: WireMessage)
        (hostId: string option)
        : Result<obj list, string> =
        let calls = collectToolCalls message.Parts
        let results = collectToolResults message.Parts

        match List.isEmpty calls, List.isEmpty results with
        | false, false -> Error "Strength Host adapter refuses a message mixing tool calls and results"
        | false, true ->
            startCallBatch pendingBatch message index hostId calls
            |> Result.bind (fun nextBatch -> loop tail nextBatch acc)
        | true, false ->
            finishResultBatch sessionId sha256 pendingBatch message results
            |> Result.bind (fun raw -> loop tail None (raw :: acc))
        | true, true ->
            emitRegularMessage sessionId sha256 pendingBatch index message hostId
            |> Result.bind (fun raw -> loop tail None (raw :: acc))

    /// STRENGTH-003/009: encode a Strength Replica provider view into the Host's
    /// native OpenCode tool-part shape. `MessageV2.toModelMessagesEffect` renders
    /// one completed `type=tool` part as the provider's tool-call + tool-result
    /// exchange; pending/running parts are rendered as interrupted errors. The
    /// logical assistant-call + tool-result pair must therefore collapse to one
    /// completed Host assistant message, never two physical Host rows.
    let tryApplyStrengthRenderedMessages
        (sessionId: string)
        (sha256: string -> string)
        (rendered: Wanxiangshu.Participant.Provider.Projection.RenderedMessages)
        : Result<obj list, string> =
        let triples =
            List.zip3 rendered.Messages rendered.HostMessageIds rendered.HostIsPhysical
            |> List.mapi (fun index triple -> index, triple)

        let rec encodeMessages remaining pending acc =
            match remaining, pending with
            | [], None -> Ok(List.rev acc)
            | [], Some _ -> Error "Strength Host adapter ended with an incomplete tool batch"
            | (index, (message, hostId, _)) :: tail, pendingBatch ->
                continueStrengthMessage sessionId sha256 encodeMessages tail pendingBatch acc index message hostId

        encodeMessages triples None []

    let private decodeSingle raw =
        match (ProviderWireCapture.decodeMessageView [ raw ]).Messages with
        | [ message ] -> Ok message
        | _ -> Error "raw Host message does not decode to exactly one wire message"

    let private decodeRaw (rawMessages: obj list) =
        rawMessages
        |> List.traverseResultM (fun raw -> decodeSingle raw |> Result.map (fun wire -> raw, wire))

    let private requireSingletonEncoded (encoded: obj list) : Result<obj, string> =
        match encoded with
        | [ raw ] -> Ok raw
        | _ -> Error "single synthetic message encoded to an unexpected cardinality"

    let private encodeInserted
        (sessionId: string)
        (sha256: string -> string)
        (message: WireMessage)
        (hostId: string option)
        : Result<obj, string> =
        match hostId with
        | None -> Error "insertion-only write-back requires an explicit synthetic Host message id"
        | Some _ ->
            let singleton: Wanxiangshu.Participant.Provider.Projection.RenderedMessages =
                { Messages = [ message ]
                  HostMessageIds = [ hostId ]
                  HostIsPhysical = [ false ] }

            tryApplyRenderedMessages sessionId sha256 singleton
            |> Result.bind requireSingletonEncoded

    let private insertOrReject
        (sessionId: string)
        (sha256: string -> string)
        (isPhysical: bool)
        (message: WireMessage)
        (hostId: string option)
        : Result<obj, string> =
        if isPhysical then
            Error "projection insertion invented a physical Host message"
        else
            encodeInserted sessionId sha256 message hostId

    let private mergeInsertions
        (sessionId: string)
        (sha256: string -> string)
        (renderedRows: (WireMessage * string option * bool) list)
        (decodedRaw: (obj * WireMessage) list)
        : Result<obj list, string> =
        let rec merge rows raw acc =
            match rows, raw with
            | [], [] -> Ok(List.rev acc)
            | [], _ :: _ -> Error "projection insertion dropped raw Host messages"
            | (message, _, _) :: tail, (rawObj, rawWire) :: rawTail when message = rawWire ->
                merge tail rawTail (rawObj :: acc)
            | (message, hostId, isPhysical) :: tail, rawRemaining ->
                insertOrReject sessionId sha256 isPhysical message hostId
                |> Result.bind (fun inserted -> merge tail rawRemaining (inserted :: acc))

        merge renderedRows decodedRaw []

    /// STRENGTH-006/009: insertion-only write-back for owner Work history.
    /// Existing Host objects are reused byte/object-for-object; only renderer rows
    /// that do not match the next raw semantic message may be synthesized, and
    /// such rows must carry an explicit algebra-owned Host id. This prevents a
    /// Strength insertion from silently re-identifying physical owner history.
    let tryApplyRenderedInsertionsPreservingBase
        (sessionId: string)
        (sha256: string -> string)
        (rawMessages: obj list)
        (rendered: Wanxiangshu.Participant.Provider.Projection.RenderedMessages)
        : Result<obj list, string> =
        result {
            let! decodedRaw = decodeRaw rawMessages

            let renderedRows =
                List.zip3 rendered.Messages rendered.HostMessageIds rendered.HostIsPhysical

            return! mergeInsertions sessionId sha256 renderedRows decodedRaw
        }

    /// PROJ-004: apply a rendered prefix to the Host message view — the one write-back
    /// adapter for the projection DSL's prefix stage. Business modules declare intents
    /// (PROJ-005) and never assemble messages themselves; this function turns the
    /// renderer's instruction into the Host object list, preserving the untouched tail
    /// verbatim so byte equality with what the provider saw is never re-derived.
    let applyRenderedPrefix
        (rawMessages: obj list)
        (rendered: Wanxiangshu.Participant.Provider.Projection.RenderedPrefix)
        : obj list =
        match rendered with
        | Wanxiangshu.Participant.Provider.Projection.RenderedPrefix.PhysicalPrefix -> rawMessages
        | Wanxiangshu.Participant.Provider.Projection.RenderedPrefix.SyntheticPrefix activation ->
            prependCompanionMemory
                rawMessages
                activation.SyntheticMessageId
                activation.Memory
                activation.CutoffExclusive

    /// Stable-identity counterpart of `applyRenderedPrefix`. The renderer keeps
    /// carrying the canonical semantic cutoff for identity/seal purposes; the
    /// Host adapter resolves deletion through XTrace-owned physical identities.
    let applyRenderedPrefixByHostIds
        (rawMessages: obj list)
        (coveredHostMessageIds: string list)
        (insertAfterHostMessageId: string option)
        (rendered: Wanxiangshu.Participant.Provider.Projection.RenderedPrefix)
        : obj list =
        match rendered with
        | Wanxiangshu.Participant.Provider.Projection.RenderedPrefix.PhysicalPrefix -> rawMessages
        | Wanxiangshu.Participant.Provider.Projection.RenderedPrefix.SyntheticPrefix activation ->
            prependCompanionMemoryByHostIds
                rawMessages
                activation.SyntheticMessageId
                activation.Memory
                coveredHostMessageIds
                insertAfterHostMessageId
