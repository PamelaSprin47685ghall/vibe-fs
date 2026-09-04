namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

module HostEventEnvelope =

    let unwrap (rawInput: obj) : obj =
        if isNull rawInput then
            rawInput
        elif not (isNull rawInput?event) then
            rawInput?event
        elif not (isNull rawInput?payload) then
            rawInput?payload
        elif not (isNull rawInput?data) && not (isNull rawInput?data?``type``) then
            rawInput?data
        else
            rawInput

    let private primitiveNonWhitespaceString (value: obj) =
        if isNull value || not (emitJsExpr value "typeof $0 === 'string'") then
            None
        else
            let text = unbox<string> value
            if String.IsNullOrWhiteSpace text then None else Some text

    let private field (value: obj) (name: string) =
        if isNull value then null else value?(name)

    let eventTypeOf (raw: obj) : string =
        if isNull raw then
            ""
        else
            primitiveNonWhitespaceString (field raw "type")
            |> Option.map (fun eventType -> eventType.ToLowerInvariant())
            |> Option.defaultValue ""

    let private tryReadSessionId (raw: obj) : SessionId option =
        let properties = raw?properties

        primitiveNonWhitespaceString (field properties "sessionID")
        |> Option.orElseWith (fun () -> primitiveNonWhitespaceString (field properties "sessionId"))
        |> Option.orElseWith (fun () -> primitiveNonWhitespaceString (field raw "sessionID"))
        |> Option.orElseWith (fun () -> primitiveNonWhitespaceString (field raw "sessionId"))
        |> Option.map SessionId.create

    let trySessionId (raw: obj) : SessionId option =
        if isNull raw then None else tryReadSessionId raw

    let private messageInfo (raw: obj) =
        let properties = if isNull raw then null else raw?properties
        if isNull properties then null else properties?info

    let private messageInfoSessionId (info: obj) =
        if isNull info then
            None
        else
            primitiveNonWhitespaceString (field info "sessionID")
            |> Option.map SessionId.create

    let tryMessageSessionId (rawInput: obj) : SessionId option =
        let raw = unwrap rawInput

        trySessionId raw
        |> Option.orElseWith (fun () -> messageInfoSessionId (messageInfo raw))
