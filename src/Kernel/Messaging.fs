module Wanxiangshu.Kernel.Messaging

open Wanxiangshu.Kernel.ToolExecutionStatusModule

/// Host-agnostic message model. `'raw` carries the original host object reference
/// through the Kernel without being inspected; only Host codec layers instantiate it.

type Role =
    | User
    | Assistant
    | ToolResult
    | System

type ToolState<'raw> =
    { status: ToolExecutionStatus
      output: string
      error: string
      input: 'raw
      operationAction: string }

type Part<'raw> =
    | TextPart of text: string
    | ToolPart of toolName: string * callID: string * state: ToolState<'raw> option * raw: 'raw
    | RawPart of raw: 'raw

type MessageInfo<'raw> =
    { id: string
      sessionID: string
      role: Role
      agent: string
      isError: bool
      toolName: string
      details: 'raw
      time: 'raw }

type Source =
    | Native
    | Synthetic of kind: string

type Message<'raw> =
    { info: MessageInfo<'raw>
      parts: Part<'raw> list
      source: Source
      raw: 'raw }

type Entry<'raw> =
    | MessageEntry of Message<'raw>
    | CustomEntry of customType: string * data: 'raw
    | RawEntry of 'raw

type FlatPart<'raw> =
    { msgIndex: int
      partIndex: int
      isUser: bool
      part: Part<'raw> }

let synthPrefixes =
    [ "caps-synth-user-"
      "caps-synth-assistant-"
      "caps-synth-ack-"
      "magic-todo-projection-"
      "magic-todo-prefix-"
      "methodology-probe-"
      "semble-synth-"
      "parallel-tool-synth-" ]

let private roleMap =
    Map.ofList
        [ "user", User
          "assistant", Assistant
          "toolresult", ToolResult
          "tool-result", ToolResult
          "tool_result", ToolResult
          "tool", ToolResult
          "system", System ]

let decodeRole (s: string) : Role =
    let lowered = s.Trim().ToLowerInvariant()
    Map.tryFind lowered roleMap |> Option.defaultValue System

let isToolResultRoleString (s: string) : bool = decodeRole s = ToolResult

/// Pure typed copy of a tool part with its state.output overwritten.
let setPartOutputTyped (part: Part<'raw>) (newOutput: string) : Part<'raw> =
    match part with
    | ToolPart(toolName, callID, Some state, raw) ->
        ToolPart(toolName, callID, Some { state with output = newOutput }, raw)
    | other -> other

let partCallID (part: Part<'raw>) : string =
    match part with
    | ToolPart(_, callID, _, _) -> callID
    | _ -> ""

let partTextStr (part: Part<'raw>) : string =
    match part with
    | TextPart text -> text
    | _ -> ""

let partIsText (part: Part<'raw>) : bool =
    match part with
    | TextPart _ -> true
    | _ -> false

let partIsTool (part: Part<'raw>) : bool =
    match part with
    | ToolPart _ -> true
    | _ -> false

/// Flatten a message list into ordered flat parts, tagging whether each
/// belongs to a user message. Pure: no IO, no host-object access.
let flatten (messages: Message<'raw> list) : FlatPart<'raw> list =
    messages
    |> List.indexed
    |> List.collect (fun (msgIdx, msg) ->
        let isUser = msg.info.role = User

        msg.parts
        |> List.indexed
        |> List.map (fun (partIdx, part) ->
            { msgIndex = msgIdx
              partIndex = partIdx
              isUser = isUser
              part = part }))

/// Skip `startIndex` messages, then collect non-empty text from assistant
/// messages' TextParts, joining with `joiner`. Pure.
let readAssistantText (messages: Message<'raw> list) (startIndex: int) (joiner: string) : string option =
    if startIndex >= List.length messages then
        None
    else
        let chunks =
            messages.[startIndex..]
            |> List.filter (fun m -> m.info.role = Assistant)
            |> List.collect (fun m ->
                m.parts
                |> List.choose (fun p ->
                    match p with
                    | TextPart text when text <> "" -> Some text
                    | _ -> None))

        if chunks.IsEmpty then
            None
        else
            Some(String.concat joiner chunks)

let private textHasCaps (t: string) =
    t <> null && t.Contains("<wanxiangshu-caps")

let private idHasCapsPrefix (id: string) =
    id <> null
    && (id.StartsWith("caps-synth-")
        || id.StartsWith("caps-call-")
        || id.StartsWith("caps-fr-")
        || id.StartsWith("caps-tool-"))

let private partLooksCaps (p: Part<'raw>) : bool =
    match p with
    | TextPart t -> textHasCaps t
    | ToolPart(_, callID, stateOpt, _) ->
        idHasCapsPrefix callID
        || (match stateOpt with
            | Some st -> textHasCaps st.output
            | None -> false)
    | _ -> false

/// Drop synthetic messages, keeping only Native-sourced ones. Pure string checks only.
let stripSyntheticBySource (messages: Message<'raw> list) : Message<'raw> list =
    messages
    |> List.filter (fun m ->
        if m.parts |> List.exists partLooksCaps then
            false
        else
            match m.source with
            | Synthetic _ -> false
            | Native ->
                let rawStr = if box m.raw <> null then string m.raw else ""
                not (idHasCapsPrefix m.info.id || textHasCaps rawStr))

/// Extract the first non-empty session ID from a list of messages. Pure.
let extractSessionID (messages: Message<'raw> list) : string =
    match
        messages
        |> List.tryPick (fun m ->
            if m.info.sessionID <> "" then
                Some m.info.sessionID
            else
                None)
    with
    | Some sid -> sid
    | None -> ""

let capsSynthUserPrefix = "caps-synth-user-"
let capsSynthAssistantPrefix = "caps-synth-assistant-"
