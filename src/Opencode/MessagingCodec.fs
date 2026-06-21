module VibeFs.Opencode.MessagingCodec

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Messaging

/// The single FFI boundary between host message objects and the strongly-typed
/// Kernel tree. All Dyn/JsInterop access for the message chain lives here.

let decodeToolState (state: obj) : ToolState option =
    if isNullish state then None
    else
        let input = get state "input"
        let operation = if isNullish input then null else get input "operation"
        let operationAction = if isNullish operation then "" else str operation "action"
        Some
            { status = str state "status"
              output = str state "output"
              error = str state "error"
              input = input
              operationAction = operationAction }

let private partDecoders: Map<string, obj -> Part> =
    Map [
        "text", fun part -> TextPart (str part "text")
    ]

let decodePart (part: obj) : Part =
    let typ = str part "type"
    match Map.tryFind typ partDecoders with
    | Some decode -> decode part
    | None ->
        match typ with
        | "tool" -> ToolPart (str part "tool", str part "callID", decodeToolState (get part "state"), part)
        | _ -> RawPart part

let decodeParts (parts: obj) : Part list =
    if isNullish parts || not (isArray parts) then []
    else (parts :?> obj array) |> Array.map decodePart |> List.ofArray

let decodeMessage (msg: obj) : Message option =
    if isNullish msg then None
    else
        let info = get msg "info"
        if isNullish info then None
        else
            let id = str info "id"
            let isErrorValue = get info "isError"
            let isError = not (isNullish isErrorValue) && (isErrorValue :?> bool)
            Some
                { info =
                      { id = id
                        sessionID = str info "sessionID"
                        role = decodeRole (str info "role")
                        agent = str info "agent"
                        isError = isError
                        toolName = str info "toolName"
                        details = get info "details"
                        time = get info "time" }
                  parts = decodeParts (get msg "parts")
                  source = classifySource id
                  raw = msg }

let decodeMessages (messages: obj array) : Message list =
    if isNullish messages then []
    else messages |> Array.choose decodeMessage |> List.ofArray

/// Encode a Part back to a host object. Text parts are rebuilt. Tool parts
/// return the carried raw reference unchanged when their typed state fields
/// match the host state (preserving object identity for dedup's "keeps ref"
/// contract); only parts mutated by a pure typed update (e.g.
/// setPartOutputTyped) get a shallow state rebuild. Raw parts pass through.
let encodePart (part: Part) : obj =
    match part with
    | TextPart text -> box (createObj [ "type", box "text"; "text", box text ])
    | ToolPart(toolName, callID, Some state, raw) ->
        let rawState = if isNull raw then null else get raw "state"
        if not (isNull raw) && not (isNullish rawState)
           && str rawState "status" = state.status
           && str rawState "output" = state.output
           && str rawState "error" = state.error then
            raw
        else
            let stateObj =
                if isNullish rawState then
                    box (createObj [
                        "status", box state.status
                        "output", box state.output
                        "error", box state.error
                        "input", state.input
                    ])
                else
                    let s1 = withKey rawState "status" (box state.status)
                    let s2 = withKey s1 "output" (box state.output)
                    withKey s2 "error" (box state.error)
            if isNull raw then
                box (createObj [ "type", box "tool"; "tool", box toolName; "callID", box callID; "state", stateObj ])
            else
                withKey raw "state" stateObj
    | ToolPart(toolName, callID, None, raw) ->
        if isNull raw then
            box (createObj [ "type", box "tool"; "tool", box toolName; "callID", box callID ])
        else raw
    | RawPart raw -> raw

let private encodeMessageInfo (info: MessageInfo) : obj =
    let timeObj = if isNull info.time then box (createObj [ "created", box 0 ]) else info.time
    createObj [
        "id", box info.id
        "sessionID", box info.sessionID
        "role", box (match info.role with User -> "user" | Assistant -> "assistant" | ToolResult -> "toolResult" | System -> "system")
        "agent", box info.agent
        "isError", box info.isError
        "toolName", box info.toolName
        "details", info.details
        "time", timeObj
        "model", box (createObj [ "providerID", box ""; "modelID", box "" ])
    ]

/// Encode a typed Message back to a host object. For native messages
/// (raw <> null) this returns the raw reference unchanged when no part was
/// mutated (preserving object identity for "preserves original" contracts);
/// only messages whose parts were rebuilt by a pure typed update get a shallow
/// copy with `parts` replaced. Synthetic messages (raw = null) build fresh.
let encodeMessage (msg: Message) : obj =
    if isNull msg.raw then
        let partsObj = msg.parts |> List.map encodePart |> List.toArray
        box (createObj [ "info", box (encodeMessageInfo msg.info); "parts", box partsObj ])
    else
        let rawParts = get msg.raw "parts"
        let encodedParts = msg.parts |> List.map encodePart |> List.toArray
        let partsUnchanged =
            not (isNullish rawParts) && isArray rawParts
            && (let arr = rawParts :?> obj array
                arr.Length = encodedParts.Length && Array.forall2 (fun a b -> obj.ReferenceEquals(a, b)) arr encodedParts)
        if partsUnchanged then msg.raw
        else withKey msg.raw "parts" (box encodedParts)

let encodeMessages (messages: Message list) : obj array =
    messages |> List.map encodeMessage |> List.toArray
