module VibeFs.Mux.MessagingCodec

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Messaging
open VibeFs.Shell
open VibeFs.Shell.Dyn

let private decodeToolStatus (part: obj) : string =
    match Dyn.str part "state" with
    | "output-available" -> "completed"
    | "input-available" -> "pending"
    | other -> other

let private decodeToolState (part: obj) : ToolState<obj> option =
    let output = Dyn.get part "output"
    let input = Dyn.get part "input"

    if Dyn.isNullish output && Dyn.isNullish input then
        None
    else
        let operation = if Dyn.isNullish input then null else Dyn.get input "operation"
        let operationAction = if Dyn.isNullish operation then "" else Dyn.str operation "action"

        Some
            { status = decodeToolStatus part
              output =
                if Dyn.isNullish output then ""
                elif Dyn.typeIs output "string" then string output
                else Dyn.str output "content"
              error = Dyn.str output "error"
              input = input
              operationAction = operationAction }

let decodePart (part: obj) : Part<obj> =
    match Dyn.str part "type" with
    | "text" -> TextPart (Dyn.str part "text")
    | "dynamic-tool" ->
        ToolPart(
            normalizeToolName opencode (Dyn.str part "toolName"),
            Dyn.str part "toolCallId",
            decodeToolState part,
            part
        )
    | _ -> RawPart part

let decodeMessage (sessionID: string) (msg: obj) : Message<obj> option =
    if Dyn.isNullish msg then
        None
    else
        Some
            { info =
                { id = Dyn.str msg "id"
                  sessionID = sessionID
                  role = decodeRole (Dyn.str msg "role")
                  agent = Dyn.str msg "agent"
                  isError = false
                  toolName = ""
                  details = null
                  time = null }
              parts =
                let parts = Dyn.get msg "parts"
                if Dyn.isNullish parts || not (Dyn.isArray parts) then []
                else (parts :?> obj array) |> Array.map decodePart |> List.ofArray
              source = classifySource (Dyn.str msg "id")
              raw = msg }

let decodeMessages (sessionID: string) (messages: obj array) : Message<obj> list =
    messages |> Array.choose (decodeMessage sessionID) |> List.ofArray

let private encodeTextPart (text: string) : obj =
    createObj [ "type", box "text"; "text", box text; "state", box "done" ]

let private outputsEquivalent (left: obj) (right: obj) : bool =
    if obj.ReferenceEquals(left, right) then
        true
    elif Dyn.isNullish left || Dyn.isNullish right then
        Dyn.isNullish left && Dyn.isNullish right
    elif Dyn.typeIs left "string" || Dyn.typeIs right "string" then
        string left = string right
    else
        JS.JSON.stringify(left) = JS.JSON.stringify(right)

let private partsEquivalent (left: obj) (right: obj) : bool =
    if obj.ReferenceEquals(left, right) then
        true
    else
        match Dyn.str left "type", Dyn.str right "type" with
        | "text", "text" -> Dyn.str left "text" = Dyn.str right "text" && Dyn.str left "state" = Dyn.str right "state"
        | "dynamic-tool", "dynamic-tool" ->
            Dyn.str left "toolName" = Dyn.str right "toolName"
            && Dyn.str left "toolCallId" = Dyn.str right "toolCallId"
            && Dyn.str left "state" = Dyn.str right "state"
            && outputsEquivalent (Dyn.get left "output") (Dyn.get right "output")
        | _ -> false

let private rawOutputMatchesState (rawPart: obj) (state: ToolState<obj>) : bool =
    let rawOutput = Dyn.get rawPart "output"
    if Dyn.isNullish rawOutput then
        state.output = ""
    elif Dyn.typeIs rawOutput "string" then
        string rawOutput = state.output
    else
        Dyn.str rawOutput "content" = state.output

let private encodeToolPartState (rawPart: obj) (state: ToolState<obj>) : obj =
    let rawOutput = Dyn.get rawPart "output"
    let nextOutput =
        if Dyn.isNullish rawOutput || Dyn.typeIs rawOutput "string" then
            box state.output
        else
            Dyn.withKey rawOutput "content" (box state.output)
    let withState = Dyn.withKey rawPart "state" (box "output-available")
    Dyn.withKey withState "output" nextOutput

let encodePart (part: Part<obj>) : obj =
    match part with
    | TextPart text -> box (encodeTextPart text)
    | ToolPart(toolName, callID, Some state, raw) ->
        if isNull raw then
            box
                (createObj
                    [ "type", box "dynamic-tool"
                      "toolName", box toolName
                      "toolCallId", box callID
                      "state", box "output-available"
                      "input", state.input
                      "output", box state.output ])
        elif rawOutputMatchesState raw state then
            raw
        else
            encodeToolPartState raw state
    | ToolPart(toolName, callID, None, raw) ->
        if isNull raw then
            box (createObj [ "type", box "dynamic-tool"; "toolName", box toolName; "toolCallId", box callID ])
        else
            raw
    | RawPart raw -> raw

let encodeMessage (msg: Message<obj>) : obj =
    let encodedParts = msg.parts |> List.map encodePart |> List.toArray
    let role =
        match msg.info.role with
        | User -> "user"
        | Assistant -> "assistant"
        | ToolResult -> "tool-result"
        | System -> "system"

    if isNull msg.raw then
        box
            (createObj
                [ "id", box msg.info.id
                  "role", box role
                  "parts", box encodedParts ])
    else
        let rawParts = Dyn.get msg.raw "parts"
        let partsUnchanged =
            if Dyn.isNullish rawParts || not (Dyn.isArray rawParts) then
                false
            else
                let arr = rawParts :?> obj array
                arr.Length = encodedParts.Length && Array.forall2 partsEquivalent arr encodedParts

        if partsUnchanged then msg.raw else Dyn.withKey msg.raw "parts" (box encodedParts)

let encodeMessages (messages: Message<obj> list) : obj array =
    messages |> List.map encodeMessage |> List.toArray
