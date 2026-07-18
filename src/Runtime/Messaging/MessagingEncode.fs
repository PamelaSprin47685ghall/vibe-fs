module Wanxiangshu.Runtime.MessagingEncode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ToolExecutionStatusModule
open Wanxiangshu.Runtime.Dyn
open Thoth.Json

let outputsEquivalent (left: obj) (right: obj) : bool =
    if obj.ReferenceEquals(left, right) then
        true
    elif isNullish left || isNullish right then
        isNullish left && isNullish right
    elif typeIs left "string" || typeIs right "string" then
        string left = string right
    else
        Encode.Auto.toString (0, left) = Encode.Auto.toString (0, right)

let partsEquivalent (left: obj) (right: obj) : bool =
    if obj.ReferenceEquals(left, right) then
        true
    else
        match str left "type", str right "type" with
        | "text", "text" -> str left "text" = str right "text" && str left "state" = str right "state"
        | "dynamic-tool", "dynamic-tool" ->
            str left "toolName" = str right "toolName"
            && str left "toolCallId" = str right "toolCallId"
            && str left "state" = str right "state"
            && outputsEquivalent (get left "output") (get right "output")
        | _ -> false

let rawOutputMatchesState (rawPart: obj) (state: ToolState<obj>) : bool =
    let rawOutput = get rawPart "output"

    if isNullish rawOutput then
        state.output = ""
    elif typeIs rawOutput "string" then
        string rawOutput = state.output
    else
        str rawOutput "content" = state.output

let encodeToolPartState (rawPart: obj) (state: ToolState<obj>) : obj =
    let rawOutput = get rawPart "output"

    let nextOutput =
        if isNullish rawOutput || typeIs rawOutput "string" then
            box state.output
        else
            withKey rawOutput "content" (box state.output)

    let withState = withKey rawPart "state" (box "output-available")
    withKey withState "output" nextOutput

let encodeTextPartBasic (text: string) : obj =
    box (createObj [ "type", box "text"; "text", box text ])

let toolStateUnchanged (rawState: obj) (state: ToolState<obj>) : bool =
    not (isNullish rawState)
    && str rawState "status" = toString state.status
    && str rawState "output" = state.output
    && str rawState "error" = state.error

let buildToolStateObj (rawState: obj) (state: ToolState<obj>) : obj =
    if isNullish rawState then
        box (
            createObj
                [ "status", box (toString state.status)
                  "output", box state.output
                  "error", box state.error
                  "input", state.input ]
        )
    else
        let s1 = withKey rawState "status" (box (toString state.status))
        let s2 = withKey s1 "output" (box state.output)
        withKey s2 "error" (box state.error)

let encodeTextPartWithState (text: string) (state: string) : obj =
    createObj [ "type", box "text"; "text", box text; "state", box state ]

let encodeOpencodeToolPart (toolName: string) (callID: string) (stateOpt: ToolState<obj> option) (raw: obj) : obj =
    match stateOpt with
    | Some state ->
        let rawState = if isNull raw then null else get raw "state"

        if not (isNull raw) && toolStateUnchanged rawState state then
            raw
        else
            let stateObj = buildToolStateObj rawState state

            if isNull raw then
                box (
                    createObj
                        [ "type", box "tool"
                          "tool", box toolName
                          "callID", box callID
                          "state", stateObj ]
                )
            else
                withKey raw "state" stateObj
    | None ->
        if isNull raw then
            box (createObj [ "type", box "tool"; "tool", box toolName; "callID", box callID ])
        else
            raw

let encodeMuxToolPart (toolName: string) (callID: string) (stateOpt: ToolState<obj> option) (raw: obj) : obj =
    match stateOpt with
    | Some state ->
        if isNull raw then
            box (
                createObj
                    [ "type", box "dynamic-tool"
                      "toolName", box toolName
                      "toolCallId", box callID
                      "state", box "output-available"
                      "input", state.input
                      "output", box state.output ]
            )
        elif rawOutputMatchesState raw state then
            raw
        else
            encodeToolPartState raw state
    | None ->
        if isNull raw then
            box (
                createObj
                    [ "type", box "dynamic-tool"
                      "toolName", box toolName
                      "toolCallId", box callID ]
            )
        else
            raw

let replacePartsOnRawMessage (rawMsg: obj) (encodedParts: obj array) : obj =
    Wanxiangshu.Runtime.Dyn.withKey rawMsg "parts" (box encodedParts)
