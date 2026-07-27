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
    // Keep the canonical serializer available at the Projection boundary so
    // callers cannot accidentally introduce a second JSON normalization rule.
    let canonicalJson = CanonicalJson.canonicalJson

    let private readField (value: obj) (name: string) : obj =
        if isNull value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let private readStringField (value: obj) (name: string) : string =
        let field = readField value name
        if isNull field then "" else unbox<string> field

    let private infoObject (rawObj: obj) : obj =
        if isNull rawObj then null
        elif not (isNull rawObj?info) then rawObj?info
        else rawObj

    let private rawParts (rawObj: obj) : obj list =
        let parts = readField rawObj "parts"

        if isNull parts then
            []
        else
            emitJsExpr parts "Array.from($0)" |> unbox<obj array> |> Array.toList

    let messageId (rawObj: obj) : string option =
        let info = infoObject rawObj
        let id = readField info "id"

        if isNull id then None else Some(unbox<string> id)

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
                        canonicalJson partObj?args

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
            let info = infoObject rawObj
            let id = readStringField info "id"

            let roleField =
                let directRole = readField rawObj "role"

                if isNull directRole then
                    readField info "role"
                else
                    directRole

            let roleStr = if isNull roleField then "" else unbox<string> roleField

            let sId =
                let directSession = readField rawObj "sessionID"

                let session =
                    if isNull directSession then
                        readField info "sessionID"
                    else
                        directSession

                if isNull session then "" else unbox<string> session

            let agent =
                let directAgent = readField rawObj "agent"

                let agentField =
                    if isNull directAgent then
                        readField info "agent"
                    else
                        directAgent

                if isNull agentField then
                    None
                else
                    Some(unbox<string> agentField)

            let parts = rawParts rawObj |> List.map projectPart

            let text =
                let directText = readField rawObj "text"

                let textField =
                    if isNull directText then
                        readField info "text"
                    else
                        directText

                if not (isNull textField) then
                    unbox<string> textField
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

    let private canonicalPartValue (part: CanonicalPart) : obj =
        match part with
        | TextPart(id, text, synthetic) ->
            createObj
                [ "id", box id
                  "type", box "text"
                  "text", box text
                  "synthetic", synthetic |> Option.map box |> Option.defaultValue null ]
        | ToolCallPart(id, callId, tool, argsStr) ->
            createObj
                [ "id", box id
                  "type", box "tool-call"
                  "callID", box callId
                  "tool", box tool
                  "args", box argsStr ]
        | CompactionPart(id, auto, overflow) ->
            createObj
                [ "id", box id
                  "type", box "compaction"
                  "auto", box auto
                  "overflow", box overflow ]
        | RawPart(id, kind, rawObj) -> createObj [ "id", box id; "type", box kind; "raw", box rawObj ]

    /// Stable message content used by Companion prefix matching.  The
    /// message id is intentionally omitted from the content projection: it is
    /// a locator, not evidence that a message's contents are unchanged.
    /// Stable message content used by Companion prefix matching.  The
    /// message id is intentionally omitted from the content projection: it is
    /// a locator, not evidence that a message's contents are unchanged.
    /// Only includes provider-visible fields (role, text, tool call metadata)
    /// and explicitly excludes timestamp, cost, usage, runtime ID, directory,
    /// status, and other non-model fields that differ between requests without
    /// affecting the cache prefix.
    let canonicalMessageJson (rawObj: obj) : string =
        match projectMessage rawObj with
        | None -> canonicalJson rawObj
        | Some message ->
            // Provider-visible projection: only fields that actually enter the model.
            // Explicitly excludes: timestamp, cost, usage, runtime ID, directory,
            // status, UI metadata, and any other non-model bookkeeping fields.
            let normalized =
                createObj
                    [ "role", box (roleToString message.Role)
                      "text", box message.Text
                      "parts",
                      box (message.Parts |> List.map canonicalPartValue |> List.toArray)
                      // Include sessionID for Companion delta routing; it is a
                      // routing identifier, not cache content — its stability is
                      // guaranteed by the causal transform pipeline.
                      "sessionID", box message.SessionId ]

            canonicalJson normalized

    let sameCanonicalMessage (left: obj) (right: obj) : bool =
        canonicalMessageJson left = canonicalMessageJson right

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
