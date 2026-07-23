module Wanxiangshu.Runtime.Serialization.Toml

open System
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
    let rec unescape j (acc: char list) =
        if j >= inner.Length then
            System.String(acc |> List.rev |> List.toArray)
        else
            match inner.[j] with
            | '\\' when j + 1 < inner.Length ->
                match inner.[j + 1] with
                | 'n' -> unescape (j + 2) ('\n' :: acc)
                | 'r' -> unescape (j + 2) ('\r' :: acc)
                | 't' -> unescape (j + 2) ('\t' :: acc)
                | '"' -> unescape (j + 2) ('"' :: acc)
                | '\\' -> unescape (j + 2) ('\\' :: acc)
                | 'b' -> unescape (j + 2) ('\b' :: acc)
                | 'f' -> unescape (j + 2) ('\f' :: acc)
                | c -> unescape (j + 2) (c :: '\\' :: acc)
            | c -> unescape (j + 1) (c :: acc)
    unescape 0 []

let private encodeMultilineBody (s: string) : string =
    let chars: char[] =
        s |> Seq.collect (function
            | '\\' -> [ '\\'; '\\' ]
            | '\r' -> [ '\\'; 'r' ]
            | c -> [ c ])
        |> Seq.toArray
    let res = System.String(chars)
    res.Replace("\"\"\"", "\\\"\\\"\\\"")

let private processStringToken (rawToken: string) (acc: string list) =
    if rawToken.Contains("\\n") || rawToken.Contains("\n") then
        let inner = rawToken.Substring(1, rawToken.Length - 2)
        let decoded = unescapeStringToken inner
        if decoded.Contains("\n") then
            let multilineBody = encodeMultilineBody decoded
            ("\"\"\"\n" + multilineBody + "\"\"\"") :: acc
        else
            rawToken :: acc
    else
        rawToken :: acc

let private formatMultiline (tomlStr: string) : string =
    let len = tomlStr.Length
    let rec parse i inString stringStart escaped (acc: string list) =
        if i >= len then
            String.Concat(acc |> List.rev |> List.toArray)
        else
            let ch = tomlStr.[i]
            if inString then
                if escaped then
                    parse (i + 1) true stringStart false acc
                elif ch = '\\' then
                    parse (i + 1) true stringStart true acc
                elif ch = '"' then
                    let rawToken = tomlStr.Substring(stringStart, i - stringStart + 1)
                    let newAcc = processStringToken rawToken acc
                    parse (i + 1) false -1 false newAcc
                else
                    parse (i + 1) true stringStart false acc
            else
                if ch = '"' then
                    parse (i + 1) true i false acc
                else
                    parse (i + 1) false -1 false (string ch :: acc)
    parse 0 false -1 false []

let stringify =
    function
    | Table _ as document -> document |> toJs |> stringifyNative |> formatMultiline
    | _ -> invalidArg "document" "TOML document root must be a table"

