namespace Wanxiangshu.Context.Scope

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection

module SessionView =

    let private renderWirePart (tableName: string) (role: string) (part: WirePart) : LlmFacing.DataBlock =
        match part with
        | WireText text ->
            LlmFacing.Data.tableArray
                tableName
                [ LlmFacing.Data.stringMember "role" role
                  LlmFacing.Data.stringMember "text" text ]
        | WireReasoning text ->
            LlmFacing.Data.tableArray
                tableName
                [ LlmFacing.Data.stringMember "role" role
                  LlmFacing.Data.stringMember "text" text ]
        | WireToolCall(callId, name, args) ->
            LlmFacing.Data.tableArray
                tableName
                [ LlmFacing.Data.stringMember "role" "assistant"
                  LlmFacing.Data.stringMember "tool" name
                  LlmFacing.Data.stringMember "arguments" args
                  LlmFacing.Data.stringMember "callId" (ToolCallId.value callId) ]
        | WireToolResult(callId, result) ->
            LlmFacing.Data.tableArray
                tableName
                [ LlmFacing.Data.stringMember "role" "toolResult"
                  LlmFacing.Data.stringMember "result" result
                  LlmFacing.Data.stringMember "callId" (ToolCallId.value callId) ]
        | WireMedia(mediaType, digest) ->
            LlmFacing.Data.tableArray
                tableName
                [ LlmFacing.Data.stringMember "role" role
                  LlmFacing.Data.stringMember "mediaType" (defaultArg mediaType "")
                  LlmFacing.Data.stringMember "digest" digest ]

    let private renderWireMessageBlocks (tableName: string) (msg: WireMessage) : LlmFacing.DataBlock list =
        if List.isEmpty msg.Parts then
            [ LlmFacing.Data.tableArray tableName [ LlmFacing.Data.stringMember "role" msg.Role ] ]
        else
            msg.Parts |> List.map (renderWirePart tableName msg.Role)

    let renderWithTable (tableName: string) (messages: WireMessage list) : string =
        let blocks = messages |> List.collect (renderWireMessageBlocks tableName)
        LlmFacing.empty |> LlmFacing.withData blocks |> LlmFacing.render

    let render (messages: WireMessage list) : string =
        renderWithTable "context" messages

    [<Emit("Array.isArray($0)")>]
    let private isArray (_value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isString (_value: obj) : bool = jsNative

    [<Emit("$0 == null ? undefined : $0[$1]")>]
    let private jsGet (target: obj) (key: string) : obj = jsNative

    let private stringOrCanonicalJson (v: obj) : string =
        if isString v then unbox<string> v
        else Wanxiangshu.Host.CanonicalJson.canonicalJson v

    let private pickJsStringOrJson (primary: obj) (fallback: obj) (defaultVal: string) : string =
        if not (isNull primary) then stringOrCanonicalJson primary
        elif not (isNull fallback) then stringOrCanonicalJson fallback
        else defaultVal

    let private pickJsResultStringOrJson (resObj: obj) (outObj: obj) (cObj: obj) : string =
        if not (isNull resObj) then stringOrCanonicalJson resObj
        elif not (isNull outObj) then stringOrCanonicalJson outObj
        elif not (isNull cObj) then stringOrCanonicalJson cObj
        else ""

    let private pickJsCallId (item: obj) (keys: string list) : string =
        keys
        |> List.tryPick (fun k ->
            let v = jsGet item k
            if not (isNull v) then Some(string v) else None)
        |> Option.defaultValue ""

    let private renderJsToolCallBlock (tableName: string) (item: obj) : LlmFacing.DataBlock =
        let nameObj = jsGet item "name"
        let name =
            if not (isNull nameObj) then string nameObj
            else
                let tObj = jsGet item "tool"
                if not (isNull tObj) then string tObj else ""
        let args = pickJsStringOrJson (jsGet item "arguments") (jsGet item "input") "{}"
        let callId = pickJsCallId item [ "id"; "callID"; "callId" ]
        LlmFacing.Data.tableArray
            tableName
            [ LlmFacing.Data.stringMember "role" "assistant"
              LlmFacing.Data.stringMember "tool" name
              LlmFacing.Data.stringMember "arguments" args
              LlmFacing.Data.stringMember "callId" callId ]

    let private renderJsToolResultBlock (tableName: string) (item: obj) : LlmFacing.DataBlock =
        let res = pickJsResultStringOrJson (jsGet item "result") (jsGet item "output") (jsGet item "content")
        let callId = pickJsCallId item [ "toolCallId"; "callID"; "callId"; "id" ]
        LlmFacing.Data.tableArray
            tableName
            [ LlmFacing.Data.stringMember "role" "toolResult"
              LlmFacing.Data.stringMember "result" res
              LlmFacing.Data.stringMember "callId" callId ]

    let private renderJsTextBlock (tableName: string) (role: string) (item: obj) : LlmFacing.DataBlock =
        let tObj = jsGet item "text"
        let text = if not (isNull tObj) then string tObj else ""
        LlmFacing.Data.tableArray
            tableName
            [ LlmFacing.Data.stringMember "role" role
              LlmFacing.Data.stringMember "text" text ]

    let private renderTypedJsItem (tableName: string) (role: string) (item: obj) : LlmFacing.DataBlock =
        let itemTypeObj = jsGet item "type"
        let itemType = if not (isNull itemTypeObj) then string itemTypeObj else "text"
        match itemType with
        | "toolCall" | "tool-call" | "tool_call" | "tool" -> renderJsToolCallBlock tableName item
        | "toolResult" | "tool-result" | "tool_result" -> renderJsToolResultBlock tableName item
        | _ -> renderJsTextBlock tableName role item

    let private renderJsItemBlock (tableName: string) (role: string) (item: obj) : LlmFacing.DataBlock =
        if isNull item then
            LlmFacing.Data.tableArray tableName [ LlmFacing.Data.stringMember "role" role ]
        else
            renderTypedJsItem tableName role item

    let private renderJsMessageBlocks (tableName: string) (msg: obj) : LlmFacing.DataBlock list =
        if isNull msg then []
        else
            let roleObj = jsGet msg "role"
            let role = if not (isNull roleObj) then string roleObj else "user"
            let content = jsGet msg "content"
            let parts = jsGet msg "parts"
            let items =
                if isArray content then unbox<obj array> content
                elif isArray parts then unbox<obj array> parts
                else [||]

            if items.Length = 0 then
                let text =
                    if isString content then unbox<string> content
                    else
                        let t = jsGet msg "text"
                        if isString t then unbox<string> t else ""
                [ LlmFacing.Data.tableArray
                      tableName
                      [ LlmFacing.Data.stringMember "role" role
                        LlmFacing.Data.stringMember "text" text ] ]
            else
                items |> Array.toList |> List.map (renderJsItemBlock tableName role)

    let blocksJsWithTable (tableName: string) (messages: obj array) : LlmFacing.DataBlock list =
        if isNull messages || messages.Length = 0 then []
        else messages |> Array.toList |> List.collect (renderJsMessageBlocks tableName)

    let renderJsWithTable (tableName: string) (messages: obj array) : string =
        blocksJsWithTable tableName messages
        |> fun blocks -> LlmFacing.empty |> LlmFacing.withData blocks |> LlmFacing.render

    let blocksJs (messages: obj array) : LlmFacing.DataBlock list =
        blocksJsWithTable "context" messages

    let private partToJs (part: MessagePart) : obj =
        match part with
        | MessagePart.Text text -> createObj [ "type" ==> "text"; "text" ==> text ]
        | MessagePart.Reasoning text -> createObj [ "type" ==> "reasoning"; "text" ==> text ]
        | MessagePart.ToolCall(callId, name, argsCanonical) ->
            createObj
                [ "type" ==> "toolCall"
                  "id" ==> callId
                  "callId" ==> callId
                  "name" ==> name
                  "tool" ==> name
                  "arguments" ==> argsCanonical ]
        | MessagePart.ToolResult(callId, resultCanonical) ->
            createObj
                [ "type" ==> "toolResult"
                  "id" ==> callId
                  "callId" ==> callId
                  "toolCallId" ==> callId
                  "result" ==> resultCanonical ]
        | MessagePart.Activity kind ->
            createObj [ "type" ==> "text"; "text" ==> sprintf "[activity: %s]" kind ]

    let private toolResultToJs (part: SessionToolPart) : obj option =
        match part.State with
        | SnapshotToolPartState.Pending -> None
        | SnapshotToolPartState.Completed output ->
            Some(
                createObj
                    [ "type" ==> "toolResult"
                      "id" ==> ToolCallId.value part.ToolCallId
                      "callId" ==> ToolCallId.value part.ToolCallId
                      "toolCallId" ==> ToolCallId.value part.ToolCallId
                      "result" ==> output ]
            )
        | SnapshotToolPartState.Failed error ->
            Some(
                createObj
                    [ "type" ==> "toolResult"
                      "id" ==> ToolCallId.value part.ToolCallId
                      "callId" ==> ToolCallId.value part.ToolCallId
                      "toolCallId" ==> ToolCallId.value part.ToolCallId
                      "result" ==> error ]
            )

    let ofSnapshotMessages (messages: SessionMessage list) : obj array =
        messages
        |> List.map (fun message ->
            let parts =
                let declared = message.Parts |> Array.map partToJs |> Array.toList
                let results = message.ToolParts |> Array.choose toolResultToJs |> Array.toList
                Array.ofList (declared @ results)

            createObj [ "id" ==> message.Id; "role" ==> message.Role; "parts" ==> parts ])
        |> List.toArray

    let renderJs (messages: obj array) : string =
        renderJsWithTable "context" messages
