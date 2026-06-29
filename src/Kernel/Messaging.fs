module Wanxiangshu.Kernel.Messaging

/// Host-agnostic message model. `'raw` carries the original host object reference
/// through the Kernel without being inspected; only Host codec layers instantiate it.

type Role =
    | User
    | Assistant
    | ToolResult
    | System

type ToolState<'raw> =
    { status: string
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

let private synthPrefixes =
    [ "caps-synth-user-"; "caps-synth-assistant-"; "caps-synth-ack-"
      "backlog-projection-"; "backlog-prefix-"
      "magic-todo-projection-"; "magic-todo-prefix-"
      "methodology-probe-"; "semble-synth-" ]

let classifySource (id: string) : Source =
    if id = "" then Native
    else
        synthPrefixes
        |> List.tryFind id.StartsWith
        |> Option.map Synthetic
        |> Option.defaultValue Native

let private roleMap =
    Map.ofList
        [ "user", User
          "assistant", Assistant
          "toolResult", ToolResult
          "tool-result", ToolResult
          "tool_result", ToolResult ]

let decodeRole (s: string) : Role = Map.tryFind s roleMap |> Option.defaultValue System

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
            { msgIndex = msgIdx; partIndex = partIdx; isUser = isUser; part = part }))

/// Skip `startIndex` messages, then collect non-empty text from assistant
/// messages' TextParts, joining with `joiner`. Pure.
let readAssistantText (messages: Message<'raw> list) (startIndex: int) (joiner: string) : string option =
    if startIndex >= List.length messages then None
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
        if chunks.IsEmpty then None else Some(String.concat joiner chunks)

/// Drop synthetic messages, keeping only Native-sourced ones. Pure.
let stripSyntheticBySource (messages: Message<'raw> list) : Message<'raw> list =
    messages |> List.filter (fun m -> m.source = Native)
