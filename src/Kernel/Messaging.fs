module VibeFs.Kernel.Messaging

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn

/// Strongly-typed view of an opencode message part (P4-P7). Tool/Raw parts carry
/// the host object reference (`raw`) so the encoder can emit the original shape
/// with only the mutated field overwritten — no speculative deep cloning.

type Role =
    | User
    | Assistant
    | ToolResult
    | System

type ToolState = { status: string; output: string; error: string; input: obj }

type Part =
    | TextPart of text: string
    | ToolPart of toolName: string * callID: string * state: ToolState option * raw: obj
    | RawPart of raw: obj

type MessageInfo =
    { id: string
      sessionID: string
      role: Role
      agent: string
      isError: bool
      toolName: string
      details: obj
      time: obj }

type Source =
    | Native
    | Synthetic of kind: string

type Message =
    { info: MessageInfo
      parts: Part list
      source: Source
      raw: obj }

type Entry =
    | MessageEntry of Message
    | CustomEntry of customType: string * data: obj
    | RawEntry of obj

let private synthPrefixes =
    [ "caps-synth-user-"; "caps-synth-assistant-"; "magic-todo-projection-"; "magic-todo-prefix-" ]

let classifySource (id: string) : Source =
    if id = "" then Native
    else
        synthPrefixes
        |> List.tryFind id.StartsWith
        |> Option.map Synthetic
        |> Option.defaultValue Native

let decodeRole (s: string) : Role =
    match s with
    | "user" -> User
    | "assistant" -> Assistant
    | "toolResult" | "tool-result" | "tool_result" -> ToolResult
    | _ -> System

let private decodeIsError (info: obj) : bool =
    let v = get info "isError"
    not (isNullish v) && (v :?> bool)

let decodeToolState (state: obj) : ToolState option =
    if isNullish state then None
    else Some { status = str state "status"; output = str state "output"; error = str state "error"; input = get state "input" }

let decodePart (part: obj) : Part =
    match str part "type" with
    | "text" -> TextPart (str part "text")
    | "tool" -> ToolPart (str part "tool", str part "callID", decodeToolState (get part "state"), part)
    | _ -> RawPart part

let private decodeParts (parts: obj) : Part list =
    if isNullish parts || not (isArray parts) then []
    else (parts :?> obj array) |> Array.map decodePart |> List.ofArray

let decodeMessage (msg: obj) : Message option =
    if isNullish msg then None
    else
        let info = get msg "info"
        if isNullish info then None
        else
            let id = str info "id"
            Some
                { info =
                      { id = id
                        sessionID = str info "sessionID"
                        role = decodeRole (str info "role")
                        agent = str info "agent"
                        isError = decodeIsError info
                        toolName = str info "toolName"
                        details = get info "details"
                        time = get info "time" }
                  parts = decodeParts (get msg "parts")
                  source = classifySource id
                  raw = msg }

let decodeEntry (entry: obj) : Entry =
    if isNullish entry then RawEntry entry
    else
        match str entry "type" with
        | "message" ->
            match decodeMessage entry with
            | Some m -> MessageEntry m
            | None -> RawEntry entry
        | "custom" -> CustomEntry (str entry "customType", get entry "data")
        | _ -> RawEntry entry

let decodeEntries (entries: obj array) : Entry list =
    if isNullish entries then [] else entries |> Array.map decodeEntry |> List.ofArray

/// Encode a Part back to a host object. Text parts are rebuilt; tool/raw parts
/// pass through their original reference (callers overwrite mutated fields via
/// the helpers below — the only sites that touch a host obj).
let encodePart (part: Part) : obj =
    match part with
    | TextPart text -> box (createObj [ "type", box "text"; "text", box text ])
    | ToolPart (_, _, _, raw) -> raw
    | RawPart raw -> raw

/// Return a fresh host message object equal to `msg.raw` but with `parts`
/// replaced. A shallow copy with one field overwritten — the single encode
/// boundary; business logic never mutates a host object in place (P11/P16).
let withParts (msg: Message) (parts: Part list) : obj =
    let encoded = parts |> List.map encodePart |> List.toArray
    withKey msg.raw "parts" (box encoded)

/// Return a fresh host object for a tool part with its `state.output` overwritten
/// (P11: replaces the clone+withKey pyramid in setPartOutput with a typed match
/// plus a single boundary encode).
let withToolOutput (part: Part) (newOutput: string) : obj =
    match part with
    | ToolPart (_, _, Some _, raw) ->
        let state = get raw "state"
        if isNullish state then raw
        else withKey raw "state" (withKey state "output" (box newOutput))
    | _ -> encodePart part
