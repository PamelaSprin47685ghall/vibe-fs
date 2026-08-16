namespace Wanxiangshu.OpenCode

open Wanxiangshu.Interaction.Dispatch.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

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
///
/// Wave 3 split: this module is the raw decode boundary. `ProviderWireCapture`
/// owns the capture variants that retain Host stable addresses; it depends on
/// `readField`/`firstString`/`rawArray`/`infoObject` here, which are therefore
/// public rather than private.
module ProviderWireDecode =

    let readField (value: obj) (name: string) : obj =
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

    let firstString (value: obj) (names: string list) =
        names |> List.tryPick (readString value)

    /// Host messages arrive either bare or wrapped as `{ info, parts }`.
    let infoObject (rawObj: obj) : obj =
        if isNull rawObj then null
        elif not (isNull rawObj?info) then rawObj?info
        else rawObj

    let rawArray (value: obj) : obj list =
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
                    // (or `state.error`). A completed/errored tool part projects
                    // as the RESULT, while only a pending call (this
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
            // `output` (or `errorText` on failure). Preserve it as typed wire data;
            // challenge verification is owned by ReviewBarrierWorkflow.
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

    let messagesFromTransformOutput (output: obj) : obj list =
        unbox<obj array> output?messages |> Array.toList

    /// The Host's own message id.
    ///
    /// Not part of either projection: an id identifies a message, it is not
    /// content the model saw. HOST-010's binding needs it, so it is read
    /// separately and stays out of both.
    let hostMessageId (rawObj: obj) : string option =
        let info = infoObject rawObj
        readString info "id" |> Option.orElse (readString rawObj "id")

    /// Top-level string field of a raw Host message (e.g. the title-request
    /// `content` preamble). Host messages are the only objects this reads —
    /// domain policy never touches raw objects.
    let topLevelString (rawObj: obj) (name: string) : string option = readString rawObj name

    /// Raw `parts` array of a Host message, for write-back paths that must
    /// preserve non-text parts verbatim.
    let rawPartsOf (rawObj: obj) : obj list =
        rawArray (if isNull rawObj then null else rawObj?parts)

    /// The Host compaction pseudo-run marker (SessionSnapshotPort): any of
    /// `agent`/`mode` = "compaction" or `summary` = true.
    let isCompactionMarker (rawObj: obj) : bool =
        let info = infoObject rawObj

        if isNull info then
            false
        else
            let label (name: string) =
                let v = readField info name
                if isNull v then "" else string v

            (label "agent").ToLowerInvariant() = "compaction"
            || (label "mode").ToLowerInvariant() = "compaction"
            || (label "summary").ToLowerInvariant() = "true"

    /// The `PromptKey` a Host message carries in its metadata (PROMPT-011).
    /// Reads the field PromptMetadataCodec wrote; single-message variant of
    /// PromptIngressCodec's input/output pair reader.
    let promptKeyOfMessage (rawObj: obj) : PromptKey option =
        let fromMetadata (source: obj) =
            if isNull source || isNull source?metadata then
                None
            else
                let value = source?metadata?(PromptMetadataCodec.PromptKeyField)

                if isNull value then None else Some(unbox<string> value)

        let info = infoObject rawObj

        let fromParts () =
            rawArray (if isNull rawObj then null else rawObj?parts)
            |> List.tryPick (fun part -> if isNull part then None else fromMetadata part)

        [ fromMetadata info; fromMetadata rawObj; fromParts () ]
        |> List.tryPick id
        |> Option.map PromptKey.create

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
