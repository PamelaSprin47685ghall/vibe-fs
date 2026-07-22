module Wanxiangshu.Runtime.Serialization.Toml

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Serialization.TomlValue

[<Import("stringify", "smol-toml")>]
let private stringifyNative (value: obj) : string = jsNative

let rec private toJs =
    function
    | String value -> box value
    | Integer value -> box value
    | Boolean value -> box value
    | StringArray values -> values |> List.toArray |> box
    | TableArray tables -> tables |> List.map (Table >> toJs) |> List.toArray |> box
    | Table fields -> fields |> List.map (fun (key, value) -> key, toJs value) |> createObj

let private unescapeStringToken (inner: string) : string =
    let rec unescape j (buf: System.Text.StringBuilder) =
        if j >= inner.Length then
            buf.ToString()
        else
            match inner.[j] with
            | '\\' when j + 1 < inner.Length ->
                match inner.[j + 1] with
                | 'n' -> buf.Append('\n') |> ignore; unescape (j + 2) buf
                | 'r' -> buf.Append('\r') |> ignore; unescape (j + 2) buf
                | 't' -> buf.Append('\t') |> ignore; unescape (j + 2) buf
                | '"' -> buf.Append('"') |> ignore; unescape (j + 2) buf
                | '\\' -> buf.Append('\\') |> ignore; unescape (j + 2) buf
                | 'b' -> buf.Append('\b') |> ignore; unescape (j + 2) buf
                | 'f' -> buf.Append('\f') |> ignore; unescape (j + 2) buf
                | c -> buf.Append('\\').Append(c) |> ignore; unescape (j + 2) buf
            | c -> buf.Append(c) |> ignore; unescape (j + 1) buf
    unescape 0 (System.Text.StringBuilder())

let private encodeMultilineBody (s: string) : string =
    let sb = System.Text.StringBuilder()
    for c in s do
        match c with
        | '\\' -> sb.Append("\\\\") |> ignore
        | '\r' -> sb.Append("\\r") |> ignore
        | _ -> sb.Append(c) |> ignore
    sb.ToString().Replace("\"\"\"", "\\\"\\\"\\\"")

let private processStringToken (rawToken: string) (acc: System.Text.StringBuilder) =
    if rawToken.Contains("\\n") || rawToken.Contains("\n") then
        let inner = rawToken.Substring(1, rawToken.Length - 2)
        let decoded = unescapeStringToken inner
        if decoded.Contains("\n") then
            let multilineBody = encodeMultilineBody decoded
            acc.Append("\"\"\"\n").Append(multilineBody).Append("\"\"\"") |> ignore
        else
            acc.Append(rawToken) |> ignore
    else
        acc.Append(rawToken) |> ignore

let private formatMultiline (tomlStr: string) : string =
    let len = tomlStr.Length
    let rec parse i inString stringStart escaped (acc: System.Text.StringBuilder) =
        if i >= len then
            acc.ToString()
        else
            let ch = tomlStr.[i]
            if inString then
                if escaped then
                    parse (i + 1) true stringStart false acc
                elif ch = '\\' then
                    parse (i + 1) true stringStart true acc
                elif ch = '"' then
                    let rawToken = tomlStr.Substring(stringStart, i - stringStart + 1)
                    processStringToken rawToken acc
                    parse (i + 1) false -1 false acc
                else
                    parse (i + 1) true stringStart false acc
            else
                if ch = '"' then
                    parse (i + 1) true i false acc
                else
                    acc.Append(ch) |> ignore
                    parse (i + 1) false -1 false acc
    parse 0 false -1 false (System.Text.StringBuilder())

let stringify =
    function
    | Table _ as document -> document |> toJs |> stringifyNative |> formatMultiline
    | _ -> invalidArg "document" "TOML document root must be a table"

