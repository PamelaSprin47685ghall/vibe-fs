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

let stringify =
    function
    | Table _ as document -> document |> toJs |> stringifyNative
    | _ -> invalidArg "document" "TOML document root must be a table"
