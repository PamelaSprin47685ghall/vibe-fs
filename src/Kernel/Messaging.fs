module VibeFs.Kernel.Messaging

/// Strongly-typed view of an opencode message part. Tool/Raw parts carry the
/// host object reference (`raw`) so the encoder can emit the original shape with
/// only mutated fields overwritten — no speculative deep cloning.

type Role =
    | User
    | Assistant
    | ToolResult
    | System

type ToolState =
    { status: string
      output: string
      error: string
      input: obj
      operationAction: string }

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

type FlatPart =
    { msgIndex: int
      partIndex: int
      isUser: bool
      part: Part }

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

/// Pure typed copy of a tool part with its state.output overwritten.
let setPartOutputTyped (part: Part) (newOutput: string) : Part =
    match part with
    | ToolPart(toolName, callID, Some state, raw) ->
        ToolPart(toolName, callID, Some { state with output = newOutput }, raw)
    | other -> other

let partCallID (part: Part) : string =
    match part with
    | ToolPart(_, callID, _, _) -> callID
    | _ -> ""

let partTextStr (part: Part) : string =
    match part with
    | TextPart text -> text
    | _ -> ""

let partIsText (part: Part) : bool =
    match part with
    | TextPart _ -> true
    | _ -> false

let partIsTool (part: Part) : bool =
    match part with
    | ToolPart _ -> true
    | _ -> false

/// Flatten a message list into ordered flat parts, tagging whether each
/// belongs to a user message. Pure: no IO, no host-object access.
let flatten (messages: Message list) : FlatPart list =
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
let readAssistantText (messages: Message list) (startIndex: int) (joiner: string) : string option =
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
let stripSyntheticBySource (messages: Message list) : Message list =
    messages |> List.filter (fun m -> m.source = Native)
