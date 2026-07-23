namespace Wanxiangshu.Next.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop

type CanonicalRole =
    | User
    | Assistant
    | System
    | UnknownRole of string

type CanonicalPart =
    | TextPart of id: string * text: string * synthetic: bool option
    | ToolCallPart of id: string * callId: string * tool: string * argsStr: string
    | CompactionPart of id: string * auto: bool * overflow: bool
    | RawPart of id: string * kind: string * rawObj: obj

type CanonicalMessage =
    { Id: string
      Role: CanonicalRole
      SessionId: string
      Agent: string option
      Text: string
      Parts: CanonicalPart list
      Raw: obj }

module Projection =

    let parseRole (roleStr: string) =
        match if isNull roleStr then "" else roleStr.ToLowerInvariant() with
        | "user" -> User
        | "assistant" -> Assistant
        | "system" -> System
        | other -> UnknownRole other

    let roleToString (role: CanonicalRole) =
        match role with
        | User -> "user"
        | Assistant -> "assistant"
        | System -> "system"
        | UnknownRole s -> s

    let projectPart (partObj: obj) : CanonicalPart =
        if isNull partObj then
            RawPart("", "null", null)
        else
            let id = if isNull partObj?id then "" else unbox<string> partObj?id

            let kind =
                if isNull partObj?``type`` then
                    ""
                else
                    unbox<string> partObj?``type``

            match kind with
            | "text" ->
                let text =
                    if isNull partObj?text then
                        ""
                    else
                        unbox<string> partObj?text

                let synth =
                    if isNull partObj?synthetic then
                        None
                    else
                        Some(unbox<bool> partObj?synthetic)

                TextPart(id, text, synth)
            | "tool-call"
            | "tool_call" ->
                let callId =
                    if isNull partObj?callID then
                        ""
                    else
                        unbox<string> partObj?callID

                let tool =
                    if isNull partObj?tool then
                        ""
                    else
                        unbox<string> partObj?tool

                let argsStr =
                    if isNull partObj?args then
                        "{}"
                    else
                        Fable.Core.JS.JSON.stringify partObj?args

                ToolCallPart(id, callId, tool, argsStr)
            | "compaction" ->
                let auto =
                    if isNull partObj?auto then
                        false
                    else
                        unbox<bool> partObj?auto

                let overflow =
                    if isNull partObj?overflow then
                        false
                    else
                        unbox<bool> partObj?overflow

                CompactionPart(id, auto, overflow)
            | other -> RawPart(id, other, partObj)

    let projectMessage (rawObj: obj) : CanonicalMessage option =
        if isNull rawObj then
            None
        else
            let id = if isNull rawObj?id then "" else unbox<string> rawObj?id
            let roleStr = if isNull rawObj?role then "" else unbox<string> rawObj?role

            let sId =
                if isNull rawObj?sessionID then
                    ""
                else
                    unbox<string> rawObj?sessionID

            let agent =
                if isNull rawObj?agent then
                    None
                else
                    Some(unbox<string> rawObj?agent)

            let parts =
                if isNull rawObj?parts then
                    []
                else
                    let pList = unbox<obj list> rawObj?parts
                    pList |> List.map projectPart

            let text =
                if not (isNull rawObj?text) then
                    unbox<string> rawObj?text
                else
                    parts
                    |> List.choose (function
                        | TextPart(_, t, _) -> Some t
                        | _ -> None)
                    |> String.concat "\n"

            Some
                { Id = id
                  Role = parseRole roleStr
                  SessionId = sId
                  Agent = agent
                  Text = text
                  Parts = parts
                  Raw = rawObj }

    let projectMessages (rawMsgs: obj list) : CanonicalMessage list = rawMsgs |> List.choose projectMessage

    // AG-CURRENT-TAIL-PRESERVED: pure prefix replacement over raw message JSON
    let replaceRawPrefix (newPrefix: obj list) (prefixLen: int) (rawMsgs: obj list) : obj list =
        let len = List.length rawMsgs

        if prefixLen <= 0 then
            List.append newPrefix rawMsgs
        elif prefixLen >= len then
            newPrefix
        else
            let rawTail = rawMsgs |> List.skip prefixLen
            List.append newPrefix rawTail

    let preserveRawTail (prefix: obj list) (rawTail: obj list) : obj list = List.append prefix rawTail
