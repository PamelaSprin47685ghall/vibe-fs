namespace Wanxiangshu.Next.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop

/// Provider-visible parts only. Unknown host noise is dropped, never raw-object
/// embedded into canonical JSON (SSOT P0-3).
type ProviderVisiblePart =
    | Text of text: string
    | Reasoning of text: string
    | ToolCall of callId: string * name: string * argsCanonical: string
    | ToolResult of callId: string * resultCanonical: string

type ProviderVisibleMessage =
    { Role: string
      Parts: ProviderVisiblePart list }

module Projection =
    let canonicalJson = CanonicalJson.canonicalJson

    let private readField (value: obj) (name: string) : obj =
        if isNull value then null else emitJsExpr (value, name) "$0[$1]"

    let private readString (value: obj) (name: string) : string option =
        let field = readField value name
        if isNull field then None
        else
            let text = unbox<string> field
            if String.IsNullOrWhiteSpace text then None else Some text

    let private infoObject (rawObj: obj) : obj =
        if isNull rawObj then null
        elif not (isNull rawObj?info) then rawObj?info
        else rawObj

    let private rawParts (rawObj: obj) : obj list =
        let parts = readField rawObj "parts"
        if isNull parts then []
        else emitJsExpr parts "Array.from($0)" |> unbox<obj array> |> Array.toList

    let messageId (rawObj: obj) : string option =
        let info = infoObject rawObj
        readString info "id" |> Option.orElse (readString rawObj "id")

    let private roleOf (rawObj: obj) : string =
        let info = infoObject rawObj
        readString rawObj "role"
        |> Option.orElse (readString info "role")
        |> Option.defaultValue ""
        |> fun value -> value.ToLowerInvariant()

    let private canonicalArgs (value: obj) : string =
        if isNull value then "{}"
        elif emitJsExpr value "typeof $0 === 'string'" then unbox<string> value
        else canonicalJson value

    /// Project one host part into provider-visible shape. Returns None for
    /// bookkeeping-only parts (step markers, ids, synthetic flags, etc.).
    let projectPart (partObj: obj) : ProviderVisiblePart option =
        if isNull partObj then
            None
        else
            let kind =
                readString partObj "type"
                |> Option.defaultValue ""
                |> fun value -> value.ToLowerInvariant()

            match kind with
            | "text" ->
                readString partObj "text"
                |> Option.map Text
            | "reasoning"
            | "thinking" ->
                readString partObj "text"
                |> Option.orElse (readString partObj "reasoning")
                |> Option.map Reasoning
            | "tool-call"
            | "tool_call"
            | "tool" ->
                let callId =
                    readString partObj "callID"
                    |> Option.orElse (readString partObj "callId")
                    |> Option.orElse (readString partObj "id")
                    |> Option.defaultValue ""

                let name =
                    readString partObj "tool"
                    |> Option.orElse (readString partObj "name")
                    |> Option.defaultValue ""

                let args =
                    let a = readField partObj "args"
                    let b = readField partObj "arguments"
                    if not (isNull a) then canonicalArgs a
                    elif not (isNull b) then canonicalArgs b
                    else "{}"

                if String.IsNullOrWhiteSpace name && String.IsNullOrWhiteSpace callId then
                    None
                else
                    Some(ToolCall(callId, name, args))
            | "tool-result"
            | "tool_result" ->
                let callId =
                    readString partObj "callID"
                    |> Option.orElse (readString partObj "callId")
                    |> Option.orElse (readString partObj "id")
                    |> Option.defaultValue ""

                let result =
                    let r = readField partObj "result"
                    let o = readField partObj "output"
                    let c = readField partObj "content"
                    if not (isNull r) then canonicalArgs r
                    elif not (isNull o) then canonicalArgs o
                    elif not (isNull c) then canonicalArgs c
                    else "null"

                Some(ToolResult(callId, result))
            // Host bookkeeping — never provider-visible.
            | "step-start"
            | "step-finish"
            | "step_start"
            | "step_finish"
            | "compaction"
            | "patch"
            | "file"
            | "image"
            | "" -> None
            | _ -> None

    /// Formal visible assistant text only. Reasoning/thinking and tool parts are excluded.
    let formalTextFromParts (parts: obj array) : string =
        if isNull parts then
            ""
        else
            parts
            |> Array.choose (fun part ->
                match projectPart part with
                | Some(Text text) -> Some text
                | _ -> None)
            |> String.concat ""

    let projectMessage (rawObj: obj) : ProviderVisibleMessage option =
        if isNull rawObj then
            None
        else
            let role = roleOf rawObj
            let parts = rawParts rawObj |> List.choose projectPart

            let parts =
                if parts <> [] then
                    parts
                else
                    // Fallback: plain text field on message root (legacy fixtures).
                    let info = infoObject rawObj
                    match readString rawObj "text" |> Option.orElse (readString info "text") with
                    | Some text -> [ Text text ]
                    | None -> []

            if String.IsNullOrWhiteSpace role && parts = [] then
                None
            else
                Some { Role = role; Parts = parts }

    let projectMessages (rawMsgs: obj list) : ProviderVisibleMessage list =
        rawMsgs |> List.choose projectMessage

    let private partValue (part: ProviderVisiblePart) : obj =
        match part with
        | Text text -> createObj [ "type", box "text"; "text", box text ]
        | Reasoning text -> createObj [ "type", box "reasoning"; "text", box text ]
        | ToolCall(callId, name, args) ->
            createObj
                [ "type", box "tool-call"
                  "callID", box callId
                  "tool", box name
                  "args", box args ]
        | ToolResult(callId, result) ->
            createObj
                [ "type", box "tool-result"
                  "callID", box callId
                  "result", box result ]

    /// Stable provider-visible JSON. Excludes id/sessionID/agent/synthetic/
    /// timestamp/cost/usage/directory/status and any host bookkeeping.
    let canonicalMessageJson (rawObj: obj) : string =
        match projectMessage rawObj with
        | None -> "null"
        | Some message ->
            let normalized =
                createObj
                    [ "role", box message.Role
                      "parts",
                      box (message.Parts |> List.map partValue |> List.toArray) ]

            canonicalJson normalized

    let sameCanonicalMessage (left: obj) (right: obj) : bool =
        canonicalMessageJson left = canonicalMessageJson right

    let replaceRawPrefix (newPrefix: obj list) (prefixLen: int) (rawMsgs: obj list) : obj list =
        let len = List.length rawMsgs

        if prefixLen <= 0 then
            List.append newPrefix rawMsgs
        elif prefixLen >= len then
            newPrefix
        else
            let rawTail = rawMsgs |> List.skip prefixLen
            List.append newPrefix rawTail

    let preserveRawTail (prefix: obj list) (rawTail: obj list) : obj list =
        List.append prefix rawTail
