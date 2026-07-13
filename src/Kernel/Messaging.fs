module Wanxiangshu.Kernel.Messaging

open Fable.Core
open Fable.Core.JsInterop
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

let backlogPrefixIdPrefix = "backlog-prefix-"

let private synthPrefixes =
    [ "caps-synth-user-"
      "caps-synth-assistant-"
      "caps-synth-ack-"
      "backlog-projection-"
      backlogPrefixIdPrefix
      "magic-todo-projection-"
      "magic-todo-prefix-"
      "methodology-probe-"
      "semble-synth-"
      "context-budget-nudge-"
      "parallel-tool-synth-" ]

[<Emit("typeof $0")>]
let private jsTypeOf (o: obj) : string = Fable.Core.JS.undefined

let classifySource (id: string) (parts: Part<obj> list option) (raw: obj option) : Source =
    let isSynth =
        if id <> "" then
            synthPrefixes |> List.exists id.StartsWith
        else
            false

    let hasCapsTextOrMeta =
        let checkText (t: string) =
            t <> null && t.Contains("<wanxiangshu-caps")

        let checkMeta (t: string) =
            t <> null
            && (t.Contains("caps-synth-")
                || t.Contains("caps-call-")
                || t.Contains("caps-fr-")
                || t.Contains("caps-tool-"))

        let checkObj (r: obj) =
            if box r = null then
                false
            else
                let checkVal (v: obj) =
                    if box v = null then
                        false
                    else
                        let s = string v
                        checkText s || checkMeta s

                if jsTypeOf r = "object" then
                    let rid = r?id
                    let rcallID = r?callID
                    let rtoolCallId = r?toolCallId
                    let rmetadata = r?metadata
                    let rkind = if box rmetadata <> null then rmetadata?kind else null

                    checkVal rid || checkVal rcallID || checkVal rtoolCallId || checkVal rkind
                else
                    let rStr = string r
                    checkText rStr || checkMeta rStr

        let partsContain =
            match parts with
            | Some pts ->
                pts
                |> List.exists (fun p ->
                    match p with
                    | TextPart t -> checkText t
                    | ToolPart(tool, callID, stateOpt, _) ->
                        checkText tool
                        || checkText callID
                        || checkMeta callID
                        || (match stateOpt with
                            | Some st -> checkText st.output || checkText st.error
                            | None -> false)
                    | RawPart r -> checkObj r)
            | None -> false

        let rawContain =
            match raw with
            | Some r -> checkObj r
            | None -> false

        partsContain || rawContain

    if isSynth then
        let prefix = synthPrefixes |> List.find id.StartsWith
        Synthetic prefix
    elif hasCapsTextOrMeta then
        Synthetic "caps"
    else
        Native

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

/// Drop synthetic messages, keeping only Native-sourced ones. Pure.
let stripSyntheticBySource (messages: Message<'raw> list) : Message<'raw> list =
    messages
    |> List.filter (fun m ->
        match m.source with
        | Synthetic _ -> false
        | Native ->
            let id = m.info.id
            let rawStr = if box m.raw <> null then string m.raw else ""

            let isCaps =
                (id <> null
                 && (id.StartsWith("caps-synth-")
                     || id.StartsWith("caps-call-")
                     || id.StartsWith("caps-fr-")
                     || id.StartsWith("caps-tool-")
                     || id.StartsWith("caps-tool-")))
                || (rawStr <> null && rawStr.Contains("<wanxiangshu-caps"))
                || (m.parts
                    |> List.exists (fun p ->
                        match p with
                        | TextPart t -> t <> null && t.Contains("<wanxiangshu-caps")
                        | ToolPart(_, callID, stateOpt, _) ->
                            (callID <> null
                             && (callID.StartsWith("caps-synth-")
                                 || callID.StartsWith("caps-call-")
                                 || callID.StartsWith("caps-fr-")
                                 || callID.StartsWith("caps-tool-")))
                            || (match stateOpt with
                                | Some st -> st.output <> null && st.output.Contains("<wanxiangshu-caps")
                                | None -> false)
                        | _ -> false))

            not isCaps)

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
