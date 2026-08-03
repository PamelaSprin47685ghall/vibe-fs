namespace Wanxiangshu.Next.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop

/// Typed semantic content decoded from one Host message part. Transport-only
/// fields never cross this boundary.
[<RequireQualifiedAccess>]
type MessagePart =
    | Text of text: string
    | Reasoning of text: string
    | ToolCall of callId: string * name: string * argsCanonical: string
    | ToolResult of callId: string * resultCanonical: string
    | Activity of kind: string

/// Raw OpenCode part codec. This is the only message-part module allowed to
/// inspect dynamic Host objects.
module HostMessageCodec =

    let private readField (value: obj) (name: string) : obj =
        if isNull value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let private readString (value: obj) (name: string) =
        let field = readField value name

        if isNull field then
            None
        else
            let text = unbox<string> field
            if String.IsNullOrWhiteSpace text then None else Some text

    let private canonicalArgs (value: obj) =
        if isNull value then
            "null"
        elif emitJsExpr value "typeof $0 === 'string'" then
            unbox<string> value
        else
            CanonicalJson.canonicalJson value

    let decodePart (raw: obj) : MessagePart option =
        if isNull raw then
            None
        else
            let kind =
                readString raw "type"
                |> Option.defaultValue ""
                |> fun value -> value.ToLowerInvariant()

            match kind with
            | "text" -> readString raw "text" |> Option.map MessagePart.Text
            | "reasoning"
            | "thinking" ->
                readString raw "text"
                |> Option.orElse (readString raw "reasoning")
                |> Option.orElse (readString raw "thinking")
                |> Option.map MessagePart.Reasoning
            | "tool-call"
            | "tool_call"
            | "tool" ->
                let callId =
                    readString raw "callID"
                    |> Option.orElse (readString raw "callId")
                    |> Option.orElse (readString raw "id")
                    |> Option.defaultValue ""

                let name =
                    readString raw "tool"
                    |> Option.orElse (readString raw "name")
                    |> Option.defaultValue ""

                let args =
                    let direct = readField raw "args"
                    let alternate = readField raw "arguments"

                    if not (isNull direct) then canonicalArgs direct
                    elif not (isNull alternate) then canonicalArgs alternate
                    else "{}"

                if String.IsNullOrWhiteSpace name && String.IsNullOrWhiteSpace callId then
                    None
                else
                    Some(MessagePart.ToolCall(callId, name, args))
            | "tool-result"
            | "tool_result" ->
                let callId =
                    readString raw "callID"
                    |> Option.orElse (readString raw "callId")
                    |> Option.orElse (readString raw "id")
                    |> Option.defaultValue ""

                let result =
                    [ "result"; "output"; "content" ]
                    |> List.tryPick (fun field ->
                        let value = readField raw field
                        if isNull value then None else Some(canonicalArgs value))
                    |> Option.defaultValue "null"

                Some(MessagePart.ToolResult(callId, result))
            | "patch"
            | "step-start"
            | "step-finish"
            | "step_start"
            | "step_finish" -> Some(MessagePart.Activity(kind.Replace('_', '-')))
            | _ -> None

    let decodeParts (rawParts: obj array) =
        if isNull rawParts then
            [||]
        else
            rawParts |> Array.choose decodePart
